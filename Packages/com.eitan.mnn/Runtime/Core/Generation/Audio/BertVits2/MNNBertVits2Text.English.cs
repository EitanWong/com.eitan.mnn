using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    internal sealed partial class MNNBertVits2Text
    {
        private Dictionary<string, string[]> _englishDictionary;
        private static readonly string[] LetterPhones =
        {
            "EY1", "B IY1", "S IY1", "D IY1", "IY1", "EH1 F", "JH IY1", "EY1 CH", "AY1",
            "JH EY1", "K EY1", "EH1 L", "EH1 M", "EH1 N", "OW1", "P IY1", "K Y UW1",
            "AA1 R", "EH1 S", "T IY1", "Y UW1", "V IY1", "D AH1 B AH1 L Y UW1", "EH1 K S", "W AY1", "Z IY1"
        };

        internal Encoded[] EncodeSegments(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 300)
                throw new ArgumentException("Enter 1–300 characters for Bert-VITS2.", nameof(text));
            text = text.Normalize(System.Text.NormalizationForm.FormKC).Replace('’', '\'');
            bool IsEnglish(char c) => c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';
            bool IsChinese(char c) => c >= '\u3400' && c <= '\u9fff';
            bool english = text.Where(c => IsEnglish(c) || IsChinese(c)).Select(IsEnglish).FirstOrDefault();
            var result = new List<Encoded>();
            int start = 0;
            for (int i = 0; i < text.Length; ++i)
            {
                if (!IsEnglish(text[i]) && !IsChinese(text[i]))
                    continue;
                bool next = IsEnglish(text[i]);
                if (next == english)
                    continue;
                string segment = text.Substring(start, i - start).Trim();
                if (segment.Length > 0)
                    result.Add(english ? EncodeEnglish(segment) : Encode(segment));
                start = i;
                english = next;
            }

            string last = text.Substring(start).Trim();
            if (last.Length > 0)
                result.Add(english ? EncodeEnglish(last) : Encode(last));
            return result.ToArray();
        }

        private void LoadEnglishDictionary()
        {
            if (_englishDictionary != null)
                return;
            var dictionary = new Dictionary<string, string[]>(StringComparer.Ordinal);
            using (var reader = Reader(_root, "eng_dict.bin"))
            {
                for (int i = 0, n = Count(reader); i < n; ++i)
                {
                    string word = Text(reader);
                    var phones = new List<string>();
                    for (int syllable = 0, count = Count(reader); syllable < count; ++syllable)
                        for (int phone = 0, size = Count(reader); phone < size; ++phone)
                            phones.Add(Text(reader));
                    dictionary.Add(word, phones.ToArray());
                }
            }

            _englishDictionary = dictionary;
        }

        private Encoded EncodeEnglish(string text)
        {
            LoadEnglishDictionary();
            text = Regex.Replace(text, @"[0-9]{1,3}(?:,[0-9]{3})+", match => match.Value.Replace(",", ""));
            text = Regex.Replace(text, @"[0-9]+(?:\.[0-9]+)?", match => EnglishNumber(match.Value));
            text = text.Replace('，', ',').Replace('。', '.').Replace('！', '!').Replace('？', '?')
                .Replace(':', ',').Replace(';', ',').Replace('：', ',').Replace('；', ',');
            var words = Regex.Matches(text, @"[A-Za-z]+(?:'[A-Za-z]+)*|[!?…,\.""\-]").Cast<Match>().ToArray();
            // Reject unsupported characters instead of silently dropping user text.
            if (Regex.Replace(text, @"[A-Za-z\s'!?…,\.""\-]", "").Length > 0 || words.Length == 0)
                throw new NotSupportedException("Unsupported character in Bert-VITS2 English text: " + text);
            var phones = new List<int>{0};
            var tones = new List<int>{8};
            var languages = new List<int>{2};
            var alignment = new List<int>{1};
            foreach (var match in words)
            {
                string word = match.Value;
                bool letter = char.IsLetter(word[0]);
                int before = phones.Count;
                if (!letter)
                {
                    phones.Add(Symbols[word]);
                    tones.Add(8);
                    languages.Add(2);
                }
                else
                {
                    string upper = word.ToUpperInvariant();
                    if (!_englishDictionary.TryGetValue(upper, out var pronunciation))
                        pronunciation = upper.Where(c => c >= 'A' && c <= 'Z').SelectMany(c => LetterPhones[c - 'A'].Split(' ')).ToArray();
                    foreach (string phone in pronunciation)
                    {
                        bool stress = char.IsDigit(phone[phone.Length - 1]);
                        string symbol = (stress ? phone.Substring(0, phone.Length - 1) : phone).ToLowerInvariant();
                        if (symbol == "v")
                            symbol = "V";
                        if (!Symbols.TryGetValue(symbol, out int phoneId))
                            throw new InvalidDataException("Unsupported English phoneme: " + phone);
                        phones.Add(phoneId);
                        tones.Add(8 + (stress ? phone[phone.Length - 1] - '0' + 1 : 0));
                        languages.Add(2);
                    }
                }

                alignment.Add(phones.Count - before);
            }

            phones.Add(0);
            tones.Add(8);
            languages.Add(2);
            alignment.Add(1);
            var repeats = alignment.Select(n => n * 2).ToArray();
            ++repeats[0];
            return new Encoded{English = true, Normalized = text, Phones = Intersperse(phones), Tones = Intersperse(tones), Languages = Intersperse(languages), Tokens = Array.Empty<int>(), Word2Phone = repeats};
        }

        private static string EnglishNumber(string value)
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

            var parts = value.Split('.');
            string result = parts[0].Length > 9 || parts[0].Length > 1 && parts[0][0] == '0'
                ? string.Join(" ", parts[0].Select(c => small[c - '0']))
                : Integer(int.Parse(parts[0], CultureInfo.InvariantCulture));
            return result + (parts.Length == 1 ? "" : " point " + string.Join(" ", parts[1].Select(c => small[c - '0'])));
        }
    }
}
