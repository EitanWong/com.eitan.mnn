using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    /// <summary>Five-speaker Chinese Sherpa VITS export with the published Bopomofo lexicon.</summary>
    public sealed class MNNSherpaVitsChinese : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<char, int> _tokens = new Dictionary<char, int>();
        private readonly Dictionary<string, int[]> _lexicon = new Dictionary<string, int[]>(StringComparer.Ordinal);
        private readonly string _path;
        private readonly byte[] _graph;
        private readonly int _threads;
        private readonly string[] _voices;
        private int _maxWord, _length, _speaker = -1;
        private float _speed;
        private MNNSingleInputModule _module;
        private bool _disposed;
        public int SampleRate => 16000;
        public string[] Voices => (string[])_voices.Clone();

        internal static bool IsComplete(string directory) => new[]{"model.mnn", "model.mnn.weight", "tokens.txt", "lexicon.txt", "G_multisperaker_latest.json"}.All(file => File.Exists(Path.Combine(directory, file)));

        public static string[] InstalledVoices(string directory)
        {
            var config = MNNModelData.Read(Path.Combine(directory, "G_multisperaker_latest.json"));
            var speakers = MNNModelData.Object(MNNModelData.Get(config, "speakers"));
            if (speakers.Count != 5 || !speakers.Select(pair => MNNModelData.Integer(pair.Value)).OrderBy(id => id).SequenceEqual(Enumerable.Range(0, 5)))
                throw new InvalidDataException("Invalid Chinese VITS speaker IDs.");
            return speakers.OrderBy(pair => MNNModelData.Integer(pair.Value)).Select(pair => pair.Key).ToArray();
        }

        public static MNNSherpaVitsChinese Load(string directory, int threads = 4)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16) throw new ArgumentOutOfRangeException(nameof(threads));
            return new MNNSherpaVitsChinese(Path.GetFullPath(directory), threads);
        }

        private MNNSherpaVitsChinese(string directory, int threads)
        {
            if (!IsComplete(directory)) throw new FileNotFoundException("Sherpa Chinese VITS needs its graph, external weights, lexicon, tokens and speaker configuration.");
            _path = Path.Combine(directory, "model.mnn");
            byte[] graph = File.ReadAllBytes(_path);
            var metadata = MNNSerializedGraph.Metadata(graph);
            if (!metadata.TryGetValue("model_type", out string type) || type != "vits" ||
                !metadata.TryGetValue("comment", out string comment) || comment != "hf-vits-models-fanchen-411" ||
                !metadata.TryGetValue("sample_rate", out string rate) || rate != "16000" ||
                !metadata.TryGetValue("add_blank", out string blank) || blank != "1" ||
                !metadata.TryGetValue("n_speakers", out string speakers) || speakers != "5")
                throw new NotSupportedException("Unsupported Sherpa Chinese VITS metadata.");
            _graph = MNNGraphExternalWeights.Bind(graph, _path + ".weight");
            _threads = threads;
            _voices = InstalledVoices(directory);
            foreach (string line in File.ReadAllLines(Path.Combine(directory, "tokens.txt")))
            {
                int separator = line.LastIndexOf(' ');
                if (separator != 1 || !int.TryParse(line.Substring(separator + 1), out int id) || id < 0 || id >= 50)
                    throw new InvalidDataException("Invalid Chinese VITS token list.");
                _tokens.Add(line[0], id);
            }
            if (_tokens.Count != 50 || !_tokens.TryGetValue('_', out int pad) || pad != 0)
                throw new NotSupportedException("Unsupported Chinese VITS token IDs.");
            foreach (string line in File.ReadLines(Path.Combine(directory, "lexicon.txt")))
            {
                string[] pieces = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (pieces.Length < 2) continue;
                int[] phones = string.Concat(pieces.Skip(1)).Select(c => _tokens.TryGetValue(c, out int id) ? id : throw new InvalidDataException("Chinese VITS lexicon contains unknown phonemes.")).ToArray();
                _lexicon[pieces[0]] = phones;
                _maxWord = Math.Max(_maxWord, pieces[0].Length);
            }
            if (_lexicon.Count == 0) throw new InvalidDataException("Empty Chinese VITS lexicon.");
        }

        private int[][] Encode(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 300)
                throw new ArgumentException("Enter 1–300 Chinese characters for VITS.", nameof(text));
            string normalized = text.Replace(',', '，').Replace('.', '。').Replace('!', '！').Replace('?', '？')
                .Replace(':', '，').Replace(';', '，').Replace('：', '，').Replace('；', '，').Replace('、', '，');
            var segments = new List<int[]>();
            var phones = new List<int>();
            bool spoken = false;
            void Flush()
            {
                if (phones.Count == 0) return;
                int[] ids = new int[2 * phones.Count + 1];
                for (int i = 0; i < phones.Count; ++i) ids[2 * i + 1] = phones[i];
                segments.Add(ids);
                phones.Clear();
            }
            for (int offset = 0; offset < normalized.Length;)
            {
                char c = normalized[offset];
                if (char.IsWhiteSpace(c)) { ++offset; continue; }
                if ("，。！？—…".IndexOf(c) >= 0)
                {
                    if (phones.Count > 0) { phones.Add(_tokens[c]); Flush(); }
                    ++offset;
                    continue;
                }
                int length = Math.Min(_maxWord, normalized.Length - offset);
                int[] unit = null;
                for (; length > 0; --length)
                    if (_lexicon.TryGetValue(normalized.Substring(offset, length), out unit)) break;
                if (unit == null || length == 0)
                    throw new NotSupportedException("No Chinese VITS pronunciation for U+" + ((int)c).ToString("X4") + ". Write numbers as Chinese words.");
                if (phones.Count + unit.Length > 200) Flush();
                phones.AddRange(unit);
                spoken = true;
                offset += length;
            }
            Flush();
            if (!spoken) throw new ArgumentException("Chinese VITS requires pronounceable text.", nameof(text));
            return segments.ToArray();
        }

        public MNNGeneratedAudio Synthesize(string text, int speakerId = 0, float speed = 1, CancellationToken cancellationToken = default)
        {
            if (float.IsNaN(speed) || speed < .5f || speed > 2) throw new ArgumentOutOfRangeException(nameof(speed));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNSherpaVitsChinese));
                cancellationToken.ThrowIfCancellationRequested();
                if (speakerId < 0 || speakerId >= _voices.Length) throw new ArgumentOutOfRangeException(nameof(speakerId));
                var parts = Encode(text);
                var waves = new List<float[]>();
                foreach (int[] ids in parts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (_module == null || _length != ids.Length || _speaker != speakerId || _speed != speed)
                    {
                        _module?.Dispose(); _module = null;
                        byte[] prepared = MNNGraphConstant.FreezeInputs(_graph, new Input("x_length", new[]{ids.Length}, 1),
                            new Input("noise_scale", new[]{.667f}, 1), new Input("length_scale", new[]{1 / speed}, 1),
                            new Input("noise_scale_w", new[]{.8f}, 1), new Input("sid", new[]{speakerId}, 1));
                        _module = new MNNSingleInputModule(_path, "x", "y", _threads, MNNBackendType.CPU, prepared, MNNPrecisionMode.High);
                        _length = ids.Length; _speaker = speakerId; _speed = speed;
                    }
                    float[] wave = _module.Run(ids, 1, ids.Length);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (wave.Length < SampleRate / 10 || wave.Length > SampleRate * 30 || wave.Any(v => float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 2))
                        throw new InvalidDataException("Chinese VITS returned an invalid waveform.");
                    float peak = wave.Max(v => Math.Abs(v));
                    if (peak < .001f) throw new InvalidDataException("Chinese VITS returned silence.");
                    if (peak > 1) for (int i = 0; i < wave.Length; ++i) wave[i] /= peak;
                    waves.Add(wave);
                }
                return new MNNGeneratedAudio(MNNSpeechDsp.JoinWaveforms(waves, SampleRate), SampleRate, text);
            }
        }

        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, int speakerId = 0, float speed = 1, CancellationToken cancellationToken = default)
            => Task.Run(() => Synthesize(text, speakerId, speed, cancellationToken), cancellationToken);

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _module?.Dispose(); _module = null;
            }
        }
    }
}
