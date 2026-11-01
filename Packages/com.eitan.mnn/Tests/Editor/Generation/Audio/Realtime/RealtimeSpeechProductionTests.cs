using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class RealtimeSpeechProductionTests
    {
        private static readonly MNNPcmFormat Format = new MNNPcmFormat(24000, 1);

        [Test]
        public void BorrowedBlockIsSplitAndPlayedBeforeSynthesisAndTextEos()
        {
            var output = new MNNRealtimeOutputPipeline(Format, 3, blockCapacity: 4);
            var response = output.BeginResponse();
            var text = new MNNRealtimeSpeechTextChannel(output, response);
            Assert.True(text.Append("Hello. "));
            var stream = new MNNRealtimeSpeechStream(output, response);
            using var emitted = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var cancel = new CancellationTokenSource();
            var task = Task.Run(() => stream.Produce(text, Format, (spoken, publish, token) =>
            {
                Assert.AreEqual("Hello.", spoken);
                var borrowed = new[] { -1f, .1f, .2f, .3f, .4f, .5f, .6f, .7f, -1f };
                publish(borrowed, 1, 7, true);
                Array.Clear(borrowed, 0, borrowed.Length);
                emitted.Set();
                release.Wait(token);
            }, cancel.Token));
            try
            {
                Assert.True(emitted.Wait(TimeSpan.FromSeconds(3)));
                Assert.False(task.IsCompleted);
                Assert.False(text.IsCompleted);
                var played = new float[7];
                output.Render(played, 0, played.Length);
                CollectionAssert.AreEqual(new[] { .1f, .2f, .3f, .4f, .5f, .6f, .7f }, played);
                Assert.AreEqual(3, output.DrainReceipts(20));
                output.AcknowledgePresentation(7, false);
                Assert.True(output.GetSegment(0).IsPresented);
                Assert.AreEqual(1, stream.SynthesisMeter.WindowChunkCount, "Count generated blocks once, not publication slices.");
                Assert.True(text.Complete());
                release.Set();
                AwaitStopped(task);
                Assert.True(task.GetAwaiter().GetResult());
                Assert.True(output.CanBeginNextResponse(response));
            }
            finally
            {
                cancel.Cancel();
                release.Set();
                AwaitStopped(task);
            }
        }

        [Test]
        public void StreamingSegmentsAreSynthesizedBeforeEarlierAudioIsPlayedAndRemainOrdered()
        {
            var output = new MNNRealtimeOutputPipeline(Format, 3, blockCapacity: 4);
            var response = output.BeginResponse();
            var text = new MNNRealtimeSpeechTextChannel(output, response, capacity: 2);
            var stream = new MNNRealtimeSpeechStream(output, response);
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancel = new CancellationTokenSource();

            var production = stream.ProduceAsync(text, Format, async (spoken, publish, token) =>
            {
                if (spoken == "First.")
                {
                    await publish(new[] { .1f, .2f }, 0, 2, true);
                    firstStarted.TrySetResult(true);
                }
                else
                {
                    Assert.AreEqual("Second.", spoken);
                    await publish(new[] { .3f, .4f }, 0, 2, true);
                    secondStarted.TrySetResult(true);
                }
            }, cancel.Token);

            try
            {
                Assert.True(text.Append("First. "));
                AwaitSignal(firstStarted.Task);
                Assert.AreEqual(2, output.QueuedFrames);

                Assert.True(text.Append("Second. "));
                AwaitSignal(secondStarted.Task);
                Assert.AreEqual(4, output.QueuedFrames,
                    "The second sentence must finish synthesis while the first sentence remains unplayed.");
                Assert.True(text.Complete());
                Assert.True(production.GetAwaiter().GetResult());

                var rendered = new float[4];
                output.Render(rendered, 0, rendered.Length);
                CollectionAssert.AreEqual(new[] { .1f, .2f, .3f, .4f }, rendered);
                Assert.AreEqual(2, output.DrainReceipts(4));
                output.AcknowledgePresentation(4, false);
                Assert.True(output.CanBeginNextResponse(response));
            }
            finally
            {
                cancel.Cancel();
                if (!production.IsCompleted)
                {
                    text.Complete();
                    Task.WhenAny(production, Task.Delay(TimeSpan.FromSeconds(3))).GetAwaiter().GetResult();
                }
            }
        }

        [TestCase(1), TestCase(2)]
        public void BackpressurePreservesEverySampleAndSegmentInMonoAndStereo(int channels)
        {
            var format = new MNNPcmFormat(24000, channels);
            var output = new MNNRealtimeOutputPipeline(format, 3, blockCapacity: 1);
            var response = output.BeginResponse();
            var text = new MNNRealtimeSpeechTextChannel(output, response);
            Assert.True(text.Append("First. Second."));
            Assert.True(text.Complete());
            var stream = new MNNRealtimeSpeechStream(output, response);
            var expected = new List<float>();
            var actual = new List<float>();
            int synthesized = 0;
            long renderedFrames = 0;
            using var cancel = new CancellationTokenSource();
            var task = Task.Run(() => stream.Produce(text, format, (spoken, publish, token) =>
            {
                ++synthesized;
                var samples = new float[11 * channels];
                for (int i = 0; i < samples.Length; ++i)
                    samples[i] = synthesized * .1f + i * .001f;
                expected.AddRange(samples);
                publish(samples, 0, samples.Length, true);
                Array.Clear(samples, 0, samples.Length);
            }, cancel.Token));
            try
            {
                var rendered = new float[3 * channels];
                while (actual.Count < 22 * channels)
                {
                    Assert.True(SpinWait.SpinUntil(() => output.QueuedFrames > 0 || task.IsCompleted, TimeSpan.FromSeconds(3)));
                    int count = (int)Math.Min(3, output.QueuedFrames) * channels;
                    Assert.Greater(count, 0, "Producer stopped before all audio arrived.");
                    output.Render(rendered, 0, count);
                    renderedFrames += count / channels;
                    // The queue watermark is approximate during concurrent publication. Count only
                    // this nonzero test signal; a render racing an enqueue may legitimately supply silence.
                    for (int i = 0; i < count; ++i)
                        if (rendered[i] != 0) actual.Add(rendered[i]);
                    output.DrainReceipts(32);
                }
                AwaitStopped(task);
                Assert.True(task.GetAwaiter().GetResult());
                CollectionAssert.AreEqual(expected, actual);
                Assert.AreEqual(2, synthesized);
                Assert.AreEqual(2, stream.SynthesisMeter.WindowChunkCount);
                output.AcknowledgePresentation(renderedFrames, false);
                Assert.True(output.CanBeginNextResponse(response));
            }
            finally
            {
                cancel.Cancel();
                AwaitStopped(task);
            }
        }

        [TestCase(false), TestCase(true)]
        public void InterruptStopsAnIdleOrBackpressuredProducerWithoutCompletingTheNextResponse(bool withAudio)
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2, blockCapacity: 1);
            var response = output.BeginResponse();
            var text = new MNNRealtimeSpeechTextChannel(output, response);
            if (withAudio) Assert.True(text.Append("Hello. "));
            var stream = new MNNRealtimeSpeechStream(output, response);
            using var cancel = new CancellationTokenSource();
            using var started = new ManualResetEventSlim();
            var task = Task.Run(() =>
            {
                started.Set();
                return stream.Produce(text, Format, (_, publish, token) => publish(new[] { .1f, .2f, .3f, .4f }, 0, 4, true), cancel.Token);
            });
            try
            {
                Assert.True(started.Wait(TimeSpan.FromSeconds(3)));
                if (withAudio) Assert.True(SpinWait.SpinUntil(() => output.QueuedFrames == 2, TimeSpan.FromSeconds(3)));
                output.Interrupt();
                var next = output.BeginResponse();
                AwaitStopped(task);
                Assert.False(task.GetAwaiter().GetResult());
                Assert.True(output.Accepts(next));
                var rendered = new float[4];
                output.Render(rendered, 0, rendered.Length);
                Assert.That(rendered, Is.All.Zero);
                Assert.False(output.CanBeginNextResponse(next), "An old producer must not complete a new response.");
            }
            finally
            {
                cancel.Cancel();
                AwaitStopped(task);
            }
        }

        [TestCase(false), TestCase(true)]
        public void CancellationReleasesAnIdleOrBackpressuredProducer(bool withAudio)
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2, blockCapacity: 1);
            var response = output.BeginResponse();
            var text = new MNNRealtimeSpeechTextChannel(output, response);
            if (withAudio) Assert.True(text.Append("Hello. "));
            using var cancel = new CancellationTokenSource();
            using var started = new ManualResetEventSlim();
            var task = Task.Run(() =>
            {
                started.Set();
                return new MNNRealtimeSpeechStream(output, response).Produce(text, Format,
                    (_, publish, token) => publish(new[] { .1f, .2f, .3f }, 0, 3, true), cancel.Token);
            });
            Assert.True(started.Wait(TimeSpan.FromSeconds(3)));
            try
            {
                if (withAudio) Assert.True(SpinWait.SpinUntil(() => output.QueuedFrames == 2, TimeSpan.FromSeconds(3)));
            }
            finally
            {
                cancel.Cancel();
                AwaitStopped(task);
            }
            Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            Assert.False(output.CanBeginNextResponse(response));
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4)]
        public void InvalidBackendProtocolDoesNotCompleteResponse(int failure)
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2, blockCapacity: 4);
            var response = output.BeginResponse();
            var text = CompletedText(output, response);
            var stream = new MNNRealtimeSpeechStream(output, response);
            var exception = Assert.Catch<Exception>(() => stream.Produce(text, Format, (_, publish, token) =>
            {
                if (failure == 0) return; // No final block.
                if (failure == 1) publish(Array.Empty<float>(), 0, 0, true);
                if (failure == 2) publish(new[] { .1f, .2f, float.NaN }, 0, 3, true);
                if (failure == 3) publish(new[] { .1f, .2f, 1.01f }, 0, 3, true);
                if (failure == 4)
                {
                    publish(new[] { .1f }, 0, 1, true);
                    publish(new[] { .2f }, 0, 1, true);
                }
            }));
            Assert.AreEqual(failure == 1 ? typeof(ArgumentOutOfRangeException) :
                failure == 2 || failure == 3 ? typeof(ArgumentException) : typeof(InvalidOperationException), exception.GetType());
            Assert.False(output.CanBeginNextResponse(response));
            Assert.AreEqual(failure == 4 ? 1 : 0, output.QueuedFrames, "Invalid samples must be rejected before publishing a valid prefix.");
            Assert.Throws<InvalidOperationException>(() => stream.Produce(text, Format, (_, __, ___) => { }));
        }

        [Test]
        public void BackendExceptionAndPrecancelledTokenPropagateWithoutSynthesisRetry()
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2);
            var response = output.BeginResponse();
            var text = CompletedText(output, response);
            var expected = new InvalidOperationException("backend failed");
            var stream = new MNNRealtimeSpeechStream(output, response);
            Assert.AreSame(expected, Assert.Throws<InvalidOperationException>(() => stream.Produce(text, Format, (_, __, ___) => throw expected)));
            var fresh = new MNNRealtimeSpeechStream(output, response);
            Assert.Throws<OperationCanceledException>(() => fresh.Produce(text, Format,
                (_, __, ___) => Assert.Fail("Cancelled producer invoked synthesis."), new CancellationToken(true)));
        }

        [Test]
        public void ArgumentsRejectFormatAndPipelineMismatchEvenWhenEpochsCoincide()
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2);
            var response = output.BeginResponse();
            var text = CompletedText(output, response);
            var otherOutput = new MNNRealtimeOutputPipeline(Format, 2);
            var otherText = new MNNRealtimeSpeechTextChannel(otherOutput, otherOutput.BeginResponse());
            Assert.AreEqual(response.Epoch, otherText.Response.Epoch, "Exercise coincident epochs in independent sessions.");
            Assert.AreNotEqual(response.Session, otherText.Response.Session);
            var stream = new MNNRealtimeSpeechStream(output, response);
            Action<string, Action<float[], int, int, bool>, CancellationToken> synthesize = (_, __, ___) => { };
            Assert.Throws<ArgumentNullException>(() => stream.Produce(null, Format, synthesize));
            Assert.Throws<ArgumentNullException>(() => stream.Produce(text, Format, null));
            Assert.Throws<ArgumentException>(() => stream.Produce(text, default, synthesize));
            Assert.Throws<ArgumentException>(() => stream.Produce(text, new MNNPcmFormat(48000, 1), synthesize));
            Assert.Throws<ArgumentException>(() => stream.Produce(text, new MNNPcmFormat(24000, 2), synthesize));
            Assert.Throws<ArgumentException>(() => stream.Produce(otherText, Format, synthesize));
        }

        [Test]
        public void EmptyCompletedTextClosesProductionAndConcurrentOrReentrantProductionIsRejected()
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2);
            var response = output.BeginResponse();
            var text = new MNNRealtimeSpeechTextChannel(output, response);
            Assert.True(text.Complete());
            var stream = new MNNRealtimeSpeechStream(output, response);
            Assert.True(stream.Produce(text, Format, (_, __, ___) => Assert.Fail("Empty text should not invoke synthesis.")));
            Assert.True(output.CanBeginNextResponse(response));
            Assert.Throws<InvalidOperationException>(() => stream.Produce(text, Format, (_, __, ___) => { }));

            response = output.BeginResponse();
            text = CompletedText(output, response);
            stream = new MNNRealtimeSpeechStream(output, response);
            Assert.True(stream.Produce(text, Format, (_, publish, token) =>
            {
                Assert.Throws<InvalidOperationException>(() => stream.Produce(text, Format, (_, __, ___) => { }));
                var otherThread = Task.Run(() => Assert.Throws<InvalidOperationException>(() => stream.Produce(text, Format, (_, __, ___) => { })));
                AwaitStopped(otherThread);
                otherThread.GetAwaiter().GetResult();
                publish(new[] { .1f }, 0, 1, true);
            }));
        }

        [Test]
        public void RetainedAndCrossThreadCallbacksAreRejected()
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2);
            var response = output.BeginResponse();
            var text = CompletedText(output, response);
            Action<float[], int, int, bool> retained = null;
            var stream = new MNNRealtimeSpeechStream(output, response);
            Assert.True(stream.Produce(text, Format, (_, publish, token) =>
            {
                retained = publish;
                var otherThread = Task.Run(() => Assert.Throws<InvalidOperationException>(() => publish(new[] { .2f }, 0, 1, true)));
                AwaitStopped(otherThread);
                otherThread.GetAwaiter().GetResult();
                publish(new[] { .1f }, 0, 1, true);
            }));
            Assert.Throws<InvalidOperationException>(() => retained(new[] { .3f }, 0, 1, true));
            Assert.AreEqual(1, output.QueuedFrames);
        }

        [Test]
        public void FullLedgerRejectsTheNextSegmentBeforeSynthesisAndDoesNotCompleteResponse()
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2, segmentCapacity: 1);
            var response = output.BeginResponse();
            var text = new MNNRealtimeSpeechTextChannel(output, response);
            Assert.True(text.Append("First. Second."));
            Assert.True(text.Complete());
            int synthesized = 0;
            var stream = new MNNRealtimeSpeechStream(output, response);
            Assert.Throws<InvalidOperationException>(() => stream.Produce(text, Format, (spoken, publish, token) =>
            {
                ++synthesized;
                Assert.AreEqual("First.", spoken);
                publish(new[] { .1f }, 0, 1, true);
                var rendered = new float[1];
                output.Render(rendered, 0, 1);
                Assert.AreEqual(.1f, rendered[0]);
                Assert.AreEqual(1, output.DrainReceipts(2));
                output.AcknowledgePresentation(1, false);
            }));
            Assert.AreEqual(1, synthesized);
            Assert.AreEqual(1, output.SegmentCount);
            Assert.True(output.GetSegment(0).IsPresented);
            Assert.AreEqual(0, output.QueuedFrames);
            Assert.False(output.CanBeginNextResponse(response), "A failed producer must not declare generation EOS.");
        }

        [Test]
        public void ConcurrentFinalTextPublicationAndEosNeverLoseTheLastSegment()
        {
            for (int iteration = 0; iteration < 128; ++iteration)
            {
                var output = new MNNRealtimeOutputPipeline(Format, 2);
                var response = output.BeginResponse();
                var text = new MNNRealtimeSpeechTextChannel(output, response, capacity: 1);
                Assert.True(text.Append("Final"));
                int synthesized = 0;
                using var cancel = new CancellationTokenSource();
                using var started = new ManualResetEventSlim();
                var task = Task.Run(() =>
                {
                    started.Set();
                    return new MNNRealtimeSpeechStream(output, response).Produce(text, Format, (spoken, publish, token) =>
                    {
                        ++synthesized;
                        Assert.AreEqual("Final", spoken);
                        publish(new[] { .1f }, 0, 1, true);
                    }, cancel.Token);
                });
                try
                {
                    Assert.True(started.Wait(TimeSpan.FromSeconds(3)));
                    Assert.True(text.Complete());
                    AwaitStopped(task);
                    Assert.True(task.GetAwaiter().GetResult());
                    Assert.AreEqual(1, synthesized, "Final segment was lost or synthesized twice at iteration " + iteration);
                    var rendered = new float[1];
                    output.Render(rendered, 0, 1);
                    Assert.AreEqual(.1f, rendered[0]);
                    Assert.AreEqual(1, output.DrainReceipts(2));
                    output.AcknowledgePresentation(1, false);
                    Assert.True(output.CanBeginNextResponse(response));
                }
                finally
                {
                    cancel.Cancel();
                    AwaitStopped(task);
                }
            }
        }

        [Test]
        public void BackpressureBudgetDefaultsAndArgumentsAreExplicit()
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2);
            var response = output.BeginResponse();
            Assert.AreEqual(TimeSpan.FromMilliseconds(300), new MNNRealtimeSpeechStream(output, response).MaximumBackpressureWait);
            foreach (var invalid in new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(-2), TimeSpan.MinValue, TimeSpan.MaxValue })
                Assert.Throws<ArgumentOutOfRangeException>(() => new MNNRealtimeSpeechStream(output, response, invalid));
            Assert.AreEqual(Timeout.InfiniteTimeSpan,
                new MNNRealtimeSpeechStream(output, response, Timeout.InfiniteTimeSpan).MaximumBackpressureWait);
            Assert.AreEqual(TimeSpan.FromTicks(1), new MNNRealtimeSpeechStream(output, response, TimeSpan.FromTicks(1)).MaximumBackpressureWait);
        }

        [Test]
        public void StalledPlaybackTimesOutWithoutCompletingOrRetryingSynthesis()
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2, blockCapacity: 1);
            var response = output.BeginResponse();
            var text = CompletedText(output, response);
            long now = 0;
            int waits = 0;
            int synthesized = 0;
            bool callbackReturned = false;
            var stream = new MNNRealtimeSpeechStream(output, response, TimeSpan.FromSeconds(1), null, () => now, () =>
            {
                Assert.Less(++waits, 3, "Producer failed to enforce the wait budget.");
                now += Stopwatch.Frequency;
            });
            Assert.Throws<TimeoutException>(() => stream.Produce(text, Format, (_, publish, token) =>
            {
                ++synthesized;
                publish(new[] { .1f, .2f, .3f, .4f }, 0, 4, true);
                callbackReturned = true;
            }));
            Assert.AreEqual(2, waits, "Equality is within budget; a longer continuous stall must fail.");
            Assert.AreEqual(1, synthesized);
            Assert.False(callbackReturned);
            Assert.AreEqual(2, output.QueuedFrames);
            Assert.AreEqual(1, stream.SynthesisMeter.WindowChunkCount);
            Assert.False(output.CanBeginNextResponse(response));
            Assert.False(output.IsFaulted, "Receipt overflow and producer timeout are separate faults.");
            var rendered = new float[4];
            output.Render(rendered, 0, rendered.Length);
            CollectionAssert.AreEqual(new[] { .1f, .2f, 0f, 0f }, rendered);
            output.DrainReceipts(4);
            Assert.False(output.GetSegment(0).IsPresented);
            output.Interrupt(); // The control runtime owns gate closure and backend quiescence.
            Assert.True(output.GetSegment(0).IsInterrupted);
        }

        [TestCase(1), TestCase(2), TestCase(8)]
        public void SuccessfulPublicationResetsTheStallBudgetWithoutLosingPcm(int channels)
        {
            var format = new MNNPcmFormat(24000, channels);
            var output = new MNNRealtimeOutputPipeline(format, 2, blockCapacity: 1);
            var response = output.BeginResponse();
            var text = CompletedText(output, response);
            long now = 0;
            long presentedFrames = 0;
            int waits = 0;
            var actual = new List<float>();
            var expected = new List<float>();
            var rendered = new float[2 * channels];
            var stream = new MNNRealtimeSpeechStream(output, response, TimeSpan.FromSeconds(1), null, () => now, () =>
            {
                Assert.Less(++waits, 5);
                now += Stopwatch.Frequency; // Each stall ends exactly at its own budget.
                output.Render(rendered, 0, rendered.Length);
                actual.AddRange(rendered);
                presentedFrames += 2;
                output.DrainReceipts(8);
                output.AcknowledgePresentation(presentedFrames, false);
            });
            Assert.True(stream.Produce(text, format, (_, publish, token) =>
            {
                foreach (int frames in new[] { 6, 4 })
                {
                    var pcm = new float[frames * channels];
                    for (int i = 0; i < pcm.Length; ++i) pcm[i] = .1f + i * .001f;
                    expected.AddRange(pcm);
                    publish(pcm, 0, pcm.Length, frames == 4);
                    Array.Clear(pcm, 0, pcm.Length);
                }
            }));
            output.Render(rendered, 0, rendered.Length);
            actual.AddRange(rendered);
            output.DrainReceipts(8);
            output.AcknowledgePresentation(presentedFrames + 2, false);
            Assert.AreEqual(4, waits);
            Assert.Greater(now, Stopwatch.Frequency, "Progress permits a long response, not a longer single stall.");
            CollectionAssert.AreEqual(expected, actual);
            Assert.AreEqual(0, output.QueuedFrames);
            Assert.AreEqual(2, stream.SynthesisMeter.WindowChunkCount);
            Assert.True(output.CanBeginNextResponse(response));
        }

        [Test]
        public void CapacityFreedAfterTheDeadlineDoesNotAdmitOverduePcm()
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2, blockCapacity: 1);
            var response = output.BeginResponse();
            var text = CompletedText(output, response);
            long now = 0;
            var rendered = new float[2];
            var stream = new MNNRealtimeSpeechStream(output, response, TimeSpan.FromSeconds(1), null, () => now, () =>
            {
                now = Stopwatch.Frequency + 1;
                output.Render(rendered, 0, rendered.Length);
            });
            Assert.Throws<TimeoutException>(() => stream.Produce(text, Format,
                (_, publish, token) => publish(new[] { .1f, .2f, .3f, .4f }, 0, 4, true)));
            CollectionAssert.AreEqual(new[] { .1f, .2f }, rendered);
            Assert.AreEqual(0, output.QueuedFrames);
            output.Render(rendered, 0, rendered.Length);
            Assert.That(rendered, Is.All.Zero);
            Assert.False(output.CanBeginNextResponse(response));
        }

        [TestCase(false), TestCase(true)]
        public void CancellationOrInterruptionTakesPrecedenceOverAnExpiredStall(bool interrupt)
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2, blockCapacity: 1);
            var response = output.BeginResponse();
            var text = CompletedText(output, response);
            long now = 0;
            MNNResponseId next = default;
            using var cancel = new CancellationTokenSource();
            var stream = new MNNRealtimeSpeechStream(output, response, TimeSpan.FromSeconds(1), null, () => now, () =>
            {
                now = Stopwatch.Frequency + 1;
                if (interrupt)
                {
                    output.Interrupt();
                    next = output.BeginResponse();
                }
                else cancel.Cancel();
            });
            Action produce = () => Assert.False(stream.Produce(text, Format,
                (_, publish, token) => publish(new[] { .1f, .2f, .3f }, 0, 3, true), cancel.Token));
            if (interrupt)
            {
                produce();
                Assert.True(output.Accepts(next));
                Assert.False(output.CanBeginNextResponse(next));
            }
            else
                Assert.Throws<OperationCanceledException>(() => produce());
        }

        [TestCase(false), TestCase(true)]
        public void InvalidBackpressureClockFailsWithoutPublishingTheRemainder(bool negative)
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2, blockCapacity: 1);
            var response = output.BeginResponse();
            var text = CompletedText(output, response);
            long now = negative ? -1 : 100;
            int waits = 0;
            var stream = new MNNRealtimeSpeechStream(output, response, TimeSpan.FromSeconds(1), null, () => now, () =>
            {
                Assert.AreEqual(1, ++waits);
                now = 99;
            });
            Assert.Throws<InvalidOperationException>(() => stream.Produce(text, Format,
                (_, publish, token) => publish(new[] { .1f, .2f, .3f }, 0, 3, true)));
            Assert.AreEqual(negative ? 0 : 1, waits);
            Assert.AreEqual(2, output.QueuedFrames);
        }

        [TestCase(false), TestCase(true)]
        public void ClockIsNotReadWhenUnblockedOrWhenOfflineBudgetIsInfinite(bool offline)
        {
            var output = new MNNRealtimeOutputPipeline(Format, 2, blockCapacity: 1);
            var response = output.BeginResponse();
            var text = CompletedText(output, response);
            int waits = 0;
            var stream = new MNNRealtimeSpeechStream(output, response,
                offline ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(1), null,
                () => throw new AssertionException("Unnecessary backpressure clock read."), () =>
                {
                    Assert.AreEqual(1, ++waits);
                    var rendered = new float[2];
                    output.Render(rendered, 0, rendered.Length);
                    CollectionAssert.AreEqual(new[] { .1f, .2f }, rendered);
                });
            Assert.True(stream.Produce(text, Format, (_, publish, token) =>
                publish(new[] { .1f, .2f, .3f }, 0, offline ? 3 : 2, true)));
            Assert.AreEqual(offline ? 1 : 0, waits);
        }

        private static MNNRealtimeSpeechTextChannel CompletedText(MNNRealtimeOutputPipeline output, MNNResponseId response)
        {
            var text = new MNNRealtimeSpeechTextChannel(output, response);
            Assert.True(text.Append("Hello."));
            Assert.True(text.Complete());
            return text;
        }

        private static void AwaitStopped(Task task)
        {
            Assert.True(SpinWait.SpinUntil(() => task.IsCompleted, TimeSpan.FromSeconds(3)), "Speech production did not stop within the test bound.");
        }

        private static void AwaitSignal(Task signal)
        {
            Task completed = Task.WhenAny(signal, Task.Delay(TimeSpan.FromSeconds(3))).GetAwaiter().GetResult();
            Assert.AreSame(signal, completed, "Streaming TTS did not start within the test bound.");
            signal.GetAwaiter().GetResult();
        }
    }
}
