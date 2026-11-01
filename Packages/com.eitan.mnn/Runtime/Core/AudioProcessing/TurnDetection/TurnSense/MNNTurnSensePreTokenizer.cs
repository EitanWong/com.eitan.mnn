using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    // The author's AutoTokenizer entry replaces the serialized Digits/ByteLevel
    // sequence with GPT-2 ByteLevel. Match that effective frontend on scalars.
    internal sealed class MNNTurnSensePreTokenizer
    {
        private static readonly string[] Contractions = { "'s", "'t", "'re", "'ve", "'m", "'ll", "'d" };
        private readonly MNNTurnSenseUnicode _unicode;
        internal MNNTurnSensePreTokenizer(string unicodePath) => _unicode = new MNNTurnSenseUnicode(unicodePath);

        internal IEnumerable<string> Split(string text)
        {
            for (int i = 0; i < text.Length;)
            {
                int end = End(text, i);
                yield return text.Substring(i, end - i);
                i = end;
            }
        }

        private int End(string text, int start)
        {
            foreach (string contraction in Contractions)
                if (start + contraction.Length <= text.Length && string.CompareOrdinal(text, start, contraction, 0, contraction.Length) == 0)
                    return start + contraction.Length;
            int at = start;
            if (text[at] == ' ' && at + 1 < text.Length && !_unicode.IsSpace(text, at + 1)) ++at;
            if (_unicode.IsLetter(text, at))
            {
                do { at += Width(text, at); } while (at < text.Length && _unicode.IsLetter(text, at));
                return at;
            }
            if (_unicode.IsNumber(text, at))
            {
                do { at += Width(text, at); } while (at < text.Length && _unicode.IsNumber(text, at));
                return at;
            }
            if (!_unicode.IsSpace(text, at))
            {
                do { at += Width(text, at); } while (at < text.Length && !_unicode.IsSpace(text, at) && !_unicode.IsLetter(text, at) && !_unicode.IsNumber(text, at));
                return at;
            }
            while (at < text.Length && _unicode.IsSpace(text, at)) at += Width(text, at);
            // \s+(?!\S) leaves the final whitespace for the next pretoken
            // when a run precedes non-whitespace; trailing whitespace stays whole.
            return at < text.Length && at - start > 1 ? at - 1 : at;
        }

        private static int Width(string text, int at) => char.IsHighSurrogate(text[at]) ? 2 : 1;
    }
}
