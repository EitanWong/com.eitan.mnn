using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Original one-source Libri1Mix DCCRNet waveform enhancement at 16 kHz.</summary>
    public sealed class MNNDccrnetEnhancement : IDisposable
    {
        public const int SampleRate = 16000;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private MNNGenerationGraph _encoder, _recurrent, _decoder;
        private bool _disposed;
        public MNNBackendType Backend => _graph?.Backend ?? _encoder.Backend;
        public MNNBackendType RecurrentBackend => _recurrent?.Backend ?? MNNBackendType.CPU;
        public MNNBackendType DecoderBackend => _decoder?.Backend ?? MNNBackendType.CPU;
        private static readonly string[] EncoderOutputs = {
            "encoder_0_real", "encoder_0_imag", "encoder_1_real", "encoder_1_imag",
            "encoder_2_real", "encoder_2_imag", "encoder_3_real", "encoder_3_imag",
            "encoder_4_real", "encoder_4_imag", "encoder_5_real", "encoder_5_imag",
            "spectrum_real", "spectrum_imag"
        };

        private MNNDccrnetEnhancement(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNDccrnetModel.IsComplete(directory)) throw new InvalidDataException("DCCRNet profile or graph verification failed.");
            if (backend == MNNBackendType.Auto)
                backend = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
            if (backend == MNNBackendType.CPU)
                _graph = LoadGraph(directory, MNNDccrnetModel.Graph, threads, backend);
            else
            {
                try
                {
                    _encoder = LoadGraph(directory, MNNDccrnetModel.Encoder, threads, backend);
                    _recurrent = LoadGraph(directory, MNNDccrnetModel.Recurrent, threads, MNNBackendType.CPU);
                    _decoder = LoadGraph(directory, MNNDccrnetModel.Decoder, threads, backend);
                }
                catch { Dispose(); throw; }
            }
        }

        private static MNNGenerationGraph LoadGraph(string directory, string graph, int threads, MNNBackendType backend) =>
            new MNNGenerationGraph(Path.Combine(directory, graph), threads, backendType: backend,
                precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None, recreateGpuSessionOnShapeChange: true);

        public static MNNDccrnetEnhancement Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.CPU && backendType != MNNBackendType.Auto && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("DCCRNet supports CPU/Auto or hybrid Metal convolution with CPU recurrence.");
            MNNPlatformSupport.RequireSupported();
            return new MNNDccrnetEnhancement(Path.GetFullPath(directory), threadCount, backendType);
        }

        /// <summary>Enhance 1000–48000 samples at 16 kHz. Preserve original forward_wav amplitude.</summary>
        public float[] Enhance(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            Validate(samples, sampleRate, cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNDccrnetEnhancement));
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                if (pcm.Length < 1000 || pcm.Length > 48000) throw new ArgumentException("Supply 1000–48000 resampled samples.", nameof(samples));
                var output = Run(pcm, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                int decodedLength = 400 + ((pcm.Length - 400) / 100) * 100;
                int expectedLength = _graph == null ? decodedLength : pcm.Length;
                if (output.Shape.Length != 3 || output.Shape[0] != 1 || output.Shape[1] != expectedLength || output.Shape[2] != 1)
                    throw new InvalidDataException("Unexpected DCCRNet enhanced waveform shape.");
                var result = output.Values;
                if (result.Length != pcm.Length) Array.Resize(ref result, pcm.Length);
                if (sampleRate != SampleRate) result = MNNSpeechDsp.Resample(result, SampleRate, sampleRate, cancellationToken);
                if (result.Length != samples.Length) Array.Resize(ref result, samples.Length);
                return result;
            }
        }

        private MNNGenerationGraph.Output Run(float[] pcm, CancellationToken token)
        {
            var input = new MNNGenerationGraph.Input("mixture", pcm, 1, pcm.Length);
            if (_graph != null) return _graph.RunOutputs(new[] { "enhanced" }, input)[0];
            var encoded = _encoder.RunOutputs(EncoderOutputs, input);
            token.ThrowIfCancellationRequested();
            var recurrent = _recurrent.RunOutputs(new[] { "rnn_real", "rnn_imag" },
                new MNNGenerationGraph.Input("real", encoded[10].Data, encoded[10].Shape),
                new MNNGenerationGraph.Input("imag", encoded[11].Data, encoded[11].Shape));
            token.ThrowIfCancellationRequested();
            var decoderInputs = new MNNGenerationGraph.Input[16];
            decoderInputs[0] = new MNNGenerationGraph.Input("rnn_real", recurrent[0].Data, recurrent[0].Shape);
            decoderInputs[1] = new MNNGenerationGraph.Input("rnn_imag", recurrent[1].Data, recurrent[1].Shape);
            for (int i = 0; i < encoded.Length; ++i)
                decoderInputs[i + 2] = new MNNGenerationGraph.Input(EncoderOutputs[i], encoded[i].Data, encoded[i].Shape);
            return _decoder.RunOutputs(new[] { "enhanced" }, decoderInputs)[0];
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
                _graph?.Dispose();
                _encoder?.Dispose();
                _recurrent?.Dispose();
                _decoder?.Dispose();
            }
        }
    }
}
