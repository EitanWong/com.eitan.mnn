using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Smart Turn v3.2 audio endpoint probability with an eight-second context.</summary>
    public sealed class MNNSmartTurn : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNSingleInputModule _module;
        private bool _disposed;
        public MNNBackendType Backend => _module.Backend;
        private MNNSmartTurn(string directory, int threads, MNNBackendType backend)
        {
            var config = MNNSmartTurnConfig.Read(directory);
            if (!MNNSmartTurnModel.IsOfficialProfile(config) && !MNNSmartTurnModel.IsRepository(config.repository))
                throw new InvalidDataException("Unknown Smart Turn profile.");
            MNNSmartTurnModel.Verify(directory);
            _module = new MNNSingleInputModule(Path.Combine(directory, MNNSmartTurnModel.PreferredGraph(directory, backend)), "input_features", "logits",
                threads, backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend, precision: MNNPrecisionMode.High);
        }
        public static MNNSmartTurn Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (!Enum.IsDefined(typeof(MNNBackendType), backendType)) throw new ArgumentOutOfRangeException(nameof(backendType));
            if (backendType == MNNBackendType.NN) throw new NotSupportedException("SmartTurn NNAPI graph/device compatibility has not been verified.");
            return new MNNSmartTurn(Path.GetFullPath(directory), threadCount, backendType);
        }
        /// <summary>Evaluate normalized mono PCM at 8–96 kHz, up to ten minutes.
        /// Only the last eight seconds are used. Short recordings are padded at the beginning.</summary>
        public MNNTurnDetectionResult Predict(float[] samples, int sampleRate, float threshold = .5f, CancellationToken cancellationToken = default)
        {
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples == null || samples.Length == 0 || samples.Length > (long)sampleRate * 600)
                throw new ArgumentException("Supply nonempty mono audio up to ten minutes.", nameof(samples));
            if (float.IsNaN(threshold) || threshold < 0 || threshold > 1) throw new ArgumentOutOfRangeException(nameof(threshold));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNSmartTurn));
                cancellationToken.ThrowIfCancellationRequested();
                for (int i = 0; i < samples.Length; ++i)
                {
                    if ((i & 16383) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                        throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(samples));
                }
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, 16000, cancellationToken, tailSamples: 128000);
                var features = MNNSmartTurnFeatures.Extract(pcm, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var output = _module.Run(features, 1, 80, 800);
                cancellationToken.ThrowIfCancellationRequested();
                if (output.Length != 1 || output[0] < 0 || output[0] > 1)
                    throw new InvalidDataException("Invalid Smart Turn probability.");
                return new MNNTurnDetectionResult(output[0], threshold, (double)samples.Length / sampleRate);
            }
        }
        public MNNTurnDetectionResult PredictWaveFile(string path, float threshold = .5f, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var samples = MNNWaveAudio.Read(path, out int rate);
            return Predict(samples, rate, threshold, cancellationToken);
        }
        public Task<MNNTurnDetectionResult> PredictAsync(float[] samples, int sampleRate, float threshold = .5f, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Predict(copy, sampleRate, threshold, cancellationToken), cancellationToken);
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _module.Dispose();
            }
        }
    }
}
