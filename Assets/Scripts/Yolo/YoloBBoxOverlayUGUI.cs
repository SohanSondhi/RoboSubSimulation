using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoboSubSimulation.Yolo
{
    /// <summary>
    /// How to map detection coordinates to canvas coordinates.
    /// </summary>
    public enum ScaleMode
    {
        /// <summary>Stretch X and Y independently to fill canvas. Use when camera fills the whole screen.</summary>
        Stretch,
        /// <summary>Preserve aspect ratio with letterboxing. Use when capture and display have different aspects.</summary>
        PreserveAspect
    }

    /// <summary>
    /// Draws 2D bounding boxes on a Canvas for the latest detection json.
    /// Assumes detections are in pixel coordinates relative to the captured frame.
    /// </summary>
    public class YoloBBoxOverlayUGUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Sentis client producing YoloResult JSON.")]
        public YoloSentisClient sentisClient;

        public RectTransform canvasRect;
        
        [Tooltip("Optional: a UI Panel/Image prefab with an Outline. If not assigned, boxes are created automatically.")]
        public RectTransform boxPrefab;

        [Header("Overlay")]
        public int maxBoxes = 50;

        [Tooltip("Stretch: scale X/Y independently to fill canvas (use when camera fills screen).\nPreserveAspect: letterbox with uniform scale (use when capture/display aspects differ).")]
        public ScaleMode scaleMode = ScaleMode.Stretch;

        [Header("Filtering")]
        [Tooltip("Minimum confidence to display a box (0-1).")]
        [Range(0f, 1f)]
        public float minConfidence = 0.25f;

        [Tooltip("Only show these class indices. Leave empty to show all. Check labels.txt for class indices (0=first label, 1=second, etc.)")]
        public List<int> showOnlyClasses;

        [Header("Box Style (used when no prefab assigned)")]
        public Color boxColor = Color.yellow;
        public float boxThickness = 3f;

        [Header("Debug")]
        [Tooltip("If enabled, logs when new JSON arrives so you can confirm real-time updates.")]
        public bool debugLogJsonUpdates = false;

        [Tooltip("Log box position calculations for debugging alignment issues.")]
        public bool debugLogBoxPositions = false;

        [Tooltip("If enabled, hides boxes when JSON hasn't changed for this many seconds (helps avoid displaying stale detections). 0 disables.")]
        public float hideIfStaleAfterSeconds = 0f;

        private readonly List<RectTransform> _pool = new List<RectTransform>();

        private string _lastJson;
        private float _lastJsonTime;

        private void Start()
        {
            if (canvasRect == null)
                canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();

            if (sentisClient == null)
                sentisClient = FindFirstObjectByType<YoloSentisClient>();

            for (int i = 0; i < maxBoxes; i++)
            {
                RectTransform rt;
                
                if (boxPrefab != null)
                {
                    rt = Instantiate(boxPrefab, transform);
                }
                else
                {
                    // Auto-create a simple box with 4 border images
                    rt = CreateBoxRuntime();
                }
                
                rt.gameObject.SetActive(false);
                _pool.Add(rt);
            }
            
            Debug.Log($"[YoloBBoxOverlayUGUI] Initialized with {maxBoxes} box pool. Canvas size: {canvasRect?.rect.width}x{canvasRect?.rect.height}");
            
            // Diagnostic: Log canvas setup to help debug alignment
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Debug.Log($"[YoloBBoxOverlayUGUI] Canvas render mode: {canvas.renderMode}, " +
                          $"sortingOrder: {canvas.sortingOrder}, " +
                          $"pixelRect: {canvas.pixelRect}, " +
                          $"scaleFactor: {canvas.scaleFactor}");
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler != null)
                    Debug.Log($"[YoloBBoxOverlayUGUI] CanvasScaler mode: {scaler.uiScaleMode}, " +
                              $"refRes: {scaler.referenceResolution}, matchWidthOrHeight: {scaler.matchWidthOrHeight}");
            }
        }

        private RectTransform CreateBoxRuntime()
        {
            var go = new GameObject("BBox", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();

            // Create 4 border edges (top, bottom, left, right)
            CreateEdge(rt, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -boxThickness));
            CreateEdge(rt, "Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, boxThickness));
            CreateEdge(rt, "Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(boxThickness, 0));
            CreateEdge(rt, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(boxThickness, 0));

            return rt;
        }

        private void CreateEdge(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
        {
            var edge = new GameObject(name, typeof(RectTransform), typeof(Image));
            edge.transform.SetParent(parent, false);

            var rt = edge.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;

            var img = edge.GetComponent<Image>();
            img.color = boxColor;
            img.raycastTarget = false;
        }

        private void Update()
        {
            if (sentisClient == null)
            {
                HideAll();
                return;
            }

            string json = sentisClient.LatestJson;
            if (string.IsNullOrEmpty(json))
            {
                HideAll();
                return;
            }

            if (!string.Equals(json, _lastJson, StringComparison.Ordinal))
            {
                _lastJson = json;
                _lastJsonTime = Time.unscaledTime;

                if (debugLogJsonUpdates)
                    Debug.Log($"[YoloBBoxOverlayUGUI] New JSON @ {Time.unscaledTime:F2}s ({json.Length} chars)");
            }
            else if (hideIfStaleAfterSeconds > 0f && (Time.unscaledTime - _lastJsonTime) > hideIfStaleAfterSeconds)
            {
                HideAll();
                return;
            }

            YoloResult result;
            try
            {
                result = JsonUtility.FromJson<YoloResult>(json);
            }
            catch
            {
                HideAll();
                return;
            }

            if (result == null || result.detections == null)
            {
                HideAll();
                return;
            }

            // Filter detections by confidence and class
            var filtered = new List<YoloDetection>();
            foreach (var d in result.detections)
            {
                // Skip if below confidence threshold
                if (d.conf < minConfidence)
                    continue;

                // Skip if class filter is set and this class is not in the list
                if (showOnlyClasses.Count > 0 && !showOnlyClasses.Contains(d.cls))
                    continue;

                filtered.Add(d);
            }

            int count = Mathf.Min(filtered.Count, _pool.Count);

            if (count == 0)
            {
                HideAll();
                return;
            }

            float cw = canvasRect.rect.width;
            float ch = canvasRect.rect.height;
            float rw = Mathf.Max(1, result.width);
            float rh = Mathf.Max(1, result.height);

            if (debugLogJsonUpdates)
                Debug.Log($"[YoloBBoxOverlayUGUI] Drawing {count} boxes (filtered from {result.detections.Count}). Canvas: {cw}x{ch}, Result: {rw}x{rh}");

            // Calculate scaling based on mode
            float sx = cw / rw;  // X scale factor
            float sy = ch / rh;  // Y scale factor
            float offX = 0f;
            float offY = 0f;

            if (scaleMode == ScaleMode.PreserveAspect)
            {
                // Letterbox: use uniform scale, center the content
                float uniformScale = Mathf.Min(sx, sy);
                sx = uniformScale;
                sy = uniformScale;
                offX = (cw - rw * uniformScale) * 0.5f;
                offY = (ch - rh * uniformScale) * 0.5f;
            }
            // Stretch mode: sx and sy already set correctly, no offset needed

            if (debugLogBoxPositions)
                Debug.Log($"[YoloBBoxOverlayUGUI] Scale: ({sx:F2}, {sy:F2}), Offset: ({offX:F1}, {offY:F1}), Mode: {scaleMode}");

            for (int i = 0; i < count; i++)
            {
                var d = filtered[i];
                var rt = _pool[i];

                rt.gameObject.SetActive(true);

                // Map detection coords to canvas coords
                float x = d.x1 * sx + offX;
                float yTop = d.y1 * sy + offY;
                float w = (d.x2 - d.x1) * sx;
                float h = (d.y2 - d.y1) * sy;

                // Anchor boxes by top-left
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);

                rt.anchoredPosition = new Vector2(x, -yTop);
                rt.sizeDelta = new Vector2(w, h);

                if (debugLogBoxPositions && i == 0)
                    Debug.Log($"[YoloBBoxOverlayUGUI] Box[0]: canvas=({x:F1}, {-yTop:F1}) size=({w:F1}x{h:F1}) det=({d.x1:F1},{d.y1:F1})-({d.x2:F1},{d.y2:F1}) cls={d.cls} conf={d.conf:F2}");
            }

            for (int i = count; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);
        }

        private void HideAll()
        {
            for (int i = 0; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);
        }
    }
}
