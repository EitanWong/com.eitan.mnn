using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    public sealed partial class MNNSupertonic
    {
        public static string[] LanguagesForVersion(string version) => version == "v1.7.3" ? new[]{"en", "ko", "ja", "ar", "bg", "cs", "da", "de", "el", "es", "et", "fi", "fr", "hi", "hr", "hu", "id", "it", "lt", "lv", "nl", "pl", "pt", "ro", "ru", "sk", "sl", "sv", "tr", "uk", "vi"} : version == "v1.6.0" ? new[]{"en", "ko", "es", "pt", "fr"} : new[]{"en"};
        // Keep attention and duration prediction bounded. Prefer sentence ends,
        // then whitespace; never cut a word or a decimal number in half.
        internal static string[] SplitText(string normalized, bool english = true)
        {
            const int limit = 160;
            var segments = new List<string>();
            string remaining = normalized;
            while (remaining.Length > limit)
            {
                int sentence = -1, space = -1;
                for (int i = 0; i < limit; ++i)
                {
                    if (remaining[i] == ' ')
                        space = i;
                    if (".!?;:".IndexOf(remaining[i]) >= 0 && remaining[i + 1] == ' ')
                        sentence = i + 1;
                }

                int end = sentence > 0 ? sentence : space;
                if (end <= 0)
                    throw new ArgumentException("Supertonic words or unbroken numbers must not exceed 160 characters.");
                string part = remaining.Substring(0, end).Trim();
                if (part.Any(char.IsLetterOrDigit))
                    segments.Add(Normalize(part, english));
                remaining = remaining.Substring(end).TrimStart();
            }

            if (remaining.Any(char.IsLetterOrDigit))
                segments.Add(Normalize(remaining, english));
            return segments.ToArray();
        }

        internal static string Normalize(string text, bool english = true)
        {
            text = Regex.Replace(Regex.Replace(text, @"\s+", " ").Trim(), @"[.!?]+$", ".").Normalize(NormalizationForm.FormKD);
            var result = new StringBuilder();
            const string marks = "\u0302\u0303\u0304\u0305\u0306\u0307\u0308\u030a\u030b\u030c\u0327\u0328\u0329\u032a\u032b\u032c\u032d\u032e\u032f";
            for (int i = 0; i < text.Length; ++i)
            {
                int cp = char.ConvertToUtf32(text, i);
                if (cp > 65535)
                    ++i;
                if (cp <= 65535 && (char.IsControl((char)cp) || cp == 0x200b || cp == 0xfeff))
                {
                    result.Append(' ');
                    continue;
                }

                if (cp <= 65535 && (char.GetUnicodeCategory((char)cp) == System.Globalization.UnicodeCategory.Format || cp >= 0xfe00 && cp <= 0xfe0f))
                    continue;
                if (cp >= 0x1f300 && cp <= 0x1faff || cp >= 0x1f1e6 && cp <= 0x1f1ff || cp >= 0x2600 && cp <= 0x27bf || cp <= 65535 && marks.IndexOf((char)cp) >= 0 || cp == 0xa9 || cp == '\\')
                    continue;
                if (cp == 0x2013 || cp == 0x2011 || cp == 0x2014)
                    result.Append('-');
                else if (cp <= 65535 && "¯_[]|/#→←".IndexOf((char)cp) >= 0)
                    result.Append(' ');
                else if (cp == 0x201c || cp == 0x201d)
                    result.Append('"');
                else if (cp == 0x2018 || cp == 0x2019 || cp == 0xb4 || cp == '`')
                    result.Append('\'');
                else
                    result.Append(char.ConvertFromUtf32(cp));
            }

            string value = result.ToString().Replace("@", " at ").Replace("e.g.,", "for example, ").Replace("i.e.,", "that is, ");
            if (!result.ToString().Any(char.IsLetterOrDigit))
                throw new ArgumentException("No pronounceable text remains after normalization.");
            // Raw long digit runs can consume the alignment and swallow speech
            // that follows, even on CPU. Expand before bounding segment length.
            if (english)
            {
                value = Regex.Replace(value, @"([$£])\s*([0-9]+(?:\.[0-9]+)?)", match => NumberWords(match.Groups[2].Value) + (match.Groups[1].Value == "$" ? " dollars" : " pounds"));
                value = Regex.Replace(value, @"[0-9]+(?:\.[0-9]+)?", match => NumberWords(match.Value));
                value = value.Replace("%", " percent ");
            }

            value = Regex.Replace(value, " ([,.!?;:'])", "$1");
            value = Regex.Replace(value, "([\"'])\\1+", "$1");
            value = Regex.Replace(value, @"\s+", " ").Trim();
            if (".!?;:,'\")]}…。」』】〉》›»".IndexOf(value[value.Length - 1]) < 0)
                value += ".";
            return value;
        }

        private static string NumberWords(string value)
        {
            string[] small = {"zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"};
            string[] tens = {"", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"};
            string Integer(int n)
            {
                if (n < 20)
                    return small[n];
                if (n < 100)
                    return tens[n / 10] + (n % 10 == 0 ? "" : " " + small[n % 10]);
                int divisor = n >= 1000000 ? 1000000 : n >= 1000 ? 1000 : 100;
                return Integer(n / divisor) + (divisor == 1000000 ? " million" : divisor == 1000 ? " thousand" : " hundred") + (n % divisor == 0 ? "" : " " + Integer(n % divisor));
            }

            string[] parts = value.Split('.');
            string number = parts[0].Length > 9 || parts[0].Length > 1 && parts[0][0] == '0' ? string.Join(" ", parts[0].Select(c => small[c - '0'])) : Integer(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture));
            return number + (parts.Length == 1 ? "" : " point " + string.Join(" ", parts[1].Select(c => small[c - '0'])));
        }

        internal static int[] Encode(string text, int[] indexer)
        {
            var ids = new List<int>();
            foreach (char c in text)
            {
                // An ideographic period can follow otherwise supported text (for example "8。").
                // Keep a native vocabulary entry when available; only fold this punctuation to its ASCII equivalent.
                char token = c == '\u3002' && (c >= indexer.Length || indexer[c] < 0) ? '.' : c;
                if (token >= indexer.Length || indexer[token] < 0)
                    throw new NotSupportedException("This Supertonic model has no token for U+" + ((int)c).ToString("X4") + ". Use text supported by this model vocabulary.");
                ids.Add(indexer[token]);
            }

            return ids.ToArray();
        }
    }
}
