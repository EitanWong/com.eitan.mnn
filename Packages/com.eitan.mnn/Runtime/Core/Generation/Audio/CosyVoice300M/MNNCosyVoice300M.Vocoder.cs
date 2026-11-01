using System;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNCosyVoice300M
    {
        private float[] Source(float[] f0, MNNCosyVoice300MSampler random, CancellationToken cancellation)
        {
            var phase = new float[9];
            for (int h = 0; h < 9; ++h) phase[h] = (float)((random.Uniform() * 2 - 1) * Math.PI);
            phase[0] = 0;
            var cycles = new float[9];
            var signal = new float[checked(f0.Length * 256)];
            for (int frame = 0; frame < f0.Length; ++frame)
            {
                cancellation.ThrowIfCancellationRequested();
                bool voiced = f0[frame] > 10;
                float noise = voiced ? .003f : .1f / 3;
                for (int n = 0; n < 256; ++n)
                {
                    float sum = _sourceBias[0];
                    for (int h = 0; h < 9; ++h)
                    {
                        cycles[h] += f0[frame] * (h + 1) / 22050f;
                        float angle = (float)(2 * Math.PI * (cycles[h] % 1)) + phase[h];
                        float value = (voiced ? .1f * (float)Math.Sin(angle) : 0) + noise * random.Normal();
                        sum += _sourceWeight[h] * value;
                    }
                    signal[frame * 256 + n] = (float)Math.Tanh(sum);
                }
            }
            return MNNSpeechSpectral16.Stft(signal, cancellation);
        }
    }
}
