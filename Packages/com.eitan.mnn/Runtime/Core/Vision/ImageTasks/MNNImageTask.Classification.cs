using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNImageTask
    {
        private MNNImageTaskResult Classify(MNNRgbImage image, CancellationToken token)
        {
            // Match this publisher's pinned mobilenet_example.py (RGB).
            float[] input = MNNImageSampling.Tensor(image, 224, 224, 0, 1, token: token);
            float[] means = {103.94f, 116.78f, 123.68f};
            for (int c = 0; c < 3; ++c)
                for (int i = 0; i < 224 * 224; ++i)
                    input[c * 224 * 224 + i] = (input[c * 224 * 224 + i] - means[c]) * .017f;
            var output = _graph.RunOutputs(new[] {"prob"}, new MNNGenerationGraph.Input("data", input, 1, 3, 224, 224))[0];
            float[] values = output.Values;
            if (values.Length != 1000 || values.Any(v => v < 0 || v > 1) || Math.Abs(values.Sum() - 1) > .001)
                throw new InvalidDataException("MobileNetV2 must return 1000 normalized class probabilities.");
            return new MNNImageTaskResult {Labels = (string[])_labels.Clone(), Probabilities = values};
        }

        private MNNImageTaskResult Dehaze(MNNRgbImage image, CancellationToken token)
        {
            // PGL-Net's reference runner reflects the right/bottom edge to a
            // multiple of 32, then crops the prediction back to the source size.
            int width = ((image.Width + 31) / 32) * 32;
            int height = ((image.Height + 31) / 32) * 32;
            var padded = ReflectPad(image, width, height);
            var output = Run(padded, "input", "output", width, height, 0, 255, token);
            RequireShape(output, 1, 3, height, width);
            float[] values = output.Values;
            var rgb = new byte[image.Pixels.Length];
            for (int y = 0; y < image.Height; ++y)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < image.Width; ++x)
                    for (int c = 0; c < 3; ++c)
                        rgb[(y * image.Width + x) * 3 + c] = MNNImageSampling.Byte(MNNImageSampling.Sample(values, width, height, x, y, c * width * height) * 255);
            }
            return new MNNImageTaskResult {Image = new MNNGeneratedImage(rgb, image.Width, image.Height, "PGL-Net-T · RW2AH dehazing", 0)};
        }

        private static MNNRgbImage ReflectPad(MNNRgbImage image, int width, int height)
        {
            var pixels = new byte[width * height * 3];
            for (int y = 0; y < height; ++y)
                for (int x = 0; x < width; ++x)
                {
                    int sx = Reflect(x, image.Width), sy = Reflect(y, image.Height);
                    Buffer.BlockCopy(image.Pixels, (sy * image.Width + sx) * 3, pixels, (y * width + x) * 3, 3);
                }
            return new MNNRgbImage(pixels, width, height);
        }

        private static int Reflect(int value, int length)
        {
            if (length <= 1) return 0;
            int period = length * 2 - 2;
            int n = value % period;
            if (n < 0) n += period;
            return n < length ? n : period - n;
        }
    }
}
