using System;
using System.Threading;

namespace MNN.Unity
{
    // Shared top-left RGB sampling. Coordinate conventions are explicit because
    // UVDoc uses align_corners, while image/face networks use half-pixel resize.
    internal static class MNNImageSampling
    {
        internal static float Pixel(byte[] rgb, int width, int height, float x, float y, int channel, bool zeros = false)
        {
            if (!zeros)
            {
                x = Math.Max(0, Math.Min(width - 1, x));
                y = Math.Max(0, Math.Min(height - 1, y));
            }
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            float Read(int xx, int yy) => xx < 0 || xx >= width || yy < 0 || yy >= height ? 0 : rgb[(yy * width + xx) * 3 + channel];
            return (Read(x0, y0) * (1 - fx) + Read(x0 + 1, y0) * fx) * (1 - fy)
                + (Read(x0, y0 + 1) * (1 - fx) + Read(x0 + 1, y0 + 1) * fx) * fy;
        }

        internal static float[] Tensor(MNNRgbImage image, int width, int height, float mean, float std,
            bool bgr = false, bool alignCorners = false, CancellationToken token = default)
        {
            var values = new float[checked(width * height * 3)];
            int plane = width * height;
            for (int y = 0; y < height; ++y)
            {
                token.ThrowIfCancellationRequested();
                float sy = alignCorners ? (height == 1 ? 0 : y * (image.Height - 1f) / (height - 1))
                    : (y + .5f) * image.Height / height - .5f;
                for (int x = 0; x < width; ++x)
                {
                    float sx = alignCorners ? (width == 1 ? 0 : x * (image.Width - 1f) / (width - 1))
                        : (x + .5f) * image.Width / width - .5f;
                    for (int c = 0; c < 3; ++c)
                        values[c * plane + y * width + x] = (Pixel(image.Pixels, image.Width, image.Height, sx, sy, bgr ? 2 - c : c) - mean) / std;
                }
            }
            return values;
        }

        // Pillow's separable, antialiased RGB bilinear resize. Each pass rounds
        // to uint8 using 22-bit coefficients, matching CLIPFeatureExtractor.
        internal static MNNRgbImage ResizeBilinear(MNNRgbImage image, int width, int height, CancellationToken token)
        {
            byte[] horizontal = ResizeAxis(image.Pixels, image.Width, image.Height, width, true, token);
            byte[] pixels = ResizeAxis(horizontal, width, image.Height, height, false, token);
            return new MNNRgbImage(pixels, width, height);
        }

        internal static MNNRgbImage ResizeBicubic(MNNRgbImage image, int width, int height, CancellationToken token)
        {
            byte[] horizontal = ResizeAxis(image.Pixels, image.Width, image.Height, width, true, token, true);
            return new MNNRgbImage(ResizeAxis(horizontal, width, image.Height, height, false, token, true), width, height);
        }

        private static double Cubic(double value)
        {
            double x = Math.Abs(value);
            return x < 1 ? (1.5 * x - 2.5) * x * x + 1 : x < 2 ? (((x - 5) * x + 8) * x - 4) * -.5 : 0;
        }

        private static byte[] ResizeAxis(byte[] input, int width, int height, int size, bool horizontal, CancellationToken token, bool cubic = false)
        {
            if (size < 1 || (long)size * (horizontal ? height : width) > 64000000) throw new ArgumentOutOfRangeException(nameof(size));
            int length = horizontal ? width : height, other = horizontal ? height : width;
            if (length == size) return input;
            int outputWidth = horizontal ? size : width;
            var output = new byte[checked(size * other * 3)];
            double scale = (double)length / size, filterScale = Math.Max(1, scale), support = filterScale * (cubic ? 2 : 1);
            for (int n = 0; n < size; ++n)
            {
                token.ThrowIfCancellationRequested();
                double center = (n + .5) * scale;
                int start = Math.Max(0, (int)(center - support + .5));
                int end = Math.Min(length, (int)(center + support + .5));
                var weights = new double[end - start];
                double sum = 0;
                for (int k = start; k < end; ++k)
                    sum += weights[k - start] = cubic ? Cubic((k + .5 - center) / filterScale) : Math.Max(0, 1 - Math.Abs((k + .5 - center) / support));
                var coefficients = new int[weights.Length];
                for (int k = 0; k < weights.Length; ++k)
                {
                    double coefficient = weights[k] / sum * 4194304;
                    coefficients[k] = (int)(coefficient + (coefficient < 0 ? -.5 : .5));
                }
                for (int j = 0; j < other; ++j)
                    for (int c = 0; c < 3; ++c)
                    {
                        long value = 2097152;
                        for (int k = start; k < end; ++k)
                        {
                            int offset = horizontal ? j * width + k : k * width + j;
                            value += (long)input[offset * 3 + c] * coefficients[k - start];
                        }
                        int target = horizontal ? j * outputWidth + n : n * outputWidth + j;
                        output[target * 3 + c] = (byte)Math.Max(0, Math.Min(255, value >> 22));
                    }
            }
            return output;
        }

        internal static byte Byte(float value) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value)));

        internal static float Sample(float[] values, int width, int height, float x, float y, int offset = 0)
        {
            x = Math.Max(0, Math.Min(width - 1, x));
            y = Math.Max(0, Math.Min(height - 1, y));
            int x0 = (int)x, y0 = (int)y, x1 = Math.Min(x0 + 1, width - 1), y1 = Math.Min(y0 + 1, height - 1);
            float fx = x - x0, fy = y - y0;
            return (values[offset + y0 * width + x0] * (1 - fx) + values[offset + y0 * width + x1] * fx) * (1 - fy)
                + (values[offset + y1 * width + x0] * (1 - fx) + values[offset + y1 * width + x1] * fx) * fy;
        }
    }
}
