using System;
using System.Threading;

namespace MNN.Unity
{
    internal sealed class MNNDeepFilterNet3Dsp
    {
        internal const int FftSize = 960, HopSize = 480, FrequencyBins = 481, ErbBins = 32, DfBins = 96;
        // libDF erb_fb(48000, 960, 32, 2); this fixed profile is validated against libDF.
        private static readonly int[] BandWidths = {2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 5, 5, 7, 7, 8, 10, 12, 13, 15, 18, 20, 24, 28, 31, 37, 42, 50, 56, 67};
        private readonly MNNFftPlan _fft = new MNNFftPlan(FftSize);
        private readonly float[] _window = CreateWindow();
        internal sealed class Features
        {
            internal readonly int Frames;
            internal readonly float[] Spectrum, Erb, Complex;
            internal Features(int frames)
            {
                Frames = frames;
                Spectrum = new float[frames * FrequencyBins * 2];
                Erb = new float[frames * ErbBins];
                Complex = new float[2 * frames * DfBins];
            }
        }
        internal Features Analyze(float[] samples, CancellationToken cancellation = default)
        {
            // Official enhance(pad=True) appends one FFT window, then libDF consumes full hops.
            var features = new Features(samples.Length / HopSize + 2);
            var real = new double[FftSize];
            var imaginary = new double[FftSize];
            for (int frame = 0; frame < features.Frames; ++frame)
            {
                cancellation.ThrowIfCancellationRequested();
                Array.Clear(imaginary, 0, imaginary.Length);
                int start = (frame - 1) * HopSize;
                for (int n = 0; n < FftSize; ++n)
                {
                    int source = start + n;
                    real[n] = source >= 0 && source < samples.Length ? samples[source] * _window[n] : 0;
                }
                _fft.Transform(real, imaginary);
                for (int bin = 0; bin < FrequencyBins; ++bin)
                {
                    int index = (frame * FrequencyBins + bin) * 2;
                    features.Spectrum[index] = (float)(real[bin] / FftSize);
                    features.Spectrum[index + 1] = (float)(imaginary[bin] / FftSize);
                }
            }
            Normalize(features, cancellation);
            return features;
        }

        private static void Normalize(Features features, CancellationToken cancellation)
        {
            const float alpha = .99f;
            var means = new float[ErbBins];
            var units = new float[DfBins];
            for (int band = 0; band < ErbBins; ++band) means[band] = -60f - 30f * band / (ErbBins - 1);
            for (int bin = 0; bin < DfBins; ++bin) units[bin] = .001f - .0009f * bin / (DfBins - 1);
            for (int frame = 0; frame < features.Frames; ++frame)
            {
                cancellation.ThrowIfCancellationRequested();
                int offset = frame * FrequencyBins * 2;
                int firstBin = 0;
                for (int band = 0; band < ErbBins; ++band)
                {
                    float energy = 0, scale = 1f / BandWidths[band];
                    for (int bin = firstBin; bin < firstBin + BandWidths[band]; ++bin)
                    {
                        float r = features.Spectrum[offset + bin * 2], im = features.Spectrum[offset + bin * 2 + 1];
                        energy += (r * r + im * im) * scale;
                    }
                    float decibels = (float)Math.Log10(energy + 1e-10f) * 10f;
                    means[band] = decibels * (1 - alpha) + means[band] * alpha;
                    // Shift normalized features by conv_lookahead=2; zero-pad the end.
                    if (frame >= 2) features.Erb[(frame - 2) * ErbBins + band] = (decibels - means[band]) / 40f;
                    firstBin += BandWidths[band];
                }
                for (int bin = 0; bin < DfBins; ++bin)
                {
                    float r = features.Spectrum[offset + bin * 2], im = features.Spectrum[offset + bin * 2 + 1];
                    units[bin] = (float)Math.Sqrt(r * r + im * im) * (1 - alpha) + units[bin] * alpha;
                    float denominator = (float)Math.Sqrt(units[bin]);
                    if (frame < 2) continue;
                    int index = (frame - 2) * DfBins + bin;
                    features.Complex[index] = r / denominator;
                    features.Complex[features.Frames * DfBins + index] = im / denominator;
                }
            }
        }

        internal float[] Filter(Features features, float[] mask, float[] coefficients, CancellationToken cancellation = default)
        {
            if (mask.Length != features.Frames * ErbBins || coefficients.Length != features.Frames * DfBins * 10)
                throw new System.IO.InvalidDataException("DeepFilterNet decoder shape does not match its feature sequence.");
            var output = new float[features.Spectrum.Length];
            for (int frame = 0; frame < features.Frames; ++frame)
            {
                cancellation.ThrowIfCancellationRequested();
                int bin = 0;
                for (int band = 0; band < ErbBins; ++band)
                {
                    float gain = mask[frame * ErbBins + band];
                    for (int n = 0; n < BandWidths[band]; ++n, ++bin)
                    {
                        int index = (frame * FrequencyBins + bin) * 2;
                        output[index] = features.Spectrum[index] * gain;
                        output[index + 1] = features.Spectrum[index + 1] * gain;
                    }
                }
                for (bin = 0; bin < DfBins; ++bin)
                {
                    float real = 0, imaginary = 0;
                    for (int tap = 0; tap < 5; ++tap)
                    {
                        int sourceFrame = frame + tap - 2;
                        if (sourceFrame < 0 || sourceFrame >= features.Frames) continue;
                        int source = (sourceFrame * FrequencyBins + bin) * 2;
                        int coef = (frame * DfBins + bin) * 10 + tap * 2;
                        float r = features.Spectrum[source], im = features.Spectrum[source + 1];
                        float cr = coefficients[coef], ci = coefficients[coef + 1];
                        real += r * cr - im * ci;
                        imaginary += r * ci + im * cr;
                    }
                    int index = (frame * FrequencyBins + bin) * 2;
                    output[index] = real; output[index + 1] = imaginary;
                }
            }
            return output;
        }

        internal float[] Synthesize(float[] spectrum, int frames, int sampleCount, CancellationToken cancellation = default)
        {
            var output = new float[sampleCount];
            var memory = new float[HopSize];
            var real = new double[FftSize];
            var imaginary = new double[FftSize];
            for (int frame = 0; frame < frames; ++frame)
            {
                cancellation.ThrowIfCancellationRequested();
                for (int bin = 0; bin < FrequencyBins; ++bin)
                {
                    int source = (frame * FrequencyBins + bin) * 2;
                    real[bin] = spectrum[source];
                    imaginary[bin] = bin == 0 || bin == FrequencyBins - 1 ? 0 : spectrum[source + 1];
                    if (bin > 0 && bin < FrequencyBins - 1)
                    {
                        real[FftSize - bin] = real[bin];
                        imaginary[FftSize - bin] = -imaginary[bin];
                    }
                }
                _fft.Transform(real, imaginary, true);
                for (int n = 0; n < HopSize; ++n)
                {
                    int destination = (frame - 1) * HopSize + n;
                    float value = (float)(real[n] * FftSize) * _window[n] + memory[n];
                    if (destination >= 0 && destination < output.Length) output[destination] = value;
                    memory[n] = (float)(real[n + HopSize] * FftSize) * _window[n + HopSize];
                }
            }
            return output;
        }

        private static float[] CreateWindow()
        {
            var window = new float[FftSize];
            for (int n = 0; n < window.Length; ++n)
            {
                double sine = Math.Sin(Math.PI * (n + .5) / FftSize);
                window[n] = (float)Math.Sin(.5 * Math.PI * sine * sine);
            }
            return window;
        }
    }
}
