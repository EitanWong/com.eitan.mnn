using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RealModel"), Explicit("Requires existing local models; never downloads assets.")]
    public sealed class LlmPackageCorrectnessTests
    {
        [TestCase("SmolLM2-135M-Instruct-MNN")]
        [TestCase("Qwen2.5-0.5B-Instruct-MNN")]
        [TestCase("Qwen3.5-0.8B-MNN")]
        [TestCase("Qwen3.5-0.8B-W4-MNN")]
        [TestCase("MNN-Pocket-0.3B-MNN")]
        public void LongHistoryPreservesTokensStreamingAndRecovery(string name)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            Assert.IsNotEmpty(root, "Set MNN_TEST_MODEL_ROOT to the existing local models.");
            string directory = Path.Combine(root, name);
            Assert.IsTrue(File.Exists(Path.Combine(directory, "config.json")), "Missing local model: " + name);
            string output = Path.GetFullPath(Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT") ?? "TestArtifacts~/LlmPackageCorrectness");
            Directory.CreateDirectory(output);
            var log = new List<string>();
            using (var model = MNNLlm.LoadForConversation(directory, 0f,
                cacheDirectory: Path.Combine(output, "state-cache-" + name), backendType: MNNBackendType.CPU))
            using (var cold = MNNLlm.LoadForConversation(directory, 0f,
                cacheDirectory: Path.Combine(output, "state-cold-" + name), backendType: MNNBackendType.CPU))
            {
                cold.EnableConversationCache = false;
                bool cacheSupported = RuntimeInformation.ProcessArchitecture != Architecture.X64 &&
                    MNN.Unity.Interop.MNNInterop.SupportsChatCacheConfiguration(model.DumpConfiguration());
                var messages = new List<MNNChatMessage> {
                    new MNNChatMessage(MNNChatRole.System, "Answer the latest question briefly. Follow the requested answer format.") };
                string notes = string.Join("\n", Enumerable.Range(0, 64).Select(i =>
                    "Record " + i + ": the delivery contains " + (i + 17) + " items."));
                string[] countries = { "France", "Italy", "Japan", "Germany" };
                MNNMultimodalResult first = null;
                int[] firstIds = null;
                string firstText = null;
                MNNChatMessage[] lastPrompt = null;
                for (int turn = 0; turn < countries.Length; ++turn)
                {
                    string question = "What is the capital of " + countries[turn] + "? Answer with one word.";
                    messages.Add(new MNNChatMessage(MNNChatRole.User, turn == 0 ? "Background records:\n" + notes + "\n\n" + question : question));
                    var context = model.PrepareConversation(messages, 2048, 32);
                    Assert.AreEqual(0, context.RemovedMessages);
                    Assert.Greater(context.InputTokens, 768);
                    lastPrompt = context.Messages.ToArray();
                    var snapshots = new List<MNNGenerationUpdate>();
                    var deltas = new StringBuilder();
                    var actual = turn % 2 == 0 ? model.GenerateConversationStreaming(context.Messages, snapshots.Add, 32) :
                        model.GenerateConversationStreamingDeltas(context.Messages, delta => deltas.Append(delta.Text), 32);
                    var expected = cold.GenerateConversation(context.Messages, 32);
                    AssertEquivalent(expected, actual);
                    Assert.AreEqual(context.InputTokens, actual.TotalPromptTokens);
                    Assert.AreEqual(actual.TotalPromptTokens, actual.PromptTokens + actual.ReusedPromptTokens);
                    if (turn % 2 == 0)
                    {
                        Assert.IsNotEmpty(snapshots);
                        for (int i = 0; i < snapshots.Count; ++i)
                        {
                            Assert.IsTrue(actual.Text.StartsWith(snapshots[i].Text, StringComparison.Ordinal));
                            if (i > 0) Assert.Greater(snapshots[i].GeneratedTokens, snapshots[i - 1].GeneratedTokens);
                        }
                        Assert.AreEqual(actual.Text, snapshots.Last().Text);
                    }
                    else Assert.AreEqual(actual.Text, deltas.ToString(), "Append each streaming delta exactly once.");
                    if (cacheSupported && turn > 0 && !name.StartsWith("Qwen3.5", StringComparison.Ordinal))
                        Assert.AreEqual(MNNConversationCacheStatus.Reused, actual.ConversationCacheStatus, "Exercise actual prefix reuse.");
                    if (actual.ConversationCacheStatus == MNNConversationCacheStatus.Reused)
                        Assert.Greater(actual.ReusedPromptTokens, 768);
                    else Assert.AreEqual(0, actual.ReusedPromptTokens);
                    if (first == null) { first = actual; firstIds = actual.GeneratedTokenIds.ToArray(); firstText = actual.Text; }
                    log.Add(name + " turn=" + turn + " input=" + actual.TotalPromptTokens + " cache=" + actual.ConversationCacheStatus +
                        " reused=" + actual.ReusedPromptTokens + " tokens=" + string.Join(",", actual.GeneratedTokenIds) + " text=" + actual.Text);
                    File.WriteAllLines(Path.Combine(output, "state-" + name + ".txt"), log);
                    TestContext.WriteLine(log.Last());
                    messages.Add(new MNNChatMessage(MNNChatRole.Assistant, actual.Text));
                }

                var edited = (MNNChatMessage[])lastPrompt.Clone();
                edited[1] = new MNNChatMessage(MNNChatRole.User, edited[1].Content.Replace("17 items", "18 items"));
                var changed = model.GenerateConversationStreaming(edited, _ => { }, 32);
                AssertEquivalent(cold.GenerateConversation(edited, 32), changed);
                Assert.AreEqual(0, changed.ReusedPromptTokens, "An edited prefix must start cold.");
                Assert.AreNotEqual(MNNConversationCacheStatus.Reused, changed.ConversationCacheStatus);

                using (var cancel = new CancellationTokenSource())
                {
                    int callbacks = 0;
                    var stopped = model.GenerateConversationStreaming(lastPrompt, _ => { ++callbacks; cancel.Cancel(); },
                        32, cancellationToken: cancel.Token);
                    Assert.AreEqual(1, callbacks);
                    Assert.IsTrue(stopped.Cancelled);
                    Assert.AreEqual(MNNConversationCacheStatus.Interrupted, stopped.ConversationCacheStatus);
                    TestContext.WriteLine("cancelled=" + stopped.Cancelled + " callbacks=" + callbacks);
                }
                var recovered = model.GenerateConversationStreaming(lastPrompt, _ => { }, 32);
                AssertEquivalent(cold.GenerateConversation(lastPrompt, 32), recovered);
                Assert.AreEqual(0, recovered.ReusedPromptTokens, "Cancellation invalidates cached native state.");
                var failure = new InvalidOperationException("application callback failure");
                Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(() =>
                    model.GenerateConversationStreaming(edited, _ => throw failure, 32)));
                var afterFailure = model.GenerateConversationStreaming(lastPrompt, _ => { }, 32);
                AssertEquivalent(cold.GenerateConversation(lastPrompt, 32), afterFailure);
                Assert.AreEqual(0, afterFailure.ReusedPromptTokens);
                Assert.AreEqual(firstText, first.Text);
                CollectionAssert.AreEqual(firstIds, first.GeneratedTokenIds, "Later generations must not mutate an earlier result.");
                TestContext.WriteLine("edited-prefix=cold; cancellation-recovery=equal; callback-recovery=equal; earlier-result=unchanged");
            }
        }

        private static void AssertEquivalent(MNNMultimodalResult expected, MNNMultimodalResult actual)
        {
            Assert.IsFalse(expected.Cancelled);
            Assert.IsFalse(actual.Cancelled);
            Assert.IsNotEmpty(actual.Text);
            Assert.That(actual.GeneratedTokens, Is.InRange(1, 32));
            Assert.AreEqual(expected.Text, actual.Text);
            Assert.AreEqual(expected.GeneratedTokens, actual.GeneratedTokens);
            CollectionAssert.AreEqual(expected.GeneratedTokenIds, actual.GeneratedTokenIds);
            Assert.AreEqual(actual.GeneratedTokens, actual.GeneratedTokenIds.Count);
            Assert.AreEqual(expected.ReachedTokenLimit, actual.ReachedTokenLimit);
            Assert.AreEqual(expected.TotalPromptTokens, actual.TotalPromptTokens);
        }
    }
}
