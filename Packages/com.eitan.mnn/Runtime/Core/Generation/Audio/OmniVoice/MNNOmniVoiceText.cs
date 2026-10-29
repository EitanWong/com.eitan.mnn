using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    internal sealed class MNNOmniVoiceText
    {
        private static readonly Regex Nonverbal = new Regex(@"\[(laughter|sigh|confirmation-en|question-en|question-ah|question-oh|question-ei|question-yi|surprise-ah|surprise-oh|surprise-wa|surprise-yo|dissatisfaction-hnn)\]", RegexOptions.CultureInvariant);
        private static readonly Regex ChineseSpaces = new Regex(@"(?<=[\u4e00-\u9fff])\s+|\s+(?=[\u4e00-\u9fff])", RegexOptions.CultureInvariant);
        private readonly MNNBytePairTokenizer _tokenizer;

        internal MNNOmniVoiceText(string root)
        {
            var json = MNNModelData.Read(Path.Combine(root, "tokenizer.json"));
            var model = MNNModelData.Get(json, "model");
            if (!Equals(MNNModelData.Get(model, "type"), "BPE"))
                throw new InvalidDataException("Expected OmniVoice Qwen2 BPE tokenizer.");
            var vocab = MNNModelData.Object(MNNModelData.Get(model, "vocab"))
                .ToDictionary(pair => pair.Key, pair => MNNModelData.Integer(pair.Value), StringComparer.Ordinal);
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            int rank = 0;
            foreach (var item in MNNModelData.Array(MNNModelData.Get(model, "merges")))
            {
                var pair = item as List<object>;
                ranks.Add(pair == null ? (string)item : pair[0] + " " + pair[1], rank++);
            }
            var added = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in MNNModelData.Array(MNNModelData.Get(json, "added_tokens")))
            {
                if ((bool)MNNModelData.Get(item, "single_word") || (bool)MNNModelData.Get(item, "lstrip") ||
                    (bool)MNNModelData.Get(item, "rstrip"))
                    throw new InvalidDataException("Unsupported OmniVoice added-token boundary rule.");
                added.Add((string)MNNModelData.Get(item, "content"), MNNModelData.Integer(MNNModelData.Get(item, "id")));
            }
            var bytes = new char[256];
            int extra = 256;
            for (int b = 0; b < 256; ++b)
                bytes[b] = (char)(b >= 33 && b <= 126 || b >= 161 && b <= 172 || b >= 174 ? b : extra++);
            var words = new Regex(@"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+", RegexOptions.CultureInvariant);
            _tokenizer = new MNNBytePairTokenizer(vocab, ranks, bytes, words, added: added, normalize: true);
        }

        internal int[] Style(string language, string instruction, bool denoise)
        {
            if (string.IsNullOrWhiteSpace(language)) language = "None";
            if (string.IsNullOrWhiteSpace(instruction)) instruction = "None";
            return _tokenizer.Encode((denoise ? "<|denoise|>" : "") + "<|lang_start|>" + language +
                "<|lang_end|><|instruct_start|>" + instruction + "<|instruct_end|>");
        }

        internal int[] Content(string text, string referenceText)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Speech text is empty.", nameof(text));
            string combined = string.IsNullOrWhiteSpace(referenceText) ? text.Trim() : referenceText.Trim() + " " + text.Trim();
            combined = Regex.Replace(combined, @"[\r\n]+", "").Replace('（', '(').Replace('）', ')');
            combined = ChineseSpaces.Replace(Regex.Replace(combined, @"[ \t]+", " "), "");
            string wrapped = "<|text_start|>" + combined + "<|text_end|>";
            var result = new List<int>();
            int start = 0;
            foreach (Match tag in Nonverbal.Matches(wrapped))
            {
                if (tag.Index > start) result.AddRange(_tokenizer.Encode(wrapped.Substring(start, tag.Index - start)));
                result.AddRange(_tokenizer.Encode(tag.Value));
                start = tag.Index + tag.Length;
            }
            if (start < wrapped.Length) result.AddRange(_tokenizer.Encode(wrapped.Substring(start)));
            if (result.Count > 1024) throw new ArgumentException("OmniVoice text exceeds 1024 tokens.", nameof(text));
            return result.ToArray();
        }
    }
}
