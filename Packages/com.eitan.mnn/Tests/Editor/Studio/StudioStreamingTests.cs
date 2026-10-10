using System;
using System.Collections;
using System.Threading;
using System.Text;
using MNN.Unity.Editor;
using MNN.Unity.Interop;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class StudioStreamingTests
    {
        private sealed class CancelledBackend : IMNNStudioBackend
        {
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                request.Cancellation.WaitHandle.WaitOne(5000);
                request.Cancellation.ThrowIfCancellationRequested();
                throw new TimeoutException("Expected a stop request.");
            }

            public void Dispose()
            {
            }
        }

        [UnityTest]
        public IEnumerator StopBeforeFirstToken_DoesNotReportAnInferenceError()
        {
            using (var session = new MNNStudioSession(new CancelledBackend()))
            {
                session.Send(new MNNStudioRequest{Prompt = "Hello"});
                session.StopGeneration();
                for (int i = 0; i < 100 && session.Busy; ++i)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.IsFalse(session.Busy);
                Assert.IsNull(session.Error);
                Assert.IsNotNull(session.Result);
                Assert.IsTrue(session.Result.Cancelled);
                Assert.AreEqual("", session.Result.Text);
            }
        }

        private sealed class StreamingBackend : IMNNStudioBackend
        {
            internal readonly ManualResetEventSlim Published = new ManualResetEventSlim(), Finish = new ManualResetEventSlim();
            internal int WorkerThread, Disposals;
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                WorkerThread = Thread.CurrentThread.ManagedThreadId;
                for (int i = 1; i <= 100; ++i)
                    request.Progress(new MNNGenerationUpdate("Partial " + i, i));
                Published.Set();
                if (!Finish.Wait(10000))
                    throw new TimeoutException();
                return new MNNStudioResult{Text = "Partial 100", Tokens = 100, Cancelled = request.Cancellation.IsCancellationRequested};
            }

            public void Dispose()
            {
                ++Disposals;
            }
        }

        [UnityTest]
        public IEnumerator Progress_IsCoalescedAndDeliveredOnEditorThreadBeforeCompletion()
        {
            int main = Thread.CurrentThread.ManagedThreadId, deliveries = 0, thread = 0;
            var backend = new StreamingBackend();
            var session = new MNNStudioSession(backend);
            session.Updated += () =>
            {
                ++deliveries;
                thread = Thread.CurrentThread.ManagedThreadId;
            };
            try
            {
                session.Send(new MNNStudioRequest{Prompt = "Hello"});
                while (!backend.Published.IsSet)
                    yield return null;
                MNNStudioJobs.Update();
                Assert.IsTrue(session.Busy);
                Assert.AreEqual("Partial 100", session.Progress.Text);
                Assert.AreEqual(1, deliveries);
                Assert.AreEqual(main, thread);
                Assert.AreNotEqual(main, backend.WorkerThread);
                session.StopGeneration();
                backend.Finish.Set();
                while (session.Busy)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.IsTrue(session.Result.Cancelled);
                Assert.AreEqual("Partial 100", session.Result.Text);
                session.Send(new MNNStudioRequest{Prompt = "Next"});
                while (session.Busy)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.IsFalse(session.Result.Cancelled);
                Assert.IsNull(session.Error);
            }
            finally
            {
                backend.Finish.Set();
                session.Dispose();
            }

            Assert.AreEqual(1, backend.Disposals);
        }

        [UnityTest]
        public IEnumerator Close_SuppressesPendingProgressAndDefersRelease()
        {
            var backend = new StreamingBackend();
            var session = new MNNStudioSession(backend);
            int callbacks = 0;
            session.Updated += () => ++callbacks;
            try
            {
                session.Send(new MNNStudioRequest{Prompt = "Hello"});
                while (!backend.Published.IsSet)
                    yield return null;
                int previous = callbacks;
                session.Dispose();
                Assert.AreEqual(0, backend.Disposals);
                backend.Finish.Set();
                while (session.Busy)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.AreEqual(previous, callbacks);
                Assert.IsNull(session.Result);
                Assert.AreEqual(1, backend.Disposals);
            }
            finally
            {
                backend.Finish.Set();
                session.Dispose();
            }
        }

        [Test]
        public void Utf8Snapshots_NeverExposeIncompleteChineseOrEmoji()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("A你🙂Z");
            for (int count = 0; count <= bytes.Length; ++count)
            {
                var prefix = new byte[count];
                Array.Copy(bytes, prefix, count);
                string decoded = MNNInterop.DecodeCompleteUtf8(prefix);
                Assert.AreEqual(-1, decoded.IndexOf('\ufffd'));
                Assert.IsTrue("A你🙂Z".StartsWith(decoded, StringComparison.Ordinal));
            }
        }

        [Test]
        public void SilenceEndpointing_RequiresSpeechAndWaitsForPause()
        {
            var activity = new MNNStudioVoiceActivity();
            activity.Reset(0);
            Assert.IsFalse(activity.ShouldSend(10, 0));
            Assert.IsFalse(activity.ShouldSend(10.1, .2f));
            Assert.IsFalse(activity.ShouldSend(10.8, 0));
            Assert.IsFalse(activity.ShouldSend(10.9, .1f));
            Assert.IsTrue(activity.ShouldSend(12.1, 0));
            activity.Reset(20);
            Assert.IsFalse(activity.ShouldSend(30, 0));
            Assert.AreEqual(0, MNNStudioAudio.Rms(null, 0, 0));
            Assert.AreEqual(1, MNNStudioAudio.Rms(new[]{1f, -1f}, 0, 2));
        }
    }
}
