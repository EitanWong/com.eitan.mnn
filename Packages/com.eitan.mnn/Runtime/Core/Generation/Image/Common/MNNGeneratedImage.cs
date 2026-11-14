using System;

namespace MNN.Unity
{
    public sealed class MNNGeneratedImage
    {
        /// <summary>RGB24 pixels, row-major from top left. Unity textures require vertically reversed rows.</summary>
        public byte[] Rgb { get; }

        public int Width { get; }

        public int Height { get; }

        public string Prompt { get; }

        public int Seed { get; }

        /// <summary>Create an RGB24 result. The caller transfers ownership of the supplied pixel array.</summary>
        public MNNGeneratedImage(byte[] rgb, int width, int height, string prompt, int seed)
        {
            if (rgb == null) throw new ArgumentNullException(nameof(rgb));
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
            if ((long)width * height * 3 != rgb.Length)
                throw new ArgumentException("Supply exactly width × height × 3 RGB24 bytes.", nameof(rgb));
            Rgb = rgb;
            Width = width;
            Height = height;
            Prompt = prompt;
            Seed = seed;
        }
    }
}
