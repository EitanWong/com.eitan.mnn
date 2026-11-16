using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Hojo V2 24 kHz speech over the package's pinned official MNN C++ ABI.
    /// All sampling, tokenization, speaker injection, cache ownership and ISTFT are managed.</summary>
    public sealed partial class MNNHojoTts : IDisposable
    {
        private static readonly Dictionary<string, string> DefaultVoices = new Dictionary<string, string>
            { { "zh", "hojo_zh_f_01" }, { "en", "hojo_en_m_02" } };
        internal static string[] SupportedLanguages => DefaultVoices.Keys.ToArray();
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _prefill, _decode, _fine, _decoder;
        private readonly MNNHojoTokenizer _tokenizer;
        private readonly float[] _embedding, _speakers, _vectors;
        private readonly string[] _voices;
        private readonly HashSet<int> _audioIds;
        private bool _disposed;
        public int SampleRate => 24000;
        public string[] Voices => (string[])_voices.Clone();
        public IReadOnlyDictionary<string, MNNBackendType> StageBackends => new Dictionary<string, MNNBackendType>
            { { "Prefill", _prefill.Backend }, { "Decode", _decode.Backend }, { "FineLocal", _fine.Backend }, { "Decoder", _decoder.Backend } };
        public static bool IsComplete(string directory) => MNNHojoTtsModel.IsComplete(directory);
        public static MNNHojoTts Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
            => new MNNHojoTts(directory, threadCount, backendType);
        private MNNHojoTts(string directory, int threads, MNNBackendType backend)
        {
            if (threads < 1 || threads > 64) throw new ArgumentOutOfRangeException(nameof(threads));
            if (!IsComplete(directory)) throw new InvalidDataException("Incomplete Hojo-TTS-Light-40M-V2-MNN installation.");
            _tokenizer = new MNNHojoTokenizer(Path.Combine(directory, "tokenizer.json"));
            _embedding = ReadFloats(directory, "token_embedding.bin");
            _speakers = ReadFloats(directory, "speaker_embeds.bin");
            _vectors = ReadFloats(directory, "speaker_vecs.bin");
            _voices = InstalledVoices(directory);
            _audioIds = new HashSet<int>(Enumerable.Range(0, 8192).Select(i => _tokenizer.Id("[" + i + "]")).Where(i => i != 0));
            // Auto is a measured per-model policy. Explicit backend choices remain available.
            if (backend == MNNBackendType.Auto) backend = MNNBackendType.CPU;
            try
            {
                _prefill = new MNNGenerationGraph(Path.Combine(directory, "prefill.mnn"), threads, backendType: backend, precisionMode: MNNPrecisionMode.High);
                _decode = new MNNGenerationGraph(Path.Combine(directory, "decode.mnn"), threads, backendType: backend, precisionMode: MNNPrecisionMode.High);
                _fine = new MNNGenerationGraph(Path.Combine(directory, "fine_local.mnn"), threads, backendType: backend, precisionMode: MNNPrecisionMode.High);
                _decoder = new MNNGenerationGraph(Path.Combine(directory, "decoder.mnn"), threads, backendType: backend, precisionMode: MNNPrecisionMode.High);
            }
            catch { _prefill?.Dispose(); _decode?.Dispose(); _fine?.Dispose(); _decoder?.Dispose(); throw; }
        }
        public static string[] InstalledVoices(string directory) => MNNModelData.Array(MNNModelData.Get(
            MNNModelData.Read(Path.Combine(directory, MNNHojoTtsModel.Config)), "voices")).Cast<string>().ToArray();
        private static float[] ReadFloats(string root, string name)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, name));
            if (!BitConverter.IsLittleEndian) for (int i = 0; i < bytes.Length; i += 4) Array.Reverse(bytes, i, 4);
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        private float[] Embed(int[] ids)
        {
            var values = new float[checked(ids.Length * 512)];
            for (int i = 0; i < ids.Length; ++i) Array.Copy(_embedding, checked(ids[i] * 512), values, i * 512, 512);
            return values;
        }
        /// <summary>Synthesize with a language-matched default voice and natural timing (1.15x model tempo,
        /// trimmed boundary silence). Speed multiplies that tempo without changing pitch.
        /// Set naturalTiming=false and speed=1 to retain the original model waveform.</summary>
        public MNNGeneratedAudio Synthesize(string text, string voice = null, int seed = 42,
            float temperature = 0.8f, int maximumTokens = 2048, CancellationToken cancellationToken = default,
            float speed = 1, bool naturalTiming = true)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 16000) throw new ArgumentException("Supply a nonempty speech segment, at most 16000 characters.", nameof(text));
            if (float.IsNaN(temperature) || float.IsInfinity(temperature) || temperature < 0 || temperature > 2) throw new ArgumentOutOfRangeException(nameof(temperature));
            if (maximumTokens < 1 || maximumTokens > 4096) throw new ArgumentOutOfRangeException(nameof(maximumTokens));
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed < .5f || speed > 2) throw new ArgumentOutOfRangeException(nameof(speed));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNHojoTts));
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(voice))
                    voice = DefaultVoices[MNNSpeechLanguages.Select(text, SupportedLanguages) ?? "en"];
                int speaker = Array.IndexOf(_voices, voice);
                if (speaker < 0) throw new ArgumentException("Unknown Hojo voice: " + voice, nameof(voice));
                var audio = Generate(text, speaker, seed, temperature, maximumTokens, cancellationToken);
                var wave = naturalTiming ? MNNSpeechDsp.TrimSpeechEdges(audio.Waveform, SampleRate, cancellationToken) : audio.Waveform;
                wave = MNNSpeechDsp.ChangeTempo(wave, SampleRate, speed * (naturalTiming ? 1.15f : 1), cancellationToken);
                return new MNNGeneratedAudio(wave, SampleRate, text);
            }
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return; _disposed = true;
                _prefill.Dispose(); _decode.Dispose(); _fine.Dispose(); _decoder.Dispose();
            }
        }
    }
}
