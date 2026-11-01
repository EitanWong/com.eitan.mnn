using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    internal sealed class MNNSpeechTokenizer
    {
        private readonly Dictionary<string, int> _vocab = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _merges = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly char[] _bytes = new char[256];
        private readonly Regex _words;
        private readonly bool _index;
        private readonly MNNBytePairTokenizer _bytePair;
        private readonly int _indexLanguageBase;
        internal MNNSpeechTokenizer(string qwenDirectory)
        {
            foreach (var pair in MNNModelData.Object(MNNModelData.Read(Path.Combine(qwenDirectory, "vocab.json"))))
                _vocab.Add(pair.Key, MNNModelData.Integer(pair.Value));
            int rank = 0;
            foreach (string line in File.ReadLines(Path.Combine(qwenDirectory, "merges.txt")))
                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#", StringComparison.Ordinal)) _merges.Add(line.TrimEnd('\r', '\n'), rank++);
            int extra = 256;
            for (int b = 0; b < 256; ++b) _bytes[b] = (char)(b >= 33 && b <= 126 || b >= 161 && b <= 172 || b >= 174 ? b : extra++);
            _words = new Regex(@"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+", RegexOptions.CultureInvariant);
            if (Enumerable.Range(0, 256).Any(i => !_vocab.ContainsKey(_bytes[i].ToString()))) throw new InvalidDataException("Incomplete Qwen byte vocabulary.");
            _bytePair = new MNNBytePairTokenizer(_vocab, _merges, _bytes, _words);
        }
        internal MNNSpeechTokenizer(string root, bool index)
        {
            _index = index;
            if (index)
            {
                for (int i = 0; i < 256; ++i) _bytes[i] = (char)i;
                foreach (string line in File.ReadLines(Path.Combine(root, "multilingual_zh_ja_yue_char_del.tiktoken")))
                {
                    var parts = line.Split(new[]{' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2) throw new InvalidDataException("Invalid IndexTTS vocabulary.");
                    _vocab.Add(new string((parts[0] == "=" ? Array.Empty<byte>() : Convert.FromBase64String(parts[0])).Select(b => (char)b).ToArray()), int.Parse(parts[1], CultureInfo.InvariantCulture));
                }
                _indexLanguageBase = _vocab.Count + 2;
                _words = new Regex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+", RegexOptions.CultureInvariant);
            }
            else
            {
                var json = MNNModelData.Read(Path.Combine(root, "text_tokenizer", "tokenizer.json"));
                var model = MNNModelData.Get(json, "model");
                foreach (var pair in MNNModelData.Object(MNNModelData.Get(model, "vocab"))) _vocab.Add(pair.Key, MNNModelData.Integer(pair.Value));
                if (_vocab.Count != 151643 || !Equals(MNNModelData.Get(model, "type"), "BPE")) throw new InvalidDataException("Expected FireRedTTS3 Qwen3 BPE vocabulary.");
                int rank = 0;
                foreach (var merge in MNNModelData.Array(MNNModelData.Get(model, "merges")))
                {
                    var pair = merge as List<object>;
                    _merges.Add(pair == null ? (string)merge : pair[0] + " " + pair[1], rank++);
                }
                int extra = 256;
                for (int b = 0; b < 256; ++b) _bytes[b] = (char)(b >= 33 && b <= 126 || b >= 161 && b <= 172 || b >= 174 ? b : extra++);
                _words = new Regex(@"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+", RegexOptions.CultureInvariant);
            }
            if (Enumerable.Range(0, 256).Any(i => !_vocab.ContainsKey(_bytes[i].ToString()))) throw new InvalidDataException("Incomplete byte vocabulary.");
            _bytePair = new MNNBytePairTokenizer(_vocab, _merges, _bytes, _words, _index);
        }

        internal int[] Encode(string text, string language)
        {
            MNNSpeechDsp.ValidateText(text, _index ? 200 : 600);
            if (language != "zh" && language != "en") throw new ArgumentException("Supported speech languages are zh and en.", nameof(language));
            if (_index)
            {
                // Same ordered punctuation substitutions as the upstream frontend.
                string[] from = {"：", "；", ";", "，", "。", "！", "？", "\n", "·", "、", "...", ",,,", "，，，", "……", "“", "”", "\"", "‘", "’", "（", "）", "(", ")", "《", "》", "【", "】", "[", "]", "—", "～", "~", "「", "」", ":"};
                string[] to = {",", ",", ",", ",", ".", "!", "?", " ", "-", ",", "…", "…", "…", "…", "'", "'", "'", "'", "'", "'", "'", "'", "'", "'", "'", "'", "'", "'", "'", "-", "-", "-", "'", "'", ","};
                var replacements = from.Select((key, i) => new {key, value = to[i]}).ToDictionary(p => p.key, p => p.value);
                text = Regex.Replace(text, string.Join("|", from.Select(Regex.Escape)), m => replacements[m.Value]).ToLowerInvariant();
            }
            if (!_index) text = text.Normalize(NormalizationForm.FormC);
            var result = new List<int>();
            result.Add(_index ? _indexLanguageBase + (language == "zh" ? 1 : 0) : language == "zh" ? 151876 : 151877);
            if (_index) text = " " + text;
            else result.Add(151676); // <|sot|>
            result.AddRange(EncodeBare(text));
            if (_index) result.RemoveAll(t => t <= 1);
            else result.Add(151677); // <|eot|>
            if (result.Count > (_index ? 120 : 512)) throw new ArgumentException("Speech text exceeds the model token budget; split it into shorter sentences.", nameof(text));
            return result.ToArray();
        }

        internal int[] EncodeBare(string text) => _bytePair.Encode(text);
    }
}
