using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    internal sealed class MNNSanaTokenizer
    {
        private readonly Dictionary<string, int> _vocab = new Dictionary<string, int>(), _ranks = new Dictionary<string, int>();
        private readonly char[] _bytes = new char[256];
        private readonly Regex _special;
        private static readonly Regex Words = new Regex(@"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+");
        internal MNNSanaTokenizer(string path)
        {
            using (var reader = File.OpenText(path))
            {
                if (reader.ReadLine() != "430 3")
                    throw new NotSupportedException("Expected Huggingface MNN tokenizer v3.");
                var counts = Integers(reader.ReadLine());
                var specialIds = Integers(reader.ReadLine()).Take(counts[0]).ToArray();
                var lengths = Integers(reader.ReadLine());
                for (int i = 0; i < lengths[0]; ++i)
                    _vocab.Add(reader.ReadLine() ?? throw new EndOfStreamException(), i);
                for (int i = 0; i < lengths[1]; ++i)
                    _ranks.Add(reader.ReadLine() ?? throw new EndOfStreamException(), i);
                _special = new Regex("(" + string.Join("|", specialIds.Select(id => Regex.Escape(_vocab.First(p => p.Value == id).Key))) + ")");
                if (!_vocab.TryGetValue("<|im_start|>", out int start) || start != 151644)
                    throw new InvalidDataException("Expected Qwen3 Sana special tokens.");
            }

            int extra = 256;
            for (int b = 0; b < 256; ++b)
                _bytes[b] = (char)(b >= 33 && b <= 126 || b >= 161 && b <= 172 || b >= 174 ? b : extra++);
        }

        private static int[] Integers(string line) => (line ?? throw new EndOfStreamException()).Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        internal int[] Encode(string text)
        {
            var result = new List<int>();
            foreach (string part in _special.Split(text))
            {
                if (part.Length == 0)
                    continue;
                if (_vocab.TryGetValue(part, out int special) && _special.IsMatch(part))
                {
                    result.Add(special);
                    continue;
                }

                foreach (Match word in Words.Matches(part))
                {
                    var pieces = Encoding.UTF8.GetBytes(word.Value).Select(b => _bytes[b].ToString()).ToList();
                    while (pieces.Count > 1)
                    {
                        int best = int.MaxValue, index = -1;
                        for (int i = 0; i < pieces.Count - 1; ++i)
                            if (_ranks.TryGetValue(pieces[i] + " " + pieces[i + 1], out int rank) && rank < best)
                            {
                                best = rank;
                                index = i;
                            }

                        if (index < 0)
                            break;
                        pieces[index] += pieces[index + 1];
                        pieces.RemoveAt(index + 1);
                    }

                    result.AddRange(pieces.Select(piece => _vocab[piece]));
                }
            }

            return result.ToArray();
        }
    }
}
