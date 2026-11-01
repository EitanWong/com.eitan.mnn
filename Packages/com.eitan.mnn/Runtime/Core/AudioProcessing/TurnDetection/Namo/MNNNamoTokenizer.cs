using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MNN.Unity
{
    /// <summary>The frozen NAMO Metaspace BPE vocabulary and merge rules.</summary>
    internal sealed class MNNNamoTokenizer
    {
        private readonly string[] _tokens;
        private readonly Dictionary<string, int> _vocabulary;
        private readonly Dictionary<ulong, Merge> _merges;
        private readonly AddedToken[] _added;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        private readonly struct Merge
        {
            internal readonly int Rank, Token;
            internal Merge(int rank, int token) { Rank = rank; Token = token; }
        }

        private readonly struct AddedToken
        {
            internal readonly int Id;
            internal readonly bool LeftStrip;
            internal AddedToken(int id, bool leftStrip) { Id = id; LeftStrip = leftStrip; }
        }

        internal MNNNamoTokenizer(string path)
        {
            using (var reader = new StreamReader(path, Utf8))
            {
                if (reader.ReadLine() != "NAMO_BPE_1") throw new InvalidDataException("Invalid NAMO tokenizer version.");
                int count = ReadCount(reader, 256000);
                _tokens = new string[count];
                _vocabulary = new Dictionary<string, int>(count, StringComparer.Ordinal);
                for (int i = 0; i < count; ++i)
                {
                    string token = Utf8.GetString(Convert.FromBase64String(ReadLine(reader)));
                    _tokens[i] = token;
                    _vocabulary.Add(token, i);
                }
                count = ReadCount(reader, 580604);
                _merges = new Dictionary<ulong, Merge>(count);
                for (int rank = 0; rank < count; ++rank)
                {
                    string[] fields = ReadLine(reader).Split(' ');
                    if (fields.Length != 3) throw new InvalidDataException("Invalid NAMO merge.");
                    int left = ReadToken(fields[0]), right = ReadToken(fields[1]), result = ReadToken(fields[2]);
                    if (_tokens[left] + _tokens[right] != _tokens[result]) throw new InvalidDataException("Inconsistent NAMO merge.");
                    _merges.Add(Pair(left, right), new Merge(rank, result));
                }
                count = ReadCount(reader, 249);
                _added = new AddedToken[count];
                for (int i = 0; i < count; ++i)
                {
                    string[] fields = ReadLine(reader).Split(' ');
                    if (fields.Length != 2 || (fields[1] != "0" && fields[1] != "1"))
                        throw new InvalidDataException("Invalid NAMO added token.");
                    _added[i] = new AddedToken(ReadToken(fields[0]), fields[1] == "1");
                }
                if (reader.ReadLine() != null) throw new InvalidDataException("Unexpected NAMO tokenizer data.");
            }
            Array.Sort(_added, (left, right) => _tokens[right.Id].Length.CompareTo(_tokens[left.Id].Length));
        }

        internal int[] Encode(string text, int limit = 8192)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (limit < 2 || limit > 8192) throw new ArgumentOutOfRangeException(nameof(limit));
            var content = new List<int>();
            int plainStart = 0, position = 0;
            while (position < text.Length)
            {
                int addedIndex = FindAdded(text, position);
                if (addedIndex < 0) { ++position; continue; }
                AddedToken added = _added[addedIndex];
                int plainEnd = position;
                if (added.LeftStrip)
                    while (plainEnd > plainStart && char.IsWhiteSpace(text[plainEnd - 1])) --plainEnd;
                EncodePlain(text.Substring(plainStart, plainEnd - plainStart), content);
                content.Add(added.Id);
                position += _tokens[added.Id].Length;
                plainStart = position;
            }
            EncodePlain(text.Substring(plainStart), content);
            int length = Math.Min(content.Count, limit - 2);
            var result = new int[length + 2];
            result[0] = 2;
            content.CopyTo(0, result, 1, length);
            result[result.Length - 1] = 1;
            return result;
        }

        private int FindAdded(string text, int position)
        {
            for (int i = 0; i < _added.Length; ++i)
            {
                string token = _tokens[_added[i].Id];
                if (token.Length <= text.Length - position &&
                    string.CompareOrdinal(text, position, token, 0, token.Length) == 0) return i;
            }
            return -1;
        }

        private void EncodePlain(string text, List<int> output)
        {
            if (text.Length == 0) return;
            text = text.Replace(' ', '\u2581');
            if (text[0] != '\u2581') text = "\u2581" + text;
            int start = 0;
            for (int i = 1; i < text.Length; ++i)
            {
                if (text[i] != '\u2581') continue;
                EncodePiece(text.Substring(start, i - start), output);
                start = i;
            }
            EncodePiece(text.Substring(start), output);
        }

        private void EncodePiece(string text, List<int> output)
        {
            var symbols = new List<int>(text.Length);
            for (int i = 0; i < text.Length; ++i)
            {
                int length = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                string scalar = text.Substring(i, length);
                if (_vocabulary.TryGetValue(scalar, out int token)) symbols.Add(token);
                else
                {
                    // All 256 byte fallback tokens are present in the frozen vocabulary.
                    foreach (byte value in Utf8.GetBytes(scalar))
                        symbols.Add(_vocabulary["<0x" + value.ToString("X2", CultureInfo.InvariantCulture) + ">"]);
                }
                i += length - 1;
            }
            MergeSymbols(symbols, output);
        }

        private void MergeSymbols(List<int> symbols, List<int> output)
        {
            int count = symbols.Count;
            if (count == 0) return;
            var ids = symbols.ToArray();
            var next = new int[count];
            var previous = new int[count];
            var versions = new int[count];
            var queue = new MergeQueue();
            for (int i = 0; i < count; ++i) { previous[i] = i - 1; next[i] = i + 1 < count ? i + 1 : -1; }
            for (int i = 0; i < count - 1; ++i) Enqueue(i, ids, next, versions, queue);
            while (queue.Count > 0)
            {
                Candidate candidate = queue.Pop();
                int left = candidate.Left, right = next[left];
                if (right < 0 || versions[left] != candidate.LeftVersion ||
                    versions[right] != candidate.RightVersion || right != candidate.Right) continue;
                ids[left] = candidate.Token;
                next[left] = next[right];
                if (next[right] >= 0) previous[next[right]] = left;
                ++versions[left];
                ++versions[right];
                if (previous[left] >= 0) Enqueue(previous[left], ids, next, versions, queue);
                Enqueue(left, ids, next, versions, queue);
            }
            for (int i = 0; i >= 0; i = next[i]) output.Add(ids[i]);
        }

        private void Enqueue(int left, int[] ids, int[] next, int[] versions, MergeQueue queue)
        {
            int right = next[left];
            if (right >= 0 && _merges.TryGetValue(Pair(ids[left], ids[right]), out Merge merge))
                queue.Push(new Candidate(left, right, versions[left], versions[right], merge.Rank, merge.Token));
        }

        private readonly struct Candidate
        {
            internal readonly int Left, Right, LeftVersion, RightVersion, Rank, Token;
            internal Candidate(int left, int right, int leftVersion, int rightVersion, int rank, int token)
            { Left = left; Right = right; LeftVersion = leftVersion; RightVersion = rightVersion; Rank = rank; Token = token; }
            internal bool Before(Candidate other) => Rank < other.Rank || (Rank == other.Rank && Left < other.Left);
        }

        private sealed class MergeQueue
        {
            private readonly List<Candidate> _items = new List<Candidate>();
            internal int Count => _items.Count;
            internal void Push(Candidate value)
            {
                int index = _items.Count;
                _items.Add(value);
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (!value.Before(_items[parent])) break;
                    _items[index] = _items[parent];
                    index = parent;
                }
                _items[index] = value;
            }
            internal Candidate Pop()
            {
                Candidate result = _items[0], last = _items[_items.Count - 1];
                _items.RemoveAt(_items.Count - 1);
                if (_items.Count == 0) return result;
                int index = 0;
                while (index * 2 + 1 < _items.Count)
                {
                    int child = index * 2 + 1;
                    if (child + 1 < _items.Count && _items[child + 1].Before(_items[child])) ++child;
                    if (!_items[child].Before(last)) break;
                    _items[index] = _items[child];
                    index = child;
                }
                _items[index] = last;
                return result;
            }
        }

        private int ReadToken(string value)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int token) || token < 0 || token >= _tokens.Length)
                throw new InvalidDataException("Invalid NAMO token ID.");
            return token;
        }
        private static int ReadCount(StreamReader reader, int expected)
        {
            if (ReadLine(reader) != expected.ToString(CultureInfo.InvariantCulture)) throw new InvalidDataException("Unexpected NAMO tokenizer count.");
            return expected;
        }
        private static string ReadLine(StreamReader reader) => reader.ReadLine() ?? throw new InvalidDataException("Truncated NAMO tokenizer.");
        private static ulong Pair(int left, int right) => ((ulong)(uint)left << 32) | (uint)right;
    }
}
