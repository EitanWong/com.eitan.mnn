using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Offline 48 kHz enhancement with the original dithered fbank and noncentered Hamming STFT.</summary>
    public sealed class MNNMossFormer2Se : IDisposable
    {
        public const int SampleRate = 48000, FftSize = 1920, HopSize = 384, FrequencyBins = 961;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNMossFormer2SeFeatures _features;
        private readonly MNNFftPlan _fft = new MNNFftPlan(FftSize);
        private readonly float[] _window;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNMossFormer2Se(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNMossFormer2SeModel.IsComplete(directory)) throw new InvalidDataException("MossFormer2 SE profile or graph verification failed.");
            var config = MNNMossFormer2SeConfig.Read(directory);
            _window = config.analysis_window;
            _features = new MNNMossFormer2SeFeatures(config);
            if (backend == MNNBackendType.Auto)
                backend = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
            _graph = new MNNGenerationGraph(Path.Combine(directory, config.graph), threads,
                backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None);
        }

        public static MNNMossFormer2Se Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType == MNNBackendType.NN) throw new NotSupportedException("MossFormer2 SE NNAPI placement has not been verified.");
            MNNPlatformSupport.RequireSupported();
            return new MNNMossFormer2Se(Path.GetFullPath(directory), threadCount, backendType);
        }

        /// <summary>Enhance 40 ms to ten seconds of PCM. Dither uses a private seed reset for each recording.</summary>
        public float[] Enhance(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNMossFormer2Se));
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                if (pcm.Length < FftSize) throw new ArgumentException("Supply at least 1920 resampled samples.", nameof(samples));
                var features = _features.Extract(pcm, out int frames, cancellationToken);
                var output = _graph.RunOutputs(new[] { "mask" }, new MNNGenerationGraph.Input("features", features, 1, frames, 180))[0];
                if (output.Values.Length != frames * FrequencyBins) throw new InvalidDataException("Unexpected MossFormer2 SE mask shape.");
                var result = Synthesize(pcm, output.Values, frames, cancellationToken);
                if (sampleRate != SampleRate) result = MNNSpeechDsp.Resample(result, SampleRate, sampleRate, cancellationToken);
                if (result.Length != samples.Length) Array.Resize(ref result, samples.Length);
                return result;
            }
        }

        private float[] Synthesize(float[] pcm, float[] mask, int frames, CancellationToken token)
        {
            var accumulation = new double[pcm.Length]; var normalization = new double[pcm.Length];
            var real = new double[FftSize]; var imaginary = new double[FftSize];
            for (int frame = 0; frame < frames; ++frame)
            {
                token.ThrowIfCancellationRequested();
                int start = frame * HopSize;
                for (int i = 0; i < FftSize; ++i)
                {
                    real[i] = (pcm[start + i] * 32768f) * _window[i]; imaginary[i] = 0;
                }
                _fft.Transform(real, imaginary);
                for (int bin = 0; bin < FrequencyBins; ++bin)
                {
                    float gain = mask[frame * FrequencyBins + bin];
                    real[bin] = (float)real[bin] * gain; imaginary[bin] = (float)imaginary[bin] * gain;
                    if (bin == 0 || bin == FrequencyBins - 1) continue;
                    real[FftSize - bin] = real[bin]; imaginary[FftSize - bin] = -imaginary[bin];
                }
                imaginary[0] = imaginary[FftSize / 2] = 0;
                _fft.Transform(real, imaginary, true);
                for (int i = 0; i < FftSize; ++i)
                {
                    accumulation[start + i] += real[i] * _window[i];
                    normalization[start + i] += (double)_window[i] * _window[i];
                }
            }
            var result = new float[pcm.Length];
            for (int i = 0; i < pcm.Length; ++i)
                result[i] = normalization[i] == 0 ? 0 : (float)(accumulation[i] / normalization[i] / 32768);
            return result;
        }

        public float[] EnhanceWaveFile(string path, out int sampleRate, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out sampleRate);
            return Enhance(samples, sampleRate, cancellationToken);
        }

        private static void Validate(float[] samples, int rate, CancellationToken token)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (rate < 8000 || rate > 96000) throw new ArgumentOutOfRangeException(nameof(rate));
            if (samples.Length > (long)rate * 10) throw new ArgumentException("Supply at most ten seconds of PCM.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                    throw new ArgumentException("Supply finite PCM in [-1,1].", nameof(samples));
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _graph.Dispose();
            }
        }
    }
}
