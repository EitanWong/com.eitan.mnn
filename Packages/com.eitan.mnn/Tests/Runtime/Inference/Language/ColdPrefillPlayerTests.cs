using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ColdPrefillPlayerTests
    {
        [TestCase("SmolLM2-135M-Instruct-MNN", MNNBackendType.CPU)]
        [TestCase("SmolLM2-135M-Instruct-MNN", MNNBackendType.Auto)]
        [TestCase("Qwen3.5-0.8B-MNN", MNNBackendType.CPU)]
        [TestCase("Qwen3.5-0.8B-MNN", MNNBackendType.Auto)]
        public void ColdChatMatchesUncachedTokensAndMeasuresLatency(string name, MNNBackendType backend)
        {
            var tuning = new MNNLlmPerformanceOptions { BackendType = backend,
                ThreadCount = 4, PrecisionMode = MNNPrecisionMode.Normal, PowerMode = MNNPowerMode.Normal };
            var messages = new[]
            {
                new MNNChatMessage(MNNChatRole.System,
                    string.Concat(Enumerable.Repeat("Follow the user's instructions. Be clear and accurate. ", 32))),
                new MNNChatMessage(MNNChatRole.User,
                    "Write a numbered list of twenty practical ways to save water at home. Explain each item in a complete sentence.")
            };
            using (var model = MNNLlm.Load(LlmPerformanceData.Model(name), tuning))
            {
                // The same instance, exact prompt, output limit and consumer. Warm
                // both paths, then alternate their order to limit order bias.
                int expectedPrompt = model.CountConversationTokens(messages);
                MNNMultimodalResult expected = null;
                for (int pair = -1; pair < 3; ++pair)
                for (int position = 0; position < 2; ++position)
                {
                    bool enabled = (pair & 1) == position;
                    model.EnableConversationCache = false;
                    model.EnableConversationCache = enabled;
                    int delivered = 0;
                    var wall = Stopwatch.StartNew();
                    var result = model.GenerateConversationStreamingDeltas(messages,
                        delta => delivered += delta.Text.Length, 32);
                    double wallMs = wall.Elapsed.TotalMilliseconds;
                    Assert.False(result.Cancelled);
                    Assert.AreEqual(0, result.ReusedPromptTokens, "Every request must start with independent full prefill.");
                    Assert.AreEqual(expectedPrompt, result.TotalPromptTokens);
                    Assert.AreEqual(result.Text.Length, delivered);
                    Assert.GreaterOrEqual(result.GeneratedTokenIds.Count, 8, "Require a meaningful decode workload.");
                    if (expected != null)
                    {
                        CollectionAssert.AreEqual(expected.GeneratedTokenIds, result.GeneratedTokenIds);
                        Assert.AreEqual(expected.Text, result.Text);
                    }
                    expected = result;
                    Assert.AreEqual(!enabled ? MNNConversationCacheStatus.Disabled :
                        RuntimeInformation.ProcessArchitecture == Architecture.X64 ?
                            MNNConversationCacheStatus.UnsupportedRuntime : MNNConversationCacheStatus.Cold,
                        result.ConversationCacheStatus);
                    TestContext.WriteLine($"COLD_PREFILL model={name}; requested={backend}; actual={model.Backend}; pair={pair}; enabled={enabled}; prompt={expectedPrompt}; output={result.GeneratedTokenIds.Count}; wall_ms={wallMs:F3}; first_text_us={result.FirstTextMicroseconds}; prefill_us={result.PrefillMicroseconds}; decode_us={result.DecodeMicroseconds}; sample_us={result.SampleMicroseconds}; consumer_us={result.ProgressCallbackMicroseconds}; ids={string.Join(",", result.GeneratedTokenIds)}");
                }
            }
        }
    }
}
