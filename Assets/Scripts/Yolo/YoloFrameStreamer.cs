using System;
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
        [Header("Capture")]
        public int width = 640;
        public int height = 640;

        [Tooltip("Target capture FPS to feed to Sentis inference.")]
        public float captureFps = 12f;

        [Tooltip("If true, this camera is inference-only and renders off-screen into a RenderTexture. " +
                 "Do NOT enable this on the player-visible camera.")]
        public bool renderOffscreen = true;

        [Header("References")]
        [Tooltip("Target Sentis client that will run inference locally.")]
        public YoloSentisClient sentisClient;

        private Camera _cam;
        private RenderTexture _rt;
        private RenderTexture _prevTargetTexture;

        private float _nextCaptureTime;

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

            EnsureResources();

            if (renderOffscreen)
            {
                // Inference camera: OK to render permanently into RT.
                _prevTargetTexture = _cam.targetTexture;
                _cam.targetTexture = _rt;
            }

            _nextCaptureTime = Time.time;
        }

        private void OnDisable()
        {
            if (_cam != null)
                _cam.targetTexture = _prevTargetTexture;

            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
        }

        private void EnsureResources()
        {
            if (_rt == null || _rt.width != width || _rt.height != height)
            {
                if (_rt != null)
                {
                    _rt.Release();
                    Destroy(_rt);
                }

                _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "YoloCaptureRT",
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _rt.Create();
            }
        }

        private void LateUpdate()
        {
            if (captureFps <= 0f)
                return;

            if (Time.time < _nextCaptureTime)
                return;

            _nextCaptureTime = Time.time + (1f / captureFps);

            if (!renderOffscreen)
            {
                // Single-camera debug mode: temporarily render into RT.
                _prevTargetTexture = _cam.targetTexture;
                _cam.targetTexture = _rt;
                _cam.Render();
                _cam.targetTexture = _prevTargetTexture;
            }

            // In offscreen mode, the camera already renders into _rt each frame.
            AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGB24, OnReadback);
        }

        private void OnReadback(AsyncGPUReadbackRequest req)
        {
            if (!enabled) return;
            if (req.hasError) return;

            try
            {
                var data = req.GetData<byte>();
                var rgb = data.ToArray();
                sentisClient.QueueRgbFrame(rgb, width, height);
            }
            catch
            {
                // ignore transient errors
            }
        }
    }
}
