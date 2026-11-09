using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class ConversationTextAssistanceTests
    {
        private sealed class Capture : IMNNAudioCaptureSource
        {
            public MNNPcmFormat CaptureFormat => new MNNPcmFormat(16000, 1);
            public int MaximumCaptureSamples => 512;
            public long DroppedCaptureBlocks => 0;
            public bool HasPendingCaptureFrames => false;
            public bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count) { info = default; count = 0; return false; }
        }
        private sealed class Model : IMNNConversationResponseProvider
        {
            public bool Slow, Fail;
            public int Running, Peak, Disposals, PlannerCalls, Calls;
            public readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public MNNRealtimeCapabilities Capabilities => null;
            public Task PrepareAsync(CancellationToken token) => Task.CompletedTask;
            public Task GenerateAsync(IReadOnlyList<MNNOpenAIMessage> history, float[] audio, int rate, Func<string, Task> delta, CancellationToken token)
            { ++PlannerCalls; throw new InvalidOperationException("Auxiliary generation must bypass tool planning."); }
            public async Task GenerateResponseAsync(IReadOnlyList<MNNOpenAIMessage> history, string instructions, Func<string, Task> delta, CancellationToken token)
            {
                int current = Interlocked.Increment(ref Running); Peak = Math.Max(Peak, current); ++Calls;
                try
                {
                    bool auxiliary = instructions.StartsWith("Continue the user's", StringComparison.Ordinal) || instructions.StartsWith("Summarize this", StringComparison.Ordinal);
                    if (auxiliary) { Entered.TrySetResult(true); if (Fail) throw new InvalidOperationException("Fixture assistance failure"); if (Slow) await Task.Delay(Timeout.Infinite, token); }
                    await delta(auxiliary ? " a useful continuation" : "A normal answer.");
                }
                finally { Interlocked.Decrement(ref Running); }
            }
            public void Dispose() { Assert.AreEqual(0, Running, "Join inference before releasing providers."); ++Disposals; }
        }
        [Test] public void CompletionNormalizesSuffixWithoutDuplicatingPrefixOrLosingLeadingSpace()
        {
            Assert.AreEqual(" the plan", MNNConversationTextAssistance.Normalize(MNNConversationTextTask.Completion, "Explain", "Explain the plan"));
            Assert.AreEqual(" for tomorrow", MNNConversationTextAssistance.Normalize(MNNConversationTextTask.Completion, "A plan", " for tomorrow\nextra"));
            Assert.AreEqual("", MNNConversationTextAssistance.Normalize(MNNConversationTextTask.Completion, "A plan", "```bad```"));
            Assert.AreEqual("", MNNConversationTextAssistance.Normalize(MNNConversationTextTask.Completion, "A plan", "bad\ttext"));
            Assert.AreEqual("会话管理", MNNConversationTextAssistance.Normalize(MNNConversationTextTask.Title, "内容", "“会话管理”\nExplanation"));
            string bounded = MNNConversationTextAssistance.Normalize(MNNConversationTextTask.Completion, "other", new string('x', 159) + "😀");
            Assert.AreEqual(159, bounded.Length); Assert.IsFalse(char.IsHighSurrogate(bounded[bounded.Length - 1]));
            Assert.Throws<ArgumentException>(() => MNNConversationTextAssistance.CreatePrompt(MNNConversationTextTask.Title, new string('x', 4001)));
        }
        [UnityTest] public IEnumerator AssistanceDoesNotPublishMessagesOrPolluteMemory() => Run(CheckIsolation());
        [UnityTest] public IEnumerator UserTurnPreemptsAssistanceOnTheSameModelGate() => Run(CheckPreemption());
        [UnityTest] public IEnumerator ResetJoinsAssistanceAndDoesNotQueueRequests() => Run(CheckReset());
        [UnityTest] public IEnumerator StopJoinsAndReleasesTheSharedModelOnce() => Run(CheckStop());
        [UnityTest] public IEnumerator OptionalProviderFailureDoesNotPreventResetOrStop() => Run(CheckFailure());
        private static IEnumerator Run(Task task)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted && watch.Elapsed.TotalSeconds < 10) yield return null;
            Assert.IsTrue(task.IsCompleted, "Assistance did not quiesce within the fixture deadline."); task.GetAwaiter().GetResult();
        }
        private sealed class Fixture
        {
            public Model Model = new Model();
            public MNNConversationRuntime Runtime;
            public MNNConversationPorts Ports;
            public MNNRealtimeSession Session;
            public Fixture()
            {
                var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 512);
                Ports = new MNNConversationPorts(new Capture(), () => Session);
                Runtime = new MNNConversationRuntime(Ports, new MNNConversationOptions { speechOutput = false, endRealtimeOnExitIntent = false }, output,
                    MNNRealtimeRoute.CascadedSpeech, new MNNConversationProviders { Model = Model });
                Session = new MNNRealtimeSession(Runtime, output);
            }
        }
        private static async Task CheckIsolation()
        {
            var f = new Fixture();
            try
            {
                await f.Session.StartAsync();
                string result = await f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "Suggest", CancellationToken.None);
                Assert.AreEqual(" a useful continuation", result); Assert.AreEqual(0, f.Runtime.Memory.Messages.Count);
                Assert.IsFalse(f.Ports.TryRead(out _)); Assert.AreEqual(0, f.Model.PlannerCalls); Assert.AreEqual(1, f.Model.Calls);
            }
            finally { await f.Session.StopAsync(); }
        }
        private static async Task CheckPreemption()
        {
            var f = new Fixture(); f.Model.Slow = true;
            try
            {
                await f.Session.StartAsync(); var auxiliary = f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "Suggest", CancellationToken.None);
                await f.Model.Entered.Task; await f.Runtime.SubmitTextAsync("My real question", CancellationToken.None);
                Assert.AreEqual("", await auxiliary); Assert.AreEqual(1, f.Model.Peak);
                Assert.AreEqual(2, f.Runtime.Memory.Messages.Count); Assert.AreEqual("My real question", f.Runtime.Memory.Messages[0].Text);
            }
            finally { await f.Session.StopAsync(); }
        }
        private static async Task CheckReset()
        {
            var f = new Fixture(); f.Model.Slow = true;
            try
            {
                await f.Session.StartAsync(); var pending = f.Runtime.AssistTextAsync(MNNConversationTextTask.Title, "Name this conversation", CancellationToken.None);
                await f.Model.Entered.Task;
                Assert.AreEqual("", await f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "Another", CancellationToken.None));
                await f.Runtime.ResetConversationAsync(); Assert.IsTrue(pending.IsCompleted); Assert.AreEqual(0, f.Model.Running); Assert.AreEqual(1, f.Model.Calls);
            }
            finally { await f.Session.StopAsync(); }
        }
        private static async Task CheckStop()
        {
            var f = new Fixture(); f.Model.Slow = true;
            await f.Session.StartAsync(); var pending = f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "Suggest", CancellationToken.None);
            await f.Model.Entered.Task; await f.Session.StopAsync(); Assert.IsTrue(pending.IsCompleted); Assert.AreEqual(1, f.Model.Disposals);
        }
        private static async Task CheckFailure()
        {
            var f = new Fixture(); f.Model.Fail = true;
            try
            {
                await f.Session.StartAsync();
                try { await f.Runtime.AssistTextAsync(MNNConversationTextTask.Completion, "Suggest", CancellationToken.None); Assert.Fail("Fixture must fail."); }
                catch (InvalidOperationException error) { Assert.AreEqual("Fixture assistance failure", error.Message); }
                await f.Runtime.ResetConversationAsync();
            }
            finally { await f.Session.StopAsync(); }
        }
    }
}
