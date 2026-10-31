using System;
using System.Threading;

namespace MNN.Unity
{
    // Author FFT640/hop160, reflect-centered periodic Hann; planar [real/imag,bin,frame].
    internal sealed class MNNTigerSpectral
    {
        internal const int Size = 640, Hop = 160, Bins = 321;
        private readonly MNNSourceRealFft _analysis = new MNNSourceRealFft(Size);
        private readonly MNNFftPlan _fft = new MNNFftPlan(Size);
        private readonly float[] _window;

        internal MNNTigerSpectral(float[] window)
        {
            if (window == null || window.Length != Size)
                throw new ArgumentException("Supply the original 640-point TIGER periodic Hann window.", nameof(window));
            _window = (float[])window.Clone();
        }

        internal float[] Analyze(float[] samples, CancellationToken token, out int frames)
        {
            frames = samples.Length / Hop + 1;
            int plane = frames * Bins;
            var spectrum = new float[2 * plane];
            var input = new float[Size];
            var real = new float[Bins];
            var imaginary = new float[Bins];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int i = 0; i < Size; ++i)
                {
                    int sample = frame * Hop + i - Size / 2;
                    if (sample < 0) sample = -sample;
                    if (sample >= samples.Length) sample = 2 * samples.Length - 2 - sample;
                    input[i] = samples[sample] * _window[i];
                }
                _analysis.Forward(input, real, imaginary);
                for (int bin = 0; bin < Bins; ++bin)
                {
                    int index = bin * frames + frame;
                    spectrum[index] = real[bin];
                    spectrum[plane + index] = imaginary[bin];
                }
            }
            return spectrum;
        }

        internal float[] Synthesize(float[] spectrum, int frames, int count, CancellationToken token)
        {
            int plane = Bins * frames;
            if (spectrum.Length != 2 * plane)
                throw new ArgumentException("Unexpected single-source TIGER spectrum.", nameof(spectrum));
            var sum = new double[Size + Hop * (frames - 1)];
            var envelope = new double[sum.Length];
            var real = new double[Size];
            var imaginary = new double[Size];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int bin = 0; bin < Bins; ++bin)
                {
                    int index = bin * frames + frame;
                    real[bin] = spectrum[index];
                    imaginary[bin] = spectrum[plane + index];
                    if (bin == 0 || bin == Bins - 1) continue;
                    real[Size - bin] = real[bin];
                    imaginary[Size - bin] = -imaginary[bin];
                }
                imaginary[0] = imaginary[Size / 2] = 0;
                _fft.Transform(real, imaginary, true);
                for (int i = 0; i < Size; ++i)
                {
                    int index = frame * Hop + i;
                    sum[index] += real[i] * _window[i];
                    envelope[index] += (double)_window[i] * _window[i];
                }
            }
            var output = new float[count];
            for (int i = 0; i < count; ++i)
            {
                int index = i + Size / 2;
                output[i] = (float)(sum[index] / envelope[index]);
            }
            return output;
        }
    }
}
