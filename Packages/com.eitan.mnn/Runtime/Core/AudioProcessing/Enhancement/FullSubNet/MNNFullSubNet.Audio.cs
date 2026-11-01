using System;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNFullSubNet
    {
        public const int SampleRate = 16000, FftSize = 512, HopSize = 256, FrequencyBins = 257;
        private readonly MNNFftPlan _fft = new MNNFftPlan(FftSize);
        private readonly float[] _window = CreateWindow();

        /// <summary>Enhance up to ten seconds using utterance-level normalization and centered periodic-Hann STFT.
        /// Each call is an independent recording; output retains the original rate and sample count.</summary>
        public float[] Enhance(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            ValidatePcm(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                ThrowIfDisposed();
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                if (_plus && pcm.Length < 1792) throw new ArgumentException("FullSubNet+ requires at least 112 ms (eight feature frames).");
                int count = pcm.Length;
                if (count <= FftSize / 2) Array.Resize(ref pcm, FftSize / 2 + 1);
                int frames = pcm.Length / HopSize + 1;
                var spectrum = Analyze(pcm, frames, cancellationToken);
                var magnitude = new float[FrequencyBins * frames];
                for (int i = 0; i < magnitude.Length; ++i)
                    magnitude[i] = (float)Math.Sqrt((double)spectrum[i] * spectrum[i] + (double)spectrum[i + magnitude.Length] * spectrum[i + magnitude.Length]);
                cancellationToken.ThrowIfCancellationRequested();
                var mask = PredictMaskCore(magnitude, frames, cancellationToken, spectrum);
                cancellationToken.ThrowIfCancellationRequested();
                var output = Synthesize(spectrum, mask, frames, count, cancellationToken);
                if (sampleRate != SampleRate) output = MNNSpeechDsp.Resample(output, SampleRate, sampleRate, cancellationToken);
                if (output.Length != samples.Length) Array.Resize(ref output, samples.Length);
                return output;
            }
        }

        public float[] EnhanceWaveFile(string path, out int sampleRate, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out sampleRate);
            return Enhance(samples, sampleRate, cancellationToken);
        }

        private float[] Analyze(float[] pcm, int frames, CancellationToken token)
        {
            var spectrum = new float[2 * FrequencyBins * frames];
            var real = new double[FftSize];
            var imaginary = new double[FftSize];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int i = 0; i < FftSize; ++i)
                {
                    int index = frame * HopSize + i - FftSize / 2;
                    if (index < 0) index = -index;
                    if (index >= pcm.Length) index = 2 * pcm.Length - 2 - index;
                    real[i] = pcm[index] * _window[i];
                    imaginary[i] = 0;
                }
                _fft.Transform(real, imaginary);
                for (int bin = 0; bin < FrequencyBins; ++bin)
                {
                    spectrum[bin * frames + frame] = (float)real[bin];
                    spectrum[(FrequencyBins + bin) * frames + frame] = (float)imaginary[bin];
                }
            }
            return spectrum;
        }

        private float[] Synthesize(float[] spectrum, float[] mask, int frames, int count, CancellationToken token)
        {
            int plane = frames * FrequencyBins;
            var accumulation = new double[(frames - 1) * HopSize + FftSize];
            var normalization = new double[accumulation.Length];
            var real = new double[FftSize];
            var imaginary = new double[FftSize];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                for (int bin = 0; bin < FrequencyBins; ++bin)
                {
                    int index = bin * frames + frame;
                    double maskReal = Decompress(mask[index]), maskImaginary = Decompress(mask[plane + index]);
                    real[bin] = maskReal * spectrum[index] - maskImaginary * spectrum[plane + index];
                    imaginary[bin] = maskImaginary * spectrum[index] + maskReal * spectrum[plane + index];
                    if (bin == 0 || bin == FftSize / 2) continue;
                    real[FftSize - bin] = real[bin];
                    imaginary[FftSize - bin] = -imaginary[bin];
                }
                imaginary[0] = imaginary[FftSize / 2] = 0;
                _fft.Transform(real, imaginary, true);
                for (int i = 0; i < FftSize; ++i)
                {
                    int index = frame * HopSize + i;
                    accumulation[index] += real[i] * _window[i];
                    normalization[index] += (double)_window[i] * _window[i];
                }
            }
            var output = new float[count];
            for (int i = 0; i < count; ++i)
            {
                int index = i + FftSize / 2;
                output[i] = (float)(accumulation[index] / normalization[index]);
            }
            return output;
        }

        private static double Decompress(float value)
        {
            double clamped = Math.Max(-9.9, Math.Min(9.9, value));
            return -10 * Math.Log((10 - clamped) / (10 + clamped));
        }

        private static float[] CreateWindow()
        {
            var window = new float[FftSize];
            for (int i = 0; i < window.Length; ++i) window[i] = (float)(.5 - .5 * Math.Cos(2 * Math.PI * i / FftSize));
            return window;
        }

        private static void ValidatePcm(float[] samples, int sampleRate, CancellationToken token)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples.Length > (long)sampleRate * 10) throw new ArgumentException("Supply at most ten seconds; normalization covers the entire recording.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                    throw new ArgumentException("Audio must contain finite PCM in [-1,1].", nameof(samples));
            }
        }
    }
}
