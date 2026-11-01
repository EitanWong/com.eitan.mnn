// Adapted from Nian27/CosyVoice3-MNN (Copyright 2026 Nian27), Apache-2.0.
// Modified for managed execution, validation and cancellation. See LICENSE.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Interop;
using MNN.Unity.Interop.Handles;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    public sealed partial class MNNCosyVoice
    {
        internal static void ValidateText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 200)
                throw new ArgumentException("Enter 1–200 characters for CosyVoice.", nameof(text));
            if (text.Contains("<|") || text.Any(char.IsControl) || !text.Any(char.IsLetterOrDigit))
                throw new ArgumentException("CosyVoice text must contain letters or digits and cannot contain control characters or model special tokens.", nameof(text));
        }

        // Bound each generation independently so EOS in one sentence cannot
        // skip the remaining sentences. Prefer punctuation, then word boundaries;
        // CJK characters have a larger speech budget than Latin characters.
        internal static string[] SplitText(string text)
        {
            var result = new List<string>();
            int start = 0;
            while (start < text.Length)
            {
                while (start < text.Length && char.IsWhiteSpace(text[start]))
                    ++start;
                if (start == text.Length)
                    break;
                int end = start, budget = 0;
                var elements = StringInfo.GetTextElementEnumerator(text, start);
                while (elements.MoveNext())
                {
                    string element = elements.GetTextElement();
                    int cost = IsCjk(element[0]) ? 3 : 1;
                    if (budget + cost > 80)
                        break;
                    budget += cost;
                    end = elements.ElementIndex + element.Length;
                }

                if (end < text.Length)
                {
                    int sentence = -1, clause = -1, space = -1;
                    for (int i = start; i < end; ++i)
                    {
                        char c = text[i];
                        if (char.IsWhiteSpace(c))
                            space = i;
                        // Decimal separators and grouped numbers are not clauses.
                        if (i > start && i + 1 < text.Length && char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
                            continue;
                        if ("!?。！？".IndexOf(c) >= 0 || (c == '.' && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1]))))
                            sentence = i + 1;
                        else if (",;:，；：".IndexOf(c) >= 0)
                            clause = i + 1;
                    }

                    if (sentence > start)
                        end = sentence;
                    else if (clause > start)
                        end = clause;
                    else if (space > start)
                        end = space;
                    else if (!IsCjk(text[end - 1]) && !IsCjk(text[end]))
                    {
                        // Keep an unbroken word intact, even beyond the budget.
                        while (end < text.Length && !char.IsWhiteSpace(text[end]) && !IsCjk(text[end]))
                            ++end;
                    }
                }

                result.Add(text.Substring(start, end - start).Trim());
                start = end;
            }

            return result.ToArray();
        }

        private static bool IsCjk(char c) => c >= '\u3400' && c <= '\u9fff';
        internal static string NormalizeEnding(string text)
        {
            text = text.Trim();
            // Supply the sentence boundary normally added by CosyVoice's text
            // frontend, including when the last visible character is a quote.
            int last = text.Length - 1;
            while (last > 0 && "\"'”’)]}」』".IndexOf(text[last]) >= 0)
                --last;
            if (".!?。！？…".IndexOf(text[last]) >= 0)
                return text;
            bool cjk = text.Any(c => c >= '\u3400' && c <= '\u9fff');
            string ending = cjk ? "。" : ".";
            return ",;:，；：".IndexOf(text[last]) >= 0 ? text.Remove(last, 1).Insert(last, ending) : text.Insert(last + 1, ending);
        }
    }
}
