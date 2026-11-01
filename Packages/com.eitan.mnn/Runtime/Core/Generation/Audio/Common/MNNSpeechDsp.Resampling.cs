// Managed frontend and synthesis math following the published IndexTTS-2.5,
// FireRedTTS3 and SeamlessM4T reference algorithms. See LICENSE.md.
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    internal static partial class MNNSpeechDsp
    {
        internal static float[] Resample(float[] audio, int source, int target, CancellationToken token, int tailSamples = 0)
        {
            token.ThrowIfCancellationRequested();
            if (source == target)
            {
                int count = tailSamples > 0 ? Math.Min(tailSamples, audio.Length) : audio.Length;
                var copy = new float[count];
                Array.Copy(audio, audio.Length - count, copy, 0, count);
                return copy;
            }

            int a = source, b = target;
            while (b != 0)
            {
                int r = a % b;
                a = b;
                b = r;
            }

            int orig = source / a, next = target / a;
            // Store only each phase's nonzero sinc support. A dense polyphase
            // kernel grows quadratically for coprime rates (e.g. 95999 Hz).
            double cutoff = Math.Min(1, (double)target / source) * .99;
            int width = (int)Math.Ceiling(6 / cutoff), size = 2 * width + 2;
            var kernels = new float[next][];
            var offsets = new int[next];
            for (int j = 0; j < next; ++j)
            {
                if ((j & 255) == 0)
                    token.ThrowIfCancellationRequested();
                double center = (double)j * orig / next;
                offsets[j] = (int)Math.Floor(center) - width;
                kernels[j] = new float[size];
                for (int k = 0; k < size; ++k)
                {
                    double t = (offsets[j] + k - center) * cutoff;
                    if (Math.Abs(t) >= 6)
                        continue;
                    double p = t * Math.PI, w = Math.Cos(p / 12);
                    kernels[j][k] = (float)((p == 0 ? 1 : Math.Sin(p) / p) * w * w * cutoff);
                }
            }

            int total = checked((int)(((long)audio.Length * target + source - 1) / source));
            var result = new float[tailSamples > 0 ? Math.Min(tailSamples, total) : total];
            int first = total - result.Length;
            for (int i = 0; i < result.Length; ++i)
            {
                if ((i & 4095) == 0)
                    token.ThrowIfCancellationRequested();
                int n = first + i;
                int start = n / next * orig + offsets[n % next];
                var kernel = kernels[n % next];
                double sum = 0;
                for (int k = Math.Max(0, -start); k < size && start + k < audio.Length; ++k)
                    sum += (double)audio[start + k] * kernel[k];
                result[i] = (float)sum;
            }

            return result;
        }
    }
}
