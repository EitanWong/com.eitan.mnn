using System;
using System.Threading;

namespace MNN.Unity
{
    internal static class MNNSmartTurnFeatures
    {
        internal static float[] Extract(float[] pcm, CancellationToken token)
        {
            const int length = 128000;
            var padded = new float[length];
            int count = Math.Min(length, pcm.Length);
            Array.Copy(pcm, pcm.Length - count, padded, length - count, count);
            double sum = 0;
            for (int i = 0; i < length; ++i) sum += padded[i];
            float mean = (float)(sum / length);
            double variance = 0;
            for (int i = 0; i < length; ++i)
            {
                float difference = padded[i] - mean;
                variance += difference * difference;
            }
            float scale = (float)Math.Sqrt((float)(variance / length) + 1e-7f);
            for (int i = 0; i < length; ++i) padded[i] = (padded[i] - mean) / scale;
            token.ThrowIfCancellationRequested();
            return MNNWhisperFeatures.ExtractCentered(padded, token);
        }
    }
}
