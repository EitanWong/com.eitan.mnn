using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Offline MossFormerGAN enhancement with original RMS, compressed spectra and Hamming STFT.</summary>
    public sealed partial class MNNMossFormerGan : IDisposable
    {
        public const int SampleRate = 16000, FftSize = 400, HopSize = 100, FrequencyBins = 201;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNFftPlan _fft = new MNNFftPlan(FftSize);
        private readonly MNNRealFft400 _analysisFft = new MNNRealFft400();
        private readonly float[] _window;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNMossFormerGan(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNMossFormerGanModel.IsComplete(directory)) throw new InvalidDataException("MossFormerGAN profile or graph verification failed.");
            _window = MNNMossFormerGanConfig.Read(directory).analysis_window;
            if (backend == MNNBackendType.Auto)
                backend = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNMossFormerGanModel.Graph), threads,
                backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None);
        }

        public static MNNMossFormerGan Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType == MNNBackendType.NN) throw new NotSupportedException("MossFormerGAN NNAPI graph/device placement has not been verified.");
            MNNPlatformSupport.RequireSupported();
            return new MNNMossFormerGan(Path.GetFullPath(directory), threadCount, backendType);
        }

        /// <summary>Enhance an independent recording of 201–160000 samples at 16kHz.
        /// Zero-energy PCM returns silence; the upstream normalization is undefined there.</summary>
        public float[] Enhance(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNMossFormerGan));
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                if (pcm.Length < 201) throw new ArgumentException("MossFormerGAN requires at least 201 resampled samples.", nameof(samples));
                double energy = 0;
                foreach (float value in pcm) energy += (double)value * value;
                if (energy == 0) return new float[samples.Length];
                float gain = (float)Math.Sqrt((float)(pcm.Length / (float)energy));
                int paddedCount = (pcm.Length + HopSize - 1) / HopSize * HopSize;
                int frames = paddedCount / HopSize + 1;
                var features = Analyze(pcm, paddedCount, frames, gain, cancellationToken);
                var outputs = _graph.RunOutputs(new[] { "mask", "residual" },
                    new MNNGenerationGraph.Input("compressed_spectrum", features, 1, 2, frames, FrequencyBins));
                cancellationToken.ThrowIfCancellationRequested();
                int plane = frames * FrequencyBins;
                if (outputs[0].Values.Length != plane || outputs[1].Values.Length != 2 * plane)
                    throw new InvalidDataException("Unexpected MossFormerGAN mask/residual shape.");
                var result = Synthesize(features, outputs[0].Values, outputs[1].Values, frames, pcm.Length, gain, cancellationToken);
                if (sampleRate != SampleRate) result = MNNSpeechDsp.Resample(result, SampleRate, sampleRate, cancellationToken);
                if (result.Length != samples.Length) Array.Resize(ref result, samples.Length);
                return result;
            }
        }

        public float[] EnhanceWaveFile(string path, out int sampleRate, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out sampleRate);
            return Enhance(samples, sampleRate, cancellationToken);
        }

        private static void Validate(float[] samples, int sampleRate, CancellationToken token)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples.Length > (long)sampleRate * 10) throw new ArgumentException("Supply at most ten seconds of mono PCM.", nameof(samples));
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
                _disposed = true;
                _graph.Dispose();
            }
        }
    }
}
