using System;
using System.Threading;

namespace MNN.Unity
{
    // Original centered, reflect-padded periodic-Hann STFT; unnormalized, one-sided.
    internal sealed class MNNAvTfGridNetSpectral
    {
        internal const int FftSize = 256, HopSize = 128, Bins = 129;
        private readonly MNNFftPlan _fft = new MNNFftPlan(FftSize);
        private readonly float[] _window;

        internal MNNAvTfGridNetSpectral(float[] window)
        {
            if (window == null || window.Length != FftSize) throw new ArgumentException("Supply the original 256-point periodic Hann window.", nameof(window));
            _window = (float[])window.Clone();
        }

        internal float[] Analyze(float[] samples, CancellationToken token, out int frames)
        {
            frames = samples.Length / HopSize + 1;
            int plane = frames * Bins;
            var output = new float[2 * plane];
            var real = new double[FftSize]; var imaginary = new double[FftSize];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int i = 0; i < FftSize; ++i)
                {
                    int index = frame * HopSize + i - FftSize / 2;
                    if (index < 0) index = -index;
                    if (index >= samples.Length) index = 2 * samples.Length - 2 - index;
                    real[i] = samples[index] * _window[i]; imaginary[i] = 0;
                }
                _fft.Transform(real, imaginary);
                for (int bin = 0; bin < Bins; ++bin)
                {
                    int index = frame * Bins + bin;
                    output[index] = (float)real[bin]; output[plane + index] = (float)imaginary[bin];
                }
            }
            return output;
        }

        internal float[] Synthesize(float[] spectrum, int frames, int count, CancellationToken token)
        {
            int plane = frames * Bins;
            if (spectrum.Length != 2 * plane) throw new ArgumentException("Unexpected AV-TFGridNet spectrum length.", nameof(spectrum));
            var sum = new double[(frames - 1) * HopSize + FftSize];
            var envelope = new double[sum.Length];
            var real = new double[FftSize]; var imaginary = new double[FftSize];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int bin = 0; bin < Bins; ++bin)
                {
                    int index = frame * Bins + bin;
                    real[bin] = spectrum[index]; imaginary[bin] = spectrum[plane + index];
                    if (bin == 0 || bin == Bins - 1) continue;
                    real[FftSize - bin] = real[bin]; imaginary[FftSize - bin] = -imaginary[bin];
                }
                imaginary[0] = imaginary[FftSize / 2] = 0;
                _fft.Transform(real, imaginary, true);
                for (int i = 0; i < FftSize; ++i)
                {
                    int index = frame * HopSize + i;
                    sum[index] += real[i] * _window[i]; envelope[index] += (double)_window[i] * _window[i];
                }
            }
            var output = new float[count];
            for (int i = 0; i < count; ++i)
            {
                int index = i + FftSize / 2;
                output[i] = (float)(sum[index] / envelope[index]);
            }
            return output;
        }
    }
}
