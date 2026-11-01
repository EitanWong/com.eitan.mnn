using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using MNN.Unity.Editor;

namespace MNN.Unity.Tests
{
    [Explicit("Requires pinned Laya weights and independent original PyTorch references; no downloads.")]
    public class TextDecisionIntegrationTests
    {
        [Test]
        public void EnglishLayaOriginalWeightsAndTokenizerMatchNativeCpu()
        {
            string models = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            string root = Path.GetFullPath(Path.Combine(models, "../../../../TestArtifacts~/TaskIntegration/NLP/Reference"));
            var reference = MNNModelData.Read(Path.Combine(root, "reference.json"));
            var tokenizer = MNNBytePairTokenizer.ReadModernBert(Path.Combine(models, "Convai--Laya-English/tokenizer.json"));
            foreach (var row in MNNModelData.Array(MNNModelData.Get(reference, "tokenizer_cases")))
                CollectionAssert.AreEqual(MNNModelData.Shape(MNNModelData.Get(row, "ids")), tokenizer.Encode((string)MNNModelData.Get(row, "text")), "Independent Rust tokenizers: " + MNNModelData.Get(row, "text"));
            using (var graph = new MNNGenerationGraph(Path.Combine(models, "Convai--Laya-English/laya-fp16.mnn"), 4, backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High))
            {
                foreach (var row in MNNModelData.Array(MNNModelData.Get(reference, "records")))
                {
                    string sample = (string)MNNModelData.Get(row, "name");
                    var inputs = new List<MNNGenerationGraph.Input>();
                    foreach (string name in new[] {"input_ids", "attention_mask", "marker_pos", "marker_mask", "qtype"})
                    {
                        byte[] bytes = File.ReadAllBytes(Path.Combine(root, sample, name + ".i32"));
                        var values = new int[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
                        inputs.Add(new MNNGenerationGraph.Input(name, values, name == "qtype" ? new[] {1} : new[] {1, values.Length}));
                    }
                    var outputs = graph.RunOutputs(new[] {"logits", "act_logits"}, inputs.ToArray());
                    int count = MNNModelData.Array(MNNModelData.Get(row, "options")).Count;
                    var expected = MNNModelData.Floats(MNNModelData.Get(row, "logits"));
                    double max = outputs[0].Values.Take(count).Select((value, i) => Math.Abs(value - expected[i])).Max();
                    var action = MNNModelData.Floats(MNNModelData.Get(row, "act_logits"));
                    double actMax = outputs[1].Values.Select((value, i) => Math.Abs(value - action[i])).Max();
                    // The upstream action head emits logits around +/-3,000.
                    // Record raw conversion error and compare the exposed
                    // softmax probability; these saturated cases do not prove
                    // calibration or equivalence near an action threshold.
                    double actualAct = 1 / (1 + Math.Exp((double)outputs[1].Values[1] - outputs[1].Values[0]));
                    double expectedAct = 1 / (1 + Math.Exp((double)action[1] - action[0]));
                    TestContext.WriteLine(sample + " logits shape=" + string.Join(",", outputs[0].Shape) + " max=" + max + " action max=" + actMax);
                    Assert.Less(max, .05, sample + " original PyTorch logits");
                    Assert.That(actualAct, Is.EqualTo(expectedAct).Within(.001), sample + " action probability");
                    Assert.AreEqual(Array.IndexOf(expected, expected.Max()), Array.IndexOf(outputs[0].Values.Take(count).ToArray(), outputs[0].Values.Take(count).Max()));
                }
            }
            using (var model = MNNTextDecision.Load(Path.Combine(models, "Convai--Laya-English")))
            {
                Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                foreach (var row in MNNModelData.Array(MNNModelData.Get(reference, "records")))
                {
                    var q = MNNModelData.Get(row, "question");
                    string instructions = (string)MNNModelData.Get(q, "ins");
                    string type = (string)MNNModelData.Get(q, "t");
                    MNNDecisionQuestion question = type == "choice" ? MNNDecisionQuestion.Choice(instructions,
                        MNNModelData.Object(MNNModelData.Get(q, "crit")).Select(p => new MNNDecisionOption(p.Key, (string)p.Value)).ToArray()) :
                        type == "score" ? MNNDecisionQuestion.Score(instructions, MNNModelData.Array(MNNModelData.Get(q, "crit")).Cast<string>().ToArray()) :
                        MNNDecisionQuestion.Boolean(instructions);
                    var input = MNNTextDecision.BuildInput(tokenizer, (string)MNNModelData.Get(row, "state"), question);
                    string sample = (string)MNNModelData.Get(row, "name");
                    foreach (var pair in new Dictionary<string, int[]> {{"input_ids", input.Ids}, {"attention_mask", input.Attention}, {"marker_pos", input.MarkerPositions}, {"marker_mask", input.MarkerMask}})
                    {
                        byte[] bytes = File.ReadAllBytes(Path.Combine(root, sample, pair.Key + ".i32"));
                        var expected = new int[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length);
                        CollectionAssert.AreEqual(expected, pair.Value, sample + " " + pair.Key);
                    }
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var result = model.Decide((string)MNNModelData.Get(row, "state"), question);
                    TestContext.WriteLine(sample + " typed_ms=" + timer.Elapsed.TotalMilliseconds);
                    var probabilities = MNNModelData.Floats(MNNModelData.Get(row, "probabilities"));
                    for (int i = 0; i < probabilities.Length; ++i) Assert.That(result.Probabilities[i], Is.EqualTo(probabilities[i]).Within(.002));
                    Assert.AreEqual(MNNModelData.Integer(MNNModelData.Get(row, "argmax")), result.BestIndex);
                    Assert.AreEqual(MNNModelData.Integer(MNNModelData.Get(row, "length")), result.InputTokens);
                    Assert.IsFalse(result.StateTruncated); Assert.IsFalse(result.QuestionTruncated);
                }
                using (var cancelled = new System.Threading.CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.Throws<OperationCanceledException>(() => model.Decide("test", MNNDecisionQuestion.Boolean("test"), cancelled.Token));
                }
                model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Decide("test", MNNDecisionQuestion.Boolean("test")));
            }
            var studioModel = MNNStudioModel.Read(Path.Combine(models, "Convai--Laya-English"));
            Assert.AreEqual(MNNStudioTask.NLP, studioModel.Task);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.NLP).CanRun(studioModel));
            using (var studio = MNNStudioNativeBackend.Load(studioModel, 4, false))
            {
                var result = studio.Run(new MNNStudioRequest {Prompt = "Please refund the duplicate payment.",
                    DecisionQuestion = MNNChatStudio.ParseDecisionQuestion(MNNDecisionKind.Boolean, "Is the customer asking for a refund?", "")});
                Assert.Greater(result.Decision.TrueProbability.Value, .5);
                Assert.IsNotEmpty(result.Text);
            }
        }
    }
}
