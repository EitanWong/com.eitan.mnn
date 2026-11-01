using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace MNN.Unity
{
    internal sealed class MNNVoxTurnTokenizer
    {
        private readonly MNNBytePairTokenizer _tokenizer;
        internal MNNVoxTurnTokenizer(string path)
        {
            var data = MNNModelData.Read(path);
            var vocab = MNNModelData.Object(MNNModelData.Get(data, "vocab")).ToDictionary(p => p.Key, p => MNNModelData.Integer(p.Value), StringComparer.Ordinal);
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            int rank = 0;
            foreach (object value in MNNModelData.Array(MNNModelData.Get(data, "merges")))
            {
                var pair = value as List<object>;
                ranks.Add(pair == null ? (string)value : pair[0] + " " + pair[1], rank++);
            }
            var added = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (object value in MNNModelData.Array(MNNModelData.Get(data, "added")))
                added.Add((string)MNNModelData.Get(value, "content"), MNNModelData.Integer(MNNModelData.Get(value, "id")));
            if (vocab.Count != 151643 || ranks.Count != 151387 || added.Count != 62)
                throw new InvalidDataException("Unexpected Qwen3-ASR tokenizer.");
            var bytes = new char[256];
            int extra = 256;
            for (int b = 0; b < 256; ++b)
                bytes[b] = (char)(b >= 33 && b <= 126 || b >= 161 && b <= 172 || b >= 174 ? b : extra++);
            var words = new Regex(@"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+", RegexOptions.CultureInvariant);
            _tokenizer = new MNNBytePairTokenizer(vocab, ranks, bytes, words, added: added, normalize: true);
        }
        internal int[] Encode(string text) => _tokenizer.Encode(text);
    }
}
