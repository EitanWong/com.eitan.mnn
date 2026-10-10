using System;
using System.Threading.Tasks;
using MNN.Unity.Interop;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity
{
    /// <summary>Local text generation using an MNN LLM directory (config, tokenizer and weights).
    /// Operations on an instance are serialized. Dispose waits for an active generation.
    /// Direct official C++ calls currently require macOS Mono and the bundled MNN 3.6.1 Apple ABI build.
    /// </summary>
    public sealed partial class MNNLlm : IDisposable
    {
        private readonly object _gate = new object ();
        private LlmHandle _handle;
        private readonly string _ownedCacheDirectory;
        private readonly MNNModelOptions _options;
        public MNNBackendType Backend
        {
            get
            {
                lock (_gate)
                {
                    ThrowIfDisposed();
                    return _options.Backend;
                }
            }
        }

        /// <summary>Configured image/audio processor runtime. Omni speech can keep
        /// its main Thinker/Talker on Metal while using CPU waveform processing.</summary>
        public MNNBackendType MediaBackend
        {
            get
            {
                lock (_gate)
                {
                    ThrowIfDisposed();
                    return _options.MediaBackend;
                }
            }
        }

        private MNNLlm(LlmHandle handle, MNNModelOptions options)
        {
            _handle = handle;
            _ownedCacheDirectory = options.OwnedCache;
            _options = options;
        }

        /// <summary>Call on the Unity main thread. Prefers an available accelerator without changing the downloaded configuration.</summary>
        public static MNNLlm Load(string modelDirectory, int threadCount = 4, bool enableThinking = false, string cacheDirectory = null, MNNPrecisionMode precisionMode = MNNPrecisionMode.Normal, MNNBackendType backendType = MNNBackendType.Auto) => LoadCore(modelDirectory, threadCount, enableThinking, cacheDirectory, precisionMode, false, backendType);
        internal static MNNLlm LoadForChat(string modelDirectory, int threadCount, bool thinking, string cacheDirectory) => LoadCore(modelDirectory, threadCount, thinking, cacheDirectory, MNNPrecisionMode.Normal, true);
        private static MNNLlm LoadCore(string modelDirectory, int threadCount, bool enableThinking, string cacheDirectory, MNNPrecisionMode precisionMode, bool chatSampling, MNNBackendType backendType = MNNBackendType.Auto)
        {
            var options = MNNModelOptions.Prepare(modelDirectory, threadCount, enableThinking, cacheDirectory, precisionMode, chatSampling, backendType);
            string ownedCache = options.OwnedCache;
            try
            {
                return new MNNLlm(options.Load(MNNInterop.MNN_Llm_create), options);
            }
            catch
            {
                RemoveOwnedCache(ownedCache);
                throw;
            }
        }

        public int CountTokens(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            lock (_gate)
            {
                ThrowIfDisposed();
                return MNNInterop.MNN_Llm_tokenize(_handle, text);
            }
        }

        /// <summary>Starts an independent conversation with deterministic greedy sampling.</summary>
        public MNNLlmResult Generate(string prompt, int maxNewTokens = 128)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentNullException(nameof(prompt));
            if (maxNewTokens < 1 || maxNewTokens > 8192)
                throw new ArgumentOutOfRangeException(nameof(maxNewTokens));
            lock (_gate)
            {
                ThrowIfDisposed();
                return UseNative(handle => MNNInterop.MNN_Llm_generate(handle, prompt, maxNewTokens));
            }
        }

        private TResult UseNative<TResult>(Func<LlmHandle, TResult> operation, Func<bool> canRetry = null)
        {
            try
            {
                return operation(_handle);
            }
            catch (MNNException error)when (Backend != MNNBackendType.CPU && (canRetry == null || canRetry()))
            {
                _handle.Dispose();
                _handle = null;
                _options.FallBackToCpu(error);
                _handle = _options.Load(MNNInterop.MNN_Llm_create);
                return operation(_handle);
            }
        }

        private void RequireSpeechCompatibility(bool generateSpeech)
        {
            if (!generateSpeech || Backend == MNNBackendType.CPU)
                return;
            if (Backend == MNNBackendType.Metal)
            {
                if (!_options.PrepareGpuSpeech())
                    return;
            }
            else
                _options.UseCpuForCompatibility("Omni speech has not been validated on this accelerator.");
            _handle.Dispose();
            _handle = null;
            _handle = _options.Load(MNNInterop.MNN_Llm_create);
        }

        public Task<MNNLlmResult> GenerateAsync(string prompt, int maxNewTokens = 128) => Task.Run(() => Generate(prompt, maxNewTokens));
        private void ThrowIfDisposed()
        {
            if (_handle == null)
                throw new ObjectDisposedException(nameof(MNNLlm));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _handle?.Dispose();
                _handle = null;
                RemoveOwnedCache(_ownedCacheDirectory);
            }
        }

        private static void RemoveOwnedCache(string path) => MNNModelOptions.RemoveOwnedCache(path);
    }
}
