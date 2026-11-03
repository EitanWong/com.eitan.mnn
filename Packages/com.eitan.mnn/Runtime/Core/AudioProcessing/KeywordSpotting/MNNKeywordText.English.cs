using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    public static partial class MNNKeywordText
    {
        private static readonly Lazy<Dictionary<string, float>> EnglishScores = new Lazy<Dictionary<string, float>>(() =>
            EnglishTokens.Select((text, id) => new { text, id }).Where(item => item.id > 2)
                .ToDictionary(item => item.text, item => EnglishPieceScores[item.id], StringComparer.Ordinal));

        /// <summary>Find the compatible model language. A phrase must be Chinese or English;
        /// lists with both languages should route each line to its corresponding model.</summary>
        public static MNNKeywordLanguage DetectLanguage(string wakeWords)
        {
            if (string.IsNullOrWhiteSpace(wakeWords)) throw new ArgumentException("Enter at least one wake word.");
            MNNKeywordLanguage found = MNNKeywordLanguage.Automatic;
            foreach (string line in wakeWords.Split('\n').Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                var language = DetectPhraseLanguage(line);
                if (found != MNNKeywordLanguage.Automatic && found != language)
                    throw new ArgumentException("Chinese and English wake words need their corresponding models. Use automatic language routing in the wake-word list.");
                found = language;
            }
            return found;
        }

        private static MNNKeywordLanguage DetectPhraseLanguage(string phrase)
        {
            if (phrase.IndexOf('@') >= 0) return phrase.IndexOf('▁') >= 0 ? MNNKeywordLanguage.English : MNNKeywordLanguage.Chinese;
            bool latin = phrase.Any(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z');
            if (latin && phrase.Any(c => c > 127 && !char.IsWhiteSpace(c)))
                throw new ArgumentException("Use a Chinese phrase or an English phrase in each row. Add different languages as separate wake words.");
            return latin ? MNNKeywordLanguage.English : MNNKeywordLanguage.Chinese;
        }

        private static string PrepareEnglishPhrase(string phrase)
        {
            string normalized = Regex.Replace(phrase.Normalize(NormalizationForm.FormKC).Trim().ToUpperInvariant(), @"\s+", " ");
            if (normalized.Length == 0 || normalized.Any(c => (c < 'A' || c > 'Z') && c != ' ' && c != '\''))
                throw new ArgumentException("The English wake model accepts English words. Write numbers as words and omit punctuation.");
            var symbols = EncodeEnglish("▁" + normalized.Replace(" ", "▁"));
            if (symbols.Count > 64 || symbols.Any(piece => !EnglishScores.Value.ContainsKey(piece)))
                throw new ArgumentException("The wake words are too long. Use fewer or shorter phrases.");
            return string.Join(" ", symbols) + " @" + phrase;
        }

        private static List<string> EncodeEnglish(string text)
        {
            // The release calls this bpe.model, but its serialized trainer type is UNIGRAM (1).
            // Select the highest-probability segmentation, matching SentencePiece's Viterbi path.
            var scores = Enumerable.Repeat(double.NegativeInfinity, text.Length + 1).ToArray();
            var pieces = new string[text.Length + 1];
            scores[0] = 0;
            int maximum = EnglishScores.Value.Keys.Max(piece => piece.Length);
            for (int offset = 0; offset < text.Length; ++offset)
            {
                for (int count = 1; count <= maximum && offset + count <= text.Length; ++count)
                {
                    string piece = text.Substring(offset, count);
                    if (!EnglishScores.Value.TryGetValue(piece, out float score) || scores[offset] + score <= scores[offset + count]) continue;
                    scores[offset + count] = scores[offset] + score;
                    pieces[offset + count] = piece;
                }
            }
            var result = new List<string>();
            for (int end = text.Length; end > 0;)
            {
                string piece = pieces[end] ?? throw new ArgumentException("This phrase contains unsupported English speech sounds.");
                result.Add(piece); end -= piece.Length;
            }
            result.Reverse();
            return result;
        }
    }
}
