using System.Threading;

namespace MNN.Unity
{
    internal static class MNNZipformerFeatures
    {
        internal static float[] Extract(float[] samples, out int frames, CancellationToken token) =>
            MNNKaldiFeatures.Extract(samples, out frames, token);
        internal static float[] ExtractFrames(float[] samples, int firstFrame, int frames, CancellationToken token) =>
            MNNKaldiFeatures.ExtractFrames(samples, firstFrame, frames, token);
    }
}
