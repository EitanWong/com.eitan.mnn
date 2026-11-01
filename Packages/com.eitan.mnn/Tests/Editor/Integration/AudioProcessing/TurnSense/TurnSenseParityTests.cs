using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class TurnSenseParityTests
    {
        [Serializable] private sealed class TokenReference { public TokenRow[] cases; public string overlong_text; }
        [Serializable] private sealed class TokenRow { public string text; public int[] input_ids, attention_mask; public int valid_tokens; }
        [Serializable] private sealed class ProbabilityReference { public ProbabilityRow[] cases; }
        [Serializable] private sealed class ProbabilityRow { public string text, @case; public float[] probabilities; public int decision; }
        private static string Root
        {
            get
            {
                string root = Environment.GetEnvironmentVariable("MNN_TEST_TURNSENSE");
                if (string.IsNullOrEmpty(root)) Assert.Ignore("Set MNN_TEST_TURNSENSE to the frozen author reference directory.");
                return root;
            }
        }
        private static string Candidate => Path.Combine(Root, "candidate-v418");
        private static TokenReference Tokens => JsonUtility.FromJson<TokenReference>(File.ReadAllText(Path.Combine(Root, "tokenizer-reference-v406.json")));
        private static ProbabilityRow[] Rows => JsonUtility.FromJson<ProbabilityReference>(File.ReadAllText(Path.Combine(Root, "reference-v407", "cases.json"))).cases;

        [Test]
        public void EveryTokenAndPaddingMaskMatchesOriginalIncludingUnicodeAndBoundaries()
        {
            var tokenizer = new MNNTurnSenseTokenizer(Path.Combine(Candidate, "tokenizer.json"));
            foreach (var row in Tokens.cases)
            {
                int[] encoded = tokenizer.Encode(row.text);
                Assert.AreEqual(row.valid_tokens, encoded.Length, row.text);
                CollectionAssert.AreEqual(row.input_ids.Take(encoded.Length), encoded, row.text);
                CollectionAssert.AreEqual(row.input_ids, encoded.Concat(Enumerable.Repeat(2, 256 - encoded.Length)), row.text);
                CollectionAssert.AreEqual(row.attention_mask, Enumerable.Repeat(1, encoded.Length).Concat(Enumerable.Repeat(0, 256 - encoded.Length)), row.text);
            }
            Assert.Throws<ArgumentException>(() => tokenizer.Encode(Tokens.overlong_text));
            Assert.Throws<ArgumentNullException>(() => tokenizer.Encode(null));
            Assert.Throws<ArgumentException>(() => tokenizer.Encode("\ud800"));
            Assert.Throws<ArgumentException>(() => tokenizer.Encode("\udc00"));
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void CompleteTranscriptProbabilitiesAndDecisionsMatchOriginal(MNNBackendType backend)
        {
            using (var model = MNNTurnSense.Load(Candidate, backendType: backend))
            {
                foreach (var row in Rows)
                {
                    var actual = model.Predict(row.text);
                    double error = Math.Max(Math.Abs(actual.NonEndOfUtteranceProbability - row.probabilities[0]), Math.Abs(actual.EndOfUtteranceProbability - row.probabilities[1]));
                    TestContext.WriteLine(row.@case + " maxabs=" + error.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    Assert.Less(error, .001, row.@case);
                    Assert.AreEqual(row.decision == 1, actual.IsComplete, row.@case);
                    var repeated = model.Predict(row.text);
                    Assert.AreEqual(actual.EndOfUtteranceProbability, repeated.EndOfUtteranceProbability, row.@case);
                    Assert.AreEqual(actual.NonEndOfUtteranceProbability, repeated.NonEndOfUtteranceProbability, row.@case);
                }
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.Metal : backend, model.Backend);
            }
        }

        [Test]
        public void AdversarialUnicodeAndWhitespaceTokensMatchOriginal()
        {
            var tokenizer = new MNNTurnSenseTokenizer(Path.Combine(Candidate, "tokenizer.json"));
            var rows = JsonUtility.FromJson<TokenReference>(File.ReadAllText(Path.Combine(Root, "tokenizer-stress-v426.json"))).cases;
            foreach (var row in rows)
                CollectionAssert.AreEqual(row.input_ids, tokenizer.Encode(row.text), row.text);
        }

        [Test]
        public void InvalidRequestsCancellationAndDisposalDoNotConsumeFollowingValidRequest()
        {
            Assert.Throws<ArgumentNullException>(() => MNNTurnSense.Load(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNTurnSense.Load(Candidate, 0));
            Assert.Throws<NotSupportedException>(() => MNNTurnSense.Load(Candidate, 1, MNNBackendType.NN));
            Assert.Throws<InvalidDataException>(() => MNNTurnSense.Load(Root));
            var model = MNNTurnSense.Load(Candidate, 1, MNNBackendType.CPU);
            try
            {
                var before = model.Predict("Hello");
                Assert.Throws<ArgumentNullException>(() => model.Predict(null));
                Assert.Throws<ArgumentException>(() => model.Predict(Tokens.overlong_text));
                Assert.Throws<OperationCanceledException>(() => model.Predict("Hello", new CancellationToken(true)));
                Assert.AreEqual(before.EndOfUtteranceProbability, model.Predict("Hello").EndOfUtteranceProbability);
            }
            finally { model.Dispose(); }
            model.Dispose();
            Assert.Throws<ObjectDisposedException>(() => model.Predict("Hello"));
        }

        [Test]
        public void MeasureWholeManagedTranscriptCallAcrossQualifiedBackendsAndThreads()
        {
            var rows = Rows.Where((row, i) => i == 3 || i == 17 || i == 26 || i == 34).ToArray();
            foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Metal })
            foreach (int threads in new[] { 1, 2, 4, 8 })
            {
                var timer = Stopwatch.StartNew();
                using (var model = MNNTurnSense.Load(Candidate, threads, backend))
                {
                    double load = timer.Elapsed.TotalMilliseconds;
                    timer.Restart();
                    model.Predict("Hello");
                    double first = timer.Elapsed.TotalMilliseconds;
                    var times = new double[rows.Length * 3];
                    for (int i = 0; i < times.Length; ++i)
                    {
                        var row = rows[i % rows.Length];
                        timer.Restart();
                        var result = model.Predict(row.text);
                        times[i] = timer.Elapsed.TotalMilliseconds;
                        Assert.Less(Math.Abs(result.EndOfUtteranceProbability - row.probabilities[1]), .001);
                        Assert.AreEqual(row.decision == 1, result.IsComplete);
                    }
                    Array.Sort(times);
                    TestContext.WriteLine("PERF backend=" + model.Backend + " threads=" + threads + " load_ms=" + load + " first_ms=" + first + " median_ms=" + times[times.Length / 2] + " max_ms=" + times.Last());
                    Assert.AreEqual(backend, model.Backend);
                }
            }
        }
    }
}
