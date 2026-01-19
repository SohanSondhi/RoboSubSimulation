using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoboSubSimulation.Yolo
{
    /// <summary>
    /// Captures frames from a camera at a target FPS and sends raw RGB24 frames to a YoloSentisClient.
    /// Uses AsyncGPUReadback to avoid stalling the render thread.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class YoloFrameStreamer : MonoBehaviour
    {
        [Header("Capture Settings")]
        [Tooltip("Base capture width.")]
        public int width = 640;

        [Tooltip("Base capture height (ignored if autoMatchScreenAspect is true).")]
        public int height = 640;

        [Tooltip("Auto-calculate height to match screen aspect ratio. This ensures bounding boxes align correctly.")]
        public bool autoMatchScreenAspect = true;

        [Tooltip("Target frames per second for capture.")]
        public float captureFps = 12f;

        [Tooltip("If true, camera renders only to RT (offscreen). If false, camera also renders to screen.")]
        public bool renderOffscreen = false;

        [Header("References")]
        [Tooltip("Target Sentis client that will run inference locally.")]
        public YoloSentisClient sentisClient;

        private Camera _cam;
        private RenderTexture _rt;
        private byte[] _rgb24;
        private float _lastCaptureTime;
        private bool _readbackPending;

        private int _actualWidth;
        private int _actualHeight;

        public RenderTexture CaptureTexture => _rt;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (sentisClient == null)
            {
                Debug.LogError("[YoloFrameStreamer] Missing sentisClient reference (YoloSentisClient). Disabling.");
                enabled = false;
                return;
            }

            UpdateCaptureDimensions();
            CreateResources();

            Debug.Log($"[YoloFrameStreamer] Started. Capture: {_actualWidth}x{_actualHeight}, Screen: {Screen.width}x{Screen.height}, RenderOffscreen: {renderOffscreen}");
        }

        private void OnDisable()
        {
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
        }

        private void UpdateCaptureDimensions()
        {
            if (autoMatchScreenAspect && Screen.width > 0 && Screen.height > 0)
            {
                float screenAspect = (float)Screen.width / Screen.height;
                _actualWidth = width;
                _actualHeight = Mathf.RoundToInt(width / screenAspect);
                _actualHeight = Mathf.Max(16, (_actualHeight / 2) * 2); // Ensure even number
            }
            else
            {
                _actualWidth = width;
                _actualHeight = height;
            }
        }

        private void CreateResources()
        {
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }

            _rt = new RenderTexture(_actualWidth, _actualHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "YoloCaptureRT",
                useMipMap = false,
                autoGenerateMips = false
            };
            _rt.Create();

            _rgb24 = new byte[_actualWidth * _actualHeight * 3];

            Debug.Log($"[YoloFrameStreamer] Created RT: {_actualWidth}x{_actualHeight}");
        }

        private void LateUpdate()
        {
            if (sentisClient == null) return;
            if (_readbackPending) return;

            float interval = 1f / Mathf.Max(1f, captureFps);
            if (Time.unscaledTime - _lastCaptureTime < interval) return;

            _lastCaptureTime = Time.unscaledTime;

            // Check if screen aspect changed
            if (autoMatchScreenAspect)
            {
                float screenAspect = (float)Screen.width / Screen.height;
                int expectedHeight = Mathf.RoundToInt(width / screenAspect);
                expectedHeight = Mathf.Max(16, (expectedHeight / 2) * 2);

                if (Mathf.Abs(expectedHeight - _actualHeight) > 2)
                {
                    Debug.Log($"[YoloFrameStreamer] Screen aspect changed, recreating RT...");
                    UpdateCaptureDimensions();
                    CreateResources();
                }
            }

            if (!renderOffscreen)
            {
                StartCoroutine(CaptureEndOfFrame());
            }
            else
            {
                // Offscreen: render directly to RT
                var prevTarget = _cam.targetTexture;
                _cam.targetTexture = _rt;
                _cam.Render();
                _cam.targetTexture = prevTarget;
                RequestReadback();
            }
        }

        private IEnumerator CaptureEndOfFrame()
        {
            yield return new WaitForEndOfFrame();

            // Render from camera to RT to capture the same view as on screen
            var prevTarget = _cam.targetTexture;
            _cam.targetTexture = _rt;
            _cam.Render();
            _cam.targetTexture = prevTarget;

            RequestReadback();
        }

        private void RequestReadback()
        {
            if (_rt == null) return;
            _readbackPending = true;
            AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGB24, OnReadback);
        }

        private void OnReadback(AsyncGPUReadbackRequest req)
        {
            _readbackPending = false;

            // Guard against callback after object destruction (e.g., stopping play mode)
            if (this == null) return;

            if (req.hasError)
            {
                Debug.LogWarning("[YoloFrameStreamer] GPU readback error.");
                return;
            }

            var data = req.GetData<byte>();
            if (data.Length != _rgb24.Length)
            {
                Debug.LogWarning($"[YoloFrameStreamer] Size mismatch: got {data.Length}, expected {_rgb24.Length}");
                return;
            }

            data.CopyTo(_rgb24);
            FlipVertical(_rgb24, _actualWidth, _actualHeight, 3);

            sentisClient?.QueueRgbFrame(_rgb24, _actualWidth, _actualHeight);
        }

        private static void FlipVertical(byte[] data, int w, int h, int channels)
        {
            int rowSize = w * channels;
            byte[] temp = new byte[rowSize];

            for (int y = 0; y < h / 2; y++)
            {
                int topRow = y * rowSize;
                int bottomRow = (h - 1 - y) * rowSize;

                Buffer.BlockCopy(data, topRow, temp, 0, rowSize);
                Buffer.BlockCopy(data, bottomRow, data, topRow, rowSize);
                Buffer.BlockCopy(temp, 0, data, bottomRow, rowSize);
            }
        }
    }
}
