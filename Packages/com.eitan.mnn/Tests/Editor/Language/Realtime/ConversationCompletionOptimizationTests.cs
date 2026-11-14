using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class ConversationCompletionOptimizationTests
    {
        private sealed class Capture : IMNNAudioCaptureSource
        {
            public MNNPcmFormat CaptureFormat => new MNNPcmFormat(16000, 1);
            public int MaximumCaptureSamples => 512;
            public long DroppedCaptureBlocks => 0;
            public bool HasPendingCaptureFrames => false;
            public bool TryReadCapture(float[] buffer, int offset, out MNNAudioFrameInfo info, out int count)
            { info = default; count = 0; return false; }
        }
        private sealed class Model : IMNNConversationResponseProvider
        {
            public int Calls, Cancelled, Running;
            public string Text = " a useful continuation";
            public bool Hold, CancelledAtPublish;
            public string[] Chunks;
            public readonly TaskCompletionSource<bool> Published = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> Finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public MNNRealtimeCapabilities Capabilities => null;
            public Task PrepareAsync(CancellationToken token) => Task.CompletedTask;
            public Task GenerateAsync(IReadOnlyList<MNNOpenAIMessage> h, float[] a, int r, Func<string, Task> d, CancellationToken t)
                => throw new AssertionException("Assistance must bypass planning.");
            public async Task GenerateResponseAsync(IReadOnlyList<MNNOpenAIMessage> h, string i, Func<string, Task> delta, CancellationToken token)
            {
                Interlocked.Increment(ref Calls); Interlocked.Increment(ref Running);
                try
                {
                    foreach (string chunk in Chunks ?? new[] { Text }) await delta(chunk);
                    CancelledAtPublish = token.IsCancellationRequested; Published.TrySetResult(true);
                    if (Hold)
                        using (token.Register(() => Finish.TrySetCanceled())) await Finish.Task;
                    token.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException) { Interlocked.Increment(ref Cancelled); throw; }
                finally { Interlocked.Decrement(ref Running); }
            }
            public void Dispose() { Assert.AreEqual(0, Running); }
        }
        private sealed class Fixture
        {
            public readonly Model Model = new Model();
            public readonly MNNConversationRuntime Runtime;
            public readonly MNNRealtimeSession Session;
            public Fixture()
            {
                var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 512);
                MNNRealtimeSession session = null;
                Runtime = new MNNConversationRuntime(new MNNConversationPorts(new Capture(), () => session),
                    new MNNConversationOptions { speechOutput = false }, output, MNNRealtimeRoute.CascadedSpeech,
                    new MNNConversationProviders { Model = Model });
                Session = session = new MNNRealtimeSession(Runtime, output);
            }
        }
        private static IEnumerator Run(Task task)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted && watch.Elapsed.TotalSeconds < 12) yield return null;
            Assert.IsTrue(task.IsCompleted, "Controlled completion did not finish."); task.GetAwaiter().GetResult();
        }
        private static Task<string> Stream(MNNConversationRuntime runtime, string prefix, Action<string> preview, CancellationToken token)
        {
            var method = typeof(MNNConversationRuntime).GetMethod("AssistTextAsync", new[]
                { typeof(MNNConversationTextTask), typeof(string), typeof(Action<string>), typeof(CancellationToken) });
            Assert.IsNotNull(method, "Completion needs an incremental preview API.");
            return (Task<string>)method.Invoke(runtime, new object[] { MNNConversationTextTask.Completion, prefix, preview, token });
        }
        [UnityTest] public IEnumerator PreviewIsVisibleBeforeTheProviderCompletes() => Run(CheckPreview());
        private static async Task CheckPreview()
        {
            var f = new Fixture(); f.Model.Hold = true;
            try
            {
                await f.Session.StartAsync();
                var seen = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                var pending = Stream(f.Runtime, "Plan", text => seen.TrySetResult(text), CancellationToken.None);
                Assert.AreEqual(" a useful ", await seen.Task);
                Assert.IsFalse(pending.IsCompleted, "First useful words must not wait for the full generation.");
                f.Model.Finish.TrySetResult(true);
                Assert.AreEqual(" a useful continuation", await pending);
            }
            finally { f.Model.Finish.TrySetResult(true); await f.Session.StopAsync(); }
        }
        [UnityTest] public IEnumerator MatchingTypedPrefixReusesCompletionUntilContextReset() => Run(CheckCache());
        private static async Task CheckCache()
        {
            var f = new Fixture();
            try
            {
                await f.Session.StartAsync();
                Assert.AreEqual(" a useful continuation", await f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "Plan", CancellationToken.None));
                Assert.AreEqual(" a useful continuation", await f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "Plan", CancellationToken.None));
                Assert.AreEqual(" useful continuation", await f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "Plan a", CancellationToken.None));
                Assert.AreEqual(1, f.Model.Calls, "Exact and consumed-prefix suggestions reuse one inference.");
                await f.Runtime.ResetConversationAsync();
                await f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "Plan", CancellationToken.None);
                Assert.AreEqual(2, f.Model.Calls, "A reset must invalidate cached suggestions.");
            }
            finally { await f.Session.StopAsync(); }
        }
        [UnityTest] public IEnumerator FirstLineStopsGenerationAndJoinsProvider() => Run(CheckStop());
        private static async Task CheckStop()
        {
            var f = new Fixture(); f.Model.Text = " for tomorrow\nAn unwanted explanation"; f.Model.Hold = true;
            try
            {
                await f.Session.StartAsync();
                Assert.AreEqual(" for tomorrow", await f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "A plan", CancellationToken.None));
                Assert.IsTrue(f.Model.CancelledAtPublish, "Stop on the first line, before waiting for the request deadline.");
                Assert.AreEqual(1, f.Model.Cancelled); Assert.AreEqual(0, f.Model.Running);
            }
            finally { await f.Session.StopAsync(); }
        }
        [UnityTest] public IEnumerator CancelledPartialSuggestionIsNeverCached() => Run(CheckCancellation());
        private static async Task CheckCancellation()
        {
            var f = new Fixture(); f.Model.Hold = true;
            try
            {
                await f.Session.StartAsync();
                using (var cancel = new CancellationTokenSource())
                {
                    var pending = f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "Plan", cancel.Token);
                    await f.Model.Published.Task; cancel.Cancel(); Assert.AreEqual("", await pending);
                }
                f.Model.Hold = false;
                Assert.AreEqual(" a useful continuation", await f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "Plan", CancellationToken.None));
                Assert.AreEqual(2, f.Model.Calls);
            }
            finally { await f.Session.StopAsync(); }
        }
        [TestCase("Please help me write a", "help me write a proposal", " proposal")]
        [TestCase("请帮我写一份项目", "请帮我写一份项目计划书", "计划书")]
        [TestCase("Plan", "<think>I should answer</think> a trip", "")]
        [TestCase("Plan", "bad\u0001suffix", "")]
        public void CompletionRejectsReasoningAndRemovesEchoedDraftTail(string prefix, string generated, string expected)
            => Assert.AreEqual(expected, MNNConversationTextAssistance.Normalize(MNNConversationTextTask.Completion, prefix, generated));

        [UnityTest] public IEnumerator StreamWithholdsEchoAndPublishesCompletedWords() => Run(CheckEchoPreview(false));
        [UnityTest] public IEnumerator StreamWithholdsAnIncompleteEchoOfTheDraftTail() => Run(CheckEchoPreview(true));
        private static async Task CheckEchoPreview(bool tail)
        {
            var f = new Fixture(); f.Model.Chunks = tail ? new[] { "help", " me", " write a", " proposal ", "for tomorrow" } :
                new[] { "Please", " help me", " write a", " proposal ", "for tomorrow" };
            var previews = new List<string>();
            try
            {
                await f.Session.StartAsync();
                Assert.AreEqual(" proposal for tomorrow", await Stream(f.Runtime, "Please help me write a", previews.Add, CancellationToken.None));
                Assert.IsNotEmpty(previews); Assert.AreEqual(" proposal ", previews[0]);
                Assert.IsTrue(previews.TrueForAll(p => p.StartsWith(" proposal", StringComparison.Ordinal)));
            }
            finally { await f.Session.StopAsync(); }
        }
        [Test] public void CompletionNeverReturnsBrokenSurrogatePairs()
        {
            Assert.AreEqual("", MNNConversationTextAssistance.Normalize(MNNConversationTextTask.Completion, "Plan", " bad\ud800suffix"));
            Assert.AreEqual("", MNNConversationTextAssistance.Normalize(MNNConversationTextTask.Completion, "Plan", "\udc00suffix"));
            Assert.AreEqual(" 😀 trip", MNNConversationTextAssistance.Normalize(MNNConversationTextTask.Completion, "Plan", " 😀 trip"));
        }
        [Test] public void LongDraftKeepsUnicodeSafeRecentContextWithinPrefillBudget()
        {
            string input = new string('x', 2975) + "😀" + new string('中', 1023);
            var prompt = MNNConversationTextAssistance.CreatePrompt(MNNConversationTextTask.Completion, input);
            Assert.LessOrEqual(prompt[1].Text.Length, 1024);
            Assert.IsFalse(char.IsLowSurrogate(prompt[1].Text[0])); StringAssert.EndsWith(new string('中', 30), prompt[1].Text);
        }
        [UnityTest] public IEnumerator CharacterBudgetStopsEvenAProviderWithoutAssistanceSupport() => Run(CheckCharacterBudget());
        private static async Task CheckCharacterBudget()
        {
            var f = new Fixture(); f.Model.Hold = true; f.Model.Text = new string('中', 255) + "😀";
            try
            {
                await f.Session.StartAsync();
                string suffix = await f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "计划", CancellationToken.None);
                Assert.AreEqual(160, suffix.Length); Assert.IsTrue(f.Model.CancelledAtPublish); Assert.AreEqual(0, f.Model.Running);
            }
            finally { await f.Session.StopAsync(); }
        }

        private sealed class Transport : HttpMessageHandler
        {
            public string Body;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Body = await request.Content.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\" a plan\"}}]}\n\ndata: [DONE]\n\n", System.Text.Encoding.UTF8, "text/event-stream") };
            }
        }
        [UnityTest] public IEnumerator RemoteAssistanceUsesAShortBudgetWithoutChangingNormalReplies() => Run(CheckRemote());
        private static async Task CheckRemote()
        {
            var handler = new Transport();
            using (var http = new HttpClient(handler))
            using (var provider = new MNNOpenAIConversationProvider(new MNNOpenAIConversationOptions
                { Endpoint = "https://example.test/v1", Model = "fixture", MaximumTokens = 8192 }, "fixture", MNNOpenAIConversationStage.Model, http))
            {
                var method = provider.GetType().GetMethod("GenerateAssistanceAsync");
                Assert.IsNotNull(method, "Provider needs a per-request assistance budget.");
                var prompt = MNNConversationTextAssistance.CreatePrompt(MNNConversationTextTask.Completion, "Plan");
                await (Task)method.Invoke(provider, new object[] { MNNConversationTextTask.Completion, prompt, (Func<string, Task>)(_ => Task.CompletedTask), CancellationToken.None });
                StringAssert.Contains("\"max_tokens\":48", handler.Body);
                await provider.GenerateAsync(prompt, null, 16000, _ => Task.CompletedTask, CancellationToken.None);
                StringAssert.Contains("\"max_tokens\":8192", handler.Body);
                using (var agent = new MNNAgentProvider(provider, new MNNAgentOptions { AllowCloud = true },
                    () => throw new AssertionException("Typing must not acquire a cloud provider.")))
                {
                    method = agent.GetType().GetMethod("GenerateAssistanceAsync");
                    Assert.IsNotNull(method, "Agent must preserve the assistance budget and bypass its planner.");
                    await (Task)method.Invoke(agent, new object[] { MNNConversationTextTask.Completion, prompt,
                        (Func<string, Task>)(_ => Task.CompletedTask), CancellationToken.None });
                    StringAssert.Contains("\"max_tokens\":48", handler.Body);
                }
            }
        }
    }
}
