using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Examples.Tests
{
    /// <summary>Deterministic integration acceptance. These fixtures do not run a real model or an audio device.</summary>
    public sealed class MNNStandaloneConversationTests
    {
        [UnityTest]
        public IEnumerator RecordedInputReachesReplyPresentationAndJoinedClose() => Run(RoundTripAsync());

        [UnityTest]
        public IEnumerator StopDuringPermissionJoinsStartupAndNeverOpensLateDevices() => Run(StopDuringPermissionAsync());

        [UnityTest]
        public IEnumerator CloseFailureRetainsOwnersForRetryAndStillStopsOtherEndpoint() => Run(CloseRetryAsync());

        [UnityTest]
        public IEnumerator StopJoinsGenerationBeforeProviderRelease() => Run(StopDuringGenerationAsync());

        [UnityTest]
        public IEnumerator ProviderFaultClosesResourcesAndKeepsTheCauseVisible() => Run(ProviderFaultAsync());

        [UnityTest]
        public IEnumerator AbandonedModelTestCancelsAndJoinsItsApplication() => Run(AbandonExerciseAsync());

        private static async Task RoundTripAsync()
        {
            var fixture = new DeterministicConversationFixture();
            var app = Create(fixture);
            try
            {
                await app.StartAsync();
                Assert.IsTrue(app.IsReady);
                Assert.IsFalse(fixture.Binding.CaptureEnabled);
                Assert.IsTrue(await app.Commands.BeginRecordingAsync());
                fixture.Binding.InjectSpeech(16);
                await app.Commands.FinishRecordingAsync(false);
                app.Tick();
                Assert.GreaterOrEqual(fixture.Recognition.ReceivedSamples, 4000);
                Assert.AreEqual("deterministic input", fixture.Model.LastUserText);
                Assert.AreEqual(2, app.Transcript.Count);
                Assert.AreEqual("deterministic input", app.Transcript[0].Text);
                Assert.AreEqual("deterministic reply.", app.Transcript[1].Text);
                var response = app.Devices.Output.CurrentResponse;
                Assert.IsFalse(app.Devices.Output.CanBeginNextResponse(response), "EOS must not count as audio presentation.");
                fixture.Binding.Render(12);
                app.Tick();
                Assert.IsTrue(app.Devices.Output.CanBeginNextResponse(response));
                Assert.Greater(fixture.Binding.NonzeroRenderedSamples, 0);
                await app.StopAsync();
                Assert.IsTrue(app.IsClosed);
                Assert.IsFalse(fixture.Binding.CaptureEnabled);
                Assert.AreEqual(1, fixture.Binding.InputStops);
                Assert.AreEqual(1, fixture.Binding.OutputStops);
                Assert.AreEqual(1, fixture.Model.DisposeCalls);
                Assert.AreEqual(1, fixture.Recognition.DisposeCalls);
                Assert.AreEqual(1, fixture.Speech.DisposeCalls);
                await app.StopAsync();
                Assert.AreEqual(1, fixture.Model.DisposeCalls);
            }
            finally { await app.StopAsync(); }
        }

        private static async Task StopDuringPermissionAsync()
        {
            var fixture = new DeterministicConversationFixture();
            fixture.Binding.BlockPermission = true;
            var app = Create(fixture);
            try
            {
                Task start = app.StartAsync();
                Assert.IsFalse(start.IsCompleted);
                Task stop = app.StopAsync();
                Assert.IsFalse(stop.IsCompleted, "Stop must join permission cleanup.");
                fixture.Binding.PermissionCleanup.TrySetResult(true);
                await stop;
                try { await start; Assert.Fail("Startup should be cancelled."); }
                catch (OperationCanceledException) { }
                Assert.IsTrue(app.IsClosed);
                Assert.AreEqual(0, fixture.Binding.InputStarts);
                Assert.AreEqual(0, fixture.Binding.OutputStarts);
                Assert.AreEqual(0, fixture.Model.PrepareCalls);
            }
            finally
            {
                fixture.Binding.PermissionCleanup.TrySetResult(true);
                await app.StopAsync();
            }
        }

        private static async Task CloseRetryAsync()
        {
            var fixture = new DeterministicConversationFixture();
            var app = Create(fixture);
            try
            {
                await app.StartAsync();
                fixture.Binding.FailInputStop = true;
                try { await app.StopAsync(); Assert.Fail("The failed close must be visible."); }
                catch (AggregateException error) { StringAssert.Contains("stop input", error.ToString()); }
                Assert.IsFalse(app.IsClosed);
                Assert.AreEqual(1, fixture.Binding.OutputStops, "One endpoint must not hide the other endpoint's close.");
                Assert.IsFalse(fixture.Binding.CaptureEnabled);
                fixture.Binding.FailInputStop = false;
                await app.StopAsync();
                Assert.IsTrue(app.IsClosed);
                Assert.AreEqual(2, fixture.Binding.InputStops);
                Assert.AreEqual(1, fixture.Model.DisposeCalls);
            }
            finally { fixture.Binding.FailInputStop = false; await app.StopAsync(); }
        }

        private static async Task StopDuringGenerationAsync()
        {
            var fixture = new DeterministicConversationFixture();
            fixture.Model.BlockGeneration = true;
            var app = Create(fixture);
            try
            {
                await app.StartAsync();
                Task submission = app.Commands.SendTextAsync("test cancellation");
                await fixture.Model.GenerationStarted.Task;
                Task stop = app.StopAsync();
                Assert.IsFalse(fixture.Binding.Capturing, "Physical devices must stop before model work joins.");
                Assert.IsFalse(fixture.Binding.Playing);
                Assert.IsFalse(stop.IsCompleted);
                Assert.AreEqual(0, fixture.Model.DisposeCalls);
                fixture.Model.GenerationCleanup.TrySetResult(true);
                await stop;
                try { await submission; } catch (OperationCanceledException) { }
                Assert.IsTrue(app.IsClosed);
                Assert.AreEqual(1, fixture.Model.DisposeCalls);
                Assert.IsFalse(fixture.Model.DisposedWhileGenerating);
            }
            finally { fixture.Model.GenerationCleanup.TrySetResult(true); await app.StopAsync(); }
        }

        private static async Task ProviderFaultAsync()
        {
            var fixture = new DeterministicConversationFixture();
            fixture.Model.GenerationFailure = new InvalidOperationException("deterministic provider failure");
            var app = Create(fixture);
            try
            {
                await app.StartAsync();
                try { await app.Commands.SendTextAsync("trigger failure"); }
                catch (InvalidOperationException) { }
                for (int attempt = 0; attempt < 1000 && !app.IsClosed; ++attempt)
                { app.Tick(); await Task.Delay(1); }
                try { await app.Shutdown; } catch (AggregateException) { }
                Assert.IsTrue(app.IsClosed, "A preserved provider fault must not hide successful resource release.");
                StringAssert.Contains("deterministic provider failure", app.Fault.ToString());
                Assert.IsFalse(fixture.Binding.Capturing || fixture.Binding.Playing || fixture.Binding.CaptureEnabled);
                Assert.AreEqual(1, fixture.Model.DisposeCalls);
                Assert.AreEqual(1, fixture.Recognition.DisposeCalls);
                Assert.AreEqual(1, fixture.Speech.DisposeCalls);
            }
            finally { await app.StopAsync(); }
        }

        private static async Task AbandonExerciseAsync()
        {
            var fixture = new DeterministicConversationFixture();
            fixture.Model.BlockGeneration = true;
            var app = Create(fixture);
            var owner = new MNNStandaloneLocalModelTests();
            async Task Exercise(CancellationToken token)
            {
                try
                {
                    await app.StartAsync();
                    var submission = app.Commands.SendTextAsync("test aborted coroutine");
                    try { await Task.Delay(Timeout.Infinite, token); }
                    finally { await app.StopAsync(); try { await submission; } catch (OperationCanceledException) { } }
                }
                finally { await app.StopAsync(); }
            }
            var waiting = owner.RunOwned(Exercise);
            IEnumerator teardown = null;
            try
            {
                Assert.IsTrue(waiting.MoveNext());
                await fixture.Model.GenerationStarted.Task;
                ((IDisposable)waiting).Dispose();
                teardown = owner.JoinStoppedExercise();
                Assert.IsTrue(teardown.MoveNext(), "Teardown must yield while generation cleanup remains owned.");
                for (int attempt = 0; attempt < 100 && fixture.Binding.Playing; ++attempt) await Task.Delay(1);
                Assert.IsFalse(fixture.Binding.Capturing || fixture.Binding.Playing);
                Assert.AreEqual(0, fixture.Model.DisposeCalls, "Cancellation must join inference before provider disposal.");
                fixture.Model.GenerationCleanup.TrySetResult(true);
                while (teardown.MoveNext()) await Task.Delay(1);
                Assert.IsTrue(app.IsClosed);
                Assert.AreEqual(1, fixture.Model.DisposeCalls);
                Assert.IsFalse(fixture.Model.DisposedWhileGenerating);
            }
            finally
            {
                fixture.Model.GenerationCleanup.TrySetResult(true);
                ((IDisposable)waiting).Dispose();
                if (teardown != null) ((IDisposable)teardown).Dispose();
                await app.StopAsync();
            }
        }

        private static MNNStandaloneConversation Create(DeterministicConversationFixture fixture) =>
            new MNNStandaloneConversation(fixture.Binding, fixture.DeviceCapabilities, fixture.Registrations,
                Path.GetFullPath("TestArtifacts~/SiriAICompletion/Standalone/Transcript"),
                new MNNConversationOptions { endRealtimeOnExitIntent = false });

        private static IEnumerator Run(Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, "Deterministic integration did not finish within 20 seconds.");
            task.GetAwaiter().GetResult();
        }
    }
}
