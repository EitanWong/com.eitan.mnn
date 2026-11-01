using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    internal sealed class MNNTurnSenseTokenizer
    {
        private readonly MNNBytePairTokenizer _bpe;
        private readonly Dictionary<string, int> _added;
        private readonly Regex _special;
        private readonly MNNTurnSensePreTokenizer _preTokenizer;

        internal MNNTurnSenseTokenizer(string path)
        {
            _preTokenizer = new MNNTurnSensePreTokenizer(Path.Combine(Path.GetDirectoryName(path), "tokenizer-unicode.json"));
            var root = MNNModelData.Object(MNNModelData.Read(path));
            var model = MNNModelData.Object(MNNModelData.Get(root, "model"));
            if (!Equals(MNNModelData.Get(model, "type"), "BPE") || MNNModelData.Get(root, "normalizer") != null || MNNModelData.Get(root, "post_processor") != null)
                throw new InvalidDataException("Expected TurnSense BPE with no normalization or postprocessor.");
            var vocabulary = MNNModelData.Object(MNNModelData.Get(model, "vocab")).ToDictionary(p => p.Key, p => MNNModelData.Integer(p.Value), StringComparer.Ordinal);
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in MNNModelData.Array(MNNModelData.Get(model, "merges")))
            {
                var pair = item as List<object>;
                ranks.Add(pair == null ? (string)item : pair[0] + " " + pair[1], ranks.Count);
            }
            _added = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in MNNModelData.Array(MNNModelData.Get(root, "added_tokens")))
            {
                foreach (string rule in new[] { "single_word", "lstrip", "rstrip", "normalized" })
                    if ((bool)MNNModelData.Get(item, rule)) throw new InvalidDataException("Unsupported added token rule.");
                _added.Add((string)MNNModelData.Get(item, "content"), MNNModelData.Integer(MNNModelData.Get(item, "id")));
            }
            _special = new Regex(string.Join("|", _added.Keys.OrderByDescending(s => s.Length).ThenBy(s => s, StringComparer.Ordinal).Select(Regex.Escape)), RegexOptions.CultureInvariant);
            var bytes = new char[256];
            int extra = 256;
            for (int b = 0; b < bytes.Length; ++b) bytes[b] = (char)(b >= 33 && b <= 126 || b >= 161 && b <= 172 || b >= 174 ? b : extra++);
            _bpe = new MNNBytePairTokenizer(vocabulary, ranks, bytes, new Regex(@"[\s\S]+", RegexOptions.CultureInvariant));
        }

        internal int[] Encode(string transcript)
        {
            if (transcript == null) throw new ArgumentNullException(nameof(transcript));
            RequireValidUtf16(transcript);
            string text = "<|user|> " + transcript;
            var ids = new List<int>();
            int start = 0;
            foreach (Match match in _special.Matches(text))
            {
                EncodeOrdinary(text.Substring(start, match.Index - start), ids);
                ids.Add(_added[match.Value]);
                start = match.Index + match.Length;
            }
            EncodeOrdinary(text.Substring(start), ids);
            if (ids.Count > MNNTurnSense.MaximumTokens)
                throw new ArgumentException("TurnSense padded256 accepts at most 256 encoded tokens; the original entry does not truncate.", nameof(transcript));
            return ids.ToArray();
        }

        private void EncodeOrdinary(string text, List<int> ids)
        {
            foreach (string piece in _preTokenizer.Split(text)) ids.AddRange(_bpe.Encode(piece));
        }

        private static void RequireValidUtf16(string text)
        {
            for (int i = 0; i < text.Length; ++i)
            {
                if (char.IsHighSurrogate(text[i]))
                {
                    if (i + 1 >= text.Length || !char.IsLowSurrogate(text[++i]))
                        throw new ArgumentException("Transcript contains an unpaired surrogate.", nameof(text));
                }
                else if (char.IsLowSurrogate(text[i])) throw new ArgumentException("Transcript contains an unpaired surrogate.", nameof(text));
            }
        }
    }
}
