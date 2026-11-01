using System;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    // Shared Kaldi fbank kernel: 25 ms/10 ms, no dither, Povey window.
    // Zipformer uses reflected edges and 20–7600 Hz; 3D-Speaker uses
    // snipped edges, 20–8000 Hz and per-bin mean subtraction.
    internal static class MNNKaldiFeatures
    {
        private sealed class Filter
        {
            internal int Start;
            internal double[] Weights;
        }

        private static readonly double[] Window = Enumerable.Range(0, 400)
            .Select(n => Math.Pow(.5 - .5 * Math.Cos(2 * Math.PI * n / 399), .85)).ToArray();
        private static readonly double[] HammingWindow = Enumerable.Range(0, 400)
            .Select(n => .54 - .46 * Math.Cos(2 * Math.PI * n / 399)).ToArray();
        private static readonly Filter[] Filters = CreateFilters(7600);
        private static readonly Filter[] SpeakerFilters = CreateFilters(8000);

        private static Filter[] CreateFilters(double highFrequency)
        {
            double low = Mel(20), delta = (Mel(highFrequency) - low) / 81;
            return Enumerable.Range(0, 80).Select(m =>
            {
                var weights = Enumerable.Range(0, 256).Select(k => Math.Max(0,
                    Math.Min((Mel(k * 16000.0 / 512) - low - m * delta) / delta,
                    (low + (m + 2) * delta - Mel(k * 16000.0 / 512)) / delta))).ToArray();
                int first = Array.FindIndex(weights, x => x > 0), last = Array.FindLastIndex(weights, x => x > 0);
                return new Filter {Start = first, Weights = weights.Skip(first).Take(last - first + 1).ToArray()};
            }).ToArray();
        }

        internal static float[] ExtractSpeaker(float[] samples, out int frames, CancellationToken token)
            => ExtractSpeakerCore(samples, out frames, token, false);

        internal static float[] Extract3dSpeaker(float[] samples, out int frames, CancellationToken token)
            => ExtractSpeakerCore(samples, out frames, token, true);

        private static float[] ExtractSpeakerCore(float[] samples, out int frames, CancellationToken token, bool normalizedPcm)
        {
            frames = samples.Length < 400 ? 0 : 1 + (samples.Length - 400) / 160;
            if (frames < (normalizedPcm ? 9 : 20)) throw new ArgumentException(normalizedPcm
                ? "3D-Speaker audio must contain at least 105 ms (nine feature frames)."
                : "Speaker audio must contain at least 215 ms.");
            var features = ExtractFrames(samples, 0, frames, token, true, normalizedSpeaker: normalizedPcm);
            for (int m = 0; m < 80; ++m)
            {
                double mean = 0;
                for (int f = 0; f < frames; ++f) mean += features[f * 80 + m];
                mean /= frames;
                for (int f = 0; f < frames; ++f) features[f * 80 + m] -= (float)mean;
            }
            return features;
        }

        internal static float[] ExtractFsmn(float[] samples, out int frames, CancellationToken token)
        {
            frames = samples.Length < 400 ? 0 : 1 + (samples.Length - 400) / 160;
            return ExtractFrames(samples, 0, frames, token, fsmn: true);
        }

        private static double Mel(double hz) => 1127 * Math.Log(1 + hz / 700);

        internal static float[] Extract(float[] samples, out int frames, CancellationToken token)
        {
            if (samples == null || samples.Length == 0)
                throw new ArgumentException("Expected nonempty 16 kHz mono PCM.", nameof(samples));
            frames = checked((int)(((long)samples.Length + 80) / 160));
            return ExtractFrames(samples, 0, frames, token);
        }

        internal static float[] ExtractFrames(float[] samples, int firstFrame, int frames, CancellationToken token, bool speaker = false, bool fsmn = false, bool normalizedSpeaker = false)
        {
            var output = new float[checked(frames * 80)];
            var real = new double[512];
            var imaginary = new double[512];
            var window = new double[400];
            for (int f = 0; f < frames; ++f)
            {
                if ((f & 31) == 0) token.ThrowIfCancellationRequested();
                double mean = 0;
                for (int n = 0; n < 400; ++n)
                {
                    int p = (firstFrame + f) * 160 + (speaker || fsmn ? 0 : -120) + n;
                    while (p < 0 || p >= samples.Length)
                        p = p < 0 ? -p - 1 : 2 * samples.Length - 1 - p;
                    // WeSpeaker/FSMN use signed-16-bit magnitudes; original 3D-Speaker
                    // fbank consumes normalized PCM. Flooring makes this distinction observable after CMN.
                    window[n] = samples[p] * ((speaker || fsmn) && !normalizedSpeaker ? 32768.0 : 1.0);
                    mean += window[n];
                }
                mean /= 400;
                Array.Clear(real, 0, real.Length);
                Array.Clear(imaginary, 0, imaginary.Length);
                for (int n = 0; n < 400; ++n)
                    real[n] = ((window[n] - mean) - .97 * (window[Math.Max(0, n - 1)] - mean)) * (fsmn ? HammingWindow[n] : Window[n]);
                MNNSpeechDsp.Fft(real, imaginary, false);
                for (int m = 0; m < 80; ++m)
                {
                    var filter = (speaker || fsmn ? SpeakerFilters : Filters)[m];
                    double sum = 0;
                    for (int j = 0; j < filter.Weights.Length; ++j)
                    {
                        int k = filter.Start + j;
                        sum += (real[k] * real[k] + imaginary[k] * imaginary[k]) * filter.Weights[j];
                    }
                    output[f * 80 + m] = (float)Math.Log(Math.Max(1.1920928955078125e-7, sum));
                }
            }
            return output;
        }
    }
}
