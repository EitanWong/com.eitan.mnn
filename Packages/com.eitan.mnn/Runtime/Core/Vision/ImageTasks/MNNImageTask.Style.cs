using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNImageTask
    {
        // The pinned Candy graph is fixed at 224x224, RGB in [0,255].
        // Antialiased resizing matches the independent Pillow/ONNX reference.
        private MNNImageTaskResult Stylize(MNNRgbImage image, CancellationToken token)
        {
            var resized = MNNImageSampling.ResizeBilinear(image, 224, 224, token);
            float[] pixels = MNNImageSampling.Tensor(resized, 224, 224, 0, 1, token: token);
            token.ThrowIfCancellationRequested();
            var output = _graph.RunOutputs(new[] {"output1"}, new MNNGenerationGraph.Input("input1", pixels, 1, 3, 224, 224))[0];
            return new MNNImageTaskResult {Image = DecodeStyle(output, token)};
        }

        internal static MNNGeneratedImage DecodeStyle(MNNGenerationGraph.Output output, CancellationToken token = default)
        {
            RequireShape(output, 1, 3, 224, 224);
            if (!(output.Data is float[] values)) throw new InvalidDataException("Candy style output must be float32 RGB.");
            const int plane = 224 * 224;
            var rgb = new byte[3 * plane];
            for (int y = 0; y < 224; ++y)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < 224; ++x)
                    for (int c = 0; c < 3; ++c)
                    {
                        float value = values[c * plane + y * 224 + x];
                        if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite Candy style output.");
                        rgb[(y * 224 + x) * 3 + c] = MNNImageSampling.Byte(Math.Max(0, Math.Min(255, value)));
                    }
            }
            return new MNNGeneratedImage(rgb, 224, 224, "Candy style · fixed 224 × 224 output", 0);
        }
    }
}
