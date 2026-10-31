using System;
using System.Threading;

namespace MNN.Unity
{
    // The model uses phase even where spectral magnitude is close to zero.
    // Preserve the source float real-FFT order before evaluating angle/cos/sin.
    internal sealed class MNNResembleDenoiserSpectral
    {
        private const int Size = 1680, Hop = 420, Bins = 841;
        private readonly MNNSourceRealFft _analysis = new MNNSourceRealFft(Size);
        private readonly MNNFftPlan _inverse = new MNNFftPlan(Size);
        private readonly float[] _window;

        internal MNNResembleDenoiserSpectral(float[] window) => _window = (float[])window.Clone();

        internal float[] Analyze(float[] samples, out int frames, out float scale, CancellationToken token)
        {
            float peak = 0;
            foreach (float value in samples) peak = Math.Max(peak, Math.Abs(value));
            scale = Math.Max(peak, 1e-7f);
            var padded = new float[samples.Length + 441];
            for (int i = 0; i < samples.Length; ++i) padded[i] = samples[i] / scale;
            peak = 0;
            foreach (float value in padded) peak = Math.Max(peak, Math.Abs(value));
            float denominator = peak + 1e-7f;
            for (int i = 0; i < padded.Length; ++i) padded[i] /= denominator;
            frames = padded.Length / Hop;
            int plane = frames * Bins;
            var features = new float[3 * plane];
            var input = new float[Size]; var real = new float[Bins]; var imaginary = new float[Bins];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int i = 0; i < Size; ++i)
                {
                    int index = frame * Hop + i - Size / 2;
                    if (index < 0) index = -index;
                    if (index >= padded.Length) index = 2 * padded.Length - 2 - index;
                    input[i] = padded[index] * _window[i];
                }
                _analysis.Forward(input, real, imaginary);
                for (int bin = 0; bin < Bins; ++bin)
                {
                    int index = bin * frames + frame;
                    float phase = (float)Math.Atan2(imaginary[bin], real[bin]);
                    features[index] = (float)Math.Sqrt((double)real[bin] * real[bin] + (double)imaginary[bin] * imaginary[bin]);
                    features[plane + index] = (float)Math.Cos(phase);
                    features[2 * plane + index] = (float)Math.Sin(phase);
                }
            }
            return features;
        }

        internal float[] Synthesize(float[] spectrum, int frames, int samples, float scale, CancellationToken token)
        {
            int plane = frames * Bins;
            if (spectrum.Length != 2 * plane) throw new ArgumentException("Unexpected Resemble Denoiser spectrum size.", nameof(spectrum));
            var sum = new double[Size + frames * Hop]; var envelope = new double[sum.Length];
            var real = new double[Size]; var imaginary = new double[Size];
            for (int frame = 0; frame <= frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                int sourceFrame = Math.Min(frame, frames - 1);
                for (int bin = 0; bin < Bins; ++bin)
                {
                    int index = bin * frames + sourceFrame;
                    real[bin] = spectrum[index]; imaginary[bin] = spectrum[plane + index];
                    if (bin == 0 || bin == Bins - 1) continue;
                    real[Size - bin] = real[bin]; imaginary[Size - bin] = -imaginary[bin];
                }
                imaginary[0] = imaginary[Size / 2] = 0;
                _inverse.Transform(real, imaginary, true);
                int start = frame * Hop;
                for (int i = 0; i < Size; ++i)
                {
                    sum[start + i] += real[i] * _window[i]; envelope[start + i] += (double)_window[i] * _window[i];
                }
            }
            var output = new float[samples];
            for (int i = 0; i < samples; ++i)
            {
                int index = i + Size / 2;
                output[i] = (float)(sum[index] / envelope[index] * scale);
            }
            return output;
        }
    }
}
