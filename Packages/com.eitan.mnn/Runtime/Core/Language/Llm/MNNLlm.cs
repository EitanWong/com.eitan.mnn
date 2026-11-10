using System;
using System.IO;
using System.Threading.Tasks;
using MNN.Unity.Interop;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity
{
    /// <summary>Local text generation using an MNN LLM directory (config, tokenizer and weights).
    /// Operations on an instance are serialized. Dispose waits for an active generation.
    /// Direct official C++ calls require macOS Mono or mobile ARM64 IL2CPP with the pinned MNN 3.6.2 compiler ABI.
    /// Supports text and image/audio input; waveform callbacks are not implemented.
    /// </summary>
    public sealed partial class MNNLlm : IDisposable
    {
        private readonly object _gate = new object ();
        private LlmHandle _handle;
        private readonly string _ownedCacheDirectory;
        private readonly MNNModelOptions _options;
        private readonly MNNTokenCountCache _tokenCounts = new MNNTokenCountCache();
        private readonly Func<string, int> _countNativeTokens;
        /// <summary>Reuse an exact token prefix between text conversations on this instance.
        /// Incompatible templates, changed history, media, cancellation and errors start cold.
        /// The pinned macOS x64 runtime currently uses full prefill after a continuation quality failure.
        /// Quantized attention configurations use full prefill after INT8 continuation quality failures.
        /// Disable to compare against full prefill. Uses the same pinned compiler ABI as generation.</summary>
        public bool EnableConversationCache
        {
            get { lock (_gate) { ThrowIfDisposed(); return _handle.ConversationCacheEnabled; } }
            set
            {
                lock (_gate)
                {
                    ThrowIfDisposed();
                    _handle.ConversationCacheEnabled = value;
                    if (!value)
                    {
                        _handle.ConversationCacheValid = false;
                    }
                }
            }
        }
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

        /// <summary>Configured image/audio processor runtime.</summary>
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
            _countNativeTokens = CountNativeTokens;
        }

        /// <summary>Loads with automatic device/model tuning for unassigned settings.
        /// Use MNNModelLoading.LoadAsync to keep loading off the render thread.</summary>
        public static MNNLlm Load(string modelDirectory, bool enableThinking = false, string cacheDirectory = null,
            MNNPrecisionMode? precisionMode = null, MNNBackendType backendType = MNNBackendType.Auto)
            => LoadAutomatic(modelDirectory, null, enableThinking, cacheDirectory, precisionMode, false, backendType);

        /// <summary>Overrides the thread count; omitted precision/power still use automatic tuning.</summary>
        public static MNNLlm Load(string modelDirectory, int threadCount, bool enableThinking = false,
            string cacheDirectory = null, MNNPrecisionMode? precisionMode = null, MNNBackendType backendType = MNNBackendType.Auto)
            => LoadAutomatic(modelDirectory, threadCount, enableThinking, cacheDirectory, precisionMode, false, backendType);

        // Preserve the original full CLR signature for already compiled callers.
        public static MNNLlm Load(string modelDirectory, int threadCount, bool enableThinking,
            string cacheDirectory, MNNPrecisionMode precisionMode, MNNBackendType backendType)
            => LoadAutomatic(modelDirectory, threadCount, enableThinking, cacheDirectory, precisionMode, false, backendType);

        /// <summary>Unassigned properties use automatic tuning; explicit properties win.
        /// A caller-owned cacheDirectory persists native caches across model disposal.</summary>
        public static MNNLlm Load(string modelDirectory, MNNLlmPerformanceOptions performance, bool enableThinking = false, string cacheDirectory = null)
            => LoadConfigured(modelDirectory, performance, enableThinking, cacheDirectory, false);

        /// <summary>Conversational sampling with automatic defaults and explicit property overrides.</summary>
        public static MNNLlm LoadForConversation(string modelDirectory, MNNLlmPerformanceOptions performance, float temperature = .85f, bool enableThinking = false, string cacheDirectory = null)
            => LoadConfigured(modelDirectory, performance, enableThinking, cacheDirectory, true, temperature);

        public static MNNLlm LoadForConversation(string modelDirectory, bool enableThinking = false,
            string cacheDirectory = null, MNNPrecisionMode? precisionMode = null, MNNBackendType backendType = MNNBackendType.Auto)
            => LoadAutomatic(modelDirectory, null, enableThinking, cacheDirectory, precisionMode, true, backendType);

        public static MNNLlm LoadForConversation(string modelDirectory, int threadCount, bool enableThinking = false,
            string cacheDirectory = null, MNNPrecisionMode? precisionMode = null, MNNBackendType backendType = MNNBackendType.Auto)
            => LoadAutomatic(modelDirectory, threadCount, enableThinking, cacheDirectory, precisionMode, true, backendType);

        public static MNNLlm LoadForConversation(string modelDirectory, int threadCount, bool enableThinking,
            string cacheDirectory, MNNPrecisionMode precisionMode, MNNBackendType backendType)
            => LoadAutomatic(modelDirectory, threadCount, enableThinking, cacheDirectory, precisionMode, true, backendType);

        /// <summary>Explicit temperature (0 to 2); unassigned execution settings use automatic tuning.
        /// Zero selects greedy decoding; positive values use penalty, top-k, top-p and temperature.</summary>
        public static MNNLlm LoadForConversation(string modelDirectory, float temperature, bool enableThinking = false,
            string cacheDirectory = null, MNNPrecisionMode? precisionMode = null, MNNBackendType backendType = MNNBackendType.Auto)
            => LoadAutomatic(modelDirectory, null, enableThinking, cacheDirectory, precisionMode, true, backendType, temperature);

        public static MNNLlm LoadForConversation(string modelDirectory, float temperature, int threadCount, bool enableThinking = false,
            string cacheDirectory = null, MNNPrecisionMode? precisionMode = null, MNNBackendType backendType = MNNBackendType.Auto)
            => LoadAutomatic(modelDirectory, threadCount, enableThinking, cacheDirectory, precisionMode, true, backendType, temperature);

        public static MNNLlm LoadForConversation(string modelDirectory, float temperature, int threadCount, bool enableThinking,
            string cacheDirectory, MNNPrecisionMode precisionMode, MNNBackendType backendType)
            => LoadAutomatic(modelDirectory, threadCount, enableThinking, cacheDirectory, precisionMode, true, backendType, temperature);

        private static MNNLlm LoadAutomatic(string directory, int? threads, bool thinking, string cache,
            MNNPrecisionMode? precision, bool conversation, MNNBackendType backend, float? temperature = null)
        {
            var tuning = new MNNLlmPerformanceOptions { BackendType = backend };
            if (threads.HasValue) tuning.ThreadCount = threads.Value;
            if (precision.HasValue) tuning.PrecisionMode = precision.Value;
            return LoadConfigured(directory, tuning, thinking, cache, conversation, temperature);
        }

        private static MNNLlm LoadConfigured(string directory, MNNLlmPerformanceOptions performance,
            bool thinking, string cache, bool conversation, float? temperature = null)
        {
            if (performance == null) throw new ArgumentNullException(nameof(performance));
            MNNPlatformSupport.RequireSupported();
            if (temperature.HasValue && (float.IsNaN(temperature.Value) || float.IsInfinity(temperature.Value) ||
                temperature.Value < 0 || temperature.Value > 2))
                throw new ArgumentOutOfRangeException(nameof(temperature));
            return LoadPrepared(MNNModelOptions.PrepareLlm(directory, performance, thinking, cache, conversation, temperature));
        }

        public static MNNLlm LoadForChat(string modelDirectory, int threadCount, bool thinking, string cacheDirectory)
            => LoadAutomatic(modelDirectory, threadCount, thinking, cacheDirectory, null, true, MNNBackendType.Auto);
        internal static MNNLlm LoadForSpeechRecognition(string directory, int threads, string cache, MNNBackendType backend, MNNPrecisionMode precision, string configurationJson = null)
            => LoadCore(directory, threads, false, cache, precision, false, backend, true, performanceJson: configurationJson);
        private static MNNLlm LoadCore(string modelDirectory, int threadCount, bool enableThinking, string cacheDirectory, MNNPrecisionMode precisionMode, bool chatSampling, MNNBackendType backendType = MNNBackendType.Auto, bool speechRecognition = false, float? conversationTemperature = null, string performanceJson = null)
        {
            var options = MNNModelOptions.Prepare(modelDirectory, threadCount, enableThinking, cacheDirectory, precisionMode, chatSampling, backendType, speechRecognition, conversationTemperature);
            if (performanceJson != null)
                options.Json = options.Json.Substring(0, options.Json.Length - 1) + "," + performanceJson.Substring(1);
            return LoadPrepared(options);
        }

        private static MNNLlm LoadPrepared(MNNModelOptions options)
        {
            string ownedCache = options.OwnedCache;
            try
            {
                using (MNNInterop.BeginNativeCall())
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
                using (MNNInterop.BeginNativeCall())
                    return _tokenCounts.Get(text, _countNativeTokens);
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
            using (MNNInterop.BeginNativeCall())
                return UseNativeScoped(operation, canRetry);
        }

        private TResult UseNativeScoped<TResult>(Func<LlmHandle, TResult> operation, Func<bool> canRetry)
        {
            try
            {
                return operation(_handle);
            }
            catch (MNNException error)when (Backend != MNNBackendType.CPU && (canRetry == null || canRetry()))
            {
                bool cacheEnabled = _handle.ConversationCacheEnabled;
                _handle.Dispose();
                _handle = null;
                _tokenCounts.Clear();
                _options.FallBackToCpu(error);
                _handle = _options.Load(MNNInterop.MNN_Llm_create);
                _handle.ConversationCacheEnabled = cacheEnabled;
                return operation(_handle);
            }
        }

        public Task<MNNLlmResult> GenerateAsync(string prompt, int maxNewTokens = 128) => Task.Run(() => Generate(prompt, maxNewTokens));

        internal string DumpConfiguration()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                using (MNNInterop.BeginNativeCall())
                    return MNNInterop.ReadLlmConfiguration(_handle);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_handle == null)
                throw new ObjectDisposedException(nameof(MNNLlm));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                using (MNNInterop.BeginNativeCall())
                    _handle?.Dispose();
                _handle = null;
                _tokenCounts.Clear();
                RemoveOwnedCache(_ownedCacheDirectory);
            }
        }

        private static void RemoveOwnedCache(string path) => MNNModelOptions.RemoveOwnedCache(path);

    }
}
