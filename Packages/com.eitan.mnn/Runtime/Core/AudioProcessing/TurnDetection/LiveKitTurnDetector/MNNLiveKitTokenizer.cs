using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    internal sealed class MNNLiveKitTokenizer
    {
        private readonly MNNBytePairTokenizer _tokenizer;
        internal MNNLiveKitTokenizer(string directory)
        {
            var vocabulary = MNNModelData.Object(MNNModelData.Read(Path.Combine(directory, "vocab.json")))
                .ToDictionary(pair => pair.Key, pair => MNNModelData.Integer(pair.Value), StringComparer.Ordinal);
            var added = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in MNNModelData.Object(MNNModelData.Read(Path.Combine(directory, "added_tokens.json"))))
                added[pair.Key] = MNNModelData.Integer(pair.Value);
            foreach (string token in new[]{"<|endoftext|>", "<|im_start|>", "<|im_end|>"})
                if (vocabulary.TryGetValue(token, out int id)) added[token] = id;
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            int rank = 0;
            foreach (string line in File.ReadLines(Path.Combine(directory, "merges.txt")))
            {
                string merge = line.Trim();
                if (string.IsNullOrEmpty(merge) || merge.StartsWith("#", StringComparison.Ordinal)) continue;
                ranks.Add(merge, rank++);
            }
            var bytes = new char[256];
            int extra = 256;
            for (int b = 0; b < 256; ++b) bytes[b] = (char)(b >= 33 && b <= 126 || b >= 161 && b <= 172 || b >= 174 ? b : extra++);
            // Digits(individual_digits=true) runs before ByteLevel in this model.
            // A preceding space is its own pretoken, and digits cannot merge.
            var words = new Regex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+", RegexOptions.CultureInvariant);
            _tokenizer = new MNNBytePairTokenizer(vocabulary, ranks, bytes, words, added: added);
        }

        internal int[] Encode(string text) => _tokenizer.Encode(Regex.Replace(text, @"\s+", " ").Trim());
    }
}
