using System;

namespace MNN.Unity
{
    /// <summary>Relative inverse depth: larger values mean closer. Values have no
    /// metric unit and cannot be compared as distances across different images.
    /// The raw grid is row-major, top-left origin; SampleAt maps source pixels
    /// to that grid with half-pixel bilinear interpolation.</summary>
    public sealed class MNNImageDepthResult
    {
        private readonly float[] _values;
        public int Width { get; }
        public int Height { get; }
        public int GridWidth { get; }
        public int GridHeight { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        public float[] RelativeInverseDepth => (float[])_values.Clone();

        internal MNNImageDepthResult(int width, int height, int gridWidth, int gridHeight, float[] values, float minimum, float maximum)
        {
            Width = width; Height = height; GridWidth = gridWidth; GridHeight = gridHeight;
            _values = values; Minimum = minimum; Maximum = maximum;
        }

        public float SampleAt(int x, int y)
        {
            if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
            return MNNImageSampling.Sample(_values, GridWidth, GridHeight,
                (x + .5f) * GridWidth / Width - .5f, (y + .5f) * GridHeight / Height - .5f);
        }
    }
}
