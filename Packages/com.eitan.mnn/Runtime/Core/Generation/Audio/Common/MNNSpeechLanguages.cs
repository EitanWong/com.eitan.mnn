using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>Conservative script checks, not statistical language identification.</summary>
    public static class MNNSpeechLanguages
    {
        public static bool HasSpeech(string text) => !string.IsNullOrEmpty(text) && text.Where((c, i) => char.IsLetterOrDigit(text, i)).Any();

        public static string Normalize(string language)
        {
            string normalized = (language ?? "").Trim().Replace('_', '-').ToLowerInvariant().Split('-')[0];
            return normalized == "cmn" ? "zh" : normalized;
        }

        /// <summary>Distinct writing systems, retaining all scripts in mixed text.</summary>
        public static string[] Scripts(string text)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (text == null) return Array.Empty<string>();
            for (int i = 0; i < text.Length; ++i)
            {
                int c = char.ConvertToUtf32(text, i);
                bool letter = char.IsLetter(text, i);
                if (c > 0xffff) ++i;
                if (!letter) continue;
                string script = c >= 0x3400 && c <= 0x9fff || c >= 0xf900 && c <= 0xfaff || c >= 0x20000 && c <= 0x323af ? "han" :
                    c >= 0x3040 && c <= 0x30ff || c >= 0xff66 && c <= 0xff9d ? "kana" :
                    c >= 0xac00 && c <= 0xd7af || c >= 0x1100 && c <= 0x11ff || c >= 0x3130 && c <= 0x318f ? "hangul" :
                    c >= 0x400 && c <= 0x52f ? "cyrillic" : c >= 0x600 && c <= 0x8ff ? "arabic" :
                    c >= 0x900 && c <= 0x97f ? "devanagari" : c >= 0x370 && c <= 0x3ff ? "greek" :
                    c <= 0x24f || c >= 0x1e00 && c <= 0x1eff ? "latin" : "other";
                result.Add(script);
            }
            return result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        public static bool SupportsText(string text, params string[] languages)
        {
            var scripts = Scripts(text);
            var supported = new HashSet<string>(languages.Select(Normalize), StringComparer.Ordinal);
            return scripts.All(script =>
                script == "han" ? supported.Contains(scripts.Contains("kana") ? "ja" : "zh") :
                script == "kana" ? supported.Contains("ja") : script == "hangul" ? supported.Contains("ko") :
                script == "cyrillic" ? supported.Overlaps(new[]{"ru", "uk", "bg", "sr", "mk", "be"}) :
                script == "arabic" ? supported.Overlaps(new[]{"ar", "fa", "ur"}) :
                script == "devanagari" ? supported.Overlaps(new[]{"hi", "mr", "ne"}) :
                script == "greek" ? supported.Contains("el") : script == "latin" ? supported.Any(IsLatinLanguage) : false);
        }

        /// <summary>Preserves every character and groups adjacent writing systems; punctuation stays with its preceding run.</summary>
        public static string[] SplitScriptRuns(string text)
        {
            var runs = new List<string>();
            int start = 0;
            string script = null;
            for (int i = 0; i < text.Length; ++i)
            {
                int length = char.IsSurrogatePair(text, i) ? 2 : 1;
                if (char.IsLetter(text, i))
                {
                    string next = Scripts(text.Substring(i, length))[0];
                    if (script != null && next != script)
                    {
                        runs.Add(text.Substring(start, i - start));
                        start = i;
                    }
                    script = next;
                }
                i += length - 1;
            }
            if (start < text.Length) runs.Add(text.Substring(start));
            return runs.ToArray();
        }

        private static bool IsLatinLanguage(string language) => new[]{"en", "de", "es", "fr", "it", "pt", "nl", "pl", "cs", "da", "et", "fi", "hr", "hu", "id", "lt", "lv", "ro", "sk", "sl", "sv", "tr", "vi", "ca", "cy", "is", "no", "sw", "af"}.Contains(language);

        public static string Select(string text, string[] languages, string preferred = null)
        {
            if (!string.IsNullOrWhiteSpace(preferred) && languages.Contains(Normalize(preferred)) && SupportsText(text, preferred))
                return Normalize(preferred);
            var scripts = Scripts(text);
            string inferred = scripts.Contains("kana") ? "ja" : scripts.Contains("han") ? "zh" : scripts.Contains("hangul") ? "ko" :
                scripts.Contains("arabic") ? "ar" : scripts.Contains("cyrillic") ? "ru" : scripts.Contains("devanagari") ? "hi" : scripts.Contains("greek") ? "el" : "en";
            if (languages.Contains(inferred) && SupportsText(text, languages)) return inferred;
            return languages.FirstOrDefault(language => SupportsText(text, language));
        }
    }
}
