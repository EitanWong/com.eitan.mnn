using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNImageTask
    {
        private MNNImageTaskResult EstimatePose(MNNRgbImage image, CancellationToken token)
        {
            int[] values = PoseTensor(image, token);
            token.ThrowIfCancellationRequested();
            var output = _graph.RunOutputs(new[] {"output_0"}, new MNNGenerationGraph.Input("input", values, 1, 192, 192, 3))[0];
            return new MNNImageTaskResult
            {
                Pose = DecodePose(output, image.Width, image.Height, token),
                Image = new MNNGeneratedImage((byte[])image.Pixels.Clone(), image.Width, image.Height, "MoveNet SinglePose Lightning", 0)
            };
        }

        // Round resized dimensions to nearest integer (ties up), center with any
        // odd extra padding on right/bottom. Reuse actual rounded sizes on decode.
        internal static void PoseGeometry(int width, int height, out int resizedWidth, out int resizedHeight, out int left, out int top)
        {
            if (width < 1 || height < 1 || (long)width * height > 16777216)
                throw new ArgumentOutOfRangeException(nameof(width), "Pose supports at most 16,777,216 source pixels.");
            int longer = Math.Max(width, height);
            resizedWidth = Math.Max(1, (int)(((long)width * 192 + longer / 2) / longer));
            resizedHeight = Math.Max(1, (int)(((long)height * 192 + longer / 2) / longer));
            left = (192 - resizedWidth) / 2; top = (192 - resizedHeight) / 2;
        }

        internal static int[] PoseTensor(MNNRgbImage image, CancellationToken token = default)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            token.ThrowIfCancellationRequested();
            PoseGeometry(image.Width, image.Height, out int width, out int height, out int left, out int top);
            var resized = MNNImageSampling.ResizeBilinear(image, width, height, token);
            var values = new int[192 * 192 * 3];
            for (int y = 0; y < height; ++y)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < width * 3; ++x)
                    values[((y + top) * 192 + left) * 3 + x] = resized.Pixels[y * width * 3 + x];
            }
            return values;
        }

        internal static MNNPoseResult DecodePose(MNNGenerationGraph.Output output, int width, int height, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            RequireShape(output, 1, 1, 17, 3);
            if (!(output.Data is float[] values)) throw new InvalidDataException("MoveNet requires float32 [1,1,17,3] y,x,score output.");
            PoseGeometry(width, height, out int resizedWidth, out int resizedHeight, out int left, out int top);
            var points = new MNNPoseKeypoint[17];
            for (int k = 0; k < points.Length; ++k)
            {
                token.ThrowIfCancellationRequested();
                float y = values[k * 3], x = values[k * 3 + 1], score = values[k * 3 + 2];
                if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) ||
                    float.IsNaN(score) || float.IsInfinity(score) || score < 0 || score > 1)
                    throw new InvalidDataException("MoveNet returned a non-finite coordinate or invalid confidence.");
                // The export adds learned offsets without clipping: joints can
                // lie outside even the input canvas. Retain those estimates.
                double sourceX = ((double)x * 192 - left) * width / resizedWidth;
                double sourceY = ((double)y * 192 - top) * height / resizedHeight;
                if (Math.Abs(sourceX) > float.MaxValue || Math.Abs(sourceY) > float.MaxValue)
                    throw new InvalidDataException("MoveNet coordinate overflows source pixel representation.");
                points[k] = new MNNPoseKeypoint((MNNPoseJoint)k, (float)sourceX, (float)sourceY, score, width, height);
            }
            return new MNNPoseResult(width, height, points);
        }
    }
}
