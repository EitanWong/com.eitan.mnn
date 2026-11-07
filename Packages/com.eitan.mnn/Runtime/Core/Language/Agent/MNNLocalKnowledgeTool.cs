using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Bounded lexical retrieval over explicitly assigned local documents. No embedding service or uploads.</summary>
    public class MNNLocalKnowledgeTool : IMNNAgentTool
    {
        private readonly List<KeyValuePair<string, string>> _passages = new List<KeyValuePair<string, string>>();
        public string Name => "local_knowledge";
        public string Description { get; }
        public bool RequiresNetwork => false;
        public MNNLocalKnowledgeTool(IEnumerable<KeyValuePair<string, string>> documents)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            var names = new List<string>();
            int size = 0;
            foreach (var doc in documents)
            {
                if (string.IsNullOrWhiteSpace(doc.Key) || doc.Key.Length > 128 || doc.Value == null ||
                    names.Count >= 32 || (size += doc.Value.Length) > 128000)
                    throw new ArgumentException("Assign at most 32 named local documents with 128000 total characters.");
                names.Add(doc.Key);
                for (int start = 0; start < doc.Value.Length; start += 700)
                    _passages.Add(new KeyValuePair<string, string>(doc.Key, doc.Value.Substring(start, Math.Min(800, doc.Value.Length - start))));
            }
            string sources = string.Join(", ", names);
            Description = "Find exact excerpts in local reference documents. Input is the user's search question. Sources: " + sources.Substring(0, Math.Min(1000, sources.Length));
        }
        public Task<string> ExecuteAsync(string input, CancellationToken token)
        {
            var matches = Search(input, token);
            return Task.FromResult(matches.Count == 0 ? "I could not find that in the configured local documents. Which document or topic should I search?" :
                string.Join("\n\n", matches.Select(p => "[" + p.Key + "]\n" + p.Value)));
        }
        public IReadOnlyList<KeyValuePair<string, string>> Search(string input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(input) || input.Length > 1024) throw new ArgumentException("Provide a bounded document search.");
            var terms = Terms(input);
            var matches = _passages.Select(p => new
            {
                Passage = p,
                Score = terms.Count(term =>
                p.Value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 || p.Key.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            })
                .Where(p => p.Score > 0).OrderByDescending(p => p.Score).Take(3).Select(p => p.Passage).ToArray();
            return matches;
        }
        private static HashSet<string> Terms(string query)
        {
            var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stop = new HashSet<string>(new[] { "the", "and", "what", "where", "which", "how", "are", "does", "this", "that", "is", "in", "of", "to", "a" }, StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Regex.Matches(query, @"[\p{L}\p{N}]+"))
            {
                string word = match.Value;
                if (word.Any(c => c >= 0x3400 && c <= 0x9fff))
                    for (int i = 0; i + 1 < word.Length; ++i) terms.Add(word.Substring(i, 2));
                else if (!stop.Contains(word) && word.Length > 1) terms.Add(word);
            }
            return terms;
        }
    }
}
