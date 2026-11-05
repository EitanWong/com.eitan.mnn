using System;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;
using NUnit.Framework;

namespace MNN.Examples.Tests
{
    /// <summary>An application must distinguish preserved faults from resources that still require cleanup.</summary>
    public sealed class MNNStandaloneSessionReleaseTests
    {
        [Test]
        public void ReportedFaultRemainsVisibleAfterAllResourcesWereReleased()
        {
            var runtime = new ReleaseRuntime();
            var session = Create(runtime);
            Assert.IsFalse(session.ResourcesReleased);
            session.StartAsync().GetAwaiter().GetResult();
            var original = new InvalidOperationException("original failure");
            Assert.Throws<InvalidOperationException>(() => session.ReportFaultAsync(original).GetAwaiter().GetResult());
            Assert.IsTrue(session.ResourcesReleased);
            Assert.AreSame(original, session.Fault);
            Assert.AreEqual(1, runtime.DisposeCalls);
            Assert.Throws<InvalidOperationException>(() => session.StopAsync().GetAwaiter().GetResult());
            Assert.IsTrue(session.ResourcesReleased);
            Assert.AreEqual(1, runtime.DisposeCalls);
        }

        [Test]
        public void FailedDisposeRemainsOwnedUntilASecondCloseActuallyReleasesIt()
        {
            var runtime = new ReleaseRuntime { FailDispose = true };
            var session = Create(runtime);
            session.StartAsync().GetAwaiter().GetResult();
            Assert.Throws<InvalidOperationException>(() => session.StopAsync().GetAwaiter().GetResult());
            Assert.IsFalse(session.ResourcesReleased);
            Assert.AreEqual(1, runtime.DisposeCalls);
            runtime.FailDispose = false;
            session.StopAsync().GetAwaiter().GetResult();
            Assert.IsTrue(session.ResourcesReleased);
            Assert.AreEqual(2, runtime.DisposeCalls);
        }

        [Test]
        public void FailedQuiescenceNeverReportsReleasedOrDisposesLiveRuntime()
        {
            var runtime = new ReleaseRuntime { FailQuiescence = true };
            var session = Create(runtime);
            session.StartAsync().GetAwaiter().GetResult();
            Assert.Throws<InvalidOperationException>(() => session.StopAsync().GetAwaiter().GetResult());
            Assert.IsFalse(session.ResourcesReleased);
            Assert.AreEqual(0, runtime.DisposeCalls);
            runtime.FailQuiescence = false;
            session.StopAsync().GetAwaiter().GetResult();
            Assert.IsTrue(session.ResourcesReleased);
            Assert.AreEqual(1, runtime.DisposeCalls);
        }

        private static MNNRealtimeSession Create(ReleaseRuntime runtime) => new MNNRealtimeSession(runtime,
            new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 160));

        private sealed class ReleaseRuntime : IMNNRealtimeSessionRuntime
        {
            internal bool FailDispose, FailQuiescence;
            internal int DisposeCalls;
            public Task PrepareAsync(CancellationToken token) => Task.CompletedTask;
            public Task StartListeningAsync(CancellationToken token) => Task.CompletedTask;
            public Task RequestCancellationAsync(MNNResponseId response) => Task.CompletedTask;
            public Task WaitForInferenceQuiescenceAsync(MNNResponseId response) => Task.CompletedTask;
            public Task RestoreContextAsync(MNNResponseId response) => Task.CompletedTask;
            public Task StopListeningAsync() => Task.CompletedTask;
            public Task WaitForQuiescenceAsync() => FailQuiescence ? Task.FromException(new InvalidOperationException("live runtime")) : Task.CompletedTask;
            public Task DisposeAsync()
            {
                ++DisposeCalls;
                return FailDispose ? Task.FromException(new InvalidOperationException("dispose failure")) : Task.CompletedTask;
            }
        }
    }
}
