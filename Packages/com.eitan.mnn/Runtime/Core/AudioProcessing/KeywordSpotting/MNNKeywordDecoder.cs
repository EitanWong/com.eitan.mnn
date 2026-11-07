using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    // Managed transducer beam search with keyword context/failure arcs. The search follows
    // sherpa's keyword decoder design; no sherpa native library or C ABI is used.
    internal sealed class MNNKeywordDecoder
    {
        private sealed class Node
        {
            internal readonly Dictionary<int, Node> Next = new Dictionary<int, Node>();
            internal Node Fail, Output;
            internal int Depth;
            internal string Phrase;
            internal float TokenScore, Score, OutputScore, Threshold;
        }

        private sealed class Hypothesis
        {
            internal Node State;
            internal double Score;
            internal int Blanks;
            internal List<int> Ids = new List<int> {-1, 0};
            internal List<float> Probabilities = new List<float>();
            internal List<int> Times = new List<int>();
        }

        private readonly Node _root = new Node();
        private List<Hypothesis> _hypotheses;
        private readonly int _trailingBlanks, _maxPaths, _maxLength, _vocabularySize;
        private readonly Func<int[], float[]> _decode;
        private readonly Func<float[], float[], float[]> _join;
        private int _frame;

        internal MNNKeywordDecoder(string keywords, string[] tokens, float threshold, float score,
            int trailingBlanks, int maxPaths, Func<int[], float[]> decode, Func<float[], float[], float[]> join, int minimumTokens = 2)
        {
            if (string.IsNullOrWhiteSpace(keywords) || keywords.Length > 32768)
                throw new ArgumentException("Enter one tokenized keyword per line (up to 32,768 characters).", nameof(keywords));
            if (!Finite(threshold) || threshold <= 0 || threshold > 1) throw new ArgumentOutOfRangeException(nameof(threshold));
            if (!Finite(score) || score < 0 || score > 10) throw new ArgumentOutOfRangeException(nameof(score));
            if (trailingBlanks < 1 || trailingBlanks > 25) throw new ArgumentOutOfRangeException(nameof(trailingBlanks));
            if (maxPaths < 1 || maxPaths > 16) throw new ArgumentOutOfRangeException(nameof(maxPaths));
            _decode = decode; _join = join; _trailingBlanks = trailingBlanks; _maxPaths = maxPaths;
            _vocabularySize = tokens.Length;
            var table = tokens.Select((text, id) => new {text, id}).ToDictionary(item => item.text, item => item.id, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in keywords.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                int label = line.IndexOf('@');
                string[] parts = (label < 0 ? line : line.Substring(0, label)).Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                var ids = new List<int>();
                float boost = score, limit = threshold;
                string phrase = label < 0 ? null : line.Substring(label + 1).Trim();
                foreach (string part in parts)
                {
                    if (part.StartsWith(":", StringComparison.Ordinal)) boost = Parse(part, 0, 10);
                    else if (part.StartsWith("#", StringComparison.Ordinal)) limit = Parse(part, float.Epsilon, 1);
                    else if (table.TryGetValue(part.Normalize(), out int id) && id > 2) ids.Add(id);
                    else throw new ArgumentException("Unknown KWS token '" + part + "'. Use space-separated tone-marked initials/finals from tokens.txt, e.g. x iǎo ài t óng x ué @小爱同学.");
                }
                if (ids.Count < minimumTokens || ids.Count > 64) throw new ArgumentException("The wake phrase is too short or too long for this model.");
                if (!seen.Add(string.Join(",", ids))) throw new ArgumentException("Duplicate keyword token sequence.");
                if (seen.Count > 128) throw new ArgumentException("At most 128 keywords can be active.");
                _maxLength = Math.Max(_maxLength, ids.Count);
                Node node = _root;
                foreach (int id in ids)
                {
                    if (!node.Next.TryGetValue(id, out var next)) node.Next[id] = next = new Node {Depth = node.Depth + 1};
                    next.TokenScore = Math.Max(boost, next.TokenScore);
                    node = next;
                }
                node.Phrase = string.IsNullOrEmpty(phrase) ? string.Concat(ids.Select(id => tokens[id])) : phrase;
                node.Threshold = limit;
            }
            if (seen.Count == 0) throw new ArgumentException("Enter at least one keyword.");
            _root.Fail = _root;
            var queue = new Queue<Node>();
            foreach (var node in _root.Next.Values) { node.Fail = _root; node.Score = node.TokenScore; queue.Enqueue(node); }
            while (queue.Count != 0)
            {
                var node = queue.Dequeue();
                // Scores depend on parents, so compute in breadth-first order after all keywords were inserted.
                node.OutputScore = (node.Phrase == null ? 0 : node.Score) + (node.Output?.OutputScore ?? 0);
                foreach (var pair in node.Next)
                {
                    var child = pair.Value;
                    child.Score = node.Score + child.TokenScore;
                    var fail = node.Fail;
                    while (fail != _root && !fail.Next.ContainsKey(pair.Key)) fail = fail.Fail;
                    child.Fail = fail.Next.TryGetValue(pair.Key, out var target) ? target : _root;
                    child.Output = child.Fail.Phrase != null ? child.Fail : child.Fail.Output;
                    queue.Enqueue(child);
                }
            }
            Reset();
        }

        private static float Parse(string part, float min, float max)
        {
            if (!float.TryParse(part.Substring(1), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !Finite(value) || value < min || value > max)
                throw new ArgumentException("Invalid keyword score/threshold: " + part);
            return value;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal void Reset(bool resetTime = true)
        {
            _hypotheses = new List<Hypothesis> {new Hypothesis {State = _root}};
            if (resetTime) _frame = 0;
        }

        private Node NextState(Node previous, int id, out float score)
        {
            score = 0;
            if (id == 0 || id == 2) return previous;
            Node state = previous;
            bool direct = state.Next.ContainsKey(id);
            while (state != _root && !state.Next.ContainsKey(id)) state = state.Fail;
            Node next = state.Next.TryGetValue(id, out var target) ? target : _root;
            score = (direct ? next.TokenScore : next.Score - previous.Score) + next.OutputScore;
            return next;
        }

        internal MNNKeywordDetection Advance(float[] encoded)
        {
            var candidates = new List<Tuple<double, int, Hypothesis, float>>();
            var cache = new Dictionary<string, float[]>();
            foreach (var hypothesis in _hypotheses)
            {
                int count = hypothesis.Ids.Count;
                var context = new[] {hypothesis.Ids[count - 2], hypothesis.Ids[count - 1]};
                string key = context[0] + "," + context[1];
                if (!cache.TryGetValue(key, out var decoded)) cache[key] = decoded = _decode(context);
                var logits = _join(encoded, decoded);
                if (logits.Length != _vocabularySize || logits.Any(value => !Finite(value))) throw new InvalidDataException("KWS output does not match the model vocabulary.");
                float max = logits.Max();
                double logSum = max + Math.Log(logits.Sum(value => Math.Exp(value - max)));
                for (int id = 0; id < logits.Length; ++id)
                {
                    if (id == 1) continue; // <sos/eos> is not a keyword unit.
                    double logProbability = logits[id] - logSum;
                    NextState(hypothesis.State, id, out float contextScore);
                    candidates.Add(Tuple.Create(hypothesis.Score + logProbability + contextScore, id, hypothesis, (float)Math.Exp(logProbability)));
                }
            }
            var next = new Dictionary<string, Hypothesis>();
            // Equivalent blank/unknown histories share a beam slot. Fill the beam
            // with distinct histories so duplicates cannot evict a viable keyword.
            foreach (var item in candidates.OrderByDescending(value => value.Item1))
            {
                int id = item.Item2;
                var previous = item.Item3;
                var hypothesis = new Hypothesis {State = previous.State, Score = item.Item1, Blanks = previous.Blanks + 1,
                    Ids = new List<int>(previous.Ids), Probabilities = new List<float>(previous.Probabilities), Times = new List<int>(previous.Times)};
                if (id != 0 && id != 2)
                {
                    hypothesis.State = NextState(previous.State, id, out _);
                    hypothesis.Blanks = 0;
                    hypothesis.Ids.Add(id); hypothesis.Probabilities.Add(item.Item4); hypothesis.Times.Add(_frame);
                    if (hypothesis.State == _root)
                    {
                        hypothesis.Ids = new List<int> {-1, 0}; hypothesis.Probabilities.Clear(); hypothesis.Times.Clear();
                    }
                    else if (hypothesis.Ids.Count > _maxLength + 2)
                    {
                        hypothesis.Ids.RemoveAt(0);
                        hypothesis.Probabilities.RemoveAt(0); hypothesis.Times.RemoveAt(0);
                    }
                }
                string key = string.Join(",", hypothesis.Ids);
                if (next.TryGetValue(key, out var same))
                {
                    double max = Math.Max(same.Score, hypothesis.Score);
                    double merged = max + Math.Log(Math.Exp(same.Score - max) + Math.Exp(hypothesis.Score - max));
                    if (hypothesis.Score > same.Score) next[key] = same = hypothesis;
                    same.Score = merged;
                }
                else next[key] = hypothesis;
                if (next.Count == _maxPaths) break;
            }
            _hypotheses = next.Values.ToList();
            var best = _hypotheses.OrderByDescending(value => value.Score).First();
            Node matched = best.State.Phrase != null ? best.State : best.State.Output;
            MNNKeywordDetection result = null;
            if (matched != null && best.Blanks > _trailingBlanks && best.Probabilities.Count >= matched.Depth)
            {
                int skip = best.Probabilities.Count - matched.Depth;
                float confidence = best.Probabilities.Skip(skip).Average();
                if (confidence >= matched.Threshold)
                {
                    result = new MNNKeywordDetection(matched.Phrase, confidence, best.Times[skip] * .04f,
                        (best.Times[best.Times.Count - 1] + 1) * .04f, best.Ids.Skip(best.Ids.Count - matched.Depth).ToArray());
                    Reset(false);
                }
            }
            if (result == null && best.Blanks > 38) Reset(false);
            // Normalizing preserves relative scores and avoids precision loss on long streams.
            if (result == null)
            {
                double max = _hypotheses.Max(value => value.Score);
                foreach (var hypothesis in _hypotheses) hypothesis.Score -= max;
            }
            ++_frame;
            return result;
        }
    }
}
