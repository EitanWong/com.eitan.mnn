using System;
using System.Threading;

namespace MNN.Unity
{
    // Shared NeMo eval log-mel stage; normalization and output padding belong to each model.
    internal sealed class MNNNeMoMelFeatures
    {
        internal const int FftSize = 512, WindowSize = 400, HopSize = 160, Bins = 80, Frequencies = 257;
        private readonly float[] _window, _filters;
        private readonly int _bins;
        private readonly bool _constantPadding;
        internal MNNNeMoMelFeatures(float[] window, float[] filters, int bins = Bins, bool constantPadding = false)
        {
            if (window?.Length != WindowSize || bins <= 0 || filters?.Length != bins * Frequencies)
                throw new ArgumentException("Expected 400 window coefficients and bins x 257 mel coefficients.");
            _window = (float[])window.Clone(); _filters = (float[])filters.Clone();
            _bins = bins; _constantPadding = constantPadding;
        }

        internal float[] Extract(float[] samples, int padTo, out int frames, out int validFrames, CancellationToken token)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty 16 kHz mono PCM.", nameof(samples));
            int count = _constantPadding ? samples.Length : Math.Max(257, samples.Length);
            var emphasized = new float[count];
            emphasized[0] = samples[0];
            for (int i = 1; i < count; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                emphasized[i] = (i < samples.Length ? samples[i] : 0) - .97f * (i - 1 < samples.Length ? samples[i - 1] : 0);
            }
            int spectralFrames = count / HopSize + 1;
            validFrames = _constantPadding ? count / HopSize : spectralFrames;
            frames = (spectralFrames + padTo - 1) / padTo * padTo;
            var output = new float[checked(frames * _bins)];
            var fft = new MNNFftPlan(FftSize); var real = new double[FftSize]; var imaginary = new double[FftSize]; var power = new float[Frequencies];
            for (int frame = 0; frame < validFrames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                Spectrum(emphasized, frame, fft, real, imaginary, power);
                for (int mel = 0; mel < _bins; ++mel)
                {
                    double energy = 0;
                    for (int frequency = 0; frequency < Frequencies; ++frequency) energy += (double)power[frequency] * _filters[mel * Frequencies + frequency];
                    float guarded = (float)energy + 5.9604644775390625e-8f;
                    output[mel * frames + frame] = (float)Math.Log(guarded);
                }
            }
            return output;
        }

        private void Spectrum(float[] samples, int frame, MNNFftPlan fft, double[] real, double[] imaginary, float[] power)
        {
            Array.Clear(real, 0, real.Length); Array.Clear(imaginary, 0, imaginary.Length);
            for (int i = 0; i < WindowSize; ++i)
            {
                int index = frame * HopSize + i - WindowSize / 2;
                if (_constantPadding && (index < 0 || index >= samples.Length)) continue;
                index = index < 0 ? -index : index >= samples.Length ? 2 * samples.Length - 2 - index : index;
                real[i + (FftSize - WindowSize) / 2] = samples[index] * _window[i];
            }
            fft.Transform(real, imaginary);
            for (int bin = 0; bin < power.Length; ++bin)
            {
                float re = (float)real[bin], im = (float)imaginary[bin];
                float magnitude = (float)Math.Sqrt(re * re + im * im);
                power[bin] = magnitude * magnitude;
            }
        }
    }
}
