using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    // Reads official serialized lexicons, not native C++ object representations.
    // Phrase pronunciation, common sandhi and numeric verbalization.
    internal sealed partial class MNNBertVits2Text
    {
        private static readonly Dictionary<string, int> Symbols = ("_ AA E EE En N OO V a a: aa ae ah ai an ang ao aw ay b by c ch d dh dy e e: eh ei en eng er ey f g gy h hh hy i i0 i: ia ian iang iao ie ih in ing iong ir iu iy j jh k ky l m my n ng ny o o: ong ou ow oy p py q r ry s sh t th ts ty u u: ua uai uan uang uh ui un uo uw v van ve vn w x y z zh zy ! ? … , . \" - SP UNK").Split(' ').Select((s, i) => new
        {
        s, i
        }).ToDictionary(v => v.s, v => v.i, StringComparer.Ordinal);
        private readonly Dictionary<string, string> _pinyin = new Dictionary<string, string>();
        private readonly Dictionary<string, string[]> _phrases = new Dictionary<string, string[]>();
        private readonly Dictionary<string, int> _tokens = new Dictionary<string, int>();
        private readonly Dictionary<string, object> _phoneMap;
        private readonly HashSet<string> _neutral;
        private readonly int _maximumPhrase;
        private readonly string _root;
        internal MNNBertVits2Text(string root)
        {
            _root = root;
            using (var r = Reader(root, "pinyin_dict.bin"))
                for (int i = 0, n = Count(r); i < n; ++i)
                {
                    string key = Text(r);
                    _pinyin.Add(key, Text(r));
                }

            using (var r = Reader(root, "phrases_dict.bin"))
                for (int i = 0, n = Count(r); i < n; ++i)
                {
                    string key = Text(r);
                    var value = new string[Count(r)];
                    for (int j = 0; j < value.Length; ++j)
                        value[j] = Text(r);
                    _phrases.Add(key, value);
                }

            using (var r = Reader(root, "cn_bert_token.bin"))
                for (int i = 0, n = Count(r); i < n; ++i)
                {
                    string key = Text(r);
                    _tokens.Add(key, r.ReadInt32());
                }

            using (var r = Reader(root, "pinyin_to_symbol_map.bin"))
                _phoneMap = MNNModelData.Object(MNNModelData.Parse(Text(r)));
            foreach (var item in MNNModelData.Array(MNNModelData.Read(Path.Combine(root, "hotwords_cn.json"))))
            {
                string word = (string)MNNModelData.Get(item, "hotword");
                _phrases[word] = MNNModelData.Array(MNNModelData.Get(item, "pinyin")).Cast<string>().ToArray();
            }

            _neutral = new HashSet<string>(MNNModelData.Array(MNNModelData.Get(MNNModelData.Read(Path.Combine(root, "default_tone_words.json")), "must_neural_tone_words")).Cast<string>());
            _maximumPhrase = _phrases.Keys.Max(k => k.Length);
        }

        private static BinaryReader Reader(string root, string file) => new BinaryReader(File.OpenRead(Path.Combine(root, file)), Encoding.UTF8);
        private static int Count(BinaryReader r)
        {
            ulong n = r.ReadUInt64();
            if (n > 1000000)
                throw new InvalidDataException("Invalid Bert-VITS2 lexicon length.");
            return (int)n;
        }

        private static string Text(BinaryReader r)
        {
            int n = Count(r);
            var bytes = r.ReadBytes(n);
            if (bytes.Length != n)
                throw new EndOfStreamException("Truncated Bert-VITS2 lexicon.");
            return new UTF8Encoding(false, true).GetString(bytes);
        }

        internal sealed class Encoded
        {
            internal string Normalized;
            internal int[] Tokens, Phones, Tones, Word2Phone, Languages;
            internal bool English;
        }

        internal Encoded Encode(string text)
        {
            string normalized = Normalize(text);
            var phones = new List<int>{0};
            var tones = new List<int>{0};
            var alignment = new List<int>{1};
            var tokens = new List<int>{101};
            for (int offset = 0; offset < normalized.Length;)
            {
                int size = 1;
                string[] syllables = null;
                for (int n = Math.Min(_maximumPhrase, normalized.Length - offset); n > 1; --n)
                    if (_phrases.TryGetValue(normalized.Substring(offset, n), out syllables))
                    {
                        size = n;
                        break;
                    }

                string word = normalized.Substring(offset, size);
                if (syllables == null)
                {
                    if (_pinyin.TryGetValue(((int)word[0]).ToString(CultureInfo.InvariantCulture), out string pinyin))
                        syllables = new[]{pinyin.Split(',')[0]};
                    else if (Symbols.ContainsKey(word))
                        syllables = new[]{word};
                    else
                        throw new NotSupportedException("Unsupported character in Chinese Bert-VITS2 segment: " + word + ".");
                }

                if (syllables.Length != size)
                    throw new InvalidDataException("Phrase/text alignment mismatch: " + word);
                var numeric = syllables.Select(NumberedPinyin).ToArray();
                Sandhi(word, numeric);
                for (int i = 0; i < size; ++i)
                {
                    string key = normalized[offset + i].ToString();
                    tokens.Add(_tokens.TryGetValue(key, out int token) ? token : 100);
                    string syllable = numeric[i];
                    int tone = syllable.Length > 1 && char.IsDigit(syllable[syllable.Length - 1]) ? syllable[syllable.Length - 1] - '0' : 0;
                    string[] symbols;
                    if (tone == 0)
                        symbols = new[]{syllable};
                    else
                    {
                        string py = syllable.Substring(0, syllable.Length - 1);
                        if (!_phoneMap.TryGetValue(py, out var symbol))
                            throw new NotSupportedException("Unsupported Chinese syllable: " + py);
                        symbols = ((string)symbol).Split(' ');
                    }

                    foreach (string symbol in symbols)
                    {
                        phones.Add(Symbols[symbol]);
                        tones.Add(tone);
                    }

                    alignment.Add(symbols.Length);
                }

                offset += size;
            }

            phones.Add(0);
            tones.Add(0);
            alignment.Add(1);
            tokens.Add(102);
            var word2ph = alignment.Select(v => v * 2).ToArray();
            ++word2ph[0];
            var result = new Encoded{Normalized = normalized, Tokens = tokens.ToArray(), Phones = Intersperse(phones), Tones = Intersperse(tones), Word2Phone = word2ph};
            result.Languages = new int[result.Phones.Length];
            return result;
        }

        private static int[] Intersperse(List<int> source)
        {
            var result = new int[source.Count * 2 + 1];
            for (int i = 0; i < source.Count; ++i)
                result[i * 2 + 1] = source[i];
            return result;
        }

        internal static string NumberedPinyin(string source)
        {
            if (source.Length == 1 && "!?…,.\"-".Contains(source))
                return source;
            var output = new StringBuilder();
            int tone = 5;
            foreach (char c in source.Normalize(NormalizationForm.FormD))
            {
                if (c == '\u0304')
                    tone = 1;
                else if (c == '\u0301')
                    tone = 2;
                else if (c == '\u030c')
                    tone = 3;
                else if (c == '\u0300')
                    tone = 4;
                else if (c == '\u0308')
                {
                    if (output.Length > 0 && output[output.Length - 1] == 'u')
                        output[output.Length - 1] = 'v';
                }
                else if (c >= '1' && c <= '5')
                    tone = c - '0';
                else
                    output.Append(c);
            }

            return output.ToString() + tone;
        }

        private void Sandhi(string word, string[] pinyin)
        {
            void Set(int i, char tone)
            {
                if (pinyin[i].Length > 1 && char.IsDigit(pinyin[i].Last()))
                    pinyin[i] = pinyin[i].Substring(0, pinyin[i].Length - 1) + tone;
            }

            for (int i = 0; i + 1 < pinyin.Length; ++i)
            {
                if (word[i] == '不' && pinyin[i + 1].EndsWith("4"))
                    Set(i, '2');
                if (word[i] == '一' && !(i > 0 && word[i - 1] == '第') && !word.All(c => "零一二三四五六七八九十百千万亿".Contains(c)))
                    Set(i, pinyin[i + 1].EndsWith("4") ? '2' : '4');
                if (pinyin[i].EndsWith("3") && pinyin[i + 1].EndsWith("3"))
                    Set(i, '2');
            }

            if (_neutral.Contains(word))
                Set(pinyin.Length - 1, '5');
        }

        internal static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 300)
                throw new ArgumentException("Enter 1–300 Chinese characters for Bert-VITS2.", nameof(text));
            if (Regex.IsMatch(text, @"[A-Za-z]"))
                throw new NotSupportedException("Split Chinese and English segments before Chinese normalization.");
            text = Regex.Replace(text.Normalize(NormalizationForm.FormC), @"[0-9]+(?:\.[0-9]+)?", m => MNNMandarinNumber.ToWords(m.Value));
            var result = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                    continue;
                int i = "，。！？：；、“”‘’（）《》【】—～".IndexOf(c);
                char mapped = i < 0 ? c : ",.!?,,,\"\"\"\"\"\"\"\"\"\"--"[i];
                result.Append(mapped);
            }

            if (result.Length == 0 || result.Length > 500)
                throw new ArgumentException("Invalid normalized Chinese text.");
            return result.ToString();
        }


    }
}
