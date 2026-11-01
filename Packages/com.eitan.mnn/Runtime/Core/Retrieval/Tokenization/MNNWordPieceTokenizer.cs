using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    // BERT BasicTokenizer + greedy WordPiece, using model vocabulary IDs rather than native objects.
    internal sealed class MNNWordPieceTokenizer
    {
        private readonly Dictionary<string, int> _vocabulary;
        private readonly bool _lowerCase, _stripAccents, _chinese;
        private readonly int _unknown, _cls, _sep, _maximumWord;
        private readonly string _specialPattern;

        internal MNNWordPieceTokenizer(string directory, MNNBertEmbeddingConfig config)
        {
            string json = Path.Combine(directory, config.tokenizer ?? "tokenizer.json");
            _lowerCase = config.lowerCase;
            _stripAccents = config.lowerCase;
            _chinese = true;
            _maximumWord = 100;
            if (File.Exists(json))
            {
                var root = MNNModelData.Object(MNNModelData.Read(MNNBertEmbeddingConfig.ResolveFile(directory, config.tokenizer ?? "tokenizer.json")));
                var model = MNNModelData.Object(MNNModelData.Get(root, "model"));
                if (MNNModelData.Get(model, "type") as string != "WordPiece" || MNNModelData.Get(model, "continuing_subword_prefix") as string != "##")
                    throw new NotSupportedException("This encoder requires a BERT WordPiece tokenizer with ## continuation pieces.");
                _vocabulary = MNNModelData.Object(MNNModelData.Get(model, "vocab")).ToDictionary(item => item.Key, item => MNNModelData.Integer(item.Value), StringComparer.Ordinal);
                _maximumWord = MNNModelData.Integer(MNNModelData.Get(model, "max_input_chars_per_word"));
                if (!root.TryGetValue("normalizer", out var normalizer) || normalizer == null || MNNModelData.Get(normalizer, "type") as string != "BertNormalizer")
                    throw new NotSupportedException("Unsupported tokenizer normalizer. Expected BertNormalizer.");
                _lowerCase = (bool)MNNModelData.Get(normalizer, "lowercase");
                var strip = MNNModelData.Get(normalizer, "strip_accents");
                _stripAccents = strip is bool accents ? accents : _lowerCase;
                _chinese = (bool)MNNModelData.Get(normalizer, "handle_chinese_chars");
                if (!(bool)MNNModelData.Get(normalizer, "clean_text") || !root.TryGetValue("pre_tokenizer", out var pre) || MNNModelData.Get(pre, "type") as string != "BertPreTokenizer")
                    throw new NotSupportedException("Unsupported WordPiece text cleaning or pre-tokenizer.");
            }
            else
            {
                string path = MNNBertEmbeddingConfig.ResolveFile(directory, config.vocabulary);
                _vocabulary = new Dictionary<string, int>(StringComparer.Ordinal);
                var lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; ++i)
                    if (!_vocabulary.TryAdd(lines[i], i))
                        throw new InvalidDataException("Duplicate WordPiece vocabulary entry.");
            }
            _unknown = Id("[UNK]");
            _cls = Id("[CLS]");
            _sep = Id("[SEP]");
            _specialPattern = string.Join("|", new[] {"[CLS]", "[SEP]", "[UNK]", "[MASK]", "[PAD]"}.Where(_vocabulary.ContainsKey).Select(Regex.Escape));
            if (_maximumWord < 1 || _maximumWord > 10000 || _vocabulary.Values.Any(id => id < 0))
                throw new InvalidDataException("Invalid WordPiece vocabulary configuration.");
        }

        private int Id(string token) => _vocabulary.TryGetValue(token, out int value) ? value : throw new InvalidDataException("Missing WordPiece special token: " + token);

        internal int[] Encode(string text, int maximumTokens)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (maximumTokens < 2)
                throw new ArgumentOutOfRangeException(nameof(maximumTokens));
            var ids = new List<int> {_cls};
            foreach (string token in Tokens(text))
            {
                foreach (int id in WordPieces(token))
                {
                    if (ids.Count >= maximumTokens - 1)
                        break;
                    ids.Add(id);
                }
                if (ids.Count >= maximumTokens - 1)
                    break;
            }
            ids.Add(_sep);
            return ids.ToArray();
        }

        private IEnumerable<string> Tokens(string text)
        {
            int start = 0;
            foreach (Match special in Regex.Matches(text, _specialPattern))
            {
                foreach (string token in BasicTokens(text.Substring(start, special.Index - start)))
                    yield return token;
                yield return special.Value;
                start = special.Index + special.Length;
            }
            foreach (string token in BasicTokens(text.Substring(start)))
                yield return token;
        }

        private IEnumerable<string> BasicTokens(string text)
        {
            var clean = new StringBuilder();
            for (int i = 0; i < text.Length;)
            {
                int scalar = char.ConvertToUtf32(text, i);
                int size = scalar > 0xffff ? 2 : 1;
                var category = CharUnicodeInfo.GetUnicodeCategory(text, i);
                if (scalar == 0 || scalar == 0xfffd || (category == UnicodeCategory.Control || category == UnicodeCategory.Format) && scalar != 9 && scalar != 10 && scalar != 13)
                {
                    i += size;
                    continue;
                }
                if (char.IsWhiteSpace(text, i))
                    clean.Append(' ');
                else
                {
                    bool separate = _chinese && IsChinese(scalar);
                    if (separate) clean.Append(' ');
                    clean.Append(text, i, size);
                    if (separate) clean.Append(' ');
                }
                i += size;
            }
            foreach (string word in clean.ToString().Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries))
            {
                string normalized = _lowerCase ? word.ToLowerInvariant() : word;
                if (_stripAccents)
                {
                    var accents = new StringBuilder();
                    string decomposed = normalized.Normalize(NormalizationForm.FormD);
                    for (int i = 0; i < decomposed.Length; ++i)
                        if (CharUnicodeInfo.GetUnicodeCategory(decomposed, i) != UnicodeCategory.NonSpacingMark)
                            accents.Append(decomposed[i]);
                    normalized = accents.ToString();
                }
                var part = new StringBuilder();
                for (int i = 0; i < normalized.Length;)
                {
                    int scalar = char.ConvertToUtf32(normalized, i), size = scalar > 0xffff ? 2 : 1;
                    if (IsPunctuation(scalar, CharUnicodeInfo.GetUnicodeCategory(normalized, i)))
                    {
                        if (part.Length > 0) { yield return part.ToString(); part.Clear(); }
                        yield return normalized.Substring(i, size);
                    }
                    else
                        part.Append(normalized, i, size);
                    i += size;
                }
                if (part.Length > 0)
                    yield return part.ToString();
            }
        }

        private IEnumerable<int> WordPieces(string token)
        {
            var offsets = new List<int>();
            for (int i = 0; i < token.Length; i += char.IsSurrogatePair(token, i) ? 2 : 1)
                offsets.Add(i);
            offsets.Add(token.Length);
            if (offsets.Count - 1 > _maximumWord)
                return new[] {_unknown};
            var result = new List<int>();
            for (int start = 0; start < offsets.Count - 1;)
            {
                int end = offsets.Count - 1, id = -1;
                while (end > start)
                {
                    string piece = (start == 0 ? "" : "##") + token.Substring(offsets[start], offsets[end] - offsets[start]);
                    if (_vocabulary.TryGetValue(piece, out id))
                        break;
                    --end;
                }
                if (end == start)
                    return new[] {_unknown};
                result.Add(id);
                start = end;
            }
            return result;
        }

        private static bool IsChinese(int value) => value >= 0x4e00 && value <= 0x9fff || value >= 0x3400 && value <= 0x4dbf || value >= 0x20000 && value <= 0x2a6df || value >= 0x2a700 && value <= 0x2b73f || value >= 0x2b740 && value <= 0x2b81f || value >= 0x2b820 && value <= 0x2ceaf || value >= 0xf900 && value <= 0xfaff || value >= 0x2f800 && value <= 0x2fa1f;
        private static bool IsPunctuation(int value, UnicodeCategory category) => value >= 33 && value <= 47 || value >= 58 && value <= 64 || value >= 91 && value <= 96 || value >= 123 && value <= 126 || category == UnicodeCategory.ConnectorPunctuation || category == UnicodeCategory.DashPunctuation || category == UnicodeCategory.OpenPunctuation || category == UnicodeCategory.ClosePunctuation || category == UnicodeCategory.InitialQuotePunctuation || category == UnicodeCategory.FinalQuotePunctuation || category == UnicodeCategory.OtherPunctuation;
    }
}
