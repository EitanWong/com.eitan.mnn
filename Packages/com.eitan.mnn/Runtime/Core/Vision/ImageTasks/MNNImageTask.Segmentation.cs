using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNImageTask
    {
        private static readonly string[] DeepLabLabels = "background|aeroplane|bicycle|bird|boat|bottle|bus|car|cat|chair|cow|diningtable|dog|horse|motorbike|person|pottedplant|sheep|sofa|train|tvmonitor".Split('|');
        public static byte SegmentationColor(int id, int channel)
        {
            byte color = 0;
            for (int bit = 0; bit < 3; ++bit, id >>= 3)
                color |= (byte)(((id >> channel) & 1) << (7 - bit));
            return color;
        }

        private MNNImageTaskResult Segment(MNNRgbImage image, CancellationToken token)
        {
            float[] pixels = SegmentationTensor(image, token);
            token.ThrowIfCancellationRequested();
            var output = _graph.RunOutputs(new[] {"ResizeBilinear_3"}, new MNNGenerationGraph.Input("sub_7", pixels, 1, 257, 257, 3))[0];
            return DecodeSegmentation(output, image.Width, image.Height, token);
        }

        internal static float[] SegmentationTensor(MNNRgbImage image, CancellationToken token = default)
        {
            var resized = MNNImageSampling.ResizeBilinear(image, 257, 257, token);
            var values = new float[257 * 257 * 3];
            for (int i = 0; i < values.Length; ++i)
                values[i] = (resized.Pixels[i] - 127.5f) / 127.5f;
            return values;
        }

        internal static MNNImageTaskResult DecodeSegmentation(MNNGenerationGraph.Output output, int width, int height, CancellationToken token = default)
        {
            RequireShape(output, 1, 257, 257, 21);
            if (!(output.Data is float[] values)) throw new InvalidDataException("DeepLab requires float32 NHWC logits.");
            if (width < 1 || height < 1 || (long)width * height > 16777216)
                throw new ArgumentOutOfRangeException(nameof(width), "Segmentation supports at most 16,777,216 source pixels.");
            var grid = new byte[257 * 257];
            var scores = new float[grid.Length];
            for (int pixel = 0; pixel < grid.Length; ++pixel)
            {
                if (pixel % 257 == 0) token.ThrowIfCancellationRequested();
                int offset = pixel * 21, best = 0;
                for (int c = 0; c < 21; ++c)
                {
                    float value = values[offset + c];
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite DeepLab logit.");
                    if (value > values[offset + best]) best = c;
                }
                double sum = 0;
                for (int c = 0; c < 21; ++c) sum += Math.Exp((double)values[offset + c] - values[offset + best]);
                grid[pixel] = (byte)best;
                scores[pixel] = (float)(1 / sum);
            }
            var ids = new byte[checked(width * height)];
            var confidence = new float[ids.Length];
            var rgb = new byte[checked(ids.Length * 3)];
            var palette = new byte[21 * 3];
            for (int id = 0; id < 21; ++id)
                for (int c = 0; c < 3; ++c) palette[id * 3 + c] = SegmentationColor(id, c);
            for (int y = 0; y < height; ++y)
            {
                token.ThrowIfCancellationRequested();
                int gy = Math.Min(256, (int)((y + .5) * 257 / height));
                for (int x = 0; x < width; ++x)
                {
                    int gx = Math.Min(256, (int)((x + .5) * 257 / width));
                    int i = y * width + x, g = gy * 257 + gx;
                    ids[i] = grid[g]; confidence[i] = scores[g];
                    // Pascal VOC's standard bit-interleaved RGB palette.
                    for (int c = 0; c < 3; ++c) rgb[i * 3 + c] = palette[grid[g] * 3 + c];
                }
            }
            return new MNNImageTaskResult
            {
                Segmentation = new MNNImageSegmentationResult(width, height, ids, confidence, DeepLabLabels),
                Image = new MNNGeneratedImage(rgb, width, height, "DeepLabV3 · Pascal VOC semantic mask", 0)
            };
        }
    }
}
