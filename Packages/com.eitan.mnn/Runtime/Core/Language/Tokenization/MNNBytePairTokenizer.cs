using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    // Shared ranked byte-pair merge engine for speech and structured text tasks.
    // Model-specific frontends supply their exact pre-tokenizer and byte alphabet.
    internal sealed class MNNBytePairTokenizer
    {
        private readonly Dictionary<string, int> _vocabulary, _ranks;
        private readonly char[] _bytes;
        private readonly Regex _words, _addedPattern;
        private readonly Dictionary<string, int> _added;
        private readonly bool _byteRanks, _normalize;

        internal MNNBytePairTokenizer(Dictionary<string, int> vocabulary, Dictionary<string, int> ranks, char[] bytes,
            Regex words, bool byteRanks = false, Dictionary<string, int> added = null, bool normalize = false, HashSet<string> leftStrip = null)
        {
            _vocabulary = vocabulary; _ranks = ranks; _bytes = bytes; _words = words; _byteRanks = byteRanks;
            _added = added; _normalize = normalize;
            if (added != null && added.Count > 0)
                _addedPattern = new Regex(string.Join("|", added.Keys.OrderByDescending(s => leftStrip?.Contains(s) == true).ThenByDescending(s => s.Length).ThenBy(s => s, StringComparer.Ordinal)
                    .Select(s => (leftStrip?.Contains(s) == true ? @"\s*" : "") + Regex.Escape(s))), RegexOptions.CultureInvariant);
        }

        internal static MNNBytePairTokenizer ReadModernBert(string path)
        {
            var root = MNNModelData.Object(MNNModelData.Read(path));
            var model = MNNModelData.Object(MNNModelData.Get(root, "model"));
            var pre = MNNModelData.Object(MNNModelData.Get(root, "pre_tokenizer"));
            if (!Equals(MNNModelData.Get(model, "type"), "BPE") || !Equals(MNNModelData.Get(pre, "type"), "ByteLevel") ||
                !Equals(MNNModelData.Get(MNNModelData.Get(root, "normalizer"), "type"), "NFC"))
                throw new InvalidDataException("Expected ModernBERT NFC/ByteLevel BPE tokenizer.");
            if ((bool)MNNModelData.Get(pre, "add_prefix_space") || !(bool)MNNModelData.Get(pre, "use_regex"))
                throw new InvalidDataException("Unsupported ByteLevel prefix/regex contract.");
            var vocabulary = MNNModelData.Object(MNNModelData.Get(model, "vocab")).ToDictionary(p => p.Key, p => MNNModelData.Integer(p.Value), StringComparer.Ordinal);
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            int rank = 0;
            foreach (var item in MNNModelData.Array(MNNModelData.Get(model, "merges")))
            {
                var pair = item as List<object>;
                ranks.Add(pair == null ? (string)item : pair[0] + " " + pair[1], rank++);
            }
            var added = new Dictionary<string, int>(StringComparer.Ordinal);
            var leftStrip = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in MNNModelData.Array(MNNModelData.Get(root, "added_tokens")))
            {
                if ((bool)MNNModelData.Get(item, "single_word") || (bool)MNNModelData.Get(item, "rstrip"))
                    throw new InvalidDataException("Unsupported ModernBERT added token boundary rule.");
                string content = (string)MNNModelData.Get(item, "content");
                if (string.IsNullOrEmpty(content)) throw new InvalidDataException("Empty added token.");
                added.Add(content, MNNModelData.Integer(MNNModelData.Get(item, "id")));
                if ((bool)MNNModelData.Get(item, "lstrip")) leftStrip.Add(content);
            }
            var bytes = new char[256]; int extra = 256;
            for (int b = 0; b < 256; ++b) bytes[b] = (char)(b >= 33 && b <= 126 || b >= 161 && b <= 172 || b >= 174 ? b : extra++);
            // ModernBERT omits C0/C1 and F5–FF, which cannot occur in valid UTF-8.
            if (Enumerable.Range(0, 245).Where(b => b != 192 && b != 193).Any(b => !vocabulary.ContainsKey(bytes[b].ToString())))
                throw new InvalidDataException("Incomplete UTF-8 byte vocabulary.");
            var words = new Regex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+", RegexOptions.CultureInvariant);
            return new MNNBytePairTokenizer(vocabulary, ranks, bytes, words, added: added, normalize: true, leftStrip: leftStrip);
        }

        internal int[] Encode(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (_normalize) text = text.Normalize(NormalizationForm.FormC);
            var result = new List<int>();
            int start = 0;
            if (_addedPattern != null)
                foreach (Match added in _addedPattern.Matches(text))
                {
                    EncodeWords(text.Substring(start, added.Index - start), result);
                    result.Add(_added.TryGetValue(added.Value, out int id) ? id : _added[added.Value.TrimStart()]); start = added.Index + added.Length;
                }
            EncodeWords(text.Substring(start), result);
            return result.ToArray();
        }

        private void EncodeWords(string text, List<int> result)
        {
            foreach (Match match in _words.Matches(text))
            {
                var pieces = Encoding.UTF8.GetBytes(match.Value).Select(b => _bytes[b].ToString()).ToList();
                while (pieces.Count > 1)
                {
                    int best = int.MaxValue, at = -1;
                    for (int i = 0; i + 1 < pieces.Count; ++i)
                    {
                        string key = pieces[i] + (_byteRanks ? "" : " ") + pieces[i + 1];
                        if ((_byteRanks ? _vocabulary : _ranks).TryGetValue(key, out int rank) && rank < best) { best = rank; at = i; }
                    }
                    if (at < 0) break;
                    pieces[at] += pieces[at + 1]; pieces.RemoveAt(at + 1);
                }
                foreach (string piece in pieces)
                    result.Add(_vocabulary.TryGetValue(piece, out int id) ? id : throw new InvalidDataException("Unknown byte-pair token."));
            }
        }
    }
}
