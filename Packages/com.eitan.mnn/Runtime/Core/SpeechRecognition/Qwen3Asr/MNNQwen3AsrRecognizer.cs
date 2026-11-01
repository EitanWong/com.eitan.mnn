using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Qwen3-ASR through the existing official MNN Llm audio API and supported Apple ABI.
    /// Cancellation is observed between decoder tokens; audio encoding and prefill are synchronous.</summary>
    public sealed class MNNQwen3AsrRecognizer : IDisposable
    {
        private readonly object _gate = new object();
        private MNNLlm _model;
        private bool _recognizing;
        private readonly string _cache, _ownedCache;
        public MNNBackendType Backend { get { lock (_gate) { ThrowIfDisposed(); return _model.Backend; } } }
        public MNNBackendType MediaBackend { get { lock (_gate) { ThrowIfDisposed(); return _model.MediaBackend; } } }

        private MNNQwen3AsrRecognizer(string directory, int threads, string cacheDirectory, MNNBackendType backend, MNNPrecisionMode precision)
        {
            MNNQwen3AsrModel.ResolveRevision(null, MNNQwen3AsrConfig.Read(directory).repository);
            MNNQwen3AsrModel.Verify(directory);
            // Quality-gated backends for this profile: prefer Metal, retain CPU fallback.
            if (backend == MNNBackendType.Auto)
                backend = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
            _cache = Path.GetFullPath(cacheDirectory ?? Path.Combine(MNNInferenceCache.DirectoryFor("Cache"), "Qwen3ASR", Guid.NewGuid().ToString("N")));
            _ownedCache = cacheDirectory == null ? _cache : null;
            try { _model = MNNLlm.LoadForSpeechRecognition(directory, threads, _cache, backend, precision); }
            catch { MNNModelOptions.RemoveOwnedCache(_ownedCache); throw; }
        }

        /// <summary>Use MNNModelLoading.LoadAsync for background loading. Auto prefers the quality-checked Metal route, with CPU fallback.</summary>
        public static MNNQwen3AsrRecognizer Load(string directory, int threadCount = 4, string cacheDirectory = null,
            MNNBackendType backendType = MNNBackendType.Auto, MNNPrecisionMode precisionMode = MNNPrecisionMode.Normal)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNQwen3AsrRecognizer(Path.GetFullPath(directory), threadCount, cacheDirectory, backendType, precisionMode);
        }

        /// <summary>Finite normalized mono PCM, 8–96 kHz, 0.1–30 seconds. No timestamp alignment.
        /// Uses the pinned model's original ASR language template; English and Chinese/English were validated.</summary>
        public MNNSpeechRecognition Recognize(float[] samples, int sampleRate, CancellationToken cancellationToken = default, Action<MNNGenerationUpdate> onProgress = null)
        {
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples == null || samples.Length < Math.Ceiling(sampleRate * .1) || samples.Length > sampleRate * 30)
                throw new ArgumentException("Qwen3-ASR expects 0.1–30 seconds of mono audio.", nameof(samples));
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_recognizing) throw new InvalidOperationException("Recognition callbacks cannot start another recognition on this instance.");
                bool silent = true;
                for (int i = 0; i < samples.Length; ++i)
                {
                    if ((i & 16383) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                        throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(samples));
                    silent &= samples[i] == 0;
                }
                if (silent) return new MNNSpeechRecognition(string.Empty, Array.Empty<int>(), Array.Empty<float>());
                string wave = Path.Combine(_cache, "input-" + Guid.NewGuid().ToString("N") + ".wav");
                _recognizing = true;
                try
                {
                    var pcm = MNNSpeechDsp.Resample(samples, sampleRate, 16000, cancellationToken);
                    // Float32 preserves normalized input; the official audio frontend owns feature extraction.
                    using (var writer = new BinaryWriter(File.Create(wave)))
                    {
                        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + pcm.Length * 4);
                        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                        writer.Write((short)3); writer.Write((short)1); writer.Write(16000); writer.Write(64000);
                        writer.Write((short)4); writer.Write((short)32);
                        writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length * 4);
                        foreach (float sample in pcm) writer.Write(sample);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = _model.GenerateConversation(new[] {new MNNChatMessage(MNNChatRole.User, "Transcribe the audio.")},
                        maxNewTokens: 256, audioPath: wave, onProgress: update => onProgress?.Invoke(update), cancellationToken: cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (result.ReachedTokenLimit) throw new InvalidDataException("Qwen3-ASR reached the transcript token limit.");
                    return new MNNSpeechRecognition(result.Text.Trim(), result.GeneratedTokenIds.ToArray(), Array.Empty<float>());
                }
                finally
                {
                    _recognizing = false;
                    if (File.Exists(wave)) File.Delete(wave);
                }
            }
        }

        public MNNSpeechRecognition RecognizeWaveFile(string path, CancellationToken cancellationToken = default, Action<MNNGenerationUpdate> onProgress = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var samples = MNNWaveAudio.Read(path, out int rate);
            return Recognize(samples, rate, cancellationToken, onProgress);
        }
        public Task<MNNSpeechRecognition> RecognizeAsync(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Recognize(copy, sampleRate, cancellationToken), cancellationToken);
        }
        private void ThrowIfDisposed() { if (_model == null) throw new ObjectDisposedException(nameof(MNNQwen3AsrRecognizer)); }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_recognizing) throw new InvalidOperationException("Recognition callbacks cannot dispose this instance.");
                _model?.Dispose(); _model = null;
                MNNModelOptions.RemoveOwnedCache(_ownedCache);
            }
        }
    }
}
