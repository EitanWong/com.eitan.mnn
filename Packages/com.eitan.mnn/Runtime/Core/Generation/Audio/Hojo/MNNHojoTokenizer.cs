using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    // Hojo's scalar BPE is deliberately separate from byte-level Qwen tokenizers.
    internal sealed class MNNHojoTokenizer
    {
        private sealed class Node
        {
            internal readonly Dictionary<char, Node> Children = new Dictionary<char, Node>();
            internal int Id = -1;
        }
        private readonly Node _added = new Node();
        private readonly Node _normalized = new Node();
        private readonly Dictionary<string, int> _vocab, _ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Regex _split, _replace;
        private readonly string _replacement;
        internal int Id(string token) => _vocab.TryGetValue(token, out int value) ? value : 0;
        internal MNNHojoTokenizer(string path)
        {
            var root = MNNModelData.Read(path);
            var normalizers = MNNModelData.Array(MNNModelData.Get(MNNModelData.Get(root, "normalizer"), "normalizers"));
            if (!Equals(MNNModelData.Get(normalizers[0], "type"), "Lowercase")) throw new InvalidDataException("Expected Hojo lowercase normalization.");
            _replace = new Regex((string)MNNModelData.Get(MNNModelData.Get(normalizers[1], "pattern"), "Regex"), RegexOptions.CultureInvariant);
            _replacement = (string)MNNModelData.Get(normalizers[1], "content");
            var model = MNNModelData.Get(root, "model");
            if (!Equals(MNNModelData.Get(model, "type"), "BPE")) throw new InvalidDataException("Expected Hojo scalar BPE.");
            _vocab = MNNModelData.Object(MNNModelData.Get(model, "vocab")).ToDictionary(p => p.Key, p => MNNModelData.Integer(p.Value));
            foreach (var item in MNNModelData.Array(MNNModelData.Get(root, "added_tokens")))
            {
                foreach (string flag in new[] { "single_word", "lstrip", "rstrip" })
                    if ((bool)MNNModelData.Get(item, flag)) throw new InvalidDataException("Unsupported Hojo added-token rule: " + flag);
                string content = (string)MNNModelData.Get(item, "content");
                int id = MNNModelData.Integer(MNNModelData.Get(item, "id"));
                _vocab[content] = id;
                bool normalized = (bool)MNNModelData.Get(item, "normalized");
                var node = normalized ? _normalized : _added;
                if (normalized) content = Normalize(content);
                foreach (char ch in content)
                {
                    if (!node.Children.TryGetValue(ch, out var child)) node.Children[ch] = child = new Node();
                    node = child;
                }
                node.Id = id;
            }
            int rank = 0;
            foreach (var item in MNNModelData.Array(MNNModelData.Get(model, "merges")))
            {
                var pair = item as List<object>;
                _ranks.Add(pair == null ? (string)item : pair[0] + " " + pair[1], rank++);
            }
            var pre = MNNModelData.Array(MNNModelData.Get(MNNModelData.Get(root, "pre_tokenizer"), "pretokenizers"));
            _split = new Regex((string)MNNModelData.Get(MNNModelData.Get(pre[1], "pattern"), "Regex"), RegexOptions.CultureInvariant);
        }

        internal int[] Encode(string text)
        {
            var output = new List<int>();
            EncodeAdded(text, _added, output, EncodePlain);
            return output.ToArray();
        }

        private static void EncodeAdded(string text, Node trie, List<int> output, Action<string, List<int>> fallback)
        {
            int plain = 0, at = 0;
            while (at < text.Length)
            {
                var node = trie; int end = at, best = at, id = -1;
                while (end < text.Length && node.Children.TryGetValue(text[end], out node))
                { ++end; if (node.Id >= 0) { best = end; id = node.Id; } }
                if (id < 0) { ++at; continue; }
                fallback(text.Substring(plain, at - plain), output);
                output.Add(id); at = plain = best;
            }
            fallback(text.Substring(plain), output);
        }

        private string Normalize(string text) => _replace.Replace(text.ToLowerInvariant(), _replacement);
        private void EncodePlain(string text, List<int> output)
        {
            EncodeAdded(Normalize(text), _normalized, output, EncodeWords);
        }
        private void EncodeWords(string text, List<int> output)
        {
            foreach (Match digitPart in Regex.Matches(text, @"\d|\D+", RegexOptions.CultureInvariant))
            {
                int start = 0;
                foreach (Match word in _split.Matches(digitPart.Value))
                {
                    Merge(digitPart.Value.Substring(start, word.Index - start), output);
                    Merge(word.Value, output); start = word.Index + word.Length;
                }
                Merge(digitPart.Value.Substring(start), output);
            }
        }

        private void Merge(string word, List<int> output)
        {
            var pieces = new List<string>();
            for (int i = 0; i < word.Length; ++i)
            {
                int width = char.IsHighSurrogate(word[i]) && i + 1 < word.Length && char.IsLowSurrogate(word[i + 1]) ? 2 : 1;
                pieces.Add(word.Substring(i, width)); i += width - 1;
            }
            while (pieces.Count > 1)
            {
                int best = int.MaxValue, at = -1;
                for (int i = 0; i + 1 < pieces.Count; ++i)
                    if (_ranks.TryGetValue(pieces[i] + " " + pieces[i + 1], out int rank) && rank < best) { best = rank; at = i; }
                if (at < 0) break;
                string left = pieces[at], right = pieces[at + 1];
                // Hugging Face BPE merges every non-overlapping occurrence at a rank.
                for (int i = 0; i + 1 < pieces.Count; ++i)
                    if (pieces[i] == left && pieces[i + 1] == right) { pieces[i] += pieces[i + 1]; pieces.RemoveAt(i + 1); }
            }
            foreach (string piece in pieces) output.Add(Id(piece));
        }
    }
}
