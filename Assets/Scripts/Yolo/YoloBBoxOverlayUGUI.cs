using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoboSubSimulation.Yolo
{
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
        public RectTransform boxPrefab; // a UI Panel/Image with an Outline or border

        [Header("Overlay")]
        public int maxBoxes = 50;

        [Header("Debug")]
        [Tooltip("If enabled, logs when new JSON arrives so you can confirm real-time updates.")]
        public bool debugLogJsonUpdates = false;

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
                var rt = Instantiate(boxPrefab, canvasRect);
                rt.gameObject.SetActive(false);
                _pool.Add(rt);
            }
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

            int count = Mathf.Min(result.detections.Count, _pool.Count);

            // Scale from capture pixel space -> canvas pixel space
            float sx = canvasRect.rect.width / Mathf.Max(1, result.width);
            float sy = canvasRect.rect.height / Mathf.Max(1, result.height);

            for (int i = 0; i < count; i++)
            {
                var d = result.detections[i];
                var rt = _pool[i];

                rt.gameObject.SetActive(true);

                float x = d.x1 * sx;
                float yTop = d.y1 * sy;
                float w = (d.x2 - d.x1) * sx;
                float h = (d.y2 - d.y1) * sy;

                // Anchor boxes by top-left.
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);

                rt.anchoredPosition = new Vector2(x, -yTop);
                rt.sizeDelta = new Vector2(w, h);
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
