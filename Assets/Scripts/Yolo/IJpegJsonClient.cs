using System;

namespace RoboSubSimulation.Yolo
{
    /// <summary>
    /// Minimal interface for components that:
    /// 1) accept captured frames (raw RGB24 bytes)
    /// 2) produce latest JSON response (detections)
    /// 
    /// Current project direction: local inference via Unity Sentis.
    /// </summary>
    public interface IJpegJsonClient
    {
        bool connected { get; }
        string LatestJson { get; }

        /// <summary>
        /// Queue a raw RGB24 frame (length must be width*height*3).
        /// </summary>
        void QueueRgbFrame(byte[] rgb24, int width, int height);
    }
}
