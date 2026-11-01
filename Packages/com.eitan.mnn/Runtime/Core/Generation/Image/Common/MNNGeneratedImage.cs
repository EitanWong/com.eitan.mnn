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

        internal MNNGeneratedImage(byte[] rgb, int width, int height, string prompt, int seed)
        {
            Rgb = rgb;
            Width = width;
            Height = height;
            Prompt = prompt;
            Seed = seed;
        }
    }
}
