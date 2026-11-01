using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNImageTask
    {
        private MNNImageTaskResult Matte(MNNRgbImage image, CancellationToken token)
        {
            var output = Run(image, "input", "output", 512, 512, 127.5f, 127.5f, token);
            RequireShape(output, 1, 1, 512, 512);
            float[] values = output.Values;
            var alpha = new float[image.Width * image.Height];
            var rgb = new byte[checked(alpha.Length * 3)];
            for (int y = 0; y < image.Height; ++y)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < image.Width; ++x)
                {
                    float a = MNNImageSampling.Sample(values, 512, 512, (x + .5f) * 512 / image.Width - .5f, (y + .5f) * 512 / image.Height - .5f);
                    if (a < -.0001f || a > 1.0001f) throw new InvalidDataException("MODNet must produce alpha probabilities.");
                    int i = y * image.Width + x;
                    alpha[i] = a = Math.Max(0, Math.Min(1, a));
                    for (int c = 0; c < 3; ++c) rgb[i * 3 + c] = MNNImageSampling.Byte(image.Pixels[i * 3 + c] * a + 255 * (1 - a));
                }
            }
            return new MNNImageTaskResult {Alpha = alpha, Image = new MNNGeneratedImage(rgb, image.Width, image.Height, "MODNet · white background preview; alpha returned separately", 0)};
        }

        private MNNImageTaskResult Upscale(MNNRgbImage image, CancellationToken token)
        {
            if ((long)image.Width * image.Height > 4000000)
                throw new ArgumentException("Real-ESRGAN input is limited to four million pixels (64 million output pixels).");
            const int tileSize = 128, halo = 40, scale = 4;
            int outWidth = checked(image.Width * scale), outHeight = checked(image.Height * scale);
            var rgb = new byte[checked(outWidth * outHeight * 3)];
            for (int top = 0; top < image.Height; top += tileSize)
                for (int left = 0; left < image.Width; left += tileSize)
                {
                    token.ThrowIfCancellationRequested();
                    int right = Math.Min(left + tileSize, image.Width), bottom = Math.Min(top + tileSize, image.Height);
                    int x0 = Math.Max(0, left - halo), y0 = Math.Max(0, top - halo);
                    int width = Math.Min(image.Width, right + halo) - x0, height = Math.Min(image.Height, bottom + halo) - y0;
                    var input = new float[3 * width * height];
                    for (int y = 0; y < height; ++y)
                        for (int x = 0; x < width; ++x)
                            for (int c = 0; c < 3; ++c)
                                input[c * width * height + y * width + x] = image.Pixels[((y0 + y) * image.Width + x0 + x) * 3 + c] / 255f;
                    var output = _graph.RunOutputs(new[] {"output"}, new MNNGenerationGraph.Input("input", input, 1, 3, height, width))[0];
                    RequireShape(output, 1, 3, height * scale, width * scale);
                    float[] values = output.Values;
                    int plane = width * height * scale * scale;
                    for (int y = top * scale; y < bottom * scale; ++y)
                    {
                        token.ThrowIfCancellationRequested();
                        for (int x = left * scale; x < right * scale; ++x)
                            for (int c = 0; c < 3; ++c)
                                rgb[(y * outWidth + x) * 3 + c] = MNNImageSampling.Byte(values[c * plane + (y - y0 * scale) * width * scale + x - x0 * scale] * 255);
                    }
                }
            return new MNNImageTaskResult {Image = new MNNGeneratedImage(rgb, outWidth, outHeight, "Real-ESRGAN x4", 0)};
        }

        private MNNImageTaskResult Rectify(MNNRgbImage image, CancellationToken token)
        {
            var output = Run(image, "image_712x488", "grid_45x31", 488, 712, 0, 255, token, bgr: true, corners: true);
            RequireShape(output, 1, 2, 45, 31);
            return new MNNImageTaskResult {Image = WarpGrid(image, output.Values, 31, 45, token)};
        }

        internal static MNNGeneratedImage WarpGrid(MNNRgbImage image, float[] grid, int gridWidth, int gridHeight, CancellationToken token = default)
        {
            if (grid == null || gridWidth < 2 || gridHeight < 2 || grid.Length != checked(2 * gridWidth * gridHeight))
                throw new ArgumentException("Expected normalized two-channel backward sampling grid.");
            foreach (float value in grid)
                if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 100)
                    throw new InvalidDataException("Invalid document sampling coordinate.");
            var rgb = new byte[image.Pixels.Length];
            for (int y = 0; y < image.Height; ++y)
            {
                token.ThrowIfCancellationRequested();
                float gy = image.Height == 1 ? 0 : y * (gridHeight - 1f) / (image.Height - 1);
                for (int x = 0; x < image.Width; ++x)
                {
                    float gx = image.Width == 1 ? 0 : x * (gridWidth - 1f) / (image.Width - 1);
                    float sx = (MNNImageSampling.Sample(grid, gridWidth, gridHeight, gx, gy) + 1) * .5f * (image.Width - 1);
                    float sy = (MNNImageSampling.Sample(grid, gridWidth, gridHeight, gx, gy, gridWidth * gridHeight) + 1) * .5f * (image.Height - 1);
                    for (int c = 0; c < 3; ++c)
                        // Publisher reference truncates uint8 rather than rounding.
                        rgb[(y * image.Width + x) * 3 + c] = (byte)Math.Max(0, Math.Min(255, MNNImageSampling.Pixel(image.Pixels, image.Width, image.Height, sx, sy, c, true)));
                }
            }
            return new MNNGeneratedImage(rgb, image.Width, image.Height, "UVDoc · backward-grid rectification", 0);
        }
    }
}
