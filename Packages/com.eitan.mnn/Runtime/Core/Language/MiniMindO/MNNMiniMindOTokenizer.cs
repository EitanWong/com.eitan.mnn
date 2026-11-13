using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    // Released MiniMind-3o ByteLevel BPE. Unlike ModernBERT, it has no normalizer.
    internal sealed class MNNMiniMindOTokenizer
    {
        private readonly MNNBytePairTokenizer _encoder;
        private readonly Dictionary<int, string> _pieces;
        private readonly Dictionary<char, byte> _bytes;
        private readonly HashSet<int> _special;

        internal MNNMiniMindOTokenizer(string path)
        {
            var root = MNNModelData.Read(path);
            var model = MNNModelData.Get(root, "model");
            var pre = MNNModelData.Get(root, "pre_tokenizer");
            if (MNNModelData.Get(root, "normalizer") != null || !Equals(MNNModelData.Get(model, "type"), "BPE") ||
                !Equals(MNNModelData.Get(pre, "type"), "ByteLevel") || (bool)MNNModelData.Get(pre, "add_prefix_space") ||
                !(bool)MNNModelData.Get(pre, "use_regex"))
                throw new InvalidDataException("Expected the released MiniMind-o ByteLevel BPE tokenizer.");
            var vocabulary = MNNModelData.Object(MNNModelData.Get(model, "vocab"))
                .ToDictionary(p => p.Key, p => MNNModelData.Integer(p.Value), StringComparer.Ordinal);
            if (vocabulary.Count != 6400) throw new InvalidDataException("MiniMind-o requires its 6400-token vocabulary.");
            _pieces = vocabulary.ToDictionary(p => p.Value, p => p.Key);
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in MNNModelData.Array(MNNModelData.Get(model, "merges")))
            {
                var pair = item as List<object>;
                ranks.Add(pair == null ? (string)item : pair[0] + " " + pair[1], ranks.Count);
            }
            var alphabet = new char[256];
            int extra = 256;
            for (int b = 0; b < 256; ++b)
                alphabet[b] = (char)(b >= 33 && b <= 126 || b >= 161 && b <= 172 || b >= 174 ? b : extra++);
            _bytes = Enumerable.Range(0, 256).ToDictionary(b => alphabet[b], b => (byte)b);
            var added = new Dictionary<string, int>(StringComparer.Ordinal);
            _special = new HashSet<int>();
            foreach (var item in MNNModelData.Array(MNNModelData.Get(root, "added_tokens")))
            {
                if ((bool)MNNModelData.Get(item, "lstrip") || (bool)MNNModelData.Get(item, "rstrip") || (bool)MNNModelData.Get(item, "single_word"))
                    throw new InvalidDataException("Unsupported MiniMind-o added token boundary rule.");
                int id = MNNModelData.Integer(MNNModelData.Get(item, "id"));
                added.Add((string)MNNModelData.Get(item, "content"), id);
                if ((bool)MNNModelData.Get(item, "special")) _special.Add(id);
            }
            var words = new Regex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+", RegexOptions.CultureInvariant);
            _encoder = new MNNBytePairTokenizer(vocabulary, ranks, alphabet, words, added: added);
        }

        internal int[] Encode(string text) => _encoder.Encode(text);

        internal string Decode(IEnumerable<int> tokens)
        {
            var bytes = new List<byte>();
            foreach (int id in tokens)
            {
                if (_special.Contains(id)) continue;
                if (!_pieces.TryGetValue(id, out string piece)) throw new InvalidDataException("Invalid MiniMind-o token ID.");
                foreach (char c in piece) bytes.Add(_bytes[c]);
            }
            // Streaming snapshots omit an incomplete trailing UTF-8 character.
            var data = bytes.ToArray();
            var chars = new char[data.Length];
            Encoding.UTF8.GetDecoder().Convert(data, 0, data.Length, chars, 0, chars.Length, false,
                out _, out int count, out _);
            return new string(chars, 0, count);
        }

        internal static string Prompt(string text, MNNChatMessage[] history = null)
        {
            var result = new StringBuilder();
            foreach (var message in history ?? Array.Empty<MNNChatMessage>())
            {
                if (message == null) throw new ArgumentException("History contains a null message.", nameof(history));
                string content = message.Content;
                result.Append("<|im_start|>").Append(message.Role.ToString().ToLowerInvariant()).Append('\n');
                if (message.Role == MNNChatRole.Assistant)
                {
                    string reasoning = "";
                    int end = content.IndexOf("</think>", StringComparison.Ordinal);
                    if (end >= 0)
                    {
                        reasoning = content.Substring(0, end);
                        int start = reasoning.LastIndexOf("<think>", StringComparison.Ordinal);
                        if (start >= 0) reasoning = reasoning.Substring(start + 7);
                        content = content.Substring(content.LastIndexOf("</think>", StringComparison.Ordinal) + 8);
                    }
                    result.Append("<think>\n").Append(reasoning.Trim('\n')).Append("\n</think>\n\n");
                    content = content.TrimStart('\n');
                }
                result.Append(content).Append("<|im_end|>\n");
            }
            return result.Append("<|im_start|>user\n").Append(text)
                .Append("<|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n").ToString();
        }
    }
}
