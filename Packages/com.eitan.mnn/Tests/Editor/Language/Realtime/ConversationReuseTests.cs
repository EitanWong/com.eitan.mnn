using System;
using System.Text;
using System.Collections;
using UnityEngine.TestTools;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ConversationReuseTests
    {
        private sealed class Capture : IMNNAudioCaptureSource
        {
            public MNNPcmFormat CaptureFormat => new MNNPcmFormat(16000, 1);
            public int MaximumCaptureSamples => 512;
            public long DroppedCaptureBlocks => 0;
            public bool HasPendingCaptureFrames => false;
            public bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count)
            { info = default; count = 0; return false; }
        }
        private sealed class Model : IMNNConversationTextProvider
        {
            public int Disposals;
            public string Instructions;
            public MNNRealtimeCapabilities Capabilities => null;
            public Task PrepareAsync(CancellationToken token) => Task.CompletedTask;
            public async Task GenerateAsync(System.Collections.Generic.IReadOnlyList<MNNOpenAIMessage> history,
                float[] audio, int rate, Func<string, Task> delta, CancellationToken token)
            { Instructions = history[0].Text; await delta("Hello "); await delta("world."); }
            public void Dispose() { ++Disposals; }
        }
        [UnityTest]
        public IEnumerator RealtimeInactivityIsOwnedByTheEngine()
        {
            var task = CheckInactivityAsync();
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }
        private async Task CheckInactivityAsync()
        {
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 512);
            MNNRealtimeSession session = null;
            var ports = new MNNConversationPorts(new Capture(), () => session);
            var runtime = new MNNConversationRuntime(ports,
                new MNNConversationOptions { speechOutput = false, realtimeInactivitySeconds = 1 }, output,
                MNNRealtimeRoute.CascadedSpeech, new MNNConversationProviders { Model = new Model() });
            session = new MNNRealtimeSession(runtime, output);
            try
            {
                await session.StartAsync();
                ports.SetRealtime(true);
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                bool exit = false;
                while (!(exit = ports.TakeRealtimeExit()) && deadline.Elapsed.TotalSeconds < 2) await Task.Delay(10);
                Assert.True(exit, "Applications must not need a second inactivity implementation.");
            }
            finally { ports.SetRealtime(false); await session.StopAsync(); }
        }
        [UnityTest]
        public IEnumerator NotificationsKeepUserAndAssistantOrder()
        {
            var task = CheckNotificationOrderAsync();
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }
        private async Task CheckNotificationOrderAsync()
        {
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 512);
            MNNRealtimeSession session = null;
            var ports = new MNNConversationPorts(new Capture(), () => session);
            var runtime = new MNNConversationRuntime(ports, new MNNConversationOptions { speechOutput = false }, output,
                MNNRealtimeRoute.CascadedSpeech, new MNNConversationProviders { Model = new Model() });
            session = new MNNRealtimeSession(runtime, output);
            try
            {
                await session.StartAsync();
                var response = session.BeginResponse();
                Assert.True(ports.TryPublishUserText("one"));
                Assert.True(ports.TryPublishAssistantSnapshot(response, new StringBuilder("reply")));
                Assert.True(ports.TryPublishUserText("two"));
                Assert.True(ports.TryRead(out var first)); Assert.AreEqual("one", first.Text);
                Assert.True(ports.TryRead(out var second)); Assert.AreEqual("reply", second.Text);
                Assert.True(ports.TryRead(out var third)); Assert.AreEqual("two", third.Text);
                Assert.True(ports.TryPublishAssistantSnapshot(response, new StringBuilder("stale")));
                await session.InterruptAsync();
                Assert.False(ports.TryRead(out _));
                Assert.False(ports.TryPublishAssistantSnapshot(response, new StringBuilder("late")));
            }
            finally { await session.StopAsync(); }
        }
        [UnityTest]
        public IEnumerator TextTurnsReuseTheEngineAndSnapshotConfiguration()
        {
            var task = TextTurnsReuseTheEngineAndSnapshotConfigurationAsync();
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted && deadline.Elapsed.TotalSeconds < 10) yield return null;
            Assert.True(task.IsCompleted, "Conversation operation did not quiesce.");
            task.GetAwaiter().GetResult();
        }
        private async Task TextTurnsReuseTheEngineAndSnapshotConfigurationAsync()
        {
            var model = new Model();
            var other = new Model();
            var providers = new MNNConversationProviders { Model = model };
            var options = new MNNConversationOptions { speechOutput = false, endRealtimeOnExitIntent = false, instructions = "original" };
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 512);
            MNNRealtimeSession session = null;
            var ports = new MNNConversationPorts(new Capture(), () => session);
            var runtime = new MNNConversationRuntime(ports, options, output, MNNRealtimeRoute.CascadedSpeech, providers);
            session = new MNNRealtimeSession(runtime, output);
            options.instructions = "changed";
            providers.Model = other;
            try
            {
                await session.StartAsync();
                await runtime.SubmitTextAsync("first", CancellationToken.None);
                Assert.AreEqual("original", model.Instructions);
                string visible = null;
                while (ports.TryRead(out var update)) if (update.Assistant && update.Text != null) visible = update.Text;
                Assert.AreEqual("Hello world.", visible);
                await runtime.SubmitTextAsync("second", CancellationToken.None);
                Assert.AreEqual(4, runtime.Memory.Messages.Count);
                Assert.AreEqual("Hello world.", runtime.Memory.Messages[1].Text);
            }
            finally { await session.StopAsync(); }
            Assert.AreEqual(1, model.Disposals);
            Assert.AreEqual(0, other.Disposals, "Replacing the caller's registration must not transfer ownership.");
        }
        [UnityTest]
        public IEnumerator SharedProviderIsReleasedOnce()
        {
            var task = SharedProviderIsReleasedOnceAsync();
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted && deadline.Elapsed.TotalSeconds < 10) yield return null;
            Assert.True(task.IsCompleted, "Conversation operation did not quiesce.");
            task.GetAwaiter().GetResult();
        }
        private async Task SharedProviderIsReleasedOnceAsync()
        {
            var model = new Model();
            var runtime = new MNNConversationRuntime(null, new MNNConversationOptions(), null,
                MNNRealtimeRoute.CascadedSpeech, new MNNConversationProviders { Model = model, Asr = model });
            await runtime.DisposeAsync();
            await runtime.DisposeAsync();
            Assert.AreEqual(1, model.Disposals);
        }
    }
}
