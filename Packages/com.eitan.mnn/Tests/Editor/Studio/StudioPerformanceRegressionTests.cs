using System;
using System.IO;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class StudioPerformanceRegressionTests
    {
        private sealed class TurnBackend : IMNNStudioBackend
        {
            internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim Continue = new ManualResetEventSlim();
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                Entered.Set();
                Continue.Wait();
                return new MNNStudioResult { Text = request.TurnLanguage + "/" + request.TurnSpeaker + "/" +
                    request.TurnQuestions[0] + "/" + request.SpeakerReference + "/" + request.GestureFeatures + "/" +
                    request.VisualFrames[0] + "/" + request.OcrCroppedLine };
            }
            public void Dispose() { Entered.Dispose(); Continue.Dispose(); }
        }

        [Test]
        public void SessionPreservesTaskInputsAndOwnsMutableRequestArrays()
        {
            var backend = new TurnBackend();
            var session = new MNNStudioSession(backend);
            try
            {
                var request = new MNNStudioRequest { Prompt = "turn", TurnLanguage = "zh", TurnSpeaker = "alice",
                    TurnQuestions = new[] { "eou" }, SpeakerReference = "speaker.wav", GestureFeatures = "pose.json",
                    VisualFrames = new[] { 42f }, OcrCroppedLine = true };
                session.Send(request);
                Assert.True(backend.Entered.Wait(3000));
                request.TurnQuestions[0] = "changed";
                request.VisualFrames[0] = -1;
                backend.Continue.Set();
                Assert.True(SpinWait.SpinUntil(() => { session.Poll(); return !session.Busy; }, 3000));
                Assert.IsNull(session.Error);
                Assert.AreEqual("zh/alice/eou/speaker.wav/pose.json/42/True", session.Result.Text);
            }
            finally { backend.Continue.Set(); session.Dispose(); }
        }

        [Test]
        public void FactoryLoadsAndReleasesOnWorkersAndCancellationSkipsInference()
        {
            int caller = Thread.CurrentThread.ManagedThreadId, loader = caller, disposer = caller;
            var loaded = new ManualResetEventSlim();
            var resume = new ManualResetEventSlim();
            var disposed = new ManualResetEventSlim();
            int runs = 0;
            var backend = new DelegateBackend(() => { ++runs; return new MNNStudioResult(); },
                () => { disposer = Thread.CurrentThread.ManagedThreadId; disposed.Set(); });
            var session = new MNNStudioSession(() =>
            {
                loader = Thread.CurrentThread.ManagedThreadId;
                loaded.Set();
                if (!resume.Wait(3000)) throw new TimeoutException();
                return backend;
            });
            try
            {
                session.Send(new MNNStudioRequest { Prompt = "test" });
                Assert.True(loaded.Wait(3000));
                Assert.AreNotEqual(caller, loader);
                session.Dispose();
                resume.Set();
                Assert.True(SpinWait.SpinUntil(() => { session.Poll(); return !session.Busy; }, 3000));
                Assert.True(disposed.IsSet);
                Assert.AreNotEqual(caller, disposer);
                Assert.AreEqual(0, runs);
            }
            finally { resume.Set(); session.Dispose(); loaded.Dispose(); resume.Dispose(); disposed.Dispose(); }
        }
        private sealed class DelegateBackend : IMNNStudioBackend
        {
            private readonly Func<MNNStudioResult> _run;
            private readonly Action _dispose;
            internal DelegateBackend(Func<MNNStudioResult> run, Action dispose) { _run = run; _dispose = dispose; }
            public MNNStudioResult Run(MNNStudioRequest request) => _run();
            public void Dispose() => _dispose();
        }

        [Test]
        public void ModelConfigurationIsASnapshotUntilExplicitRefresh()
        {
            string root = Path.GetFullPath(Path.Combine("TestArtifacts~", "StudioPerformance", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            try
            {
                string config = Path.Combine(root, "config.json");
                File.WriteAllText(config, "{\"model_type\":\"piper\"}");
                var model = new MNNStudioModel("snapshot", root, MNNStudioTask.SpeechSynthesis, 0);
                Assert.True(model.IsPiper);
                File.WriteAllText(config, "{}");
                Assert.True(model.IsPiper, "GUI queries use the discovery snapshot; Refresh replaces the model.");
                Assert.False(new MNNStudioModel("snapshot", root, MNNStudioTask.SpeechSynthesis, 0).IsPiper);
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
