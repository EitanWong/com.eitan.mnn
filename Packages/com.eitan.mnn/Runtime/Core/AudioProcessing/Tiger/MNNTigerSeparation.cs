using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Noncausal two-source TIGER separation with the original PCM amplitude and source order.</summary>
    public sealed class MNNTigerSeparation : IDisposable
    {
        public const int SampleRate = 16000;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNTigerSpectral _spectral;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNTigerSeparation(string directory, int threads, MNNBackendType backend, MNNGpuTuningMode tuning, MNNPrecisionMode precision)
        {
            var config = MNNTigerModel.ReadVerified(directory);
            _spectral = new MNNTigerSpectral(config.analysis_window);
            if (backend == MNNBackendType.Auto)
                backend = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNTigerModel.Graph), threads,
                backendType: backend, precisionMode: precision,
                externalWeightsPath: Path.Combine(directory, MNNTigerModel.Graph + ".weight"),
                gpuTuningMode: tuning, recreateGpuSessionOnShapeChange: true);
        }

        public static MNNTigerSeparation Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto) =>
            LoadWithTuning(directory, threadCount, backendType, MNNGpuTuningMode.None);

        // Candidate comparisons use the same complete PCM path as the public API.
        internal static MNNTigerSeparation LoadWithTuning(string directory, int threadCount, MNNBackendType backendType, MNNGpuTuningMode tuning, MNNPrecisionMode precision = MNNPrecisionMode.High)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("TIGER has validated CPU and Metal profiles only.");
            if (!Enum.IsDefined(typeof(MNNGpuTuningMode), tuning)) throw new ArgumentOutOfRangeException(nameof(tuning));
            if (!Enum.IsDefined(typeof(MNNPrecisionMode), precision)) throw new ArgumentOutOfRangeException(nameof(precision));
            MNNPlatformSupport.RequireSupported();
            return new MNNTigerSeparation(Path.GetFullPath(directory), threadCount, backendType, tuning, precision);
        }

        /// <summary>Separate 321–48000 samples at 16 kHz, or equivalent mono PCM at 8–96 kHz.
        /// Slots have no persistent speaker identity. Output preserves the input sample rate and length.</summary>
        public MNNSeparatedAudio Separate(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNTigerSeparation));
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                if (pcm.Length < 321 || pcm.Length > 48000)
                    throw new ArgumentException("Supply 321–48000 samples after resampling to16kHz.", nameof(samples));
                var spectrum = _spectral.Analyze(pcm, cancellationToken, out int frames);
                var output = _graph.RunOutputs(new[] { "separated" },
                    new MNNGenerationGraph.Input("spectrum", spectrum, 1, 2, MNNTigerSpectral.Bins, frames))[0];
                cancellationToken.ThrowIfCancellationRequested();
                if (output.Shape.Length != 4 || output.Shape[0] != 1 || output.Shape[1] != 4 ||
                    output.Shape[2] != MNNTigerSpectral.Bins || output.Shape[3] != frames)
                    throw new InvalidDataException("Unexpected TIGER source spectrum shape.");
                float[] values = output.Values;
                int plane = 2 * MNNTigerSpectral.Bins * frames;
                var first = Restore(values, 0, plane, frames, pcm.Length, sampleRate, samples.Length, cancellationToken);
                var second = Restore(values, plane, plane, frames, pcm.Length, sampleRate, samples.Length, cancellationToken);
                return new MNNSeparatedAudio(first, second, sampleRate);
            }
        }

        public MNNSeparatedAudio SeparateWaveFile(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var samples = MNNWaveAudio.Read(path, out int sampleRate);
            return Separate(samples, sampleRate, cancellationToken);
        }

        private float[] Restore(float[] values, int offset, int plane, int frames, int count, int sampleRate, int inputCount, CancellationToken token)
        {
            var spectrum = new float[plane];
            Array.Copy(values, offset, spectrum, 0, plane);
            var pcm = _spectral.Synthesize(spectrum, frames, count, token);
            if (sampleRate == SampleRate) return pcm;
            var resampled = MNNSpeechDsp.Resample(pcm, SampleRate, sampleRate, token);
            if (resampled.Length != inputCount) Array.Resize(ref resampled, inputCount);
            return resampled;
        }

        private static void Validate(float[] samples, int sampleRate, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples.Length > (long)sampleRate * 3) throw new ArgumentException("Supply at most three seconds of mono PCM.", nameof(samples));
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
