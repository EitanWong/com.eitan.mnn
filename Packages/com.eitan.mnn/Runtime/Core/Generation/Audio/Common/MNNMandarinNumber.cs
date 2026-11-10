using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    // Shared cardinal verbalization. Leading-zero identifiers and large values
    // retain every digit instead of being parsed through a bounded integer.
    internal static class MNNMandarinNumber
    {
        internal static string NormalizeKokoro(string text)
        {
            if (string.IsNullOrEmpty(text) || !Regex.IsMatch(text, @"[\u3400-\u9fff]")) return text;
            return Regex.Replace(text,
                @"(?<![A-Za-z0-9０-９])[-+＋－]?(?:[0-9０-９]{1,3}(?:[,，][0-9０-９]{3})+|[0-9０-９]+)(?:[.．][0-9０-９]+)?[%％]?", SpokenMatch);
        }

        private static string SpokenMatch(Match match)
        {
            string value = match.Value.Normalize(NormalizationForm.FormKC).Replace(",", "");
            string sign = value[0] == '-' ? "负" : value[0] == '+' ? "正" : "";
            if (sign.Length > 0) value = value.Substring(1);
            bool percent = value.EndsWith("%", StringComparison.Ordinal);
            if (percent) value = value.Substring(0, value.Length - 1);
            return sign + (percent ? "百分之" : "") + ToWords(value);
        }

        internal static string ToWords(string value)
        {
            const string digits = "零一二三四五六七八九";
            string[] parts = value.Split('.');
            var result = new StringBuilder();
            if (parts[0].Length > 8 || parts[0].Length > 1 && parts[0][0] == '0')
                foreach (char c in parts[0])
                    result.Append(digits[c - '0']);
            else
            {
                int number = int.Parse(parts[0], CultureInfo.InvariantCulture);
                string Group(int n)
                {
                    if (n == 0)
                        return "零";
                    string s = n.ToString(CultureInfo.InvariantCulture);
                    var b = new StringBuilder();
                    bool zero = false;
                    for (int i = 0; i < s.Length; ++i)
                    {
                        int d = s[i] - '0';
                        if (d == 0)
                        {
                            zero = b.Length > 0;
                            continue;
                        }

                        if (zero)
                        {
                            b.Append('零');
                            zero = false;
                        }

                        b.Append(digits[d]);
                        b.Append(" 十百千"[s.Length - i - 1] == ' ' ? "" : " 十百千"[s.Length - i - 1].ToString());
                    }

                    string r = b.ToString();
                    return r.StartsWith("一十") ? r.Substring(1) : r;
                }

                result.Append(number >= 10000 ? Group(number / 10000) + "万" + (number % 10000 == 0 ? "" : (number % 10000 < 1000 ? "零" : "") + Group(number % 10000)) : Group(number));
            }

            if (parts.Length > 1)
            {
                result.Append('点');
                foreach (char c in parts[1])
                    result.Append(digits[c - '0']);
            }

            return result.ToString();
        }
    }
}
