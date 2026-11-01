using System;

namespace MNN.Unity
{
    /// <summary>Semantic classes and softmax confidence in original-image pixels, top-left origin.
    /// Pixels are nearest-neighbor projections from the model's 257x257 grid; confidence is not calibrated.</summary>
    public sealed class MNNImageSegmentationResult
    {
        private readonly byte[] _classes;
        private readonly float[] _confidence;
        private readonly string[] _labels;
        public int Width { get; }
        public int Height { get; }
        public byte[] ClassIds => (byte[])_classes.Clone();
        public float[] Confidence => (float[])_confidence.Clone();
        public string[] Labels => (string[])_labels.Clone();
        public int[] ClassPixelCounts
        {
            get
            {
                var counts = new int[_labels.Length];
                foreach (byte id in _classes) ++counts[id];
                return counts;
            }
        }

        internal MNNImageSegmentationResult(int width, int height, byte[] classes, float[] confidence, string[] labels)
        {
            Width = width; Height = height;
            _classes = classes; _confidence = confidence; _labels = (string[])labels.Clone();
        }

        public int ClassAt(int x, int y) => _classes[Index(x, y)];
        public float ConfidenceAt(int x, int y) => _confidence[Index(x, y)];
        private int Index(int x, int y)
        {
            if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
            return y * Width + x;
        }
    }
}
