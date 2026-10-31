using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Mayavoz DCCRN checkpoint-owned first-eval forward, with hop padding and length restoration.</summary>
    public sealed class MNNDccrnEnhancement : IDisposable
    {
        public const int SampleRate = 16000;
        private readonly object _gate = new object();
        private MNNGenerationGraph _graph, _analysis, _encoder, _recurrent, _decoder;
        private bool _disposed;
        private static readonly string[] EncoderOutputs = { "skip0", "skip1", "skip2", "skip3", "skip4", "skip5" };
        public MNNBackendType Backend => _graph?.Backend ?? _encoder.Backend;
        public MNNBackendType AnalysisBackend => _analysis?.Backend ?? MNNBackendType.CPU;
        public MNNBackendType RecurrentBackend => _recurrent?.Backend ?? MNNBackendType.CPU;
        public MNNBackendType DecoderBackend => _decoder?.Backend ?? MNNBackendType.CPU;

        private MNNDccrnEnhancement(string directory, int threads, MNNBackendType backend, MNNGpuTuningMode tuning, MNNPrecisionMode precision)
        {
            MNNDccrnModel.ReadVerified(directory);
            if (backend == MNNBackendType.Auto) backend = MNNBackendType.CPU;
            try
            {
                if (backend == MNNBackendType.CPU)
                    _graph = LoadGraph(directory, MNNDccrnModel.Graph, threads, backend, tuning, precision);
                else
                {
                    _analysis = LoadGraph(directory, MNNDccrnModel.Analysis, threads, MNNBackendType.CPU, tuning, MNNPrecisionMode.High);
                    _encoder = LoadGraph(directory, MNNDccrnModel.Encoder, threads, backend, tuning, precision);
                    _recurrent = LoadGraph(directory, MNNDccrnModel.Recurrent, threads, MNNBackendType.CPU, tuning, MNNPrecisionMode.High);
                    _decoder = LoadGraph(directory, MNNDccrnModel.Decoder, threads, backend, tuning, precision);
                }
            }
            catch { Dispose(); throw; }
        }

        private static MNNGenerationGraph LoadGraph(string directory, string name, int threads, MNNBackendType backend, MNNGpuTuningMode tuning, MNNPrecisionMode precision) =>
            new MNNGenerationGraph(Path.Combine(directory, name), threads, backendType: backend, precisionMode: precision,
                externalWeightsPath: Path.Combine(directory, MNNDccrnModel.Weights), gpuTuningMode: tuning, recreateGpuSessionOnShapeChange: true);

        public static MNNDccrnEnhancement Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto) =>
            LoadWithTuning(directory, threadCount, backendType, MNNGpuTuningMode.None);

        internal static MNNDccrnEnhancement LoadWithTuning(string directory, int threadCount, MNNBackendType backendType, MNNGpuTuningMode tuning, MNNPrecisionMode precision = MNNPrecisionMode.High)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("DCCRN supports CPU or Metal encoding/decoding with CPU analysis and recurrence.");
            if (!Enum.IsDefined(typeof(MNNGpuTuningMode), tuning)) throw new ArgumentOutOfRangeException(nameof(tuning));
            if (!Enum.IsDefined(typeof(MNNPrecisionMode), precision)) throw new ArgumentOutOfRangeException(nameof(precision));
            MNNPlatformSupport.RequireSupported();
            return new MNNDccrnEnhancement(Path.GetFullPath(directory), threadCount, backendType, tuning, precision);
        }

        /// <summary>Enhance up to three seconds of finite mono PCM at 8–96 kHz. Preserve amplitude, length and sample rate.</summary>
        public float[] Enhance(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNDccrnEnhancement));
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                if (pcm.Length < 1 || pcm.Length > 48000) throw new ArgumentException("Supply 1–48000 resampled samples.", nameof(samples));
                var padded = new float[((pcm.Length + 99) / 100) * 100];
                Array.Copy(pcm, padded, pcm.Length);
                var output = Run(padded, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (output.Shape.Length != 3 || output.Shape[0] != 1 || output.Shape[1] != 1 || output.Shape[2] != padded.Length)
                    throw new InvalidDataException("Unexpected DCCRN enhanced waveform shape.");
                var result = output.Values;
                if (result.Length != pcm.Length) Array.Resize(ref result, pcm.Length);
                if (sampleRate != SampleRate) result = MNNSpeechDsp.Resample(result, SampleRate, sampleRate, cancellationToken);
                if (result.Length != samples.Length) Array.Resize(ref result, samples.Length);
                return result;
            }
        }

        private MNNGenerationGraph.Output Run(float[] pcm, CancellationToken token)
        {
            var input = new MNNGenerationGraph.Input("pcm", pcm, 1, 1, pcm.Length);
            if (_graph != null) return _graph.RunOutputs(new[] { "enhanced" }, input)[0];
            var analysis = _analysis.RunOutputs(new[] { "features", "magnitude", "phase" }, input);
            token.ThrowIfCancellationRequested();
            var skips = _encoder.RunOutputs(EncoderOutputs, analysis[0].AsInput("features"));
            token.ThrowIfCancellationRequested();
            var recurrent = _recurrent.RunOutputs(new[] { "recurrent" }, skips[5].AsInput("encoded"));
            token.ThrowIfCancellationRequested();
            var inputs = new MNNGenerationGraph.Input[9];
            inputs[0] = recurrent[0].AsInput("recurrent");
            for (int i = 0; i < skips.Length; ++i) inputs[i + 1] = skips[i].AsInput(EncoderOutputs[i]);
            inputs[7] = analysis[1].AsInput("magnitude");
            inputs[8] = analysis[2].AsInput("phase");
            return _decoder.RunOutputs(new[] { "enhanced" }, inputs)[0];
        }

        public float[] EnhanceWaveFile(string path, out int sampleRate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var samples = MNNWaveAudio.Read(path, out sampleRate);
            return Enhance(samples, sampleRate, cancellationToken);
        }

        private static void Validate(float[] samples, int sampleRate, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples.Length > sampleRate * 3L) throw new ArgumentException("Supply at most three seconds of mono PCM.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i])) throw new ArgumentException("Supply finite mono PCM.", nameof(samples));
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _graph?.Dispose(); _analysis?.Dispose(); _encoder?.Dispose(); _recurrent?.Dispose(); _decoder?.Dispose();
            }
        }
    }
}
