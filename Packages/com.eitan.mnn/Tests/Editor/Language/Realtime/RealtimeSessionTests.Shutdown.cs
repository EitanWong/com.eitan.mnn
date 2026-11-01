using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class RealtimeSessionTests
    {
        [Test]
        public void StopWaitsForInFlightStartBeforeClosingAndDisposingRuntime()
        {
            var runtime = new FakeRuntime{StartListening = PendingTask()};
            var session = new MNNRealtimeSession(runtime, NewOutput());
            var start = session.StartAsync();
            Assert.False(start.IsCompleted);
            AwaitCount(ref runtime.ListenCount);
            var stop = session.StopAsync();
            Assert.AreEqual(MNNRealtimeSessionState.Stopping, session.State);
            Assert.False(runtime.Calls.Contains("stop"));
            Assert.False(runtime.Calls.Contains("dispose"));
            runtime.CompleteStartListening();
            Assert.Throws<OperationCanceledException>(() => start.GetAwaiter().GetResult());
            stop.GetAwaiter().GetResult();
            Assert.That(runtime.Calls, Is.EqualTo(new[]{"prepare", "listen", "stop", "join", "dispose"}));
            Assert.AreEqual(MNNRealtimeSessionState.Stopped, session.State);
        }

        [Test]
        public void StopCancelsPendingStartBeforeJoiningAndDisposing()
        {
            var runtime = new FakeRuntime{StartListening = PendingTask(), CancelPendingStart = true};
            var session = new MNNRealtimeSession(runtime, NewOutput());
            var start = session.StartAsync();
            AwaitCount(ref runtime.ListenCount);
            var stop = session.StopAsync();
            Assert.Throws<TaskCanceledException>(() => start.GetAwaiter().GetResult());
            stop.GetAwaiter().GetResult();
            Assert.That(runtime.Calls, Is.EqualTo(new[]{"prepare", "listen", "stop", "join", "dispose"}));
            Assert.AreEqual(MNNRealtimeSessionState.Stopped, session.State);
        }

        [Test]
        public void StartupFailureIsSurfacedAndStopStillReleasesPartiallyPreparedRuntime()
        {
            var runtime = new FakeRuntime{StartListeningError = new InvalidOperationException("device start failed")};
            var session = new MNNRealtimeSession(runtime, NewOutput());
            Exception error = null;
            try
            {
                session.StartAsync().GetAwaiter().GetResult();
            }
            catch (Exception failure)
            {
                error = failure;
            }

            Assert.NotNull(error);
            Assert.IsInstanceOf<InvalidOperationException>(error);
            Assert.AreEqual("device start failed", error.Message);
            Assert.AreEqual(MNNRealtimeSessionState.Faulted, session.State);
            Assert.That(runtime.Calls, Is.EqualTo(new[]{"prepare", "listen", "stop", "join", "dispose"}));
            session.StopAsync().GetAwaiter().GetResult();
            Assert.That(runtime.Calls, Is.EqualTo(new[]{"prepare", "listen", "stop", "join", "dispose"}));
            Assert.AreEqual(MNNRealtimeSessionState.Stopped, session.State);
        }

        [Test]
        public void StartupFailureAndConcurrentStopShareOneRuntimeShutdown()
        {
            var stopListening = PendingTask();
            var runtime = new FakeRuntime{StartListeningError = new InvalidOperationException("device start failed"), StopListening = stopListening.Task};
            var session = new MNNRealtimeSession(runtime, NewOutput());
            Task start = session.StartAsync();
            AwaitCount(ref runtime.StopCount);
            Assert.True(runtime.Calls.Contains("stop"));
            Task stop = session.StopAsync();
            Assert.AreEqual(1, runtime.StopCount);
            Assert.AreEqual(0, runtime.DisposeCount);
            stopListening.TrySetResult(true);
            Assert.Throws<InvalidOperationException>(() => start.GetAwaiter().GetResult());
            stop.GetAwaiter().GetResult();
            Assert.That(runtime.Calls, Is.EqualTo(new[]{"prepare", "listen", "stop", "join", "dispose"}));
            Assert.AreEqual(1, runtime.StopCount);
            Assert.AreEqual(1, runtime.DisposeCount);
            Assert.AreEqual(MNNRealtimeSessionState.Stopped, session.State);
        }

        [Test]
        public void StopIsIdempotentAndDisposesExactlyOnce()
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            var first = session.StopAsync();
            var second = session.StopAsync();
            first.GetAwaiter().GetResult();
            Assert.True(second.IsCompleted);
            Assert.AreEqual(1, runtime.DisposeCount);
            Assert.AreEqual(1, runtime.StopCount);
            Assert.True(session.StopAsync().IsCompleted);
        }

        [Test]
        public void StopAttemptsJoinAfterDeviceFailureButNeverDisposesBeforeConfirmedQuiescence()
        {
            var deviceFailure = new FakeRuntime{StopError = new InvalidOperationException("device stop failed")};
            var deviceSession = new MNNRealtimeSession(deviceFailure, NewOutput());
            deviceSession.StartAsync().GetAwaiter().GetResult();
            Assert.Throws<InvalidOperationException>(() => deviceSession.StopAsync().GetAwaiter().GetResult());
            Assert.That(deviceFailure.Calls, Is.EqualTo(new[]{"prepare", "listen", "stop", "join", "dispose"}));
            var unjoined = new FakeRuntime{QuiescenceError = new InvalidOperationException("worker still active")};
            var unjoinedSession = new MNNRealtimeSession(unjoined, NewOutput());
            unjoinedSession.StartAsync().GetAwaiter().GetResult();
            Assert.Throws<InvalidOperationException>(() => unjoinedSession.StopAsync().GetAwaiter().GetResult());
            Assert.That(unjoined.Calls, Is.EqualTo(new[]{"prepare", "listen", "stop", "join"}));
            Assert.AreEqual(0, unjoined.DisposeCount);
            unjoined.QuiescenceError = null;
            unjoinedSession.StopAsync().GetAwaiter().GetResult();
            Assert.AreEqual(2, unjoined.StopCount);
            Assert.AreEqual(2, unjoined.QuiescenceCount);
            Assert.AreEqual(1, unjoined.DisposeCount);
            Assert.AreEqual(MNNRealtimeSessionState.Stopped, unjoinedSession.State);
        }

        [Test]
        public void ReentrantStopDuringPreparationWaitsForThePublishedStartTask()
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            Task stop = null;
            runtime.OnPrepare = _ =>
            {
                stop = session.StopAsync();
                Assert.False(stop.IsCompleted, "Preparation is still using the runtime.");
                Assert.AreEqual(0, runtime.DisposeCount);
            };
            var start = session.StartAsync();
            AwaitStopped(start);
            Assert.Throws<OperationCanceledException>(() => start.GetAwaiter().GetResult());
            Assert.NotNull(stop);
            AwaitStopped(stop);
            stop.GetAwaiter().GetResult();
            CollectionAssert.AreEqual(new[]{"prepare", "stop", "join", "dispose"}, runtime.Calls);
            Assert.AreEqual(MNNRealtimeSessionState.Stopped, session.State);
        }

        [Test]
        public void ReentrantStartDuringPreparationSharesThePublishedTask()
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            Task reentrant = null;
            runtime.OnPrepare = _ => reentrant = session.StartAsync();
            var start = session.StartAsync();
            start.GetAwaiter().GetResult();
            Assert.AreSame(start, reentrant);
            Assert.AreEqual(MNNRealtimeSessionState.Running, session.State);
            session.StopAsync().GetAwaiter().GetResult();
        }

        [Test]
        public void ReentrantStopDuringDeviceShutdownSharesThePublishedStopTask()
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            Task reentrant = null;
            runtime.OnStop = () =>
            {
                reentrant = session.StopAsync();
                Assert.False(reentrant.IsCompleted);
            };
            var stop = session.StopAsync();
            stop.GetAwaiter().GetResult();
            Assert.AreSame(stop, reentrant);
            Assert.AreEqual(1, runtime.StopCount);
            Assert.AreEqual(1, runtime.DisposeCount);
            Assert.AreEqual(MNNRealtimeSessionState.Stopped, session.State);
        }

        [TestCase(false), TestCase(true)]
        public void SynchronousRuntimePrefixDoesNotBlockTheStopGateOrStateLock(bool duringRecovery)
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var stopReturned = new ManualResetEventSlim();
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            MNNResponseId response = default;
            if (duringRecovery)
            {
                session.StartAsync().GetAwaiter().GetResult();
                response = session.BeginResponse();
                runtime.OnCancel = BlockRuntime;
            }
            else
            {
                runtime.OnPrepare = _ => BlockRuntime();
            }

            var operation = Task.Run(() => duringRecovery ? session.InterruptAsync() : session.StartAsync());
            Task stop = null;
            Task requestStop = null;
            try
            {
                Assert.True(entered.Wait(TimeSpan.FromSeconds(3)), "Runtime was not entered.");
                requestStop = Task.Run(() =>
                {
                    stop = session.StopAsync();
                    stopReturned.Set();
                });
                Assert.True(stopReturned.Wait(TimeSpan.FromSeconds(3)), "Runtime retained the session lock and blocked stopping.");
                Assert.AreEqual(MNNRealtimeSessionState.Stopping, session.State);
                Assert.False(stop.IsCompleted, "Shutdown must wait for the blocked operation.");
                if (duringRecovery)
                    Assert.False(session.Output.Accepts(response));
                Assert.AreEqual(0, runtime.DisposeCount);
            }
            finally
            {
                release.Set();
                AwaitStopped(operation);
                if (requestStop != null)
                {
                    AwaitStopped(requestStop);
                    requestStop.GetAwaiter().GetResult();
                }

                var cleanup = stop ?? session.StopAsync();
                AwaitStopped(cleanup);
                cleanup.GetAwaiter().GetResult();
            }

            if (duringRecovery)
                operation.GetAwaiter().GetResult();
            else
                Assert.Throws<OperationCanceledException>(() => operation.GetAwaiter().GetResult());
            Assert.AreEqual(MNNRealtimeSessionState.Stopped, session.State);
            void BlockRuntime()
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)), "Test did not release the runtime.");
            }
        }

        [Test]
        public void ThrowingCancellationCallbackStillJoinsAndDisposesBeforeReportingFailure()
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            var expected = new InvalidOperationException("cancellation observer failed");
            runtime.OnPrepare = token => token.Register(() => throw expected);
            session.StartAsync().GetAwaiter().GetResult();
            Task stop = null;
            Assert.DoesNotThrow(() => stop = session.StopAsync());
            AwaitStopped(stop);
            var failure = Assert.Throws<InvalidOperationException>(() => stop.GetAwaiter().GetResult());
            var cancellationFailure = failure.InnerException as AggregateException;
            Assert.NotNull(cancellationFailure);
            CollectionAssert.Contains(cancellationFailure.InnerExceptions, expected);
            Assert.AreSame(cancellationFailure, session.Fault);
            Assert.AreEqual(1, runtime.StopCount);
            Assert.AreEqual(1, runtime.QuiescenceCount);
            Assert.AreEqual(1, runtime.DisposeCount);
            Assert.AreEqual(MNNRealtimeSessionState.Faulted, session.State);
        }

        [Test]
        public void CancellationCallbackDoesNotBlockStopCallerOrAllowPrematureDisposal()
        {
            using var callbackEntered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var stopReturned = new ManualResetEventSlim();
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            Task reentrant = null;
            runtime.OnPrepare = token => token.Register(() =>
            {
                reentrant = session.StopAsync();
                callbackEntered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)), "Test did not release the cancellation callback.");
            });
            session.StartAsync().GetAwaiter().GetResult();
            Task stop = null;
            var request = Task.Run(() =>
            {
                stop = session.StopAsync();
                stopReturned.Set();
            });
            try
            {
                Assert.True(stopReturned.Wait(TimeSpan.FromSeconds(3)), "Stop caller was blocked by a cancellation callback.");
                Assert.True(callbackEntered.Wait(TimeSpan.FromSeconds(3)));
                Assert.AreSame(stop, reentrant);
                Assert.False(stop.IsCompleted);
                Assert.AreEqual(0, runtime.DisposeCount);
                Assert.AreEqual(0, runtime.StopCount, "Runtime shutdown must follow cancellation callbacks.");
                Assert.AreEqual(MNNRealtimeSessionState.Stopping, session.State);
            }
            finally
            {
                release.Set();
                AwaitStopped(request);
                request.GetAwaiter().GetResult();
                AwaitStopped(stop);
                stop.GetAwaiter().GetResult();
            }

            Assert.AreEqual(1, runtime.DisposeCount);
            Assert.AreEqual(MNNRealtimeSessionState.Stopped, session.State);
        }

        [Test]
        public void PrecancelledStartDoesNotPrepareOrListenAndStillSettlesCleanup()
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            var start = session.StartAsync(new CancellationToken(true));
            AwaitStopped(start);
            Assert.Throws<OperationCanceledException>(() => start.GetAwaiter().GetResult());
            Assert.True(start.IsCanceled);
            CollectionAssert.AreEqual(new[]{"stop", "join", "dispose"}, runtime.Calls);
            Assert.AreEqual(MNNRealtimeSessionState.Faulted, session.State);
            var stop = session.StopAsync();
            AwaitStopped(stop);
            stop.GetAwaiter().GetResult();
            Assert.AreEqual(1, runtime.DisposeCount);
            Assert.AreEqual(MNNRealtimeSessionState.Stopped, session.State);
        }
    }
}
