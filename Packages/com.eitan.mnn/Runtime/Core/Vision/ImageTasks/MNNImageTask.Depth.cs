using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNImageTask
    {
        // DPT's minimal scaling policy: choose the scale closest to 1, keep
        // aspect ratio, round to patch-size multiples with ties-to-even.
        internal static void DepthGeometry(int width, int height, out int resizedWidth, out int resizedHeight)
        {
            if (width < 1 || height < 1 || (long)width * height > 16777216)
                throw new ArgumentOutOfRangeException(nameof(width), "Depth supports at most 16,777,216 source pixels.");
            double sx = 518.0 / width, sy = 518.0 / height;
            double scale = Math.Abs(1 - sx) < Math.Abs(1 - sy) ? sx : sy;
            resizedWidth = checked((int)Math.Round(width * scale / 14) * 14);
            resizedHeight = checked((int)Math.Round(height * scale / 14) * 14);
            if (resizedWidth < 14 || resizedHeight < 14 || (long)(resizedWidth / 14) * (resizedHeight / 14) > 3072)
                throw new ArgumentOutOfRangeException(nameof(width), "Depth requires 1–3072 image patches after aspect-preserving resize. Crop very wide/tall images first.");
        }

        internal static float[] DepthTensor(MNNRgbImage image, CancellationToken token = default)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            token.ThrowIfCancellationRequested();
            DepthGeometry(image.Width, image.Height, out int width, out int height);
            var resized = MNNImageSampling.ResizeBicubic(image, width, height, token);
            int plane = width * height;
            var values = new float[plane * 3];
            float[] mean = {.485f, .456f, .406f}, std = {.229f, .224f, .225f};
            for (int y = 0; y < height; ++y)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < width; ++x)
                    for (int c = 0; c < 3; ++c)
                        values[c * plane + y * width + x] = (resized.Pixels[(y * width + x) * 3 + c] * (1f / 255) - mean[c]) / std[c];
            }
            return values;
        }

        private MNNImageTaskResult EstimateDepth(MNNRgbImage image, CancellationToken token)
        {
            DepthGeometry(image.Width, image.Height, out int width, out int height);
            float[] values = DepthTensor(image, token);
            token.ThrowIfCancellationRequested();
            var output = _graph.RunOutputs(new[] {"predicted_depth"}, new MNNGenerationGraph.Input("pixel_values", values, 1, 3, height, width))[0];
            return DecodeDepth(output, image.Width, image.Height, token);
        }

        internal static MNNImageTaskResult DecodeDepth(MNNGenerationGraph.Output output, int width, int height, CancellationToken token = default)
        {
            DepthGeometry(width, height, out int gridWidth, out int gridHeight);
            RequireShape(output, 1, gridHeight, gridWidth);
            if (!(output.Data is float[] values)) throw new InvalidDataException("Depth requires float32 relative inverse depth.");
            float minimum = float.MaxValue, maximum = 0;
            for (int i = 0; i < values.Length; ++i)
            {
                if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
                float value = values[i];
                if (!(value >= 0 && value <= float.MaxValue)) throw new InvalidDataException("Depth contains a negative or non-finite value.");
                minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value);
            }
            var depth = new MNNImageDepthResult(width, height, gridWidth, gridHeight, values, minimum, maximum);
            var pixels = new byte[width * height * 3];
            double range = (double)maximum - minimum;
            for (int y = 0; y < height; ++y)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < width; ++x)
                {
                    byte gray = range > 0 ? MNNImageSampling.Byte((float)((depth.SampleAt(x, y) - minimum) / range * 255)) : (byte)0;
                    int index = (y * width + x) * 3;
                    pixels[index] = gray; pixels[index + 1] = gray; pixels[index + 2] = gray;
                }
            }
            return new MNNImageTaskResult {Depth = depth, Image = new MNNGeneratedImage(pixels, width, height, "Depth Anything V2 Small · relative inverse depth · lighter is closer", 0)};
        }
    }
}
