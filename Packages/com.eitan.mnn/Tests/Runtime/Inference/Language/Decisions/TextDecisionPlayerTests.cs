using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("TextDecisionPlayer")]
    public class TextDecisionPlayerTests
    {
        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void EnglishLayaSixOriginalReferencesMemoryAndSpeed(MNNBackendType requested)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_TEXT_DECISION_ROOT");
            if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Application.persistentDataPath, "text-decision");
            if (!File.Exists(Path.Combine(root, "laya-fp16.mnn"))) Assert.Ignore("Stage pinned English Laya graph/tokenizer/manifest and Reference/ under persistentDataPath/text-decision or --text-decision-root.");
            string referencePath = Path.Combine(root, "Reference", "reference.json");
            using (var sha = SHA256.Create())
                Assert.AreEqual("6bbc3c742b4e0f9140905a13df531f65417fbf63685567e115b7e1ca61ffc117", BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(referencePath))).Replace("-", "").ToLowerInvariant());
            var reference = MNNModelData.Read(referencePath);
            var tokenizer = MNNBytePairTokenizer.ReadModernBert(Path.Combine(root, "tokenizer.json"));
            foreach (var row in MNNModelData.Array(MNNModelData.Get(reference, "tokenizer_cases")))
                CollectionAssert.AreEqual(MNNModelData.Shape(MNNModelData.Get(row, "ids")), tokenizer.Encode((string)MNNModelData.Get(row, "text")));
            var before = PlayerMeasurements.Read(); var timer = Stopwatch.StartNew();
            using (var model = MNNTextDecision.Load(root, backendType: requested))
            {
                double load = timer.Elapsed.TotalMilliseconds;
                var loaded = PlayerMeasurements.Read();
                Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                int cases = 0;
                foreach (var row in MNNModelData.Array(MNNModelData.Get(reference, "records")))
                {
                    var q = MNNModelData.Get(row, "question");
                    string instructions = (string)MNNModelData.Get(q, "ins"), type = (string)MNNModelData.Get(q, "t");
                    MNNDecisionQuestion question = type == "choice" ? MNNDecisionQuestion.Choice(instructions,
                        MNNModelData.Object(MNNModelData.Get(q, "crit")).Select(p => new MNNDecisionOption(p.Key, (string)p.Value)).ToArray()) :
                        type == "score" ? MNNDecisionQuestion.Score(instructions, MNNModelData.Array(MNNModelData.Get(q, "crit")).Cast<string>().ToArray()) : MNNDecisionQuestion.Boolean(instructions);
                    string state = (string)MNNModelData.Get(row, "state"), sample = (string)MNNModelData.Get(row, "name");
                    timer.Restart(); var result = model.Decide(state, question); double first = timer.Elapsed.TotalMilliseconds;
                    timer.Restart(); var repeat = model.Decide(state, question); double warm = timer.Elapsed.TotalMilliseconds;
                    var expected = MNNModelData.Floats(MNNModelData.Get(row, "probabilities"));
                    double maxError = 0;
                    for (int i = 0; i < expected.Length; ++i)
                    {
                        maxError = Math.Max(maxError, Math.Abs(result.Probabilities[i] - expected[i]));
                        Assert.That(repeat.Probabilities[i], Is.EqualTo(result.Probabilities[i]).Within(1e-5));
                    }
                    Assert.Less(maxError, .002);
                    Assert.AreEqual(MNNModelData.Integer(MNNModelData.Get(row, "argmax")), result.BestIndex);
                    Assert.AreEqual(MNNModelData.Integer(MNNModelData.Get(row, "length")), result.InputTokens);
                    Assert.That(result.ActProbability, Is.EqualTo(Convert.ToDouble(MNNModelData.Get(row, "act_probability"), CultureInfo.InvariantCulture)).Within(.001));
                    Assert.False(result.StateTruncated); Assert.False(result.QuestionTruncated);
                    var after = PlayerMeasurements.Read();
                    string measurement = string.Format(CultureInfo.InvariantCulture,
                        "{{\"device\":\"{0}\",\"sample\":\"{1}\",\"requested\":\"{2}\",\"actual\":\"{3}\",\"load_ms\":{4},\"first_ms\":{5},\"warm_ms\":{6},\"rss_before\":{7},\"rss_loaded\":{8},\"rss_after\":{9},\"peak_rss\":{10},\"physical_footprint\":{11},\"max_probability_error\":{12}}}",
                        SystemInfo.deviceModel, sample, requested, model.Backend, load, first, warm, before.residentBytes, loaded.residentBytes,
                        after.residentBytes, after.peakResidentBytes, after.physicalFootprintBytes, maxError);
                    File.WriteAllText(Path.Combine(root, sample + "-" + requested + "-measurements.json"), measurement);
                    TestContext.WriteLine(measurement); ++cases;
                }
                Assert.AreEqual(6, cases);
                using (var cancelled = new System.Threading.CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.Throws<OperationCanceledException>(() => model.Decide("test", MNNDecisionQuestion.Boolean("test"), cancelled.Token));
                }
                model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Decide("test", MNNDecisionQuestion.Boolean("test")));
            }
        }
    }
}
