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
        public void InterruptSilencesSynchronouslyAndRestoresOnlyAfterInferenceQuiesces()
        {
            var runtime = new FakeRuntime{Quiescence = PendingTask()};
            var output = NewOutput();
            var session = new MNNRealtimeSession(runtime, output);
            session.StartAsync().GetAwaiter().GetResult();
            var response = session.BeginResponse();
            Assert.True(output.TryRegisterSegment(response, 0, "Hello."));
            var speech = new float[80];
            Array.Fill(speech, .5f);
            Assert.True(output.TryEnqueue(speech, 0, speech.Length, new MNNSpeechChunkInfo(response, 0, 0, true)));
            var interrupted = session.InterruptAsync();
            AwaitCount(ref runtime.InferenceQuiescenceCount);
            var render = new float[80];
            output.Render(render, 0, render.Length);
            Assert.That(render, Is.All.EqualTo(0f));
            Assert.AreEqual(MNNRealtimeSessionState.Recovering, session.State);
            Assert.That(runtime.Calls, Is.EqualTo(new[]{"prepare", "listen", "cancel", "quiesce"}));
            Assert.False(interrupted.IsCompleted);
            runtime.CompleteQuiescence();
            interrupted.GetAwaiter().GetResult();
            Assert.AreEqual(MNNRealtimeSessionState.Running, session.State);
            Assert.That(runtime.Calls, Is.EqualTo(new[]{"prepare", "listen", "cancel", "quiesce", "restore"}));
            session.StopAsync().GetAwaiter().GetResult();
        }

        [Test]
        public void RepeatedInterruptsShareRecoveryAndDoNotCancelNextResponse()
        {
            var runtime = new FakeRuntime{Quiescence = PendingTask()};
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            var old = session.BeginResponse();
            var first = session.InterruptAsync();
            var duplicate = session.InterruptAsync();
            Assert.AreSame(first, duplicate);
            AwaitCount(ref runtime.CancelCount);
            Assert.AreEqual(1, runtime.CancelCount);
            runtime.CompleteQuiescence();
            first.GetAwaiter().GetResult();
            var next = session.BeginResponse();
            Assert.AreNotEqual(old, next);
            Assert.True(session.Output.Accepts(next));
            session.StopAsync().GetAwaiter().GetResult();
        }

        [Test]
        public void ProducerCanRequestInterruptAndExitBeforeRecoveryJoinsIt()
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            session.BeginResponse();
            var releaseProducer = PendingTask();
            Task recovery = null;
            var producer = Task.Run(async () =>
            {
                await releaseProducer.Task.ConfigureAwait(false);
                recovery = session.InterruptAsync();
            });
            runtime.InferenceTask = producer;
            releaseProducer.TrySetResult(true);
            Assert.True(SpinWait.SpinUntil(() => producer.IsCompleted, TimeSpan.FromSeconds(3)), "The response producer must return after requesting interruption; it must not await recovery, which joins the producer.");
            Assert.NotNull(recovery);
            Assert.True(SpinWait.SpinUntil(() => recovery.IsCompleted, TimeSpan.FromSeconds(3)), "Recovery must finish after the producer has exited.");
            recovery.GetAwaiter().GetResult();
            Assert.AreEqual(MNNRealtimeSessionState.Running, session.State);
            session.StopAsync().GetAwaiter().GetResult();
        }

        [Test]
        public void NaturalEosKeepsQueuedSpeechAliveUntilPresentedBeforeNextResponse()
        {
            var session = new MNNRealtimeSession(new FakeRuntime(), NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            var response = session.BeginResponse();
            Assert.True(session.Output.TryRegisterSegment(response, 3, "Done."));
            var speech = new float[80];
            Array.Fill(speech, .25f);
            Assert.True(session.Output.TryEnqueue(speech, 0, speech.Length, new MNNSpeechChunkInfo(response, 3, 0, true)));
            Assert.True(session.CompleteResponse(response));
            Assert.False(session.Output.TryEnqueue(speech, 0, speech.Length, new MNNSpeechChunkInfo(response, 3, 1, true)));
            Assert.Throws<InvalidOperationException>(() => session.BeginResponse());
            session.Output.Render(speech, 0, speech.Length);
            Assert.AreEqual(1, session.Output.DrainReceipts(4));
            Assert.Throws<InvalidOperationException>(() => session.BeginResponse());
            session.Output.AcknowledgePresentation(80, estimated: false);
            var next = session.BeginResponse();
            Assert.AreNotEqual(response, next);
            session.StopAsync().GetAwaiter().GetResult();
        }

        [Test]
        public void FailedRecoveryFaultsClosedAndCannotStartAnotherResponse()
        {
            var runtime = new FakeRuntime{RestoreError = new InvalidOperationException("context restore failed")};
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            session.BeginResponse();
            Exception error = null;
            try
            {
                session.InterruptAsync().GetAwaiter().GetResult();
            }
            catch (Exception failure)
            {
                error = failure;
            }

            Assert.NotNull(error);
            Assert.IsInstanceOf<InvalidOperationException>(error);
            Assert.AreEqual("context restore failed", error.Message);
            Assert.AreEqual(MNNRealtimeSessionState.Faulted, session.State);
            Assert.Throws<InvalidOperationException>(() => session.BeginResponse());
            var silence = new float[80];
            session.Output.Render(silence, 0, silence.Length);
            Assert.That(silence, Is.All.EqualTo(0f));
            session.StopAsync().GetAwaiter().GetResult();
        }

        [Test]
        public void ReentrantStopDuringRecoveryWaitsForThePublishedRecoveryTask()
        {
            var runtime = new FakeRuntime();
            var session = new MNNRealtimeSession(runtime, NewOutput());
            session.StartAsync().GetAwaiter().GetResult();
            var response = session.BeginResponse();
            Task stop = null;
            Task repeatedInterrupt = null;
            runtime.OnCancel = () =>
            {
                repeatedInterrupt = session.InterruptAsync();
                stop = session.StopAsync();
                Assert.False(stop.IsCompleted, "Inference cancellation is still using the runtime.");
                Assert.AreEqual(0, runtime.DisposeCount);
            };
            var recovery = session.InterruptAsync();
            AwaitStopped(recovery);
            recovery.GetAwaiter().GetResult();
            Assert.AreSame(recovery, repeatedInterrupt);
            AwaitStopped(stop);
            stop.GetAwaiter().GetResult();
            CollectionAssert.AreEqual(new[]{"prepare", "listen", "cancel", "quiesce", "restore", "stop", "join", "dispose"}, runtime.Calls);
            Assert.False(session.Output.Accepts(response));
            Assert.AreEqual(MNNRealtimeSessionState.Stopped, session.State);
        }
    }
}
