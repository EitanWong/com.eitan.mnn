using System;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    // kaldi-native-fbank Whisper frontend: centered, reflected, periodic Hann,
    // 400-point FFT and Slaney-normalized mel energies; no preemphasis or DC removal.
    internal static class MNNWhisperFeatures
    {
        private sealed class Filter
        {
            internal int Start;
            internal float[] Weights;
        }

        private static readonly double[] Window = Enumerable.Range(0, 400)
            .Select(n => (double)(float)(.5 - .5 * Math.Cos(2 * Math.PI * n / 400))).ToArray();
        private static readonly double[] ChirpReal = Enumerable.Range(0, 400).Select(n => Math.Cos(Math.PI * n * n / 400)).ToArray();
        private static readonly double[] ChirpImaginary = Enumerable.Range(0, 400).Select(n => Math.Sin(Math.PI * n * n / 400)).ToArray();
        private static readonly double[] KernelReal = new double[1024];
        private static readonly double[] KernelImaginary = new double[1024];
        private static readonly Filter[] Filters = CreateFilters();

        static MNNWhisperFeatures()
        {
            for (int n = 0; n < 400; ++n)
            {
                KernelReal[n] = ChirpReal[n]; KernelImaginary[n] = ChirpImaginary[n];
                if (n == 0) continue;
                KernelReal[1024 - n] = ChirpReal[n]; KernelImaginary[1024 - n] = ChirpImaginary[n];
            }
            MNNSpeechDsp.Fft(KernelReal, KernelImaginary, false);
        }

        private static float Hertz(float mel) => mel <= 15 ? 200f / 3 * mel : 1000 * (float)Math.Exp((mel - 15) * .06875177742094911f);
        private static Filter[] CreateFilters()
        {
            float delta = (15 + 14.545078505785561f * (float)Math.Log(8)) / 81;
            return Enumerable.Range(0, 80).Select(m =>
            {
                float left = Hertz(m * delta), center = Hertz((m + 1) * delta), right = Hertz((m + 2) * delta);
                var weights = Enumerable.Range(0, 201).Select(k =>
                    Math.Max(0, Math.Min((40 * k - left) / (center - left), (right - 40 * k) / (right - center))) * (2 / (right - left))).ToArray();
                int first = Array.FindIndex(weights, x => x > 0), last = Array.FindLastIndex(weights, x => x > 0);
                return new Filter {Start = first, Weights = weights.Skip(first).Take(last - first + 1).ToArray()};
            }).ToArray();
        }

        internal static float[] Extract(float[] samples, out int frames, out int paddedFrames, CancellationToken token)
        {
            if (samples == null || samples.Length < 400 || samples.Length > 472000)
                throw new ArgumentException("Whisper expects 25 ms to 29.5 seconds of 16 kHz mono audio.", nameof(samples));
            frames = (samples.Length + 80) / 160;
            // Match sherpa's ten-second feature tail, round to an even encoder length.
            paddedFrames = Math.Min(3000, (frames + 1001) / 2 * 2);
            return ExtractCore(samples, frames, paddedFrames, false, token);
        }

        // Hugging Face Whisper uses centered reflection without repeating boundary samples.
        // Smart Turn supplies an already normalized, exactly eight-second recording.
        internal static float[] ExtractCentered(float[] samples, CancellationToken token) =>
            ExtractCore(samples, 800, 800, true, token);

        private static float[] ExtractCore(float[] samples, int frames, int paddedFrames, bool centered, CancellationToken token)
        {
            var result = new float[checked(paddedFrames * 80)];
            var re = new double[1024]; var im = new double[1024]; var power = new double[201];
            float maximum = -10;
            for (int f = 0; f < frames; ++f)
            {
                if ((f & 15) == 0) token.ThrowIfCancellationRequested();
                Array.Clear(re, 0, re.Length); Array.Clear(im, 0, im.Length);
                for (int n = 0; n < 400; ++n)
                {
                    int p = f * 160 - (centered ? 200 : 120) + n;
                    if (p < 0) p = centered ? -p : -p - 1;
                    if (p >= samples.Length) p = 2 * samples.Length - p - (centered ? 2 : 1);
                    double x = (float)(samples[p] * Window[n]);
                    re[n] = x * ChirpReal[n]; im[n] = -x * ChirpImaginary[n];
                }
                // Bluestein reduces the non-power-of-two DFT to a cached convolution.
                // Scratch arrays are reused for every frame; no trigonometry in this loop.
                MNNSpeechDsp.Fft(re, im, false);
                for (int k = 0; k < re.Length; ++k)
                {
                    double real = re[k] * KernelReal[k] - im[k] * KernelImaginary[k];
                    im[k] = re[k] * KernelImaginary[k] + im[k] * KernelReal[k]; re[k] = real;
                }
                MNNSpeechDsp.Fft(re, im, true);
                // The final unit-magnitude chirp does not change the power spectrum.
                for (int k = 0; k < power.Length; ++k) power[k] = re[k] * re[k] + im[k] * im[k];
                for (int m = 0; m < 80; ++m)
                {
                    var filter = Filters[m]; double sum = 0;
                    for (int k = 0; k < filter.Weights.Length; ++k) sum += power[filter.Start + k] * filter.Weights[k];
                    float value = (float)Math.Log10(Math.Max(1e-10, sum));
                    result[m * paddedFrames + f] = value; maximum = Math.Max(maximum, value);
                }
            }
            float floor = maximum - 8;
            for (int m = 0; m < 80; ++m)
            {
                token.ThrowIfCancellationRequested();
                for (int f = 0; f < frames; ++f)
                    result[m * paddedFrames + f] = (Math.Max(result[m * paddedFrames + f], floor) + 4) / 4;
            }
            return result;
        }
    }
}
