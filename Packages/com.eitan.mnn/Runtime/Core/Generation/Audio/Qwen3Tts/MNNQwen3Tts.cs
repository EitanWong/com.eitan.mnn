using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Interop;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity
{
    /// <summary>Official Qwen3-TTS Talker pipeline, mono 24 kHz. CPU FP32 compute;
    /// unavailable until a supported waveform callback ABI is implemented.</summary>
    // Experimental: INT8/FP16 acceptance tests currently fail; not exposed by Studio.
    internal sealed class MNNQwen3Tts : IDisposable
    {
        private readonly object _gate = new object();
        private LlmHandle _handle;
        private readonly string _ownedCache;
        private static readonly string[] Languages = { "auto", "chinese", "english", "german", "italian", "portuguese", "spanish", "japanese", "korean", "french", "russian" };
        private static readonly string[] Required = { "config.json", "llm_config.json", "tokenizer.txt", "talker.mnn", "talker.mnn.weight", "talker_embeddings_bf16.bin", "talker_text_embeddings_bf16.bin", "code_predictor.mnn", "code_predictor.mnn.weight", "code_predictor_embeddings_bf16.bin", "codec_embedder.mnn", "speech_decoder.mnn", "speech_decoder.mnn.weight", "speaker_encoder.mnn", "speaker_encoder.mnn.weight" };

        private MNNQwen3Tts(LlmHandle handle, string cache) { _handle = handle; _ownedCache = cache; }

        internal static bool IsComplete(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return false;
            foreach (string name in Required)
                if (!File.Exists(Path.Combine(directory, name)) || new FileInfo(Path.Combine(directory, name)).Length == 0) return false;
            try
            {
                var data = MNNModelData.Object(MNNModelData.Read(Path.Combine(directory, "llm_config.json")));
                return data.TryGetValue("model_type", out var type) && type as string == "qwen3_tts" &&
                    data.TryGetValue("talker_type", out var talker) && talker as string == "qwen3_tts";
            }
            catch (IOException) { return false; }
            catch (ArgumentException) { return false; }
        }

        /// <summary>Call on the Unity main thread. Supports the official Talker export layout.</summary>
        public static MNNQwen3Tts Load(string directory, int threads = 4, string cacheDirectory = null)
        {
            if (threads < 1 || threads > 64) throw new ArgumentOutOfRangeException(nameof(threads));
            if (!IsComplete(directory)) throw new InvalidDataException("Incomplete or unsupported Qwen3-TTS Talker export.");
            MNNInterop.RequireWaveformAbi();
            var options = MNNModelOptions.Prepare(directory, threads, false, cacheDirectory, MNNPrecisionMode.High, backendType: MNNBackendType.CPU);
            options.Json = options.Json.Substring(0, options.Json.Length - 1) + ",\"memory\":\"high\",\"attention_mode\":0,\"async\":false}";
            try { return new MNNQwen3Tts(options.Load(MNNInterop.MNN_Llm_create), options.OwnedCache); }
            catch { MNNModelOptions.RemoveOwnedCache(options.OwnedCache); throw; }
        }

        /// <summary>Text up to 500 characters, language name from the model's supported set.
        /// A local reference WAV (0.5–15 seconds) supplies speaker identity for Base weights. Cancellation is observed
        /// before and after the synchronous native generation; it cannot interrupt decode.</summary>
        public MNNGeneratedAudio Synthesize(string text, string referenceWavePath, string language = "auto", int maxFrames = 512, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 500) throw new ArgumentException("Supply 1–500 characters.", nameof(text));
            language = language?.ToLowerInvariant();
            if (Array.IndexOf(Languages, language) < 0) throw new ArgumentException("Unsupported Qwen3-TTS language.", nameof(language));
            if (maxFrames < 1 || maxFrames > 2048) throw new ArgumentOutOfRangeException(nameof(maxFrames));
            if (string.IsNullOrWhiteSpace(referenceWavePath)) throw new ArgumentNullException(nameof(referenceWavePath));
            referenceWavePath = Path.GetFullPath(referenceWavePath);
            MNNReferenceAudio.FromWaveFile(referenceWavePath);
            lock (_gate)
            {
                if (_handle == null) throw new ObjectDisposedException(nameof(MNNQwen3Tts));
                cancellationToken.ThrowIfCancellationRequested();
                var waveform = MNNInterop.GenerateQwen3Tts(_handle, text, language, maxFrames, referenceWavePath);
                cancellationToken.ThrowIfCancellationRequested();
                return MNNSpeechDsp.Result(waveform, 24000, text);
            }
        }

        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, string referenceWavePath, string language = "auto", int maxFrames = 512, CancellationToken cancellationToken = default) => Task.Run(() => Synthesize(text, referenceWavePath, language, maxFrames, cancellationToken));
        public void Dispose()
        {
            lock (_gate)
            {
                _handle?.Dispose(); _handle = null;
                MNNModelOptions.RemoveOwnedCache(_ownedCache);
            }
        }
    }
}
