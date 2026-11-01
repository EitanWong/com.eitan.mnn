using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    internal sealed class MNNCosyVoice300MTokenizer
    {
        private readonly MNNBytePairTokenizer _tokenizer;
        internal MNNCosyVoice300MTokenizer(string directory)
        {
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string line in File.ReadLines(Path.Combine(directory, "whisper.tiktoken")))
            {
                var fields = line.Split(new[] {' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length != 2) throw new InvalidDataException("Invalid Whisper vocabulary.");
                // Whisper stores rank 50256 as "=", the empty byte token.
                string key = fields[0] == "=" ? string.Empty : new string(Convert.FromBase64String(fields[0]).Select(b => (char)b).ToArray());
                ranks.Add(key, int.Parse(fields[1], CultureInfo.InvariantCulture));
            }
            if (ranks.Count != 50257 || ranks.Values.Distinct().Count() != ranks.Count || ranks.Values.Min() != 0 || ranks.Values.Max() != 50256)
                throw new InvalidDataException("Expected the multilingual Whisper vocabulary.");
            var special = MNNModelData.Object(MNNModelData.Read(Path.Combine(directory, "whisper-special-tokens.json")))
                .ToDictionary(p => p.Key, p => MNNModelData.Integer(p.Value), StringComparer.Ordinal);
            var bytes = Enumerable.Range(0, 256).Select(i => (char)i).ToArray();
            if (bytes.Any(b => !ranks.ContainsKey(b.ToString()))) throw new InvalidDataException("Incomplete Whisper byte vocabulary.");
            var words = new Regex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+", RegexOptions.CultureInvariant);
            _tokenizer = new MNNBytePairTokenizer(ranks, null, bytes, words, byteRanks: true, added: special);
        }
        internal int[] Encode(string text) => _tokenizer.Encode(text);
    }
}
