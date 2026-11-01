using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MNN.Unity.Interop;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class LlmOptimizationPlayerTests
    {
        [TestCase("SmolLM2-135M-Instruct-MNN", MNNBackendType.CPU)]
        [TestCase("SmolLM2-135M-Instruct-MNN", MNNBackendType.Auto)]
        [TestCase("Qwen3.5-0.8B-MNN", MNNBackendType.CPU)]
        [TestCase("Qwen3.5-0.8B-MNN", MNNBackendType.Auto)]
        public void ExactContextBudgetMatchesNativePromptAndColdGeneration(string name, MNNBackendType backend)
        {
            using (var model = MNNLlm.Load(LlmPerformanceData.Model(name), Tuning(backend)))
            {
                var system = new MNNChatMessage(MNNChatRole.System, "Answer briefly and accurately.");
                var user = new MNNChatMessage(MNNChatRole.User, "What is the capital of France? Answer only the city name.");
                var mandatory = new[] { system, user };
                var history = new[] { system,
                    new MNNChatMessage(MNNChatRole.User, string.Concat(Enumerable.Repeat("Old question. ", 128))),
                    new MNNChatMessage(MNNChatRole.Assistant, string.Concat(Enumerable.Repeat("Old answer. ", 128))), user };
                int exact = model.CountConversationTokens(mandatory);
                var prepared = model.PrepareConversation(history, exact + 32, 32);
                Assert.AreEqual(2, prepared.RemovedMessages);
                Assert.AreEqual(exact, prepared.InputTokens);
                Assert.AreSame(system, prepared.Messages[0]);
                Assert.AreSame(user, prepared.Messages[1]);
                var actual = model.GenerateConversationStreamingDeltas(prepared.Messages, _ => { }, 32);
                Assert.AreEqual(exact, actual.TotalPromptTokens);
                Assert.AreEqual(RuntimeInformation.ProcessArchitecture == Architecture.X64 ?
                    MNNConversationCacheStatus.UnsupportedRuntime : MNNConversationCacheStatus.Cold, actual.ConversationCacheStatus);
                model.EnableConversationCache = false;
                var reference = model.GenerateConversation(mandatory, 32);
                Assert.AreEqual(MNNConversationCacheStatus.Disabled, reference.ConversationCacheStatus);
                CollectionAssert.AreEqual(reference.GeneratedTokenIds, actual.GeneratedTokenIds);
                Assert.AreEqual(reference.Text, actual.Text);
                Assert.Throws<ArgumentException>(() => model.PrepareConversation(history, exact + 31, 32));
                TestContext.WriteLine($"model={name}; backend={model.Backend}; full_tokens={model.CountConversationTokens(history)}; fitted_tokens={exact}; removed={prepared.RemovedMessages}; text={actual.Text}");
            }
        }

        [TestCase("SmolLM2-135M-Instruct-MNN")]
        [TestCase("Qwen3.5-0.8B-MNN")]
        public void RepeatedTokenCountsMatchUncachedOfficialTokenizer(string name)
        {
            string directory = LlmPerformanceData.Model(name);
            var options = MNNModelOptions.Prepare(directory, 4, false, null, MNNPrecisionMode.Normal,
                backendType: MNNBackendType.CPU);
            try
            {
                using (MNNInterop.BeginNativeCall())
                using (var raw = options.Load(MNNInterop.MNN_Llm_create))
                using (var model = MNNLlm.Load(directory, Tuning(MNNBackendType.CPU)))
                {
                    string text = string.Concat(Enumerable.Repeat("Unity 端侧 inference 😀. ", 128));
                    int expected = MNNInterop.MNN_Llm_tokenize(raw, text);
                    Assert.AreEqual(expected, model.CountTokens(text));
                    var clock = Stopwatch.StartNew();
                    for (int i = 0; i < 64; ++i) Assert.AreEqual(expected, MNNInterop.MNN_Llm_tokenize(raw, text));
                    long nativeTicks = clock.ElapsedTicks;
                    clock.Restart();
                    for (int i = 0; i < 64; ++i) Assert.AreEqual(expected, model.CountTokens(text));
                    TestContext.WriteLine($"model={name}; repeated_counts=64; tokens={expected}; uncached_ms={nativeTicks * 1000d / Stopwatch.Frequency:F3}; cached_ms={clock.Elapsed.TotalMilliseconds:F3}; scope=token-count-only");
                }
            }
            finally { MNNModelOptions.RemoveOwnedCache(options.OwnedCache); }
        }

        [TestCase("SmolLM2-135M-Instruct-MNN")]
        [TestCase("Qwen3.5-0.8B-MNN")]
        public void ExplicitPrefillChunkingPreservesGreedyTokens(string name)
        {
            var messages = new[] { new MNNChatMessage(MNNChatRole.User,
                string.Concat(Enumerable.Repeat("The sky is blue. ", 40)) + "What is the capital of France? Answer only the city name.") };
            MNNMultimodalResult expected;
            using (var model = MNNLlm.Load(LlmPerformanceData.Model(name), Tuning(MNNBackendType.CPU)))
                expected = model.GenerateConversation(messages, 16);
            var tuning = Tuning(MNNBackendType.CPU);
            tuning.PrefillChunkSize = 32;
            tuning.FlashAttention = true;
            using (var model = MNNLlm.Load(LlmPerformanceData.Model(name), tuning))
            {
                var result = model.GenerateConversation(messages, 16);
                Assert.AreEqual(expected.TotalPromptTokens, result.TotalPromptTokens);
                CollectionAssert.AreEqual(expected.GeneratedTokenIds, result.GeneratedTokenIds);
                Assert.AreEqual(expected.Text, result.Text);
                TestContext.WriteLine($"model={name}; chunk=32; baseline_prefill_us={expected.PrefillMicroseconds}; chunk_prefill_us={result.PrefillMicroseconds}; tokens={result.TotalPromptTokens}");
            }
        }

        [Test]
        public void PersistentCacheLeasesReuseReleasedSlotsAndInvalidateIdentities()
        {
            string root = Path.Combine(ArtifactRoot(), "llm-lease-" + Guid.NewGuid().ToString("N"));
            string model = Path.Combine(root, "model"), caches = Path.Combine(root, "cache");
            Directory.CreateDirectory(model);
            File.WriteAllText(Path.Combine(model, "config.json"), "{}");
            var tuning = Tuning(MNNBackendType.CPU);
            try
            {
                string original;
                using (var first = MNNLlmCache.Acquire(caches, model, "native1", "device1", tuning))
                {
                    original = first.DirectoryPath;
                    File.WriteAllText(Path.Combine(original, "marker"), "retained");
                    using (var concurrent = MNNLlmCache.Acquire(caches, model, "native1", "device1", tuning))
                        Assert.AreNotEqual(original, concurrent.DirectoryPath);
                }
                using (var again = MNNLlmCache.Acquire(caches, model, "native1", "device1", tuning))
                {
                    Assert.AreEqual(original, again.DirectoryPath);
                    Assert.AreEqual("retained", File.ReadAllText(Path.Combine(original, "marker")));
                }
                using (var changed = MNNLlmCache.Acquire(caches, model, "native2", "device1", tuning)) Assert.AreNotEqual(original, changed.DirectoryPath);
                using (var changed = MNNLlmCache.Acquire(caches, model, "native1", "device2", tuning)) Assert.AreNotEqual(original, changed.DirectoryPath);
                tuning.ThreadCount = 2;
                using (var changed = MNNLlmCache.Acquire(caches, model, "native1", "device1", tuning)) Assert.AreNotEqual(original, changed.DirectoryPath);
                tuning.ThreadCount = 4;
                File.WriteAllText(Path.Combine(model, "config.json"), "{\"dynamic_option\":1}");
                using (var changed = MNNLlmCache.Acquire(caches, model, "native1", "device1", tuning)) Assert.AreNotEqual(original, changed.DirectoryPath);
                Assert.Throws<ArgumentException>(() => MNNLlmCache.Acquire(model, model, "native1", "device1", tuning));
            }
            finally { Directory.Delete(root, true); }
        }

        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void NativeModelCanReloadUsingItsReleasedPersistentCache(MNNBackendType backend)
        {
            string directory = LlmPerformanceData.Model("Qwen3.5-0.8B-MNN");
            string root = Path.Combine(ArtifactRoot(), "llm-reload-" + Guid.NewGuid().ToString("N"));
            var tuning = Tuning(backend);
            string device = SystemInfo.operatingSystem + "|" + SystemInfo.graphicsDeviceName + "|" + SystemInfo.graphicsDeviceVersion;
            var messages = new[] { new MNNChatMessage(MNNChatRole.User, "What is the capital of France? Answer only the city name.") };
            MNNMultimodalResult previous = null;
            string path = null;
            try
            {
                for (int repeat = 0; repeat < 2; ++repeat)
                using (var cache = MNNLlmCache.Acquire(root, directory, "pinned-validation-build", device, tuning))
                {
                    if (path != null) Assert.AreEqual(path, cache.DirectoryPath);
                    path = cache.DirectoryPath;
                    var timer = Stopwatch.StartNew();
                    using (var model = MNNLlm.Load(directory, tuning, cacheDirectory: path))
                    {
                        double loadMs = timer.Elapsed.TotalMilliseconds;
                        var result = model.GenerateConversation(messages, 16);
                        Assert.IsNotEmpty(result.Text);
                        if (previous != null) CollectionAssert.AreEqual(previous.GeneratedTokenIds, result.GeneratedTokenIds);
                        previous = result;
                        TestContext.WriteLine($"cache_repeat={repeat}; requested={backend}; actual={model.Backend}; load_ms={loadMs:F3}; prefill_us={result.PrefillMicroseconds}; files={Directory.GetFiles(path).Length}");
                    }
                }
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static MNNLlmPerformanceOptions Tuning(MNNBackendType backend) => new MNNLlmPerformanceOptions {
            BackendType = backend, PrecisionMode = MNNPrecisionMode.Normal, PowerMode = MNNPowerMode.Normal };

        private static string ArtifactRoot() => Environment.GetEnvironmentVariable("MNN_INFERENCE_CACHE_DIRECTORY") ?? Application.persistentDataPath;
    }
}
