using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RealModel"), Explicit("Requires the already downloaded model weights; never downloads assets.")]
    public sealed class LlmLongContextTests
    {
        [TestCase("SmolLM2-135M-Instruct-MNN")]
        [TestCase("Qwen2.5-0.5B-Instruct-MNN")]
        [TestCase("Qwen3.5-0.8B-MNN")]
        [TestCase("MNN-Pocket-0.3B-MNN")]
        public void LongHistoryAnswersLatestQuestionAndMatchesColdStreaming(string name)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            Assert.IsNotEmpty(root, "Set MNN_TEST_MODEL_ROOT to the existing downloaded models.");
            string directory = Path.Combine(root, name);
            Assert.IsTrue(File.Exists(Path.Combine(directory, "config.json")), "Missing local model: " + name);
            string output = Path.GetFullPath(Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT") ?? "TestArtifacts~/LlmLongContext");
            Directory.CreateDirectory(output);
            var log = new List<string>();
            using (var model = MNNLlm.LoadForConversation(directory, 0f,
                cacheDirectory: Path.Combine(output, "cache-" + name), backendType: MNNBackendType.CPU))
            using (var coldModel = MNNLlm.LoadForConversation(directory, 0f,
                cacheDirectory: Path.Combine(output, "cold-" + name), backendType: MNNBackendType.CPU))
            {
                coldModel.EnableConversationCache = false;
                var messages = new List<MNNChatMessage> {
                    new MNNChatMessage(MNNChatRole.System, "Answer the latest question briefly. Follow the requested answer format.") };
                string notes = string.Join("\n", Enumerable.Range(0, 64).Select(i =>
                    "Record " + i + ": the delivery contains " + (i + 17) + " items."));
                string[] countries = { "France", "Italy", "Japan", "Germany" };
                string[] capitals = { "Paris", "Rome", "Tokyo", "Berlin" };
                for (int turn = 0; turn < countries.Length; ++turn)
                {
                    string question = "What is the capital of " + countries[turn] + "? Answer with one word.";
                    messages.Add(new MNNChatMessage(MNNChatRole.User, turn == 0 ? "Background records:\n" + notes + "\n\n" + question : question));
                    var context = model.PrepareConversation(messages, 2048, 32);
                    Assert.AreEqual(0, context.RemovedMessages, "This gate must retain the long history.");
                    Assert.Greater(context.InputTokens, 768, "Exercise a genuinely long prompt for these small models.");
                    var cached = model.GenerateConversationStreaming(context.Messages, _ => { }, 32);
                    var cold = coldModel.GenerateConversation(context.Messages, 32);
                    log.Add(name + " turn=" + turn + " input=" + context.InputTokens + " cache=" + cached.ConversationCacheStatus +
                        " cached=" + cached.Text + " cold=" + cold.Text);
                    File.WriteAllLines(Path.Combine(output, name + ".txt"), log);
                    TestContext.WriteLine(log.Last());
                    Assert.IsFalse(cached.Cancelled);
                    Assert.IsFalse(cold.Cancelled);
                    Assert.AreEqual(cold.Text, cached.Text, "Cache/streaming state must not change deterministic answers.");
                    StringAssert.Contains(capitals[turn].ToLowerInvariant(), cold.Text.ToLowerInvariant());
                    Assert.Less(cold.GeneratedTokens, 32, "The short answer must stop instead of looping to its budget.");
                    messages.Add(new MNNChatMessage(MNNChatRole.Assistant, cold.Text));
                }
            }
        }
    }
}
