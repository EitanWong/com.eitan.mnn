using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>FRCRN complex-spectrum enhancement. STFT/iSTFT and overlap-add stay in managed DSP.</summary>
    public sealed class MNNFrcrnSe : IDisposable
    {
        public const int SampleRate = 16000, FftSize = 640, HopSize = 320, FrequencyBins = 321;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNFftPlan _fft = new MNNFftPlan(FftSize);
        private bool _disposed;

        private MNNFrcrnSe(string directory, int threads, MNNBackendType backend)
        {
            MNNFrcrnSeModel.Verify(directory);
            var config = MNNFrcrnSeConfig.Read(directory);
            if (config.sample_rate != SampleRate || config.fft_size != FftSize || config.hop_size != HopSize || config.frequency_bins != FrequencyBins)
                throw new InvalidDataException("Unsupported FRCRN graph contract.");
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNFrcrnSeModel.PreferredGraph(directory, backend)), threads,
                backendType: backend, precisionMode: MNNPrecisionMode.High);
        }
        public static MNNFrcrnSe Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (!Enum.IsDefined(typeof(MNNBackendType), backendType)) throw new ArgumentOutOfRangeException(nameof(backendType));
            return new MNNFrcrnSe(Path.GetFullPath(directory), threadCount, backendType);
        }
        public MNNBackendType Backend => _graph.Backend;

        public float[] Enhance(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples.Length > (long)sampleRate * 120) throw new ArgumentException("Supply at most 120 seconds of mono PCM.", nameof(samples));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNFrcrnSe));
                Validate(samples, cancellationToken);
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                int frames = 1 + (Math.Max(0, pcm.Length - FftSize) + HopSize - 1) / HopSize;
                var input = new float[checked(FrequencyBins * 2 * frames)];
                var window = CreateWindow();
                var real = new double[FftSize];
                var imaginary = new double[FftSize];
                for (int frame = 0; frame < frames; ++frame)
                {
                    if ((frame & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                    Array.Clear(real, 0, real.Length); Array.Clear(imaginary, 0, imaginary.Length);
                    int start = frame * HopSize;
                    for (int n = 0; n < FftSize; ++n)
                    {
                        int source = start + n;
                        float value = source < 0 || source >= pcm.Length ? 0 : pcm[source];
                        real[n] = value * window[n];
                    }
                    _fft.Transform(real, imaginary);
                    for (int bin = 0; bin < FrequencyBins; ++bin)
                    {
                        input[bin * frames + frame] = (float)real[bin];
                        input[(FrequencyBins + bin) * frames + frame] = (float)imaginary[bin];
                    }
                }
                var output = _graph.Run("outputs", new MNNGenerationGraph.Input("inputs", input, 1, FrequencyBins * 2, frames));
                if (output.Length != input.Length) throw new InvalidDataException("FRCRN returned an invalid complex spectrum.");
                var waveform = new float[checked(frames * HopSize + FftSize)];
                var normalization = new float[waveform.Length];
                for (int frame = 0; frame < frames; ++frame)
                {
                    if ((frame & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                    Array.Clear(real, 0, real.Length); Array.Clear(imaginary, 0, imaginary.Length);
                    for (int bin = 0; bin < FrequencyBins; ++bin)
                    {
                        real[bin] = output[bin * frames + frame];
                        imaginary[bin] = output[(FrequencyBins + bin) * frames + frame];
                        if (bin > 0 && bin < FrequencyBins - 1) { real[FftSize - bin] = real[bin]; imaginary[FftSize - bin] = -imaginary[bin]; }
                    }
                    _fft.Transform(real, imaginary, true);
                    int start = frame * HopSize;
                    for (int n = 0; n < FftSize; ++n)
                    {
                        float gain = window[n];
                        waveform[start + n] += (float)real[n] * gain;
                        normalization[start + n] += gain * gain;
                    }
                }
                var enhanced = new float[pcm.Length];
                for (int i = 0; i < enhanced.Length; ++i)
                {
                    enhanced[i] = waveform[i] / (normalization[i] + 1e-8f);
                }
                if (sampleRate == SampleRate) return enhanced;
                var resampled = MNNSpeechDsp.Resample(enhanced, SampleRate, sampleRate, cancellationToken);
                if (resampled.Length != samples.Length) Array.Resize(ref resampled, samples.Length);
                return resampled;
            }
        }
        public float[] EnhanceWaveFile(string path, CancellationToken cancellationToken = default)
        {
            return EnhanceWaveFile(path, out _, cancellationToken);
        }
        public float[] EnhanceWaveFile(string path, out int sampleRate, CancellationToken cancellationToken = default)
        {
            var pcm = MNNWaveAudio.Read(path, out sampleRate);
            return Enhance(pcm, sampleRate, cancellationToken);
        }
        public Task<float[]> EnhanceAsync(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Enhance(copy, sampleRate, cancellationToken), cancellationToken);
        }
        private static float[] CreateWindow()
        {
            var result = new float[FftSize];
            for (int i = 0; i < result.Length; ++i) result[i] = (float)Math.Sqrt(.5 - .5 * Math.Cos(2 * Math.PI * i / FftSize));
            return result;
        }
        private static void Validate(float[] values, CancellationToken token)
        {
            for (int i = 0; i < values.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]) || Math.Abs(values[i]) > 1) throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(values));
            }
        }
        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _graph.Dispose(); } }
    }
}
