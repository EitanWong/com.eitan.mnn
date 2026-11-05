using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>One language/model decision shared by settings, preflight and the capture worker.</summary>
    public static class MNNWakeWordSettings
    {
        public static IReadOnlyDictionary<MNNKeywordLanguage, string> Group(MNNAcousticSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!Enum.IsDefined(typeof(MNNKeywordLanguage), settings.keywordLanguage)) throw new ArgumentException("Choose a wake-word language.");
            var groups = new Dictionary<MNNKeywordLanguage, List<string>>();
            foreach (string line in (settings.keywords ?? "").Split('\n').Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                var language = settings.keywordLanguage == MNNKeywordLanguage.Automatic ? MNNKeywordText.DetectLanguage(line) : settings.keywordLanguage;
                if (!groups.TryGetValue(language, out var words)) groups[language] = words = new List<string>();
                words.Add(line.Trim());
            }
            if (groups.Count == 0) throw new ArgumentException("Enter at least one wake word.");
            return groups.ToDictionary(pair => pair.Key, pair => MNNKeywordText.Prepare(string.Join("\n", pair.Value), pair.Key));
        }

        public static string ModelDirectory(MNNAcousticSettings settings, MNNKeywordLanguage language) =>
            language == MNNKeywordLanguage.English ?
                (string.IsNullOrWhiteSpace(settings.englishKeywordDirectory) ? MNNKeywordSpotterConfig.EnglishDirectoryName : settings.englishKeywordDirectory) : settings.keywordDirectory;

        public static void CopyWake(MNNAcousticSettings source, MNNAcousticSettings target)
        {
            target.wakeWord = source.wakeWord; target.keywords = source.keywords;
            target.keywordLanguage = source.keywordLanguage; target.keywordThreshold = source.keywordThreshold;
            target.keywordDirectory = source.keywordDirectory; target.englishKeywordDirectory = source.englishKeywordDirectory;
        }
        public static bool OnlyWakeChanged(MNNAcousticSettings before, MNNAcousticSettings after)
        {
            if (before == null || after == null) return false;
            var normalized = before.Snapshot(); CopyWake(after, normalized);
            return normalized.SamePipeline(after);
        }
        public static bool SameWake(MNNAcousticSettings before, MNNAcousticSettings after) =>
            before.wakeWord == after.wakeWord && before.keywords == after.keywords &&
            before.keywordLanguage == after.keywordLanguage && before.keywordThreshold == after.keywordThreshold &&
            before.keywordDirectory == after.keywordDirectory && before.englishKeywordDirectory == after.englishKeywordDirectory;

        public static string MergeDisplayWords(string original, IEnumerable<string> words)
        {
            if (words == null) throw new ArgumentNullException(nameof(words));
            var previous = (original ?? "").Split('\n').GroupBy(line => MNNKeywordText.GetDisplayText(line), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            return string.Join("\n", words.Select(word =>
            {
                string text = (word ?? "").Trim();
                return previous.TryGetValue(text, out string saved) ? saved : text;
            }));
        }
    }
}
