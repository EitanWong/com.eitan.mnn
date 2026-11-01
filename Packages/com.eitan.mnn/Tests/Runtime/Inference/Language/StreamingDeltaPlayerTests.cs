using System;
using System.Collections;
using System.Text;
using System.Threading;
using MNN.Unity.Interop;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class StreamingDeltaPlayerTests
    {
        [TestCase("SmolLM2-135M-Instruct-MNN", MNNBackendType.CPU)]
        [TestCase("SmolLM2-135M-Instruct-MNN", MNNBackendType.Auto)]
        [TestCase("Qwen3.5-0.8B-MNN", MNNBackendType.CPU)]
        [TestCase("Qwen3.5-0.8B-MNN", MNNBackendType.Auto)]
        public void DeltasMatchCumulativeAndCompleteGeneration(string name, MNNBackendType backend)
        {
            using (var model = Load(name, backend))
            {
                foreach (string prompt in new[] { "Count from 1 to 20. Output only the numbers.",
                    "Repeat this text exactly, without explanation: Unity 你好 😀." })
                {
                    var messages = new[] { new MNNChatMessage(MNNChatRole.User, prompt) };
                    var expected = model.GenerateConversation(messages, 96);
                    string snapshot = null;
                    var legacy = model.GenerateConversationStreaming(messages, update => snapshot = update.Text, 96);
                    var joined = new StringBuilder();
                    int updates = 0, tokens = 0;
                    bool returned = false;
                    var actual = model.GenerateConversationStreamingDeltas(messages, delta =>
                    {
                        Assert.IsFalse(returned);
                        Assert.IsNotEmpty(delta.Text);
                        Assert.Greater(delta.GeneratedTokens, tokens);
                        tokens = delta.GeneratedTokens;
                        joined.Append(delta.Text);
                        Assert.IsTrue(expected.Text.StartsWith(joined.ToString(), StringComparison.Ordinal));
                        ++updates;
                    }, 96);
                    returned = true;
                    Assert.Greater(updates, 0);
                    Assert.AreEqual(expected.Text, snapshot);
                    Assert.AreEqual(expected.Text, joined.ToString());
                    Assert.AreEqual(expected.Text, actual.Text);
                    CollectionAssert.AreEqual(expected.GeneratedTokenIds, actual.GeneratedTokenIds);
                    CollectionAssert.AreEqual(expected.GeneratedTokenIds, legacy.GeneratedTokenIds);
                    Assert.AreEqual(expected.ReachedTokenLimit, actual.ReachedTokenLimit);
                    Assert.Greater(actual.FirstTextMicroseconds, 0);
                    TestContext.WriteLine($"model={name}; requested={backend}; actual={model.Backend}; tokens={actual.GeneratedTokens}; updates={updates}; first_us={actual.FirstTextMicroseconds}; callback_us={actual.ProgressCallbackMicroseconds}; text={actual.Text}");
                }
            }
        }

        [TestCase("SmolLM2-135M-Instruct-MNN", MNNBackendType.CPU)]
        [TestCase("SmolLM2-135M-Instruct-MNN", MNNBackendType.Auto)]
        [TestCase("Qwen3.5-0.8B-MNN", MNNBackendType.CPU)]
        [TestCase("Qwen3.5-0.8B-MNN", MNNBackendType.Auto)]
        public void DeltaCancellationAndConsumerFailureResetState(string name, MNNBackendType backend)
        {
            using (var model = Load(name, backend))
            {
                var messages = new[] { new MNNChatMessage(MNNChatRole.User, "Explain gravity in detail.") };
                var joined = new StringBuilder();
                using (var cancel = new CancellationTokenSource())
                {
                    var result = model.GenerateConversationStreamingDeltas(messages, delta =>
                    {
                        joined.Append(delta.Text);
                        cancel.Cancel();
                    }, 96, cancellationToken: cancel.Token);
                    Assert.IsTrue(result.Cancelled);
                    Assert.IsNotEmpty(result.Text);
                    Assert.AreEqual(joined.ToString(), result.Text);
                    Assert.Less(result.GeneratedTokens, 96);
                }
                var originalBackend = model.Backend;
                var failure = new MNNException(MNNErrorCode.NoExecution, "consumer stopped");
                var observed = Assert.Throws<MNNException>(() => model.GenerateConversationStreamingDeltas(messages,
                    _ => throw failure, 96));
                Assert.AreSame(failure, observed);
                Assert.AreEqual(originalBackend, model.Backend, "A callback failure must never trigger replay on CPU.");
                var next = new[] { new MNNChatMessage(MNNChatRole.User, "What is the capital of France? Answer only the city name.") };
                var recovered = model.GenerateConversationStreamingDeltas(next, _ => { }, 16);
                Assert.AreEqual(0, recovered.ReusedPromptTokens);
                StringAssert.Contains("paris", recovered.Text.ToLowerInvariant());
                using (var cold = Load(name, backend))
                    CollectionAssert.AreEqual(cold.GenerateConversation(next, 16).GeneratedTokenIds, recovered.GeneratedTokenIds);
            }
        }

        [UnityTest]
        public IEnumerator AsyncDeltasRunOffCallerAndPreserveOutput()
        {
            using (var model = Load("Qwen3.5-0.8B-MNN", MNNBackendType.CPU))
            {
                int caller = Thread.CurrentThread.ManagedThreadId;
                int callbackThread = caller;
                var joined = new StringBuilder();
                var task = model.GenerateConversationStreamingDeltasAsync(new[] {
                    new MNNChatMessage(MNNChatRole.User, "What is 2+2? Answer only the number.") }, delta =>
                {
                    callbackThread = Thread.CurrentThread.ManagedThreadId;
                    joined.Append(delta.Text);
                }, 16);
                while (!task.IsCompleted) yield return null;
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreNotEqual(caller, callbackThread);
                Assert.AreEqual(task.Result.Text, joined.ToString());
                StringAssert.Contains("4", task.Result.Text);
            }
        }

        [TestCase("")]
        [TestCase("1234567890123456789012")]
        [TestCase("12345678901234567890123")]
        [TestCase("你好，世界🙂一二三四五六七八九十")]
        [TestCase("before\0after")]
        public void ReusedNativeStringBufferRespectsOffsetsAndLengths(string text)
        {
            MNNInterop.RequireAbi();
            byte[] expected = Encoding.UTF8.GetBytes(text);
            byte[] buffer = new byte[expected.Length + 16];
            byte[] original = buffer;
            using (var native = new MNNInterop.NativeString(text))
            {
                for (int start = 0; start <= expected.Length; ++start)
                {
                    int count = MNNInterop.CopyStringBytes(ref native.Value, start, ref buffer);
                    Assert.AreSame(original, buffer);
                    Assert.AreEqual(expected.Length - start, count);
                    for (int i = 0; i < count; ++i) Assert.AreEqual(expected[start + i], buffer[i]);
                }
            }
        }

        private static MNNLlm Load(string name, MNNBackendType backend) => MNNLlm.Load(LlmPerformanceData.Model(name),
            new MNNLlmPerformanceOptions { BackendType = backend, ThreadCount = 4,
                PrecisionMode = MNNPrecisionMode.Normal, PowerMode = MNNPowerMode.Normal });
    }
}
