using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    // SentencePiece BPE vocabulary in the publisher's MNN tokenizer format.
    // Normalization is restricted to plain Chinese/English, NFKC and whitespace.
    internal sealed class MNNMossTokenizer
    {
        private readonly Dictionary<string, int> _pieces = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _scores = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _bytes = new Dictionary<string, int>(StringComparer.Ordinal);
        internal MNNMossTokenizer(string path)
        {
            if (path.EndsWith(".model", StringComparison.Ordinal))
            {
                LoadSentencePieceModel(File.ReadAllBytes(path));
                if (_bytes.Count != 256) throw new InvalidDataException("Missing MOSS byte fallback vocabulary.");
                return;
            }
            using (var reader = File.OpenText(path))
            {
                if (reader.ReadLine() != "430 0" || reader.ReadLine() != "0 0 0" || reader.ReadLine() != "" || reader.ReadLine() != "16384")
                    throw new InvalidDataException("Expected MOSS Nano SentencePiece BPE vocabulary.");
                for (int id = 0; id < 16384; ++id)
                {
                    string line = reader.ReadLine();
                    if (line == null) throw new InvalidDataException("Incomplete MOSS vocabulary.");
                    var fields = line.Split(' ');
                    if (fields.Length != 3) throw new InvalidDataException("Invalid MOSS vocabulary entry.");
                    string piece = Encoding.UTF8.GetString(Convert.FromBase64String(fields[0]));
                    int type = int.Parse(fields[2], CultureInfo.InvariantCulture);
                    AddPiece(piece, float.Parse(fields[1], CultureInfo.InvariantCulture), type, id);
                }
            }
            if (_bytes.Count != 256) throw new InvalidDataException("Missing MOSS byte fallback vocabulary.");
        }

        private void AddPiece(string piece, float score, int type, int id)
        {
            if (type == 1) { _pieces.Add(piece, id); _scores.Add(piece, score); }
            else if (type == 6) _bytes.Add(piece, id);
        }

        private void LoadSentencePieceModel(byte[] bytes)
        {
            int id = 0, modelType = 1;
            using (var reader = new BinaryReader(new MemoryStream(bytes, false)))
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                int tag = Varint(reader);
                if (tag != 10 && tag != 18) { Skip(reader, tag); continue; }
                byte[] message = reader.ReadBytes(Varint(reader));
                using (var item = new BinaryReader(new MemoryStream(message, false)))
                {
                    string piece = null; float score = 0; int type = 1;
                    while (item.BaseStream.Position < item.BaseStream.Length)
                    {
                        int field = Varint(item);
                        if (tag == 18 && field == 24) modelType = Varint(item);
                        else if (tag == 10 && field == 10) piece = Encoding.UTF8.GetString(item.ReadBytes(Varint(item)));
                        else if (tag == 10 && field == 21) score = item.ReadSingle();
                        else if (tag == 10 && field == 24) type = Varint(item);
                        else Skip(item, field);
                    }
                    if (tag == 10)
                    {
                        if (piece == null) throw new InvalidDataException("Missing SentencePiece token.");
                        AddPiece(piece, score, type, id++);
                    }
                }
            }
            if (id != 16384 || modelType != 2) throw new InvalidDataException("Expected the MOSS 16384-token SentencePiece BPE model.");
        }

        private static int Varint(BinaryReader reader)
        {
            uint value = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte part = reader.ReadByte(); value |= (uint)(part & 127) << shift;
                if (part < 128) return checked((int)value);
            }
            throw new InvalidDataException("Invalid SentencePiece protobuf varint.");
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
                default: throw new InvalidDataException("Unsupported SentencePiece protobuf field.");
            }
            if (count > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Truncated SentencePiece protobuf.");
            reader.BaseStream.Position += count;
        }

        internal int[] Encode(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 500) throw new ArgumentException("Supply 1–500 characters.", nameof(text));
            if (text.Contains("<|") || text.Any(c => char.IsControl(c) && !char.IsWhiteSpace(c)))
                throw new ArgumentException("Supply plain speech text without special tokens.", nameof(text));
            text = "▁" + Regex.Replace(text.Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ").Replace(" ", "▁");
            var symbols = new List<string>();
            for (int i = 0; i < text.Length; ++i)
            {
                int length = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                symbols.Add(text.Substring(i, length)); i += length - 1;
            }
            while (symbols.Count > 1)
            {
                int at = -1; float best = float.NegativeInfinity;
                for (int i = 0; i + 1 < symbols.Count; ++i)
                    if (_scores.TryGetValue(symbols[i] + symbols[i + 1], out float score) && score > best)
                    { best = score; at = i; }
                if (at < 0) break;
                symbols[at] += symbols[at + 1]; symbols.RemoveAt(at + 1);
            }
            var ids = new List<int>();
            foreach (string piece in symbols)
            {
                if (_pieces.TryGetValue(piece, out int id)) ids.Add(id);
                else foreach (byte value in Encoding.UTF8.GetBytes(piece)) ids.Add(_bytes["<0x" + value.ToString("X2", CultureInfo.InvariantCulture) + ">"]);
            }
            if (ids.Count > 250) throw new ArgumentException("Split MOSS speech text into at most 250 tokens per request.", nameof(text));
            return ids.ToArray();
        }
    }
}
