using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    // The matching repository lexicons contain IPA, not model token IDs.
    // Keep sparse tokenizer slots: removing empty lines shifts Mandarin tones.
    internal sealed class MNNKokoroText
    {
        private readonly Dictionary<char, int> _tokens = new Dictionary<char, int>();
        private readonly Dictionary<string, int[]> _lexicon = new Dictionary<string, int[]>(StringComparer.Ordinal);
        private int _maxWord;

        internal MNNKokoroText(string directory, bool plainTokenizer = false)
        {
            string[] lines = File.ReadAllLines(Path.Combine(directory, plainTokenizer ? "tokens.txt" : "tokenizer.txt"));
            if (plainTokenizer)
            {
                _tokens['$'] = 0;
                foreach (string line in lines)
                {
                    int separator = line.LastIndexOf(' ');
                    if (separator != 1 || !int.TryParse(line.Substring(separator + 1), out int id) || id < 0)
                        throw new InvalidDataException("Unsupported Kokoro token list entry.");
                    _tokens.Add(line[0], id);
                }
            }
            else
            {
                if (lines.Length < 5 || lines[0].Trim() != "430 1" || !int.TryParse(lines[3], out int count) || count < 1 || lines.Length < count + 4)
                    throw new InvalidDataException("Expected a Kokoro sparse MNN tokenizer.");
                for (int i = 0; i < count; ++i)
                {
                    string token = Encoding.UTF8.GetString(Convert.FromBase64String(lines[i + 4]));
                    if (token.Length == 0)
                        continue;
                    if (token.Length != 1 || _tokens.ContainsKey(token[0]))
                        throw new InvalidDataException("Unsupported Kokoro tokenizer entry.");
                    _tokens.Add(token[0], i);
                }
            }
            if (!_tokens.TryGetValue('$', out int pad) || pad != 0 || !_tokens.ContainsKey(' '))
                throw new InvalidDataException("Unexpected Kokoro boundary/space tokens.");
            foreach (string path in new[]{"lexicon-zh.txt", "lexicon-us-en.txt"}.Select(name => Path.Combine(directory, name)).Where(File.Exists))
                foreach (string line in File.ReadLines(path))
                {
                    string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                        continue;
                    // Sherpa v1.1 marks an unavailable pronunciation with this
                    // sentinel. Leave that word unsupported, rather than making
                    // the entire otherwise valid lexicon impossible to load.
                    if (parts.Skip(1).Contains("❓"))
                        continue;
                    string word = parts[0].ToLowerInvariant();
                    int[] phones = string.Concat(parts.Skip(1)).Select(Token).ToArray();
                    _lexicon[word] = phones;
                    if (!(word[0] >= 'a' && word[0] <= 'z'))
                        _maxWord = Math.Max(_maxWord, word.Length);
                }
            if (_lexicon.Count == 0)
                throw new InvalidDataException("Kokoro requires its matching pronunciation lexicon.");
        }

        private int Token(char value)
        {
            if (!_tokens.TryGetValue(value, out int id))
                throw new NotSupportedException("Kokoro tokenizer has no phoneme U+" + ((int)value).ToString("X4") + ".");
            return id;
        }

        internal int[][] Encode(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 2000)
                throw new ArgumentException("Enter 1–2000 characters for Kokoro.", nameof(text));
            string normalized = Regex.Replace(text.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"\s+", " ")
                .Replace('。', '.').Replace('，', ',').Replace('！', '!').Replace('？', '?').Replace('；', ';').Replace('：', ':')
                .Replace('“', '"').Replace('”', '"').Replace('’', '\'').Replace('‘', '\'').Replace('-', '—').Trim();
            var segments = new List<int[]>();
            var current = new List<int>();
            bool spoken = false;
            void Append(int[] unit)
            {
                if (unit.Length > 200)
                    throw new ArgumentException("Kokoro pronunciation exceeds the segment limit.");
                if (current.Count + unit.Length > 200)
                {
                    segments.Add(current.ToArray());
                    current.Clear();
                }
                current.AddRange(unit);
            }
            for (int offset = 0; offset < normalized.Length;)
            {
                char c = normalized[offset];
                if (" .,;:!?—…\"()'".IndexOf(c) >= 0)
                {
                    Append(new[]{Token(c == '\'' ? '"' : c)});
                    ++offset;
                    if (".!?;".IndexOf(c) >= 0 && current.Count > 0)
                    {
                        segments.Add(current.ToArray());
                        current.Clear();
                    }
                    continue;
                }
                // Latin words must match in full; character-by-character speech
                // would silently turn an unknown word into a different sentence.
                bool latin = c >= 'a' && c <= 'z';
                int length = Math.Min(_maxWord, normalized.Length - offset);
                if (latin)
                {
                    length = 1;
                    while (offset + length < normalized.Length && (normalized[offset + length] >= 'a' && normalized[offset + length] <= 'z' ||
                        normalized[offset + length] == '\'' && offset + length + 1 < normalized.Length && normalized[offset + length + 1] >= 'a' && normalized[offset + length + 1] <= 'z'))
                        ++length;
                }
                int[] phones = null;
                int match = length;
                for (; match > 0; --match)
                {
                    if (_lexicon.TryGetValue(normalized.Substring(offset, match), out phones))
                        break;
                    if (latin)
                        break;
                }
                if (phones == null || match == 0)
                    throw new NotSupportedException("No Kokoro pronunciation for: " + normalized.Substring(offset, latin ? length : 1) + ". Supply text covered by the installed lexicon.");
                if (latin && current.Count > 0 && current[current.Count - 1] != Token(' '))
                    Append(new[]{Token(' ')});
                Append(phones);
                spoken = true;
                offset += match;
            }
            if (!spoken)
                throw new ArgumentException("Kokoro requires pronounceable text.", nameof(text));
            if (current.Count > 0)
                segments.Add(current.ToArray());
            return segments.Where(part => part.Any(id => id != Token(' '))).ToArray();
        }
    }
}
