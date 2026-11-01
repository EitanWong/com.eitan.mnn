using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using MNN.Unity.Interop;

namespace MNN.Unity.Tests
{
    public sealed class ConversationCachePlayerTests
    {
        [TestCase("SmolLM2-135M-Instruct-MNN", 9)]
        [TestCase("SmolLM2-135M-Instruct-MNN", 10)]
        [TestCase("Qwen3.5-0.8B-MNN", 9)]
        [TestCase("Qwen3.5-0.8B-MNN", 10)]
        public void QuantizedAttentionUsesFullPrefill(string name, int attentionMode)
        {
            var options = MNNModelOptions.Prepare(LlmPerformanceData.Model(name), 4, false,
                null, MNNPrecisionMode.Normal, backendType: MNNBackendType.CPU);
            options.Json = options.Json.Substring(0, options.Json.Length - 1) +
                ",\"power\":\"normal\",\"attention_mode\":" + attentionMode + "}";
            try
            {
                using (MNNInterop.BeginNativeCall())
                using (var model = options.Load(MNNInterop.MNN_Llm_create))
                {
                    var messages = new List<MNNChatMessage> {
                        new MNNChatMessage(MNNChatRole.System,
                            string.Concat(Enumerable.Repeat("Answer briefly and accurately. ", 80))),
                        new MNNChatMessage(MNNChatRole.User, "What is 2+2? Answer only the number.")
                    };
                    var first = MNNInterop.GenerateChat(model, messages, null, null, false, 256, 16);
                    messages.Add(new MNNChatMessage(MNNChatRole.Assistant, first.Text));
                    foreach (string country in new[] { "France", "Italy", "Japan" })
                    {
                        messages.Add(new MNNChatMessage(MNNChatRole.User,
                            "What is the capital of " + country + "? Answer only the city name."));
                        var actual = MNNInterop.GenerateChat(model, messages, null, null, false, 256, 24);
                        Assert.AreEqual(0, actual.ReusedPromptTokens);
                        Assert.AreEqual(RuntimeInformation.ProcessArchitecture == Architecture.X64 ?
                            MNNConversationCacheStatus.UnsupportedRuntime : MNNConversationCacheStatus.UnsupportedModel,
                            actual.ConversationCacheStatus);
                        model.ConversationCacheEnabled = false;
                        var reference = MNNInterop.GenerateChat(model, messages, null, null, false, 256, 24);
                        CollectionAssert.AreEqual(reference.GeneratedTokenIds, actual.GeneratedTokenIds);
                        Assert.AreEqual(reference.Text, actual.Text);
                        Assert.AreEqual(reference.TotalPromptTokens, actual.PromptTokens);
                        TestContext.WriteLine(name + " mode=" + attentionMode + " " + country +
                            " reused=" + actual.ReusedPromptTokens + " text=" + actual.Text);
                        model.ConversationCacheEnabled = true;
                        // Keep a completed native history for the next enabled request.
                        MNNInterop.GenerateChat(model, messages, null, null, false, 256, 24);
                        messages.Add(new MNNChatMessage(MNNChatRole.Assistant, actual.Text));
                    }
                }
            }
            finally { MNNModelOptions.RemoveOwnedCache(options.OwnedCache); }
        }

        [TestCase("SmolLM2-135M-Instruct-MNN")]
        [TestCase("Qwen3.5-0.8B-MNN")]
        public void ExactCacheMatchesColdAndRecoversAfterStateChanges(string name)
        {
            string directory = LlmPerformanceData.Model(name);
            Assert.True(File.Exists(Path.Combine(directory, "config.json")), directory);
            var tuning = new MNNLlmPerformanceOptions { BackendType = MNNBackendType.CPU,
                ThreadCount = 4, PrecisionMode = MNNPrecisionMode.Normal, PowerMode = MNNPowerMode.Normal };
            string artifactRoot = Environment.GetEnvironmentVariable("MNN_INFERENCE_CACHE_DIRECTORY");
#if UNITY_EDITOR
            Assert.False(string.IsNullOrEmpty(artifactRoot), "Set MNN_INFERENCE_CACHE_DIRECTORY under TestArtifacts~.");
#else
            if (string.IsNullOrEmpty(artifactRoot)) artifactRoot = Application.persistentDataPath;
#endif
            string cacheRoot = Path.Combine(artifactRoot, "cache-probe-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var model = MNNLlm.Load(directory, tuning, cacheDirectory: cacheRoot))
                using (var cold = MNNLlm.Load(directory, tuning, cacheDirectory: Path.Combine(cacheRoot, "cold")))
                using (var uncounted = MNNLlm.Load(directory, tuning, cacheDirectory: Path.Combine(cacheRoot, "uncounted")))
                {
                    cold.EnableConversationCache = false;
                    var messages = new List<MNNChatMessage>
                    {
                        new MNNChatMessage(MNNChatRole.System, string.Concat(Enumerable.Repeat("Answer briefly and accurately. ", 80))),
                        new MNNChatMessage(MNNChatRole.User, "What is 2+2? Answer only the number.")
                    };
                    var first = model.GenerateConversationStreaming(messages, _ => {}, 16);
                    CollectionAssert.AreEqual(first.GeneratedTokenIds,
                        uncounted.GenerateConversationStreaming(messages, _ => {}, 16).GeneratedTokenIds);
                    Assert.False(first.Cancelled);
                    bool verifiedContinuation = RuntimeInformation.ProcessArchitecture != Architecture.X64;
                    Assert.AreEqual(verifiedContinuation ? MNNConversationCacheStatus.Cold : MNNConversationCacheStatus.UnsupportedRuntime,
                        first.ConversationCacheStatus);
                    messages.Add(new MNNChatMessage(MNNChatRole.Assistant, first.Text));
                    messages.Add(new MNNChatMessage(MNNChatRole.User, "What is the capital of France? Answer only the city name."));
                    // Rendering/budgeting must not disturb a live native conversation.
                    int exact = model.CountConversationTokens(messages);
                    model.CountTokens("An independent tokenizer operation, not generation.");
                    var cached = model.GenerateConversationStreaming(messages, _ => {}, 16);
                    // Compare the SAME cached execution with/without count/render calls.
                    // A full prefill can use a different floating-point reduction order.
                    var untouched = uncounted.GenerateConversationStreaming(messages, _ => {}, 16);
                    Assert.AreEqual(untouched.ReusedPromptTokens, cached.ReusedPromptTokens);
                    CollectionAssert.AreEqual(untouched.GeneratedTokenIds, cached.GeneratedTokenIds,
                        "Token counting must not alter the live conversation state.");
                    Assert.AreEqual(exact, cached.TotalPromptTokens);
                    var reference = cold.GenerateConversationStreaming(messages, _ => {}, 16);
                    Equal(reference, cached, "paris");
                    if (verifiedContinuation && name.StartsWith("SmolLM2", StringComparison.Ordinal)) Assert.Greater(cached.ReusedPromptTokens, 0);
                    if (!verifiedContinuation) Assert.AreEqual(0, cached.ReusedPromptTokens);
                    Assert.AreEqual(!verifiedContinuation ? MNNConversationCacheStatus.UnsupportedRuntime :
                        cached.ReusedPromptTokens > 0 ? MNNConversationCacheStatus.Reused : MNNConversationCacheStatus.HistoryMismatch,
                        cached.ConversationCacheStatus);
                    Log("second", name, cached, reference);
                    foreach (string next in new[] { "What is the capital of Italy? Answer only the city name.", "What is the capital of Japan? Answer only the city name." })
                    {
                        messages.Add(new MNNChatMessage(MNNChatRole.Assistant, cached.Text));
                        messages.Add(new MNNChatMessage(MNNChatRole.User, next));
                        cached = model.GenerateConversationStreaming(messages, _ => {}, 24);
                        reference = cold.GenerateConversationStreaming(messages, _ => {}, 24);
                        Log("later", name, cached, reference);
                        Equal(reference, cached, next.Contains("Italy") ? "rome" : "tokyo");
                    }
                    if (name.StartsWith("Qwen", StringComparison.Ordinal))
                    {
                        messages.Add(new MNNChatMessage(MNNChatRole.Assistant, cached.Text));
                        messages.Add(new MNNChatMessage(MNNChatRole.User, "五加六等于几？只回答数字。"));
                        var chinese = model.GenerateConversationStreaming(messages, _ => {}, 24);
                        var chineseCold = cold.GenerateConversationStreaming(messages, _ => {}, 24);
                        Log("chinese", name, chinese, chineseCold);
                        Equal(chineseCold, chinese);
                        Assert.AreEqual(0, chinese.ReusedPromptTokens, "A rewritten reasoning template must take the cold path.");
                        StringAssert.Contains("11", chinese.Text);
                    }
                    // Template revisions and history trimming must recompute instead of reusing stale state.
                    messages[0] = new MNNChatMessage(MNNChatRole.System, "Answer briefly.");
                    var edited = model.GenerateConversation(messages, 16);
                    Equal(cold.GenerateConversation(messages, 16), edited);
                    Assert.AreEqual(0, edited.ReusedPromptTokens);
                    var trimmed = new[] { new MNNChatMessage(MNNChatRole.User, "What is 2+2? Answer only the number.") };
                    Equal(cold.GenerateConversation(trimmed, 16), model.GenerateConversation(trimmed, 16));
                    // An independent validation/classification request invalidates the conversation cache.
                    model.Generate("Reply yes.", 4);
                    var afterPlain = model.GenerateConversation(messages, 16);
                    Assert.AreEqual(0, afterPlain.ReusedPromptTokens);
                    Equal(cold.GenerateConversation(messages, 16), afterPlain);
                    using (var cancel = new CancellationTokenSource())
                    {
                        var partial = model.GenerateConversationStreaming(messages, _ => cancel.Cancel(), 16, cancellationToken: cancel.Token);
                        Assert.True(partial.Cancelled);
                    }
                    var recovered = model.GenerateConversation(messages, 16);
                    Assert.AreEqual(0, recovered.ReusedPromptTokens);
                    Equal(cold.GenerateConversation(messages, 16), recovered);
                    Assert.Throws<InvalidOperationException>(() => model.GenerateConversationStreaming(messages,
                        _ => throw new InvalidOperationException("consumer stopped"), 16));
                    Equal(cold.GenerateConversation(messages, 16), model.GenerateConversation(messages, 16));
                    model.EnableConversationCache = false;
                    Assert.AreEqual(0, model.GenerateConversation(messages, 16).ReusedPromptTokens);
                    model.EnableConversationCache = true;
                    Equal(cold.GenerateConversation(messages, 16), model.GenerateConversation(messages, 16));
                }
            }
            finally { if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true); }
        }
        private static void Equal(MNNMultimodalResult expected, MNNMultimodalResult actual, string requiredAnswer = null)
        {
            Assert.AreEqual(expected.TotalPromptTokens, actual.TotalPromptTokens);
            if (actual.ReusedPromptTokens > 0)
            {
                Assert.IsNotNull(requiredAnswer, "A reused/full-prefill comparison needs an explicit task quality gate.");
                StringAssert.Contains(requiredAnswer, expected.Text.ToLowerInvariant());
                StringAssert.Contains(requiredAnswer, actual.Text.ToLowerInvariant());
            }
            else
            {
                Assert.AreEqual(expected.Text, actual.Text);
                CollectionAssert.AreEqual(expected.GeneratedTokenIds, actual.GeneratedTokenIds);
            }
        }
        private static void Log(string stage, string name, MNNMultimodalResult cached, MNNMultimodalResult cold)
        {
            TestContext.WriteLine(name + " " + stage + " reused=" + cached.ReusedPromptTokens +
                " full=" + cached.TotalPromptTokens + " computed=" + cached.PromptTokens +
                " firstTextUs=" + cached.FirstTextMicroseconds + " coldFirstTextUs=" + cold.FirstTextMicroseconds +
                " prefillUs=" + cached.PrefillMicroseconds + " coldPrefillUs=" + cold.PrefillMicroseconds + " text=" + cached.Text);
        }
    }
}
