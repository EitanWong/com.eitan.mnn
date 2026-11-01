using System;
using System.Threading;

namespace MNN.Unity
{
    // VBx features.py, Apache-2.0, Copyright Brno University of Technology.
    // Original float64 fbank and sliding CMN, cast only after CMN.
    internal sealed class MNNVbxFeatures
    {
        private readonly double[] _window, _mel;
        private readonly MNNFftPlan _fft = new MNNFftPlan(512, false);
        internal MNNVbxFeatures(MNNVbxConfig config) { _window = config.Window; _mel = config.Mel; }

        internal float[] Extract(float[] pcm, CancellationToken token)
        {
            var signal = DitherAndReflect(pcm, token);
            int frames = (signal.Length - 400) / 160 + 1;
            var raw = new double[frames * 64]; var real = new double[512]; var imaginary = new double[512];
            for (int t = 0; t < frames; ++t)
            {
                token.ThrowIfCancellationRequested();
                Spectrum(signal, t * 160, real, imaginary);
                for (int m = 0; m < 64; ++m)
                {
                    double sum = 0;
                    for (int k = 0; k < 257; ++k) sum += real[k] * _mel[k * 64 + m];
                    raw[t * 64 + m] = Math.Log(Math.Max(1, sum));
                }
            }
            return Normalize(raw, frames, token);
        }
        private static double[] DitherAndReflect(float[] pcm, CancellationToken token)
        {
            var result = new double[pcm.Length + 320]; var random = new MNNVbxUniform(3);
            for (int i = 0; i < pcm.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                result[i + 120] = (long)(pcm[i] * 32768.0) + 8 * (random.Next() * 2 - 1);
            }
            for (int i = 0; i < 120; ++i) result[i] = result[239 - i];
            for (int i = 0; i < 200; ++i) result[pcm.Length + 120 + i] = result[pcm.Length + 119 - i];
            return result;
        }
        private void Spectrum(double[] signal, int offset, double[] real, double[] imaginary)
        {
            Array.Clear(real, 0, real.Length); Array.Clear(imaginary, 0, imaginary.Length);
            double mean = 0;
            for (int i = 0; i < 400; ++i) mean += signal[offset + i];
            mean /= 400;
            double previous = signal[offset] - mean;
            for (int i = 0; i < 400; ++i)
            {
                double current = signal[offset + i] - mean;
                real[i] = (current - .97 * previous) * _window[i]; previous = current;
            }
            _fft.Transform(real, imaginary);
            for (int i = 0; i < 257; ++i) real[i] = real[i] * real[i] + imaginary[i] * imaginary[i];
        }
        private static float[] Normalize(double[] raw, int frames, CancellationToken token)
        {
            var cumulative = new double[(frames + 1) * 64]; var result = new float[raw.Length];
            for (int t = 0; t < frames; ++t)
                for (int m = 0; m < 64; ++m) cumulative[(t + 1) * 64 + m] = cumulative[t * 64 + m] + raw[t * 64 + m];
            int length = Math.Min(frames, 300);
            for (int t = 0; t < frames; ++t)
            {
                token.ThrowIfCancellationRequested(); int start = Math.Max(Math.Min(t - 150, frames - length), 0);
                for (int m = 0; m < 64; ++m)
                    result[t * 64 + m] = (float)(raw[t * 64 + m] - (cumulative[(start + length) * 64 + m] - cumulative[start * 64 + m]) / length);
            }
            return result;
        }
    }
}
