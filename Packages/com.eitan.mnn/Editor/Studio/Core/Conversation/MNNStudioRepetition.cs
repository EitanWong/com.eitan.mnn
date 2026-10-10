using System;

namespace MNN.Unity.Editor
{
    // Only stop long, exact, consecutive loops. Intentional short repetitions and
    // code/numeric structures are excluded. This is a guard, not a semantic guarantee.
    internal static class MNNStudioRepetition
    {
        internal static bool TryTrim(string text, out string trimmed)
        {
            trimmed = text;
            if (string.IsNullOrEmpty(text) || text.Length < 144 || text.Contains("```"))
                return false;
            int end = text.Length;
            for (int length = 48; length <= Math.Min(512, end / 3); ++length)
            {
                int start = end - length * 3;
                if (string.CompareOrdinal(text, start, text, start + length, length) != 0 || string.CompareOrdinal(text, start, text, start + 2 * length, length) != 0)
                    continue;
                int letters = 0;
                for (int i = start; i < start + length; ++i)
                    if (char.IsLetter(text[i]))
                        ++letters;
                if (letters < length / 2)
                    continue;
                while (start >= length && string.CompareOrdinal(text, start - length, text, start, length) == 0)
                    start -= length;
                trimmed = text.Substring(0, start + length).TrimEnd();
                return true;
            }

            return false;
        }
    }
}
