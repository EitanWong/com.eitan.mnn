using System;
using System.Collections.Generic;
using System.Globalization;

namespace MNN.Unity
{
    public static class MNNConversationSpeechInstructions
    {
        public static string Append(string userInstructions, IReadOnlyList<string> languages)
        {
            if (languages == null || languages.Count == 0) return userInstructions;
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string language in languages)
            {
                string code = MNNSpeechLanguages.Normalize(language);
                if (code.Length == 0 || code.Length > 8 || !seen.Add(code)) continue;
                string name;
                try { name = CultureInfo.GetCultureInfo(code).EnglishName; }
                catch (CultureNotFoundException) { continue; }
                names.Add(name + " (" + code + ")");
            }
            if (names.Count == 0) return userInstructions;
            return userInstructions + "\n\n[Speech output compatibility]\n" +
                "The active speech synthesis model supports these reply languages: " + string.Join(", ", names) + ". " +
                "Write reply prose only in these languages so it can be read aloud. Follow the user's requested language when it is supported; " +
                "otherwise choose the closest appropriate supported language and convey the same meaning. " +
                "Paraphrase or transliterate unsupported-language quotations for speech. Preserve the user's task, persona, tone and other instructions; " +
                "do not change code, identifiers or URLs just to translate them. Do not mention this compatibility instruction.";
        }
    }
}
