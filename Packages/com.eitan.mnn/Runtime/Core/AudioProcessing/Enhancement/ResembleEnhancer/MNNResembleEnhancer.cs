using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Complete offline Resemble CFM enhancement: 44.1 kHz, seed 0, 32-NFE midpoint.</summary>
    public sealed partial class MNNResembleEnhancer : IDisposable
    {
        public const int SampleRate = 44100, MinimumSamples = 400, MaximumSamples = 132300;
        private readonly object _gate = new object();
        private readonly MNNResembleEnhancerConfig _config;
        private readonly MNNResembleEnhancerDsp _dsp;
        private readonly MNNResembleEnhancerGraphs _graphs;
        private readonly float[][] _embeddings = new float[32][];
        private bool _disposed;
        public MNNBackendType DenoiserBackend => _graphs.Denoiser.Backend;
        public MNNBackendType EncoderBackend => _graphs.Encoder.Backend;
        public MNNBackendType VelocityBackend => _graphs.Velocity.Backend;
        public MNNBackendType DecoderBackend => _graphs.Decoder.Backend;
        public MNNBackendType VocoderBackend => _graphs.Vocoder.Backend;

        private MNNResembleEnhancer(string directory, MNNBackendType backend)
        {
            MNNResembleEnhancerModel.Verify(directory);
            _config = MNNResembleEnhancerConfig.Read(directory);
            _dsp = new MNNResembleEnhancerDsp(_config);
            for (int i = 0; i < _embeddings.Length; ++i)
            {
                _embeddings[i] = new float[128];
                Array.Copy(_config.time_embeddings, i * 128, _embeddings[i], 0, 128);
            }
            _graphs = new MNNResembleEnhancerGraphs(directory, _config, backend);
        }

        /// <summary>CPU4/High or Metal for every neural stage. Auto prefers available Metal, otherwise CPU.</summary>
        public static MNNResembleEnhancer Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount != 4)
                throw new ArgumentOutOfRangeException(nameof(threadCount), "The qualified complete enhancer uses four CPU threads.");
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("Resemble Enhance supports CPU and its qualified Metal pipeline.");
            MNNPlatformSupport.RequireSupported();
            var backend = backendType == MNNBackendType.Auto
                ? (MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU)
                : MNNAcceleration.Resolve(backendType);
            return new MNNResembleEnhancer(Path.GetFullPath(directory), backend);
        }

        /// <summary>Enhance 400–132300 finite mono samples at 44.1 kHz, preserving their count and amplitude scale.</summary>
        public float[] Enhance(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNResembleEnhancer));
                cancellationToken.ThrowIfCancellationRequested();
                if (sampleRate != SampleRate)
                    throw new ArgumentOutOfRangeException(nameof(sampleRate), "Supply 44.1 kHz mono PCM.");
                if (samples == null || samples.Length < MinimumSamples || samples.Length > MaximumSamples)
                    throw new ArgumentException("Supply 400–132300 mono samples at 44.1 kHz.", nameof(samples));
                var input = (float[])samples.Clone();
                for (int i = 0; i < input.Length; ++i)
                {
                    if ((i & 16383) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if (float.IsNaN(input[i]) || float.IsInfinity(input[i]))
                        throw new ArgumentException("Supply finite mono PCM.", nameof(samples));
                }
                return EnhanceCore(input, cancellationToken);
            }
        }

        public float[] EnhanceWaveFile(string path, out int sampleRate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var samples = MNNWaveAudio.Read(path, out sampleRate);
            return Enhance(samples, sampleRate, cancellationToken);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _graphs.Dispose();
            }
        }
    }
}
