using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Sentis;

namespace RoboSubSimulation.Yolo
{
    /// <summary>
    /// Local (in-process) YOLOv8 inference client using Unity Sentis.
    /// Accepts raw RGB24 frames and exposes detections as YoloResult JSON.
    /// </summary>
    public class YoloSentisClient : MonoBehaviour, IJpegJsonClient
    {
        [Header("Model")]
        public ModelAsset modelAsset;

        [Tooltip("One label per line. Must match class count (nc).")]
        public TextAsset labels;

        [Header("Input")]
        [Tooltip("Input size expected by the model (square).")]
        public int inputSize = 640;

        [Tooltip("If the capture is letterboxed, detections are mapped back to the original capture size.")]
        public bool assumeLetterboxed = true;

        [Header("Thresholds")]
        [Range(0f, 1f)]
        public float scoreThreshold = 0.10f;

        [Range(0f, 1f)]
        public float iouThreshold = 0.45f;

        [Header("Backend")]
        public BackendType backend = BackendType.GPUCompute;

        [Header("Debug")]
        public bool logModelShapes = true;
        public bool logFirstFrame = true;

        public bool connected => _worker != null;
        public string LatestJson { get; private set; }

        // Raw RGB24 frame buffer
        readonly object _lock = new object();
        byte[] _pendingRgb;
        int _pendingWidth;
        int _pendingHeight;

        Worker _worker;
        string[] _labels;
        bool _loggedOnce;

        // Output buffer reused.
        readonly List<YoloDetection> _detections = new List<YoloDetection>(128);

        // Throttled debug logging
        float _nextStatusLogTime;
        int _lastDetCount;
        YoloDetection _lastTopDet;

        void OnEnable()
        {
            if (modelAsset == null)
            {
                Debug.LogError("[YoloSentisClient] modelAsset not set.");
                return;
            }

            var model = ModelLoader.Load(modelAsset);
            _worker = new Worker(model, backend);

            _labels = (labels != null)
                ? labels.text.Replace("\r", "").Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
                : Array.Empty<string>();

            if (logModelShapes)
            {
                foreach (var i in model.inputs)
                    Debug.Log($"[YoloSentisClient] Input: {i.name} {i.shape}");

                foreach (var o in model.outputs)
                    Debug.Log($"[YoloSentisClient] Output: {o.name} (index {o.index})");
            }

            LatestJson = string.Empty;
            _loggedOnce = false;
            _nextStatusLogTime = 0f;
            _lastDetCount = 0;
            _lastTopDet = null;
        }

        void OnDisable()
        {
            _worker?.Dispose();
            _worker = null;
        }

        public void QueueRgbFrame(byte[] rgb24, int width, int height)
        {
            if (rgb24 == null || rgb24.Length == 0) return;
            lock (_lock)
            {
                _pendingRgb = rgb24;
                _pendingWidth = width;
                _pendingHeight = height;
            }
        }

        void Update()
        {
            if (_worker == null) return;

            byte[] rgb;
            int w, h;
            lock (_lock)
            {
                rgb = _pendingRgb;
                w = _pendingWidth;
                h = _pendingHeight;
                _pendingRgb = null;
            }

            if (rgb == null) return;
            if (w <= 0 || h <= 0) return;

            if (!_loggedOnce && logFirstFrame)
            {
                Debug.Log($"[YoloSentisClient] First frame received: {w}x{h} bytes={rgb.Length}");
                _loggedOnce = true;
            }

            RunOnce(rgb, w, h);
        }

        void RunOnce(byte[] rgb24, int srcW, int srcH)
        {
            // Letterbox src -> square inputSize, keep scale/pad for mapping back.
            float scale = Mathf.Min((float)inputSize / srcW, (float)inputSize / srcH);
            int newW = Mathf.RoundToInt(srcW * scale);
            int newH = Mathf.RoundToInt(srcH * scale);
            int padX = (inputSize - newW) / 2;
            int padY = (inputSize - newH) / 2;

            // Build NCHW float tensor (1,3,H,W) normalized to 0..1
            var shape = new TensorShape(1, 3, inputSize, inputSize);
            using var input = new Tensor<float>(shape, clearOnInit: true);

            // Sample + write into tensor
            // src is RGB24, row-major, origin top-left from Unity readback.
            // We treat tensor coords as top-left as well; overlay uses same orientation.
            for (int y = 0; y < newH; y++)
            {
                int sy = Mathf.Clamp(Mathf.FloorToInt(y / scale), 0, srcH - 1);
                int srcRow = sy * srcW * 3;
                int ty = y + padY;

                for (int x = 0; x < newW; x++)
                {
                    int sx = Mathf.Clamp(Mathf.FloorToInt(x / scale), 0, srcW - 1);
                    int si = srcRow + (sx * 3);

                    float r = rgb24[si + 0] * (1f / 255f);
                    float g = rgb24[si + 1] * (1f / 255f);
                    float b = rgb24[si + 2] * (1f / 255f);

                    int tx = x + padX;

                    input[0, 0, ty, tx] = r;
                    input[0, 1, ty, tx] = g;
                    input[0, 2, ty, tx] = b;
                }
            }

            // Sentis 2.x: SetInput + Schedule, then PeekOutput.
            _worker.Schedule(input);

            // On GPU backends, PeekOutput() returns a GPU-resident tensor.
            // You must ReadbackAndClone() before indexing it from C#.
            using var gpuOutput = _worker.PeekOutput() as Tensor<float>;
            if (gpuOutput == null) return;

            using var output = gpuOutput.ReadbackAndClone() as Tensor<float>;
            if (output == null) return;

            // Expect [1, 4+nc, 8400] (here nc=4)
            int channels = output.shape[1];
            int count = output.shape[2];
            int nc = Mathf.Max(0, channels - 4);

            if (_labels.Length > 0 && _labels.Length != nc)
            {
                // Warn once, keep running.
                Debug.LogWarning($"[YoloSentisClient] labels.txt count ({_labels.Length}) != model class count ({nc}).");
            }

            _detections.Clear();

            for (int i = 0; i < count; i++)
            {
                float cx = output[0, 0, i];
                float cy = output[0, 1, i];
                float w = output[0, 2, i];
                float h = output[0, 3, i];

                // pick best class
                int bestCls = -1;
                float bestScore = 0f;

                for (int c = 0; c < nc; c++)
                {
                    float s = output[0, 4 + c, i];
                    if (s > bestScore)
                    {
                        bestScore = s;
                        bestCls = c;
                    }
                }

                if (bestCls < 0 || bestScore < scoreThreshold)
                    continue;

                // cx,cy,w,h are in input pixel space for Ultralytics ONNX.
                float x1 = cx - (w * 0.5f);
                float y1 = cy - (h * 0.5f);
                float x2 = cx + (w * 0.5f);
                float y2 = cy + (h * 0.5f);

                // Clip to model input
                x1 = Mathf.Clamp(x1, 0, inputSize);
                y1 = Mathf.Clamp(y1, 0, inputSize);
                x2 = Mathf.Clamp(x2, 0, inputSize);
                y2 = Mathf.Clamp(y2, 0, inputSize);

                // Map back from letterboxed model space -> source capture space
                if (assumeLetterboxed)
                {
                    x1 = (x1 - padX) / scale;
                    y1 = (y1 - padY) / scale;
                    x2 = (x2 - padX) / scale;
                    y2 = (y2 - padY) / scale;

                    x1 = Mathf.Clamp(x1, 0, srcW);
                    y1 = Mathf.Clamp(y1, 0, srcH);
                    x2 = Mathf.Clamp(x2, 0, srcW);
                    y2 = Mathf.Clamp(y2, 0, srcH);
                }

                _detections.Add(new YoloDetection
                {
                    x1 = x1,
                    y1 = y1,
                    x2 = x2,
                    y2 = y2,
                    cls = bestCls,
                    conf = bestScore,
                });
            }

            ApplyNmsInPlace(_detections, iouThreshold);

            // Cache status info for periodic logging.
            _lastDetCount = _detections.Count;
            _lastTopDet = (_detections.Count > 0) ? _detections[0] : null;

            // Log once per second so we can tell if inference is producing any detections.
            if (Time.unscaledTime >= _nextStatusLogTime)
            {
                _nextStatusLogTime = Time.unscaledTime + 1f;

                if (_lastDetCount == 0)
                {
                    Debug.Log($"[YoloSentisClient] dets=0 (scoreThr={scoreThreshold:0.00})");
                }
                else
                {
                    Debug.Log($"[YoloSentisClient] dets={_lastDetCount} top: cls={_lastTopDet.cls} conf={_lastTopDet.conf:0.000} box=({_lastTopDet.x1:0.0},{_lastTopDet.y1:0.0})-({_lastTopDet.x2:0.0},{_lastTopDet.y2:0.0})");
                }
            }

            var result = new YoloResult
            {
                frame_id = 0,
                width = srcW,
                height = srcH,
                detections = _detections
            };

            LatestJson = JsonUtility.ToJson(result);
        }

        static void ApplyNmsInPlace(List<YoloDetection> dets, float iouThreshold)
        {
            if (dets == null || dets.Count == 0) return;

            dets.Sort((a, b) => b.conf.CompareTo(a.conf));

            var keep = new List<YoloDetection>(dets.Count);

            for (int i = 0; i < dets.Count; i++)
            {
                var a = dets[i];
                bool suppressed = false;

                for (int k = 0; k < keep.Count; k++)
                {
                    var b = keep[k];
                    if (IoU(a, b) > iouThreshold)
                    {
                        suppressed = true;
                        break;
                    }
                }

                if (!suppressed)
                    keep.Add(a);
            }

            dets.Clear();
            dets.AddRange(keep);
        }

        static float IoU(YoloDetection a, YoloDetection b)
        {
            float x1 = Mathf.Max(a.x1, b.x1);
            float y1 = Mathf.Max(a.y1, b.y1);
            float x2 = Mathf.Min(a.x2, b.x2);
            float y2 = Mathf.Min(a.y2, b.y2);

            float iw = Mathf.Max(0f, x2 - x1);
            float ih = Mathf.Max(0f, y2 - y1);
            float inter = iw * ih;

            float areaA = Mathf.Max(0f, a.x2 - a.x1) * Mathf.Max(0f, a.y2 - a.y1);
            float areaB = Mathf.Max(0f, b.x2 - b.x1) * Mathf.Max(0f, b.y2 - b.y1);
            float union = areaA + areaB - inter;

            return union <= 0f ? 0f : inter / union;
        }
    }
}
