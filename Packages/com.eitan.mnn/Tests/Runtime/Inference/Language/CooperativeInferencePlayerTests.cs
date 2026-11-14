using System;
using System.Collections;
using System.Text;
using System.Threading;
using MNN.Unity;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class CooperativeInferencePlayerTests
    {
        [UnityTest]
        public IEnumerator TextAsyncStreamsCancelsAndRecovers()
        {
            using (var model = MNNLlm.Load(LlmPerformanceData.Model("MNN-Pocket-0.3B-MNN"), 1,
                precisionMode: MNNPrecisionMode.High, backendType: MNNBackendType.CPU))
            {
                const string prompt = "What is the capital of France? Answer briefly.";
                var joined = new StringBuilder();
                bool callbackOwnership = false;
                var first = model.GenerateStreamingAsync(prompt, delta =>
                {
                    joined.Append(delta.Text);
                    Assert.Throws<InvalidOperationException>(() => model.Generate(prompt, 1));
                    Assert.Throws<InvalidOperationException>(() => model.Dispose());
                    callbackOwnership = true;
                }, 32);
#if UNITY_WEBGL && !UNITY_EDITOR
                Assert.Throws<InvalidOperationException>(() => model.Generate(prompt, 1));
                Assert.Throws<InvalidOperationException>(() => model.Dispose());
#endif
                while (!first.IsCompleted) yield return null;
                var result = first.GetAwaiter().GetResult();
                Assert.AreEqual("The capital of France is Paris.", result.Text);
                Assert.AreEqual(result.Text, joined.ToString());
                Assert.IsTrue(callbackOwnership);
                using (var cancellation = new CancellationTokenSource())
                {
                    var interrupted = model.GenerateStreamingAsync(prompt, delta => cancellation.Cancel(), 32, cancellation.Token);
                    while (!interrupted.IsCompleted) yield return null;
                    Assert.IsTrue(interrupted.IsCanceled);
                }
                var recovered = model.GenerateAsync(prompt, 32);
                while (!recovered.IsCompleted) yield return null;
                Assert.AreEqual(result.Text, recovered.GetAwaiter().GetResult().Text);
                Assert.AreEqual(result.GeneratedTokens, recovered.Result.GeneratedTokens);
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    var skipped = model.GenerateStreamingAsync(prompt, null, 32, cancellation.Token);
                    while (!skipped.IsCompleted) yield return null;
                    Assert.IsTrue(skipped.IsCanceled);
                }
            }
        }

        [UnityTest]
        public IEnumerator CallbackFailureDoesNotPoisonNextRequest()
        {
            using (var model = MNNLlm.Load(LlmPerformanceData.Model("MNN-Pocket-0.3B-MNN"), 1,
                precisionMode: MNNPrecisionMode.High, backendType: MNNBackendType.CPU))
            {
                const string prompt = "What is the capital of France? Answer briefly.";
                var failed = model.GenerateStreamingAsync(prompt, delta => throw new InvalidOperationException("callback sentinel"), 32);
                while (!failed.IsCompleted) yield return null;
                Assert.IsTrue(failed.IsFaulted);
                Assert.IsInstanceOf<InvalidOperationException>(failed.Exception.InnerException);
                Assert.AreEqual("callback sentinel", failed.Exception.InnerException.Message);
                var recovered = model.GenerateAsync(prompt, 32);
                while (!recovered.IsCompleted) yield return null;
                Assert.AreEqual("The capital of France is Paris.", recovered.GetAwaiter().GetResult().Text);
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.CountTokens("hello"));
            }
        }
    }
}
