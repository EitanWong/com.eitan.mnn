using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>LS-EEND simulated 1–8 speaker checkpoint, complete-recording input and stateful one-frame neural execution.</summary>
    public sealed class MNNLsEendDiarization : IDisposable
    {
        public const int SampleRate = 8000;
        public const int DefaultThreadCount = 2;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _encoder, _decoder;
        private readonly MNNLsEendFeatures _features;
        private bool _disposed;
        public MNNBackendType EncoderBackend => _encoder.Backend;
        public MNNBackendType DecoderBackend => _decoder.Backend;

        private MNNLsEendDiarization(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNLsEendModel.IsComplete(directory)) throw new InvalidDataException("LS-EEND profile or graph verification failed.");
            _features = new MNNLsEendFeatures(MNNLsEendConfig.Read(directory));
            if (backend == MNNBackendType.Auto) backend = MNNBackendType.CPU;
            _encoder = new MNNGenerationGraph(Path.Combine(directory, MNNLsEendModel.Encoder), threads,
                backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None);
            try
            {
                _decoder = new MNNGenerationGraph(Path.Combine(directory, MNNLsEendModel.Decoder), threads,
                    backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None);
            }
            catch { _encoder.Dispose(); throw; }
        }

        public static MNNLsEendDiarization Load(string directory, int threadCount = DefaultThreadCount, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("LS-EEND accelerator execution is unverified on this backend.");
            MNNPlatformSupport.RequireSupported();
            return new MNNLsEendDiarization(Path.GetFullPath(directory), threadCount, backendType);
        }

        /// <summary>Analyze 1–480000 finite mono PCM samples at exactly 8 kHz, with fresh state per call.</summary>
        public MNNLsEendActivity Analyze(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNLsEendDiarization));
                var features = _features.Extract(samples, cancellationToken);
                return new MNNLsEendActivity(AnalyzeFeatures(features, cancellationToken), (double)samples.Length / sampleRate);
            }
        }
        public MNNLsEendActivity AnalyzeWaveFile(string path, CancellationToken cancellationToken = default)
        {
            var samples = MNNWaveAudio.Read(path, out int sampleRate);
            return Analyze(samples, sampleRate, cancellationToken);
        }

        private float[] AnalyzeFeatures(float[] features, CancellationToken token)
        {
            int frames = features.Length / 345;
            var result = new float[frames * 9];
            var encoderKv = new float[4 * 1 * 4 * 64 * 64]; var encoderScale = new float[4 * 4];
            var encoderConv = new float[4 * 1 * 256 * 15];
            var decoderKv = new float[2 * 10 * 4 * 64 * 64]; var decoderScale = new float[2 * 4];
            var window = new float[256 * 19]; var frame = new float[345];
            for (int step = 0; step < frames + 9; ++step)
            {
                token.ThrowIfCancellationRequested();
                float[] embedding = null;
                if (step < frames)
                {
                    Array.Copy(features, step * 345, frame, 0, 345);
                    var output = _encoder.RunOutputs(new[] { "embedding", "next_kv", "next_scale", "next_conv" },
                        new MNNGenerationGraph.Input("feature", frame, 1, 1, 345),
                        new MNNGenerationGraph.Input("previous_kv", encoderKv, 4, 1, 4, 64, 64),
                        new MNNGenerationGraph.Input("previous_scale", encoderScale, 4, 4),
                        new MNNGenerationGraph.Input("previous_conv", encoderConv, 4, 1, 256, 15));
                    embedding = Require(output[0], 1, 1, 256);
                    encoderKv = Require(output[1], 4, 1, 4, 64, 64);
                    encoderScale = Require(output[2], 4, 4); encoderConv = Require(output[3], 4, 1, 256, 15);
                }
                for (int channel = 0; channel < 256; ++channel)
                {
                    Array.Copy(window, channel * 19 + 1, window, channel * 19, 18);
                    window[channel * 19 + 18] = embedding == null ? 0 : embedding[channel];
                }
                if (step < 9) continue;
                var decoded = _decoder.RunOutputs(new[] { "posterior", "next_kv", "next_scale" },
                    new MNNGenerationGraph.Input("embedding_window", window, 1, 256, 19),
                    new MNNGenerationGraph.Input("previous_kv", decoderKv, 2, 10, 4, 64, 64),
                    new MNNGenerationGraph.Input("previous_scale", decoderScale, 2, 4));
                Array.Copy(Require(decoded[0], 1, 1, 9), 0, result, (step - 9) * 9, 9);
                decoderKv = Require(decoded[1], 2, 10, 4, 64, 64); decoderScale = Require(decoded[2], 2, 4);
            }
            return result;
        }

        private static float[] Require(MNNGenerationGraph.Output output, params int[] shape)
        {
            if (output.Shape.Length != shape.Length) throw new InvalidDataException("Unexpected LS-EEND tensor rank.");
            for (int i = 0; i < shape.Length; ++i)
                if (output.Shape[i] != shape[i]) throw new InvalidDataException("Unexpected LS-EEND tensor shape.");
            return output.Values;
        }
        private static void Validate(float[] samples, int sampleRate, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (sampleRate != SampleRate) throw new ArgumentOutOfRangeException(nameof(sampleRate), "LS-EEND requires 8 kHz PCM.");
            if (samples == null || samples.Length < 1 || samples.Length > 480000) throw new ArgumentException("Supply 1–480000 mono samples.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i])) throw new ArgumentException("Supply finite floating-point PCM.", nameof(samples));
            }
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _decoder.Dispose(); _encoder.Dispose();
            }
        }
    }
}
