// Adapted from Nian27/CosyVoice3-MNN (Copyright 2026 Nian27), Apache-2.0.
// Modified for managed signal processing and cancellation. See LICENSE.md.
using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    // Managed counterpart of the release's HiFT reference runner: 16-point
    // centered STFT, periodic Hann, hop 4 and the exported source generator.
    internal static class MNNCosyVoiceVocoder
    {
        internal static float[] Source(float[] f0, float[] weight, float bias, CancellationToken token)
        {
            if (f0 == null || f0.Length == 0 || f0.Length > 2000 || weight == null || weight.Length != 9)
                throw new InvalidDataException("Invalid CosyVoice source shape.");
            var source = new float[checked(f0.Length * 480)];
            var phase = new double[9];
            var sine = new float[9];
            uint state = 1986;
            for (int frame = 0; frame < f0.Length; ++frame)
            {
                token.ThrowIfCancellationRequested();
                bool voiced = f0[frame] > 10;
                float noise = voiced ? .003f : .1f / 3;
                for (int h = 0; h < 9; ++h)
                {
                    phase[h] += (double)f0[frame] * (h + 1) / 24000 * (2 * Math.PI * 480);
                    sine[h] = .1f * (float)Math.Sin(phase[h]);
                }

                for (int n = 0; n < 480; ++n)
                {
                    double merged = bias;
                    for (int h = 0; h < 9; ++h)
                    {
                        // Matches upstream's deterministic uniform xorshift table.
                        state ^= state << 13;
                        state ^= state >> 17;
                        state ^= state << 5;
                        float excitation = (voiced ? sine[h] : 0) + noise * ((state >> 8) / 16777216f);
                        merged += weight[h] * excitation;
                    }

                    source[frame * 480 + n] = (float)Math.Tanh(merged);
                }
            }

            return source;
        }

        internal static float[] Stft(float[] source, CancellationToken token) => MNNSpeechSpectral16.Stft(source, token);
        internal static float[] Istft(float[] spectral, int frames, CancellationToken token) => MNNSpeechSpectral16.Istft(spectral, frames, token);
    }
}
