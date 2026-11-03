using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace MNN.Unity
{
    /// <summary>Offline ordinary-text preparation for the supported Chinese Wenetspeech KWS model.
    /// Does not load native libraries, write files, download assets or require Python.</summary>
    public static partial class MNNKeywordText
    {
        public const string LanguageHint = "This wake model supports Chinese. Enter Chinese phrases, one per line. English or mixed-language wake words require a compatible model.";
        private static readonly Lazy<Lexicon> Pronunciations = new Lazy<Lexicon>(ReadLexicon);

        private sealed class Lexicon
        {
            internal readonly Dictionary<string, string> Entries = new Dictionary<string, string>(StringComparer.Ordinal);
            internal int MaximumLength;
        }

        /// <summary>Plain display text for a saved configuration. Editors can retain the original
        /// configuration until this displayed text changes, preserving legacy pronunciation overrides.</summary>
        public static string GetDisplayText(string wakeWords) => string.Join("\n", (wakeWords ?? "").Split('\n')
            .Select(line => line.IndexOf('@') < 0 ? line.Trim() : line.Substring(line.IndexOf('@') + 1).Trim()));

        /// <summary>Prepare ordinary Chinese phrases, one per line, with model vocabulary and language checks.
        /// Legacy token lines with @labels remain accepted for saved configurations.</summary>
        public static string Prepare(string wakeWords, MNNKeywordLanguage language = MNNKeywordLanguage.Automatic)
        {
            ValidateInput(wakeWords, language);
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in wakeWords.Split('\n').Select(source => source.Trim().Normalize()).Where(source => source.Length > 0))
            {
                // Keep previous serialized model specifications working without asking users to migrate them.
                string prepared = line.IndexOf('@') >= 0 ? line : PreparePhrase(line);
                if (!seen.Add(TokenSequence(prepared))) throw new ArgumentException("Wake words have duplicate pronunciations. Keep one of each phrase.");
                if (seen.Count > 128) throw new ArgumentException("Enter at most 128 wake words.");
                result.Add(prepared);
            }
            string specification = string.Join("\n", result);
            if (specification.Length > 32768) throw new ArgumentException("The wake words are too long. Use fewer or shorter phrases.");
            if (wakeWords.IndexOf('@') >= 0) ValidateLegacy(specification);
            return specification;
        }

        private static void ValidateInput(string wakeWords, MNNKeywordLanguage language)
        {
            if (!Enum.IsDefined(typeof(MNNKeywordLanguage), language)) throw new ArgumentOutOfRangeException(nameof(language));
            if (language == MNNKeywordLanguage.English) throw new ArgumentException(LanguageHint);
            if (string.IsNullOrWhiteSpace(wakeWords) || wakeWords.Length > 32768 || wakeWords.IndexOf('\0') >= 0)
                throw new ArgumentException("Enter wake words, one phrase per line (up to 32,768 characters).");
        }

        private static string TokenSequence(string specification) => string.Join(" ",
            specification.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !part.StartsWith("@", StringComparison.Ordinal) && !part.StartsWith(":", StringComparison.Ordinal) && !part.StartsWith("#", StringComparison.Ordinal)));

        private static void ValidateLegacy(string specification)
        {
            // Construction validates the saved format without evaluating either inference callback.
            try { _ = new MNNKeywordDecoder(specification, ModelTokens, .25f, 1, 1, 4, null, null); }
            catch (ArgumentException) { throw new ArgumentException("Saved wake-word settings are invalid. Enter the wake words again."); }
        }

        private static string PreparePhrase(string phrase)
        {
            if (phrase.Any(char.IsWhiteSpace)) throw new ArgumentException(LanguageHint);
            var lexicon = Pronunciations.Value;
            var parts = new List<string>();
            for (int offset = 0; offset < phrase.Length;)
            {
                int length = Math.Min(lexicon.MaximumLength, phrase.Length - offset);
                string value = null;
                while (length > 0 && !lexicon.Entries.TryGetValue(phrase.Substring(offset, length), out value)) --length;
                if (length == 0)
                    throw new ArgumentException("Use Chinese characters with a supported Mandarin pronunciation. English, numbers and punctuation are not supported by this wake model.");
                parts.AddRange(value.Split(' '));
                offset += length;
            }
            if (parts.Count < 2 || parts.Count > 64) throw new ArgumentException("Choose a wake phrase with 2–64 speech sounds (usually 2–4 Chinese characters).");
            return string.Join(" ", parts) + " @" + phrase;
        }

        private static Lexicon ReadLexicon()
        {
            var lexicon = new Lexicon();
            using (var input = new MemoryStream(Convert.FromBase64String(PronunciationData())))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int split = line.IndexOf('\t');
                    string word = line.Substring(0, split);
                    lexicon.Entries.Add(word, line.Substring(split + 1));
                    lexicon.MaximumLength = Math.Max(lexicon.MaximumLength, word.Length);
                }
            }
            return lexicon;
        }
    }
}
