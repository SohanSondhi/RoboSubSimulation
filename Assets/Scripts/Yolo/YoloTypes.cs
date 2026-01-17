using System;
using System.Collections.Generic;

namespace RoboSubSimulation.Yolo
{
    [Serializable]
    public class YoloDetection
    {
        // Pixel coords in source image space.
        public float x1;
        public float y1;
        public float x2;
        public float y2;

        public int cls;
        public float conf;
    }

    [Serializable]
    public class YoloResult
    {
        public int frame_id;
        public int width;
        public int height;
        public List<YoloDetection> detections = new List<YoloDetection>();
    }
}
