using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    /// <summary>Indic VITS q8 export with its character vocabulary and explicit speaker/emotion inputs.</summary>
    public sealed class MNNIndicVits : IDisposable
    {
        internal static readonly string[] SpeakerNames = {"ASM_F", "ASM_M", "BEN_F", "BEN_M", "BRX_F", "BRX_M", "DOI_F", "DOI_M", "KAN_F", "KAN_M", "MAI_M", "MAL_F", "MAR_F", "MAR_M", "NEP_F", "PAN_F", "PAN_M", "SAN_M", "TAM_F", "TEL_F"};
        internal static readonly string[] StyleNames = {"ALEXA", "ANGER", "BB", "BOOK", "CONV", "DIGI", "DISGUST", "FEAR", "HAPPY", "NEWS", "SAD", "SURPRISE", "UMANG", "WIKI"};
        internal static readonly int[] StyleIds = {0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 14, 15, 16};
        internal static readonly string[] SpeakerExamples =
        {
            "নমস্কাৰ, আপোনাক স্বাগতম।", "নমস্কাৰ, আপোনাক স্বাগতম।",
            "নমস্কার, আপনাকে স্বাগতম।", "নমস্কার, আপনাকে স্বাগতম।",
            "खुलुमबाय।", "खुलुमबाय।", "नमस्कार, तुंदा स्वागत ऐ।", "नमस्कार, तुंदा स्वागत ऐ।",
            "ನಮಸ್ಕಾರ, ನಿಮಗೆ ಸ್ವಾಗತ.", "ನಮಸ್ಕಾರ, ನಿಮಗೆ ಸ್ವಾಗತ.", "नमस्कार, अहाँक स्वागत अछि।",
            "നമസ്കാരം, സ്വാഗതം.", "नमस्कार, आपले स्वागत आहे।", "नमस्कार, आपले स्वागत आहे।",
            "नमस्कार, तपाईंलाई स्वागत छ।", "ਸਤਿ ਸ੍ਰੀ ਅਕਾਲ, ਤੁਹਾਡਾ ਸੁਆਗਤ ਹੈ।", "ਸਤਿ ਸ੍ਰੀ ਅਕਾਲ, ਤੁਹਾਡਾ ਸੁਆਗਤ ਹੈ।",
            "नमस्ते, स्वागतम्।", "வணக்கம் உலகம். இது ஒரு குரல் தொகுப்பு சோதனை.", "నమస్కారం, మీకు స్వాగతం."
        };
        /// <summary>Installed frontend speaker identifiers; each call returns an independent snapshot.</summary>
        public static string[] InstalledSpeakers => (string[])SpeakerNames.Clone();
        public static string[] InstalledStyles => (string[])StyleNames.Clone();
        public static int[] InstalledStyleIds => (int[])StyleIds.Clone();
        public static string[] SpeakerExampleTexts => (string[])SpeakerExamples.Clone();
        /// <summary>Documented speaker labels, ordered by speaker ID (0–19).</summary>
        public string[] Voices => (string[])SpeakerNames.Clone();
        private readonly object _gate = new object();
        private readonly Dictionary<char, int> _vocabulary = new Dictionary<char, int>();
        private readonly string _path;
        private readonly byte[] _graph;
        private readonly int _threads, _speakerCount, _emotionCount;
        private MNNSingleInputModule _module;
        private int _length, _speaker = -1, _emotion = -1;
        private bool _disposed;
        public int SampleRate => 24000;

        internal static bool IsComplete(string directory) => File.Exists(Path.Combine(directory, "vits_tts_q8.mnn"))
            && File.Exists(Path.Combine(directory, "vocab.json")) && File.Exists(Path.Combine(directory, "config.json"))
            && File.Exists(Path.Combine(directory, "tokenizer_config.json"));

        public static MNNIndicVits Load(string directory, int threads = 4)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16)
                throw new ArgumentOutOfRangeException(nameof(threads));
            return new MNNIndicVits(Path.GetFullPath(directory), threads);
        }

        private MNNIndicVits(string directory, int threads)
        {
            if (!IsComplete(directory))
                throw new FileNotFoundException("Indic VITS needs vits_tts_q8.mnn, vocab.json, config.json and tokenizer_config.json.");
            object config = MNNModelData.Read(Path.Combine(directory, "config.json"));
            object tokenizer = MNNModelData.Read(Path.Combine(directory, "tokenizer_config.json"));
            if ((string)MNNModelData.Get(config, "model_type") != "indic_vits_model" ||
                MNNModelData.Integer(MNNModelData.Get(config, "sampling_rate")) != SampleRate ||
                (string)MNNModelData.Get(tokenizer, "tokenizer_class") != "IndicVitsTokenizer" ||
                !Equals(MNNModelData.Get(tokenizer, "add_blank"), true) ||
                !Equals(MNNModelData.Get(tokenizer, "phonemize"), false) ||
                !Equals(MNNModelData.Get(tokenizer, "is_uroman"), false))
                throw new NotSupportedException("Unsupported Indic VITS tokenizer or audio contract.");
            _speakerCount = MNNModelData.Integer(MNNModelData.Get(config, "num_speakers"));
            _emotionCount = MNNModelData.Integer(MNNModelData.Get(config, "num_emotions"));
            int size = MNNModelData.Integer(MNNModelData.Get(config, "vocab_size"));
            if (_speakerCount != 1024 || _emotionCount != 32 || size != 1260)
                throw new NotSupportedException("Unsupported Indic VITS control or vocabulary dimensions.");
            foreach (var pair in MNNModelData.Object(MNNModelData.Read(Path.Combine(directory, "vocab.json"))))
            {
                int id = MNNModelData.Integer(pair.Value);
                if (id < 0 || id >= size)
                    throw new InvalidDataException("Indic VITS token ID is out of range.");
                if (pair.Key.Length == 1 && !char.IsSurrogate(pair.Key[0]))
                    _vocabulary.Add(pair.Key[0], id);
            }
            _path = Path.Combine(directory, "vits_tts_q8.mnn");
            _graph = File.ReadAllBytes(_path);
            _threads = threads;
        }

        internal static int[] Encode(string text, IReadOnlyDictionary<char, int> vocabulary, out string normalized)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 300)
                throw new ArgumentException("Enter 1–300 characters for Indic VITS.", nameof(text));
            normalized = Regex.Replace(text.ToLowerInvariant(), @"\s+", " ").Trim();
            if (!normalized.Any(char.IsLetterOrDigit))
                throw new ArgumentException("Indic VITS requires pronounceable text.", nameof(text));
            var result = new int[checked(2 * normalized.Length + 1)];
            for (int i = 0; i < normalized.Length; ++i)
            {
                if (!vocabulary.TryGetValue(normalized[i], out int id))
                    throw new NotSupportedException("No Indic VITS token for U+" + ((int)normalized[i]).ToString("X4") + ".");
                // This MNN export's published runner uses blank ID 0, including
                // both boundaries; tokenizer.pad_token_id 95 is not equivalent.
                result[2 * i + 1] = id;
            }
            return result;
        }

        public MNNGeneratedAudio Synthesize(string text, int speakerId = 18, int emotionId = 0, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNIndicVits));
                cancellationToken.ThrowIfCancellationRequested();
                if (speakerId < 0 || speakerId >= SpeakerNames.Length || speakerId >= _speakerCount)
                    throw new ArgumentOutOfRangeException(nameof(speakerId));
                if (emotionId < 0 || emotionId >= _emotionCount)
                    throw new ArgumentOutOfRangeException(nameof(emotionId));
                int[] ids = Encode(text, _vocabulary, out string normalized);
                if (_module == null || _length != ids.Length || _speaker != speakerId || _emotion != emotionId)
                {
                    _module?.Dispose();
                    _module = null;
                    byte[] prepared = MNNGraphConstant.FreezeInputs(_graph,
                        new Input("attention_mask", Enumerable.Repeat(1, ids.Length).ToArray(), 1, ids.Length),
                        new Input("speaker_id", new[]{speakerId}, 1), new Input("emotion_id", new[]{emotionId}, 1));
                    _module = new MNNSingleInputModule(_path, "input_ids", "waveform", _threads, MNNBackendType.CPU, prepared, MNNPrecisionMode.High);
                    _length = ids.Length;
                    _speaker = speakerId;
                    _emotion = emotionId;
                }
                float[] wave = _module.Run(ids, 1, ids.Length);
                cancellationToken.ThrowIfCancellationRequested();
                if (wave.Length < SampleRate / 10 || wave.Length > SampleRate * 30 || wave.Any(v => float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 2))
                    throw new InvalidDataException("Indic VITS returned an invalid waveform.");
                float peak = wave.Max(v => Math.Abs(v));
                if (peak < .001f)
                    throw new InvalidDataException("Indic VITS returned silence.");
                if (peak > 1)
                    for (int i = 0; i < wave.Length; ++i)
                        wave[i] /= peak;
                return new MNNGeneratedAudio(wave, SampleRate, normalized);
            }
        }

        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, int speakerId = 18, int emotionId = 0, CancellationToken cancellationToken = default)
            => Task.Run(() => Synthesize(text, speakerId, emotionId, cancellationToken), cancellationToken);

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _module?.Dispose();
                _module = null;
            }
        }
    }
}
