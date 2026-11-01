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
        public void ReportedProducerFaultSilencesBeforeShutdownAndRetainsTheFirstError()
        {
            var releaseStop = PendingTask();
            var runtime = new FakeRuntime{StopListening = releaseStop.Task};
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            var response = session.BeginResponse();
            var error = new TimeoutException("TTS publication stalled");
            var pcm = new float[80];
            Array.Fill(pcm, .5f);
            Assert.True(session.Output.TryRegisterSegment(response, 0, "Hello."));
            Assert.True(session.Output.TryEnqueue(pcm, 0, pcm.Length, new MNNSpeechChunkInfo(response, 0, 0, true)));
            Task fault = null;
            try
            {
                fault = session.ReportFaultAsync(response, error);
                Assert.AreEqual(MNNRealtimeSessionState.Stopping, session.State);
                Assert.AreSame(error, session.Fault);
                Assert.AreSame(fault, session.ReportFaultAsync(response, new Exception("late duplicate")));
                Assert.AreSame(fault, session.ReportFaultAsync(new Exception("secondary device failure")));
                Assert.AreSame(fault, session.StopAsync());
                Assert.Throws<InvalidOperationException>(() => session.BeginResponse());
                Assert.False(session.CompleteResponse(response));
                session.Output.Render(pcm, 0, pcm.Length);
                Assert.That(pcm, Is.All.EqualTo(0f));
                AwaitCount(ref runtime.StopCount);
                Assert.False(fault.IsCompleted);
                Assert.AreEqual(0, runtime.DisposeCount);
            }
            finally
            {
                releaseStop.TrySetResult(true);
                if (fault != null)
                    AwaitStopped(fault);
            }

            var failure = Assert.Throws<InvalidOperationException>(() => fault.GetAwaiter().GetResult());
            Assert.AreSame(error, failure.InnerException);
            Assert.AreSame(error, session.Fault);
            Assert.AreEqual(MNNRealtimeSessionState.Faulted, session.State);
            Assert.AreEqual(1, runtime.DisposeCount);
            Assert.AreSame(fault, session.StopAsync(), "A disposed runtime must never be shut down twice.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LateOrForeignResponseFaultCannotStopTheActiveResponse(bool foreign)
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            var old = session.BeginResponse();
            session.InterruptAsync().GetAwaiter().GetResult();
            // Even before a replacement starts, the interrupted producer no longer owns the session.
            session.ReportFaultAsync(old, new Exception("cancelled producer")).GetAwaiter().GetResult();
            var current = session.BeginResponse();
            var identity = foreign ? NewOutput().BeginResponse() : old;
            session.ReportFaultAsync(identity, new Exception("late producer")).GetAwaiter().GetResult();
            Assert.AreEqual(MNNRealtimeSessionState.Running, session.State);
            Assert.IsNull(session.Fault);
            Assert.True(session.Output.Accepts(current));
            Assert.AreEqual(0, runtime.StopCount);
            session.StopAsync().GetAwaiter().GetResult();
        }

        [Test]
        public void ReportedFaultDuringPreparationCancelsAndJoinsBeforeDisposing()
        {
            var runtime = new FakeRuntime{StartListening = PendingTask(), CancelPendingStart = true};
            var session = new MNNRealtimeSession(runtime, NewOutput());
            var start = session.StartAsync();
            AwaitCount(ref runtime.ListenCount);
            var error = new Exception("device disconnected during startup");
            var fault = session.ReportFaultAsync(error);
            AwaitStopped(start);
            Assert.Throws<TaskCanceledException>(() => start.GetAwaiter().GetResult());
            AwaitStopped(fault);
            Assert.AreSame(error, Assert.Throws<InvalidOperationException>(() => fault.GetAwaiter().GetResult()).InnerException);
            CollectionAssert.AreEqual(new[]{"prepare", "listen", "stop", "join", "dispose"}, runtime.Calls);
            Assert.AreEqual(MNNRealtimeSessionState.Faulted, session.State);
        }

        [Test]
        public void ReentrantFaultDuringNormalShutdownUsesThePublishedTask()
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            var error = new Exception("capture cleanup failed");
            Task reported = null;
            runtime.OnStop = () => reported = session.ReportFaultAsync(error);
            var stop = session.StopAsync();
            AwaitStopped(stop);
            Assert.AreSame(stop, reported);
            Assert.AreSame(error, Assert.Throws<InvalidOperationException>(() => stop.GetAwaiter().GetResult()).InnerException);
            Assert.AreEqual(1, runtime.StopCount);
            Assert.AreEqual(1, runtime.DisposeCount);
        }

        [Test]
        public void ReportedFaultDoesNotDisposeUnjoinedResourcesAndSurvivesShutdownRetry()
        {
            var joinError = new Exception("worker still active");
            var runtime = new FakeRuntime{QuiescenceError = joinError};
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            var error = new Exception("capture processing failed");
            var fault = session.ReportFaultAsync(error);
            AwaitStopped(fault);
            var failure = Assert.Throws<InvalidOperationException>(() => fault.GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[]{error, joinError}, ((AggregateException)failure.InnerException).Flatten().InnerExceptions);
            Assert.AreEqual(0, runtime.DisposeCount);
            runtime.QuiescenceError = null;
            var retry = session.StopAsync();
            AwaitStopped(retry);
            Assert.AreNotSame(fault, retry);
            Assert.AreSame(error, Assert.Throws<InvalidOperationException>(() => retry.GetAwaiter().GetResult()).InnerException);
            Assert.AreSame(error, session.Fault);
            Assert.AreEqual(1, runtime.DisposeCount);
            Assert.AreEqual(2, runtime.QuiescenceCount);
            Assert.AreEqual(MNNRealtimeSessionState.Faulted, session.State);
        }

        [Test]
        public void ReportedFaultWaitsForRecoveryBeforeGlobalShutdown()
        {
            var runtime = new FakeRuntime{Quiescence = PendingTask()};
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            session.BeginResponse();
            var recovery = session.InterruptAsync();
            AwaitCount(ref runtime.InferenceQuiescenceCount);
            var error = new Exception("audio worker failed during recovery");
            Task fault = null;
            try
            {
                fault = session.ReportFaultAsync(error);
                Assert.False(fault.IsCompleted);
                Assert.AreEqual(0, runtime.StopCount);
                Assert.AreEqual(0, runtime.DisposeCount);
            }
            finally
            {
                runtime.CompleteQuiescence();
                AwaitStopped(recovery);
                if (fault != null)
                    AwaitStopped(fault);
            }

            recovery.GetAwaiter().GetResult();
            Assert.AreSame(error, Assert.Throws<InvalidOperationException>(() => fault.GetAwaiter().GetResult()).InnerException);
            CollectionAssert.AreEqual(new[]{"prepare", "listen", "cancel", "quiesce", "restore", "stop", "join", "dispose"}, runtime.Calls);
        }

        [Test]
        public void CaptureWorkerCanReportFailureWithoutJoiningItselfOrReleasingLiveResources()
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            var response = session.BeginResponse();
            var error = new Exception("input processor failed");
            Task fault = null;
            using (var worker = new MNNRealtimeAudioCaptureWorker(new SingleBlockCaptureSource(), (_, __, ___) =>
            {
                // Report, then exit this owner. An external control owner observes the returned task.
                Volatile.Write(ref fault, session.ReportFaultAsync(error));
                throw error;
            }, maximumCaptureAge: Timeout.InfiniteTimeSpan))
            {
                runtime.OnStop = () =>
                {
                    if (!worker.Stop(TimeSpan.FromSeconds(3)))
                        throw new TimeoutException("Capture worker failed to exit.");
                };
                runtime.OnDispose = () =>
                {
                    if (!worker.IsFaulted || !ReferenceEquals(error, worker.Fault))
                        throw new InvalidOperationException("Disposal preceded worker exit.");
                };
                worker.Start();
                Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref fault) != null, TimeSpan.FromSeconds(3)));
                AwaitStopped(fault);
                Assert.AreSame(error, Assert.Throws<InvalidOperationException>(() => fault.GetAwaiter().GetResult()).InnerException);
                Assert.False(session.Output.Accepts(response));
                Assert.AreSame(error, worker.Fault);
                Assert.AreEqual(1, runtime.DisposeCount);
            }
        }

        [TestCase(false), TestCase(true)]
        public void ExternalCaptureCompletionObserverReportsOnlyFailureAndJoinsBeforeDisposal(bool fail)
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            var response = session.BeginResponse();
            var error = new InvalidOperationException("input processor failed");
            bool joined = false;
            using var worker = new MNNRealtimeAudioCaptureWorker(new SingleBlockCaptureSource(), (_, __, ___) =>
            {
                if (fail)
                    throw error;
            }, maximumCaptureAge: Timeout.InfiniteTimeSpan);
            runtime.OnStop = () =>
            {
                if (!worker.Stop(TimeSpan.FromSeconds(3)))
                    throw new TimeoutException("Capture worker failed to exit.");
                joined = true;
            };
            runtime.OnDispose = () =>
            {
                if (!joined)
                    throw new InvalidOperationException("Disposal preceded worker join.");
            };
            async Task ObserveCompletion()
            {
                try
                {
                    await worker.Completion.ConfigureAwait(false);
                }
                catch (Exception failure)
                {
                    await session.ReportFaultAsync(failure).ConfigureAwait(false);
                }
            }

            Task observer = ObserveCompletion();
            Assert.False(observer.IsCompleted);
            worker.Start();
            if (!fail)
                Assert.True(worker.Stop(TimeSpan.FromSeconds(3)));
            AwaitStopped(observer);
            if (fail)
            {
                Assert.AreSame(error, Assert.Throws<InvalidOperationException>(() => observer.GetAwaiter().GetResult()).InnerException);
                Assert.AreSame(error, session.Fault);
                Assert.False(session.Output.Accepts(response));
                Assert.True(joined);
                Assert.AreEqual(1, runtime.DisposeCount);
            }
            else
            {
                observer.GetAwaiter().GetResult();
                Assert.IsNull(session.Fault);
                Assert.True(session.Output.Accepts(response));
                Assert.AreEqual(0, runtime.StopCount);
                Assert.AreEqual(0, runtime.DisposeCount);
                session.StopAsync().GetAwaiter().GetResult();
                Assert.True(joined);
                Assert.AreEqual(1, runtime.DisposeCount);
            }
        }

        [Test]
        public void FaultReportingValidatesInputsAndCannotReopenAStoppedSession()
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            Assert.Throws<ArgumentNullException>(() => session.ReportFaultAsync(null));
            Assert.Throws<ArgumentException>(() => session.ReportFaultAsync(default, new Exception()));
            var response = NewOutput().BeginResponse();
            Assert.Throws<ArgumentNullException>(() => session.ReportFaultAsync(response, null));
            Assert.AreEqual(MNNRealtimeSessionState.Created, session.State);
            session.StopAsync().GetAwaiter().GetResult();
            session.ReportFaultAsync(new Exception("late device callback")).GetAwaiter().GetResult();
            session.ReportFaultAsync(response, new Exception("late producer callback")).GetAwaiter().GetResult();
            Assert.AreEqual(MNNRealtimeSessionState.Stopped, session.State);
            Assert.IsNull(session.Fault);
            Assert.AreEqual(1, runtime.DisposeCount);
        }

        [Test]
        public void SpeechBackpressureTimeoutReportsThroughSessionAndCannotLeaveQueuedSpeechPlaying()
        {
            var ownerExited = PendingTask();
            var runtime = new FakeRuntime{StopListening = ownerExited.Task};
            var output = new MNNRealtimeOutputPipeline(Format, 2, blockCapacity: 1);
            var session = new MNNRealtimeSession(runtime, output);
            session.StartAsync().GetAwaiter().GetResult();
            var response = session.BeginResponse();
            var text = new MNNRealtimeSpeechTextChannel(output, response);
            Assert.True(text.Append("Hello."));
            Assert.True(text.Complete());
            long now = 0;
            int attempts = 0;
            var stream = new MNNRealtimeSpeechStream(output, response, TimeSpan.FromSeconds(1), null, () => now, () => now += System.Diagnostics.Stopwatch.Frequency);
            Task fault = null;
            var producer = Task.Run(() =>
            {
                try
                {
                    stream.Produce(text, Format, (_, publish, token) =>
                    {
                        ++attempts;
                        publish(new[]{.1f, .2f, .3f, .4f}, 0, 4, true);
                    });
                }
                catch (Exception error)
                {
                    // Runtime owner exits production before its control owner observes shutdown.
                    Volatile.Write(ref fault, session.ReportFaultAsync(response, error));
                }
                finally
                {
                    ownerExited.TrySetResult(true);
                }
            });
            AwaitStopped(producer);
            producer.GetAwaiter().GetResult();
            AwaitStopped(fault);
            var failure = Assert.Throws<InvalidOperationException>(() => fault.GetAwaiter().GetResult());
            Assert.IsInstanceOf<TimeoutException>(failure.InnerException);
            Assert.AreSame(failure.InnerException, session.Fault);
            Assert.AreEqual(1, attempts);
            var pcm = new float[4];
            output.Render(pcm, 0, pcm.Length);
            Assert.That(pcm, Is.All.EqualTo(0f));
            Assert.AreEqual(0, output.QueuedFrames);
            Assert.True(output.GetSegment(0).IsInterrupted);
            Assert.AreEqual(1, runtime.DisposeCount);
        }

        [Test]
        public void ConcurrentFaultsAndStopShareOneShutdownAndRetainOnePrimaryError()
        {
            var releaseStop = PendingTask();
            var runtime = new FakeRuntime{StopListening = releaseStop.Task};
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            session.BeginResponse();
            var errors = new Exception[8];
            var reporters = new Task[8];
            var shutdowns = new Task[8];
            try
            {
                for (int i = 0; i < errors.Length; ++i)
                {
                    int index = i;
                    errors[index] = new Exception("worker " + index);
                    reporters[index] = Task.Run(() =>
                    {
                        shutdowns[index] = index == 0 ? session.StopAsync() : session.ReportFaultAsync(errors[index]);
                    });
                }

                Assert.True(Task.WaitAll(reporters, TimeSpan.FromSeconds(3)));
                foreach (var shutdown in shutdowns)
                    Assert.AreSame(shutdowns[0], shutdown);
                CollectionAssert.Contains(errors, session.Fault);
                Assert.AreEqual(MNNRealtimeSessionState.Stopping, session.State);
            }
            finally
            {
                releaseStop.TrySetResult(true);
                foreach (var reporter in reporters)
                    if (reporter != null)
                        AwaitStopped(reporter);
            }

            AwaitStopped(shutdowns[0]);
            Assert.AreSame(session.Fault, Assert.Throws<InvalidOperationException>(() => shutdowns[0].GetAwaiter().GetResult()).InnerException);
            Assert.AreEqual(1, runtime.StopCount);
            Assert.AreEqual(1, runtime.QuiescenceCount);
            Assert.AreEqual(1, runtime.DisposeCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FatalFaultCanCloseBeforeStartOrAfterGenerationEos(bool afterEos)
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            var error = new Exception("device failure");
            Task fault;
            if (afterEos)
            {
                session.StartAsync().GetAwaiter().GetResult();
                var response = session.BeginResponse();
                Assert.True(session.CompleteResponse(response));
                fault = session.ReportFaultAsync(response, error);
            }
            else
                fault = session.ReportFaultAsync(error);
            AwaitStopped(fault);
            Assert.AreSame(error, Assert.Throws<InvalidOperationException>(() => fault.GetAwaiter().GetResult()).InnerException);
            Assert.AreEqual(MNNRealtimeSessionState.Faulted, session.State);
            Assert.Throws<InvalidOperationException>(() => session.StartAsync());
            Assert.AreEqual(1, runtime.DisposeCount);
        }
    }
}
