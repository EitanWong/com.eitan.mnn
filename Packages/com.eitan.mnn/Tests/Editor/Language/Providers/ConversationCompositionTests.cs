using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
namespace MNN.Unity.Tests
{
    public sealed class ConversationCompositionTests
    {
        private sealed class Capture : IMNNAudioCaptureSource
        {
            public MNNPcmFormat CaptureFormat => new MNNPcmFormat(16000, 1);
            public int MaximumCaptureSamples => 512;
            public long DroppedCaptureBlocks => 0;
            public bool HasPendingCaptureFrames => false;
            public bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count) { info = default; count = 0; return false; }
        }
        private sealed class Recognition : IMNNConversationAsrProvider
        {
            internal int Prepared, Disposed;
            internal bool FailPreparation, FailRelease;
            internal TaskCompletionSource<bool> Block;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public MNNRealtimeCapabilities Capabilities => Caps;
            public Task PrepareAsync(CancellationToken token) { ++Prepared; if (FailPreparation) throw new InvalidOperationException("fixture load"); return Task.CompletedTask; }
            public async Task<string> TranscribeAsync(float[] audio, int sampleRate, CancellationToken token) { Entered.TrySetResult(true); if (Block != null) await Block.Task.ConfigureAwait(false); token.ThrowIfCancellationRequested(); return "heard " + audio.Length; }
            public void Dispose() { ++Disposed; if (FailRelease) throw new InvalidOperationException("fixture release"); }
        }
        private static MNNRealtimeCapabilities Caps => new MNNRealtimeCapabilities(MNNRealtimeFeature.SpeechRecognition, "composition fixture", "Managed behavior only", execution: MNNRealtimeExecution.Managed);
        private sealed class PreparingProvider : IMNNConversationProvider
        {
            internal MNNRealtimeCapabilities Actual = Caps;
            public MNNRealtimeCapabilities Capabilities => Actual;
            public Task PrepareAsync(CancellationToken token)
            {
                Actual = new MNNRealtimeCapabilities(MNNRealtimeFeature.None, "unavailable after load", null,
                    "The loaded model cannot provide the registered task.", execution: MNNRealtimeExecution.Managed);
                return Task.CompletedTask;
            }
            public void Dispose() { }
        }
        [Test]
        public void PreparedCapabilitiesMustStillMatchBeforePublishingAUsableStage()
        {
            var provider = new PreparingProvider(); int prepared = 0;
            var registration = new MNNConversationStageRegistration(Caps, () => provider) { Prepared = () => ++prepared };
            Assert.Throws<InvalidOperationException>(() => registration.PrepareAsync(provider, CancellationToken.None).GetAwaiter().GetResult());
            Assert.Zero(prepared, "A preflight claim that is lost during native loading cannot become an active route stage.");
        }

        [Test]
        public void OptionalTranscriptionReusesPreparedProviderAndReleasesOnce()
        {
            var model = new Recognition(); int created = 0;
            var service = new MNNConversationTranscriptionService(new MNNConversationStageRegistration(Caps, () => { ++created; return model; }));
            Assert.AreEqual("heard 512", service.TranscribeAsync(new float[512], CancellationToken.None).Result);
            Assert.AreEqual("heard 1024", service.TranscribeAsync(new float[1024], CancellationToken.None).Result);
            Assert.AreEqual(1, created); Assert.AreEqual(1, model.Prepared);
            service.ReleaseAsync().GetAwaiter().GetResult(); service.ReleaseAsync().GetAwaiter().GetResult(); Assert.AreEqual(1, model.Disposed);
            Assert.Throws<ObjectDisposedException>(() => service.TranscribeAsync(new float[8], CancellationToken.None).GetAwaiter().GetResult());
        }
        [Test]
        public void CancelledWaiterCannotReleaseActiveTranscriptionAndShutdownWaits()
        {
            var model = new Recognition { Block = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) };
            var service = new MNNConversationTranscriptionService(new MNNConversationStageRegistration(Caps, () => model));
            var first = service.TranscribeAsync(new float[512], CancellationToken.None); Assert.True(model.Entered.Task.Wait(2000));
            using (var cancel = new CancellationTokenSource())
            { var waiting = service.TranscribeAsync(new float[8], cancel.Token); cancel.Cancel(); Assert.Catch<OperationCanceledException>(() => waiting.GetAwaiter().GetResult()); }
            var release = service.ReleaseAsync(); Assert.False(release.IsCompleted); Assert.AreEqual(0, model.Disposed);
            model.Block.TrySetResult(true); Assert.AreEqual("heard 512", first.Result); release.GetAwaiter().GetResult(); Assert.AreEqual(1, model.Disposed);
        }
        [Test]
        public void FailedPreparationReleasesFailedModelAndRetryUsesNewInstance()
        {
            var failed = new Recognition { FailPreparation = true }; var good = new Recognition(); int created = 0;
            var service = new MNNConversationTranscriptionService(new MNNConversationStageRegistration(Caps, () => ++created == 1 ? failed : good));
            Assert.Throws<InvalidOperationException>(() => service.TranscribeAsync(new float[8], CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual(1, failed.Disposed); Assert.AreEqual("heard 8", service.TranscribeAsync(new float[8], CancellationToken.None).Result);
            service.ReleaseAsync().GetAwaiter().GetResult(); Assert.AreEqual(1, good.Disposed);
        }
        [Test]
        public void CapabilityMismatchPreventsInstallationAndPreparation()
        {
            var model = new Recognition(); int installs = 0;
            var different = new MNNRealtimeCapabilities(MNNRealtimeFeature.TextInput, "other", "fixture", execution: MNNRealtimeExecution.Managed);
            var registration = new MNNConversationStageRegistration(different, () => model, token => { ++installs; return Task.CompletedTask; });
            Assert.Throws<InvalidOperationException>(() => registration.PrepareAsync(model, CancellationToken.None).GetAwaiter().GetResult()); Assert.AreEqual(0, installs + model.Prepared);
        }
        [Test]
        public void FailedRouteCreationCleansAliasedProviderOnlyOnce()
        {
            var model = new Recognition(); var registration = new MNNConversationStageRegistration(Caps, () => model);
            var context = new MNNConversationPorts(new Capture(), () => null);
            var factory = new MNNConversationRuntimeFactory(context, new MNNConversationOptions { speechOutput = false }, new MNNConversationRegistrations { Model = registration, Asr = registration });
            var report = new MNNRealtimeReport(MNNRealtimeRoute.CascadedSpeech, MNNRealtimeMode.InterruptibleConversation, MNNSpeechRoute.IndependentTts,
                0, 0, 0, Caps, Caps, null);
            Assert.Throws<MNNRealtimeRouteUnavailableException>(() => factory.CreateAsync(report, null, CancellationToken.None)); Assert.AreEqual(1, model.Disposed);
        }
        [Test]
        public void CreationCleanupAggregatesFailureAndDeduplicatesOwners()
        {
            var model = new Recognition { FailRelease = true }; var error = new InvalidOperationException("creation");
            var aggregate = Assert.Throws<AggregateException>(() => MNNConversationRuntimeFactory.DisposeUntransferredProviders(new[] { model, model }, error));
            Assert.AreEqual(2, aggregate.InnerExceptions.Count); Assert.That(aggregate.InnerExceptions[0], Is.SameAs(error)); Assert.AreEqual(1, model.Disposed);
        }
        [Test]
        public void StaticCleanupFailureExposesItsRetryableOwners()
        {
            var model = new Recognition { FailRelease = true };
            var failure = Assert.Throws<AggregateException>(() => MNNConversationRuntimeFactory.DisposeUntransferredProviders(new[] { model }, new InvalidOperationException("creation")));
            var cleanup = MNNConversationRuntimeFactory.GetFailedCreationCleanup(failure);
            Assert.AreEqual(1, cleanup.PendingProviderCount);
            model.FailRelease = false;
            cleanup.Dispose();
            cleanup.Dispose();
            Assert.AreEqual(0, cleanup.PendingProviderCount);
            Assert.AreEqual(2, model.Disposed);
        }
        [Test]
        public void FailedCreationRetainsFailedOwnersForFinalReleaseRetry()
        {
            var failed = new Recognition { FailRelease = true };
            var successful = new Recognition();
            var context = new MNNConversationPorts(new Capture(), () => null);
            var factory = new MNNConversationRuntimeFactory(context, new MNNConversationOptions { speechOutput = false },
                new MNNConversationRegistrations
                {
                    Model = new MNNConversationStageRegistration(Caps, () => failed),
                    Asr = new MNNConversationStageRegistration(Caps, () => successful)
                });
            var report = new MNNRealtimeReport(MNNRealtimeRoute.CascadedSpeech, MNNRealtimeMode.InterruptibleConversation,
                MNNSpeechRoute.IndependentTts, 0, 0, 0, Caps, Caps, null);
            Assert.Throws<AggregateException>(() => factory.CreateAsync(report, null, CancellationToken.None));
            Assert.AreEqual(1, failed.Disposed);
            Assert.AreEqual(1, successful.Disposed);
            failed.FailRelease = false;
            factory.ReleaseAsync().GetAwaiter().GetResult();
            factory.ReleaseAsync().GetAwaiter().GetResult();
            Assert.AreEqual(2, failed.Disposed, "Failed creation must retain its failed resource until final cleanup succeeds.");
            Assert.AreEqual(1, successful.Disposed, "Successful cleanup must not run twice.");
        }
        [Test]
        public void StagePlanEnablesOnlyTheTasksTheRouteNeeds()
        {
            var config = new MNNConversationOptions { speechOutput = false, endRealtimeOnExitIntent = false };
            var plan = MNNConversationStagePlan.ForRoute(config, MNNRealtimeRoute.AudioTextToSpeech, providerVad: true, providerTurn: true);
            Assert.True(plan.TurnInput && plan.Vad && plan.Turn); Assert.False(plan.Asr || plan.Tts);
            plan = MNNConversationStagePlan.ForRoute(config, MNNRealtimeRoute.AudioTextToSpeech, turnRequiresTranscript: true); Assert.True(plan.Asr);
            plan = MNNConversationStagePlan.ForRoute(config, MNNRealtimeRoute.CascadedSpeech); Assert.True(plan.Asr);
        }
    }
}
