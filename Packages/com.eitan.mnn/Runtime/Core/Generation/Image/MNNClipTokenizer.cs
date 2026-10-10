using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    internal sealed class MNNClipTokenizer
    {
        private readonly Dictionary<string, object> _vocab;
        private readonly Dictionary<string, int> _ranks = new Dictionary<string, int>();
        private readonly Dictionary<string, int[]> _cache = new Dictionary<string, int[]>();
        private readonly char[] _bytes = new char[256];
        private static readonly Regex Words = new Regex(@"<\|startoftext\|>|<\|endoftext\|>|'s|'t|'re|'ve|'m|'ll|'d|[\p{L}]+|[\p{N}]|[^\s\p{L}\p{N}]+", RegexOptions.IgnoreCase);
        internal MNNClipTokenizer(string directory)
        {
            _vocab = MNNModelData.Object(MNNModelData.Read(Path.Combine(directory, "vocab.json")));
            if (!_vocab.ContainsKey("<|startoftext|>") || MNNModelData.Integer(_vocab["<|startoftext|>"]) != 49406 || MNNModelData.Integer(_vocab["<|endoftext|>"]) != 49407)
                throw new InvalidDataException("Expected CLIP SD 1.5 vocabulary.");
            int rank = 0;
            foreach (string line in File.ReadLines(Path.Combine(directory, "merges.txt")))
            {
                if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                    continue;
                _ranks.Add(line.Trim(), rank++);
            }

            int extra = 256;
            for (int b = 0; b < 256; ++b)
                _bytes[b] = (char)(b >= 33 && b <= 126 || b >= 161 && b <= 172 || b >= 174 ? b : extra++);
        }

        internal int[] Encode(string text)
        {
            // CLIP uses lowercase, collapsed whitespace and twice-unescaped HTML entities.
            text = System.Net.WebUtility.HtmlDecode(System.Net.WebUtility.HtmlDecode(text));
            text = Regex.Replace(text.Normalize(NormalizationForm.FormC), @"\s+", " ").Trim().ToLowerInvariant();
            var ids = new List<int>{49406};
            foreach (Match word in Words.Matches(text))
            {
                if (!_cache.TryGetValue(word.Value, out var tokens))
                {
                    if (word.Value == "<|startoftext|>" || word.Value == "<|endoftext|>")
                        tokens = new[]{MNNModelData.Integer(_vocab[word.Value])};
                    else
                    {
                        var pieces = Encoding.UTF8.GetBytes(word.Value).Select(b => _bytes[b].ToString()).ToList();
                        pieces[pieces.Count - 1] += "</w>";
                        while (pieces.Count > 1)
                        {
                            int best = int.MaxValue;
                            string pair = null;
                            for (int i = 0; i < pieces.Count - 1; ++i)
                            {
                                string key = pieces[i] + " " + pieces[i + 1];
                                if (_ranks.TryGetValue(key, out int value) && value < best)
                                {
                                    best = value;
                                    pair = key;
                                }
                            }

                            if (pair == null)
                                break;
                            for (int i = 0; i < pieces.Count - 1; ++i)
                                if (pieces[i] + " " + pieces[i + 1] == pair)
                                {
                                    pieces[i] += pieces[i + 1];
                                    pieces.RemoveAt(i + 1);
                                }
                        }

                        tokens = pieces.Select(p => MNNModelData.Integer(_vocab[p])).ToArray();
                    }

                    _cache[word.Value] = tokens;
                }

                ids.AddRange(tokens);
                if (ids.Count >= 76)
                    break;
            }

            if (ids.Count > 76)
                ids.RemoveRange(76, ids.Count - 76);
            ids.Add(49407);
            while (ids.Count < 77)
                ids.Add(49407);
            return ids.ToArray();
        }
    }
}
