using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Noncausal 8 kHz SpEx+ target speaker extraction with reusable reference conditioning.</summary>
    public sealed class MNNSpExPlusExtraction : IDisposable
    {
        public const int SampleRate = 8000;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _reference, _audio;
        private bool _disposed;
        public MNNBackendType Backend => _audio.Backend;
        public MNNBackendType ReferenceBackend => _reference.Backend;

        private MNNSpExPlusExtraction(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNSpExPlusModel.IsComplete(directory)) throw new InvalidDataException("SpEx+ profile or graph verification failed.");
            if (backend == MNNBackendType.Auto)
                backend = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
            _reference = new MNNGenerationGraph(Path.Combine(directory, MNNSpExPlusModel.ReferenceGraph), threads,
                backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None,
                recreateGpuSessionOnShapeChange: true);
            try
            {
                _audio = new MNNGenerationGraph(Path.Combine(directory, MNNSpExPlusModel.AudioGraph), threads,
                    backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None,
                    recreateGpuSessionOnShapeChange: true);
            }
            catch { _reference.Dispose(); throw; }
        }

        public static MNNSpExPlusExtraction Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("SpEx+ supports validated CPU and Metal profiles only.");
            MNNPlatformSupport.RequireSupported();
            return new MNNSpExPlusExtraction(Path.GetFullPath(directory), threadCount, backendType);
        }

        /// <summary>Encode 35 ms–8 s of unpadded, mono target speech. The immutable result can be reused.</summary>
        public MNNSpExPlusReference EncodeReference(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                var pcm = Prepare(samples, sampleRate, 280, 64000, cancellationToken);
                var result = _reference.RunOutputs(new[] { "embedding" }, new MNNGenerationGraph.Input("reference", pcm, 1, pcm.Length))[0];
                if (result.Shape.Length != 2 || result.Shape[0] != 1 || result.Shape[1] != 256)
                    throw new InvalidDataException("Unexpected SpEx+ reference shape.");
                cancellationToken.ThrowIfCancellationRequested();
                return new MNNSpExPlusReference(result.Values);
            }
        }

        /// <summary>Extract 2.5 ms–4 s of mono mixture. Preserves source amplitude and restores the original sample count.</summary>
        public float[] Extract(float[] samples, MNNSpExPlusReference reference, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (reference == null) throw new ArgumentNullException(nameof(reference));
                var pcm = Prepare(samples, sampleRate, 20, 32000, cancellationToken);
                var result = _audio.RunOutputs(new[] { "target" }, new MNNGenerationGraph.Input("mixture", pcm, 1, pcm.Length),
                    new MNNGenerationGraph.Input("embedding", reference.Values, 1, 256))[0];
                if (result.Shape.Length != 2 || result.Shape[0] != 1 || result.Shape[1] != pcm.Length)
                    throw new InvalidDataException("Unexpected SpEx+ waveform shape.");
                cancellationToken.ThrowIfCancellationRequested();
                if (sampleRate == SampleRate) return result.Values;
                var restored = MNNSpeechDsp.Resample(result.Values, SampleRate, sampleRate, cancellationToken);
                if (restored.Length != samples.Length) Array.Resize(ref restored, samples.Length);
                return restored;
            }
        }

        public MNNSpExPlusReference EncodeReferenceWaveFile(string path, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out int rate);
            return EncodeReference(samples, rate, cancellationToken);
        }

        public float[] ExtractWaveFile(string path, MNNSpExPlusReference reference, out int sampleRate, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out sampleRate);
            return Extract(samples, reference, sampleRate, cancellationToken);
        }

        private static float[] Prepare(float[] samples, int rate, int minimum, int maximum, CancellationToken token)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (rate < 8000 || rate > 96000) throw new ArgumentOutOfRangeException(nameof(rate));
            if ((long)samples.Length * SampleRate < (long)minimum * rate || (long)samples.Length * SampleRate > (long)maximum * rate)
                throw new ArgumentException("Recording duration is outside the validated SpEx+ range.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i])) throw new ArgumentException("Supply finite mono PCM.", nameof(samples));
            }
            return MNNSpeechDsp.Resample(samples, rate, SampleRate, token);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MNNSpExPlusExtraction));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _reference.Dispose(); _audio.Dispose();
            }
        }
    }
}
