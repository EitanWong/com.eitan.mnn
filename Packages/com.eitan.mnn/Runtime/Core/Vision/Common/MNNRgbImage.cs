using System;

namespace MNN.Unity
{
    /// <summary>Owned RGB24 pixels, row-major from the top left; safe to share with inference workers.</summary>
    public sealed class MNNRgbImage
    {
        internal byte[] Pixels { get; }
        public int Width { get; }
        public int Height { get; }

        public MNNRgbImage(byte[] pixels, int width, int height)
        {
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));
            if (width < 1 || height < 1 || (long)width * height > 64000000 || pixels.Length != (long)width * height * 3)
                throw new ArgumentException("Expected RGB24 pixels and positive dimensions, at most 64 million pixels.");
            Pixels = (byte[])pixels.Clone();
            Width = width;
            Height = height;
        }

        public byte[] ToArray() => (byte[])Pixels.Clone();
    }
}
