using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Original improved U16/512 SudoRMRF two-speaker WSJ0-2mix inference.</summary>
    public sealed class MNNSudoRmRfSeparation : IDisposable
    {
        public const int SampleRate = 8000;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNSudoRmRfSeparation(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNSudoRmRfModel.IsComplete(directory)) throw new InvalidDataException("SudoRMRF profile or graph verification failed.");
            if (backend == MNNBackendType.Auto)
                backend = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNSudoRmRfModel.Graph), threads,
                backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None,
                recreateGpuSessionOnShapeChange: true);
        }

        public static MNNSudoRmRfSeparation Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("SudoRMRF has validated CPU and Metal profiles only.");
            MNNPlatformSupport.RequireSupported();
            return new MNNSudoRmRfSeparation(Path.GetFullPath(directory), threadCount, backendType);
        }

        /// <summary>Separate 2–32000 mono samples at 8 kHz, or equivalent recordings at 8–96 kHz.
        /// Speaker slots and amplitude follow the author; slots have no persistent speaker identity.</summary>
        public MNNSeparatedAudio Separate(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNSudoRmRfSeparation));
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                if (pcm.Length < 2) throw new ArgumentException("Supply at least two resampled samples.", nameof(samples));
                var normalized = Normalize(pcm, out float mean, out float std);
                var output = _graph.RunOutputs(new[] { "separated" }, new MNNGenerationGraph.Input("mixture", normalized, 1, normalized.Length))[0];
                cancellationToken.ThrowIfCancellationRequested();
                if (output.Shape.Length != 3 || output.Shape[0] != 1 || output.Shape[1] != normalized.Length || output.Shape[2] != 2)
                    throw new InvalidDataException("Unexpected SudoRMRF separated waveform shape.");
                var first = Restore(output.Values, pcm.Length, 0, mean, std);
                var second = Restore(output.Values, pcm.Length, 1, mean, std);
                return new MNNSeparatedAudio(AtSampleRate(first, sampleRate, samples.Length, cancellationToken),
                    AtSampleRate(second, sampleRate, samples.Length, cancellationToken), sampleRate);
            }
        }

        public MNNSeparatedAudio SeparateWaveFile(string path, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out int sampleRate);
            return Separate(samples, sampleRate, cancellationToken);
        }

        private static float[] Normalize(float[] pcm, out float mean, out float std)
        {
            double sum = 0;
            for (int i = 0; i < pcm.Length; ++i) sum += pcm[i];
            mean = (float)(sum / pcm.Length);
            double variance = 0;
            for (int i = 0; i < pcm.Length; ++i)
            {
                double delta = (double)pcm[i] - mean;
                variance += delta * delta;
            }
            std = (float)Math.Sqrt(variance / (pcm.Length - 1));
            var normalized = new float[((pcm.Length + 319) / 320) * 320];
            for (int i = 0; i < pcm.Length; ++i) normalized[i] = (pcm[i] - mean) / (std + 1e-9f);
            return normalized;
        }

        private static float[] Restore(float[] values, int length, int speaker, float mean, float std)
        {
            var output = new float[length];
            for (int i = 0; i < length; ++i) output[i] = values[i * 2 + speaker] * std + mean;
            return output;
        }

        private static float[] AtSampleRate(float[] pcm, int sampleRate, int length, CancellationToken token)
        {
            if (sampleRate == SampleRate) return pcm;
            var resampled = MNNSpeechDsp.Resample(pcm, SampleRate, sampleRate, token);
            if (resampled.Length != length) Array.Resize(ref resampled, length);
            return resampled;
        }

        private static void Validate(float[] samples, int sampleRate, CancellationToken token)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples.Length > (long)sampleRate * 4) throw new ArgumentException("Supply at most four seconds of mono PCM.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]))
                    throw new ArgumentException("Supply finite mono PCM.", nameof(samples));
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
