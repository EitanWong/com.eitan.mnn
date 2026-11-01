using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MNN.Unity
{
    // The pinned Pocket tokenizer is SentencePiece UNIGRAM with an identity
    // normalizer, preserved spaces, a dummy prefix and byte fallback.
    internal sealed class MNNPocketText
    {
        private readonly Dictionary<string, int> _ids = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _scores = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly int[] _bytes = Enumerable.Repeat(-1, 256).ToArray();
        private float _unknownScore = float.PositiveInfinity;
        private int _maxLength;

        internal MNNPocketText(string path)
        {
            int id = 0, modelType = 1;
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    int tag = Varint(reader);
                    if (tag != 10 && tag != 18 && tag != 26) { Skip(reader, tag); continue; }
                    using (var item = Message(reader))
                    {
                        string piece = null; float score = 0; int type = 1;
                        string normalizer = ""; bool dummy = true, remove = true, escape = true;
                        while (item.BaseStream.Position < item.BaseStream.Length)
                        {
                            int field = Varint(item);
                            if (tag == 10 && field == 10) piece = Text(item);
                            else if (tag == 10 && field == 21) score = item.ReadSingle();
                            else if (tag == 10 && field == 24) type = Varint(item);
                            else if (tag == 18 && field == 24) modelType = Varint(item);
                            else if (tag == 26 && field == 10) normalizer = Text(item);
                            else if (tag == 26 && field == 18)
                            {
                                if (Varint(item) != 0) throw new InvalidDataException("Pocket expects identity normalization.");
                            }
                            else if (tag == 26 && field == 24) dummy = Varint(item) != 0;
                            else if (tag == 26 && field == 32) remove = Varint(item) != 0;
                            else if (tag == 26 && field == 40) escape = Varint(item) != 0;
                            else Skip(item, field);
                        }
                        if (tag == 26 && (normalizer != "identity" || !dummy || remove || !escape))
                            throw new InvalidDataException("Unsupported Pocket tokenizer normalization.");
                        if (tag != 10) continue;
                        if (string.IsNullOrEmpty(piece) || float.IsNaN(score) || float.IsInfinity(score))
                            throw new InvalidDataException("Invalid Pocket token.");
                        if (type == 1)
                        {
                            _ids.Add(piece, id); _scores.Add(piece, score);
                            _maxLength = Math.Max(_maxLength, piece.Length); _unknownScore = Math.Min(_unknownScore, score);
                        }
                        else if (type == 6)
                        {
                            if (piece.Length != 6 || !piece.StartsWith("<0x", StringComparison.Ordinal) || piece[5] != '>')
                                throw new InvalidDataException("Invalid Pocket byte token.");
                            _bytes[Convert.ToInt32(piece.Substring(3, 2), 16)] = id;
                        }
                        else if (type != 2 && type != 3) throw new InvalidDataException("Unsupported Pocket token type.");
                        ++id;
                    }
                }
            }
            if (id != 4000 || modelType != 1 || _bytes.Any(value => value < 0) || _ids.Count == 0)
                throw new InvalidDataException("Expected the Pocket 4000-token SentencePiece unigram model.");
            _unknownScore -= 10;
        }

        internal static string Prepare(string text)
        {
            MNNSpeechDsp.ValidateText(text, 500);
            text = text.Trim().Replace('\n', ' ').Replace('\r', ' ').Replace("  ", " ");
            text = char.ToUpperInvariant(text[0]) + text.Substring(1);
            if (char.IsLetterOrDigit(text[text.Length - 1])) text += ".";
            return text;
        }

        internal int[] Encode(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 501) throw new ArgumentException("Supply 1–500 characters.", nameof(text));
            string normalized = "▁" + text.Replace(' ', '▁');
            var scores = Enumerable.Repeat(float.NegativeInfinity, normalized.Length + 1).ToArray();
            var previous = new int[scores.Length]; var tokens = Enumerable.Repeat(-1, scores.Length).ToArray(); scores[0] = 0;
            for (int at = 0; at < normalized.Length; ++at)
            {
                if (float.IsNegativeInfinity(scores[at])) continue;
                int scalarLength = char.IsHighSurrogate(normalized[at]) && at + 1 < normalized.Length && char.IsLowSurrogate(normalized[at + 1]) ? 2 : 1;
                bool hasScalar = false;
                for (int length = scalarLength; length <= _maxLength && at + length <= normalized.Length; ++length)
                {
                    string piece = normalized.Substring(at, length);
                    if (!_ids.TryGetValue(piece, out int id)) continue;
                    if (length == scalarLength) hasScalar = true;
                    float score = scores[at] + _scores[piece];
                    if (score > scores[at + length]) { scores[at + length] = score; previous[at + length] = at; tokens[at + length] = id; }
                }
                if (!hasScalar && scores[at] + _unknownScore > scores[at + scalarLength])
                {
                    scores[at + scalarLength] = scores[at] + _unknownScore;
                    previous[at + scalarLength] = at; tokens[at + scalarLength] = -1;
                }
            }
            var pieces = new List<int[]>();
            for (int end = normalized.Length; end > 0; end = previous[end])
            {
                if (tokens[end] >= 0) pieces.Add(new[]{tokens[end]});
                else pieces.Add(Encoding.UTF8.GetBytes(normalized.Substring(previous[end], end - previous[end])).Select(value => _bytes[value]).ToArray());
            }
            pieces.Reverse(); return pieces.SelectMany(piece => piece).ToArray();
        }

        private static BinaryReader Message(BinaryReader reader)
        {
            int length = Varint(reader);
            if (length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Truncated Pocket tokenizer.");
            return new BinaryReader(new MemoryStream(reader.ReadBytes(length), false));
        }
        private static string Text(BinaryReader reader)
        {
            using (var message = Message(reader)) return new UTF8Encoding(false, true).GetString(message.ReadBytes((int)message.BaseStream.Length));
        }
        private static int Varint(BinaryReader reader)
        {
            uint value = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte part = reader.ReadByte(); value |= (uint)(part & 127) << shift;
                if (part < 128) return checked((int)value);
            }
            throw new InvalidDataException("Invalid Pocket tokenizer varint.");
        }
        private static void Skip(BinaryReader reader, int tag)
        {
            int count;
            switch (tag & 7)
            {
                case 0: Varint(reader); return;
                case 1: count = 8; break;
                case 2: count = Varint(reader); break;
                case 5: count = 4; break;
                default: throw new InvalidDataException("Unsupported Pocket tokenizer field.");
            }
            if (count > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Truncated Pocket tokenizer field.");
            reader.BaseStream.Position += count;
        }
    }
}
