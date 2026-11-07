using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class RealtimeDeviceSessionTests
    {
        [Test]
        public void NegotiatesOutputAfterCaptureAndRebindsADeferredRouteChange()
        {
            var binding = new Binding { CaptureStartsImmediately = false };
            var clock = new Clock();
            clock.OnDelay = () => { binding.Capturing = true; binding.Rate = 44100; };
            var session = new MNNRealtimeDeviceSession(binding, new MNNRealtimeDeviceSessionOptions(), clock);
            var capture = session.Capture;
            var output = session.OpenAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(44100, output.Format.SampleRate);
            Assert.AreEqual(2, binding.BindCount);
            Assert.AreSame(capture, session.Capture);
            Assert.True(session.IsReady);
            session.SetCaptureEnabled(true); Assert.True(binding.CaptureEnabled);
            session.CloseAsync().GetAwaiter().GetResult();
            Assert.False(binding.CaptureEnabled); Assert.True(session.IsClosed);
            session.ResetForRetry(); Assert.False(session.IsClosed);
            Assert.AreSame(capture, session.Capture);
        }

        [Test]
        public void RouteLossStopsBeforeAnyPresentationAcknowledgment()
        {
            var binding = new Binding(); var session = new MNNRealtimeDeviceSession(binding);
            var output = session.OpenAsync(CancellationToken.None).GetAwaiter().GetResult();
            var response = output.BeginResponse(); output.TryRegisterSegment(response, 0, "spoken");
            var pcm = new float[output.MaximumSamplesPerChunk]; Array.Fill(pcm, .25f);
            output.TryEnqueue(pcm, 0, pcm.Length, new MNNSpeechChunkInfo(response, 0, 0, true));
            output.Render(pcm, 0, pcm.Length); binding.RenderedFrames = 480000;
            binding.Audible = false; session.Tick();
            Assert.True(session.IsClosed); Assert.NotNull(session.Fault);
            output.DrainReceipts(256); Assert.False(output.GetSegment(0).IsPresented);
            session.CloseAsync().GetAwaiter().GetResult();
        }

        [Test]
        public void StopAttemptsBothEndpointsAndRetriesOnlyFailedOwnership()
        {
            var binding = new Binding(); var session = new MNNRealtimeDeviceSession(binding);
            session.OpenAsync(CancellationToken.None).GetAwaiter().GetResult();
            binding.FailStopCapture = true;
            Assert.Throws<AggregateException>(() => session.CloseAsync().GetAwaiter().GetResult());
            Assert.AreEqual(1, binding.OutputStops);
            Assert.Throws<InvalidOperationException>(() => session.ResetForRetry());
            binding.FailStopCapture = false;
            session.CloseAsync().GetAwaiter().GetResult();
            Assert.AreEqual(2, binding.InputStops); Assert.AreEqual(1, binding.OutputStops);
            session.ResetForRetry(); Assert.AreEqual(1, binding.Resets);
        }

        [Test]
        public void WarmupTimeoutClosesBothDevicesAndRequiresExplicitReset()
        {
            var binding = new Binding { CaptureStartsImmediately = false };
            var session = new MNNRealtimeDeviceSession(binding,
                new MNNRealtimeDeviceSessionOptions { CaptureWarmupSeconds = .02 }, new Clock());
            Assert.Throws<TimeoutException>(() => session.OpenAsync(CancellationToken.None).GetAwaiter().GetResult());
            session.CloseAsync().GetAwaiter().GetResult();
            Assert.AreEqual(1, binding.InputStops); Assert.AreEqual(1, binding.OutputStops);
            Assert.Throws<InvalidOperationException>(() => session.OpenAsync(CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void HalfDuplexPolicyKeepsUserCaptureContinuousAndStopsUnprotectedCues()
        {
            var policy = new MNNRealtimeCapturePolicy();
            policy.RequestCapture(true);
            Assert.True(policy.ShouldStopCue(false));
            Assert.False(policy.TryBeginCue(false, 500));
            Assert.True(policy.CaptureRequested, "Echo health never withdraws input ownership.");
            policy.ProtectCaptureFromSounds(false);
            Assert.True(policy.TryBeginCue(false, 500));
            Assert.True(policy.SpeakerAudible(false, 0));
            policy.Reset(); Assert.False(policy.CaptureRequested);
        }

        [Test]
        public void EchoQualificationExpiresUsingAValueOnlyWorkerRead()
        {
            var health = new MNNRealtimeEchoHealth();
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            health.PublishAt(true, now, 50);
            Assert.True(health.IsHealthyAt(now));
            Assert.False(health.IsHealthyAt(now + System.Diagnostics.Stopwatch.Frequency));
            health.PublishAt(false, now); Assert.False(health.IsHealthyAt(now));
        }

        [UnityTest]
        public IEnumerator CloseCancelsPendingPermissionAndWaitsItsStartupCleanup()
        {
            var cleanup = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var binding = new Binding { BlockPermission = true, PermissionCleanup = cleanup.Task };
            var session = new MNNRealtimeDeviceSession(binding);
            int controlThread = Thread.CurrentThread.ManagedThreadId;
            var opening = session.OpenAsync(CancellationToken.None);
            Assert.False(opening.IsCompleted);
            var closing = session.CloseAsync();
            try
            {
                Assert.False(closing.IsCompleted, "Closing must join the pending permission cleanup.");
                Assert.Throws<InvalidOperationException>(() => session.ResetForRetry(),
                    "Startup retains session ownership until its control-thread cleanup completes.");
            }
            finally { cleanup.TrySetResult(true); }
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            while (!closing.IsCompleted && elapsed.ElapsedMilliseconds < 2000) yield return null;
            Assert.True(closing.IsCompleted, "Closing must complete after startup cleanup releases ownership.");
            closing.GetAwaiter().GetResult();
            Assert.True(opening.IsCanceled || opening.IsFaulted);
            Assert.AreEqual(controlThread, binding.PermissionCleanupThread,
                "Permission cleanup must retain the application's device control thread.");
            Assert.True(session.IsClosed);
            Assert.Zero(binding.BindCount);
            Assert.Zero(binding.InputStops);
            session.ResetForRetry();
            Assert.False(session.HasBeenUsed);
        }

        [Test]
        public void StartupAndCleanupFailuresAreBothReportedAndOwnershipCanRetry()
        {
            var binding = new Binding { FailCaptureStart = true, FailStopCapture = true };
            var session = new MNNRealtimeDeviceSession(binding);
            var error = Assert.Throws<AggregateException>(() => session.OpenAsync(CancellationToken.None).GetAwaiter().GetResult());
            StringAssert.Contains("capture start", error.ToString());
            StringAssert.Contains("stop input", error.ToString());
            Assert.AreEqual(1, binding.OutputStops);
            binding.FailStopCapture = false;
            session.CloseAsync().GetAwaiter().GetResult(); session.ResetForRetry();
            Assert.AreEqual(2, binding.InputStops);
        }

        private sealed class Clock : IMNNRealtimeDeviceClock
        {
            public double Seconds { get; private set; }
            internal Action OnDelay;
            public Task DelayAsync(int milliseconds, CancellationToken token)
            { token.ThrowIfCancellationRequested(); Seconds += milliseconds / 1000.0; OnDelay?.Invoke(); return Task.CompletedTask; }
        }
        private sealed class Binding : MNNRealtimeDeviceBinding, IMNNAudioCaptureSource
        {
            internal bool Capturing, CaptureStartsImmediately = true, CaptureEnabled, Audible = true, Playing;
            internal bool FailStopCapture;
            internal bool BlockPermission, FailCaptureStart;
            internal Task PermissionCleanup;
            internal int PermissionCleanupThread;
            internal int Rate = 48000, BindCount, InputStops, OutputStops, Resets;
            internal long RenderedFrames;
            public override IMNNAudioCaptureSource Capture => this;
            public override MNNPcmFormat OutputFormat => new MNNPcmFormat(Rate, 1);
            public override MNNRealtimeDeviceSnapshot ReadState() => new MNNRealtimeDeviceSnapshot(
                active: true, inputActive: true, capturing: Capturing, captureStarting: !Capturing,
                outputActive: true, outputRunning: true, playing: Playing, audible: Audible,
                routeMatches: true, outputFormat: OutputFormat, renderedFrames: RenderedFrames);
            public override void ValidateFresh() { }
            public override async Task RequestPermissionAsync(CancellationToken token)
            {
                if (!BlockPermission) { token.ThrowIfCancellationRequested(); return; }
                try { await Task.Delay(Timeout.Infinite, token); }
                finally
                {
                    if (PermissionCleanup != null) await PermissionCleanup;
                    PermissionCleanupThread = Thread.CurrentThread.ManagedThreadId;
                }
            }
            public override void StartCapture(Action<MNNPcmFormat, int> negotiated)
            { if (FailCaptureStart) throw new InvalidOperationException("capture start"); negotiated(CaptureFormat, MaximumCaptureSamples); Capturing = CaptureStartsImmediately; }
            public override void BindOutput(MNNRealtimeOutputPipeline output, MNNPcmFormat captureFormat, int capacity) => ++BindCount;
            public override void StartPlayback() => Playing = true;
            public override void SetCaptureEnabled(bool enabled) => CaptureEnabled = enabled;
            public override void StopCapture() { ++InputStops; if (FailStopCapture) throw new InvalidOperationException("stop input"); Capturing = false; }
            public override void StopPlayback() { ++OutputStops; Playing = false; }
            public override void ResetForRetry() => ++Resets;
            public MNNPcmFormat CaptureFormat => new MNNPcmFormat(16000, 1);
            public int MaximumCaptureSamples => 160;
            public long DroppedCaptureBlocks => 0;
            public bool HasPendingCaptureFrames => false;
            public bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count)
            { info = default; count = 0; return false; }
        }
    }
}
