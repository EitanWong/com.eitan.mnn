using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Original one-source Libri1Mix DPTNet waveform enhancement at 16 kHz.</summary>
    public sealed class MNNDptnetEnhancement : IDisposable
    {
        public const int SampleRate = 16000;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNDptnetEnhancement(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNDptnetModel.IsComplete(directory)) throw new InvalidDataException("DPTNet profile or graph verification failed.");
            if (backend == MNNBackendType.Auto) backend = MNNBackendType.CPU;
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNDptnetModel.Graph), threads,
                backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None);
        }

        public static MNNDptnetEnhancement Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.CPU && backendType != MNNBackendType.Auto)
                throw new NotSupportedException("DPTNet supports CPU/Auto. Metal source parity failed; other accelerator placement is unverified.");
            MNNPlatformSupport.RequireSupported();
            return new MNNDptnetEnhancement(Path.GetFullPath(directory), threadCount, backendType);
        }

        /// <summary>Enhance 16–48000 samples at 16 kHz. Preserve original forward_wav amplitude.</summary>
        public float[] Enhance(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNDptnetEnhancement));
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                if (pcm.Length < 16 || pcm.Length > 48000) throw new ArgumentException("Supply 16–48000 resampled samples.", nameof(samples));
                var output = _graph.RunOutputs(new[] { "enhanced" }, new MNNGenerationGraph.Input("mixture", pcm, 1, pcm.Length))[0];
                cancellationToken.ThrowIfCancellationRequested();
                if (output.Shape.Length != 3 || output.Shape[0] != 1 || output.Shape[1] != pcm.Length || output.Shape[2] != 1)
                    throw new InvalidDataException("Unexpected DPTNet enhanced waveform shape.");
                var result = output.Values;
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
            if (samples.Length > sampleRate * 3L) throw new ArgumentException("Supply at most three seconds of mono PCM.", nameof(samples));
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
