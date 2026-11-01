using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class RealtimeAudioCaptureWorkerTests
    {
        private static readonly MNNPcmFormat Format = new MNNPcmFormat(16000, 1);

        [TestCase(16000)]
        [TestCase(48000)]
        public void PausedProducerFlushesEveryFinalSampleAndCanResumeWithoutClosingTheWorker(int rate)
        {
            int count = rate / 5 + 37;
            var pcm = new float[count];
            for (int i = 0; i < pcm.Length; ++i) pcm[i] = (float)(.2 * Math.Sin(i * 2 * Math.PI * 240 / rate));
            var source = new FakeCaptureSource(32, sampleRate: rate, maximumSamples: rate / 100);
            using var input = new MNNRealtimeModelInputBuffer(32, Timeout.InfiniteTimeSpan);
            var activity = MNNRealtimeFrameVoiceActivity.Energy(.01f);
            var processor = new MNNRealtimeVoiceInputProcessor(new MNNPcmFormat(rate, 1), activity, rate / 100);
            using var worker = new MNNRealtimeAudioCaptureWorker(source, processor, input);
            worker.Start();
            for (int offset = 0, sequence = 0; offset < count; ++sequence)
            {
                int take = Math.Min(rate / 100, count - offset);
                var block = new float[take];
                Array.Copy(pcm, offset, block, 0, take);
                Assert.True(source.Publish(block, sequence));
                offset += take;
            }
            Assert.True(worker.FlushAsync().Wait(2000));
            var actual = new List<float>();
            var frame = new float[512];
            while (input.TryRead(frame, 0, out _)) actual.AddRange(frame);
            int valid = (int)Math.Ceiling((double)count * 16000 / rate);
            Assert.AreEqual((valid + 511) / 512 * 512, actual.Count);
            var resampler = new MNNStreamingResampler(new MNNPcmFormat(rate, 1), Format);
            var reference = new float[valid + 1024];
            resampler.Process(pcm, 0, count, reference, 0, reference.Length, out int consumed, out int produced);
            resampler.Complete(reference, produced, reference.Length - produced, out int tail);
            Assert.AreEqual(count, consumed);
            Assert.AreEqual(valid, produced + tail);
            for (int i = 0; i < valid; ++i) Assert.AreEqual(reference[i], actual[i], .000001f, "PCM sample " + i);
            Assert.True(worker.IsRunning);
            Assert.True(source.Publish(new float[rate / 100], sequence: (count + rate / 100 - 1) / (rate / 100)));
            Assert.True(worker.FlushAsync().Wait(2000));
            Assert.True(input.TryRead(frame, 0, out var next));
            Assert.True(next.Discontinuity);
            Assert.Zero(worker.DroppedCaptureBlocks);
            Assert.Zero(worker.DroppedModelFrames);
        }

        [Test]
        public void PendingFlushCompletesWithCancellationWhenStopWinsTheRace()
        {
            var source = new FakeCaptureSource(1);
            Assert.True(source.Publish(.25f, sequence: 0));
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var worker = new MNNRealtimeAudioCaptureWorker(source, (_, __, ___) =>
            {
                entered.Set();
                release.Wait();
            });
            worker.Start();
            try
            {
                Assert.True(entered.Wait(2000));
                Task flush = worker.FlushAsync();
                Assert.AreSame(flush, worker.FlushAsync());
                Assert.False(worker.Stop(TimeSpan.Zero));
                release.Set();
                Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
                Assert.True(flush.IsCompleted, "Stopping must not strand a final-drain caller.");
                Assert.Throws<OperationCanceledException>(() => flush.GetAwaiter().GetResult());
                Assert.True(worker.Completion.IsCompleted);
            }
            finally { release.Set(); }
        }

        [Test]
        public void WorkerDrainsInOrderAndReusesItsScratchBuffer()
        {
            var source = new FakeCaptureSource(capacity: 3);
            var samples = new float[4];
            Assert.True(source.Publish(.1f, sequence: 0));
            Assert.True(source.Publish(.2f, sequence: 1));
            Assert.True(source.Publish(.3f, sequence: 2));
            using (var completed = new CountdownEvent(3))
            {
                var sequences = new long[3];
                var values = new float[3];
                var counts = new int[3];
                float[] firstBuffer = null;
                bool sameBuffer = true;
                int index = 0;
                using (var worker = new MNNRealtimeAudioCaptureWorker(source, (buffer, count, info) =>
                {
                    int slot = Interlocked.Increment(ref index) - 1;
                    if (slot == 0)
                        firstBuffer = buffer;
                    else if (!ReferenceEquals(firstBuffer, buffer))
                        sameBuffer = false;
                    sequences[slot] = info.Sequence;
                    values[slot] = buffer[0];
                    counts[slot] = count;
                    completed.Signal();
                }))
                {
                    worker.Start();
                    Assert.True(completed.Wait(TimeSpan.FromSeconds(2)), "Capture worker did not drain the bounded queue.");
                    Assert.True(worker.Stop(TimeSpan.FromSeconds(1)));
                    Assert.True(worker.IsStopped);
                    Assert.AreEqual(TaskStatus.RanToCompletion, worker.Completion.Status);
                    Assert.AreEqual(3, worker.ProcessedBlocks);
                    Assert.AreEqual(TimeSpan.FromMilliseconds(300), worker.MaximumCaptureAge);
                    Assert.That(sequences, Is.EqualTo(new long[] { 0, 1, 2 }));
                    Assert.That(values, Is.EqualTo(new[] { .1f, .2f, .3f }));
                    Assert.That(counts, Is.All.EqualTo(samples.Length));
                    Assert.True(sameBuffer, "The worker must reuse its preallocated sample buffer.");
                }
            }
        }

        [Test]
        public void StopHasABoundedWaitAndCanBeRetriedAfterTheProcessorQuiesces()
        {
            var source = new FakeCaptureSource(capacity: 3);
            Assert.True(source.Publish(.5f, sequence: 0));
            Assert.True(source.Publish(.6f, sequence: 1));
            Assert.True(source.Publish(.7f, sequence: 2));
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            using (var drained = new CountdownEvent(3))
            using (var worker = new MNNRealtimeAudioCaptureWorker(source, (_, __, ___) =>
            {
                if (drained.CurrentCount == 3)
                {
                    entered.Set();
                    release.Wait();
                }
                drained.Signal();
            }))
            {
                worker.Start();
                Assert.True(entered.Wait(TimeSpan.FromSeconds(2)), "Processor callback did not start.");
                Assert.False(worker.Stop(TimeSpan.Zero), "Stop must honor its caller-supplied wait bound.");
                release.Set();
                Assert.True(worker.Stop(TimeSpan.FromSeconds(1)));
                Assert.True(worker.IsStopped);
                Assert.AreEqual(3, worker.ProcessedBlocks, "Stop drains blocks published before the producer stopped.");
                Assert.AreEqual(0, source.DroppedCaptureBlocks);
            }
        }

        [Test]
        public void ProcessorExceptionFaultsWorkerAndIsObservableAfterJoin()
        {
            var source = new FakeCaptureSource(capacity: 1);
            Assert.True(source.Publish(.5f, sequence: 0));
            var expected = new InvalidOperationException("processor failure");
            using (var worker = new MNNRealtimeAudioCaptureWorker(source, (_, __, ___) => throw expected))
            {
                worker.Start();
                Assert.True(SpinWait.SpinUntil(() => worker.IsFaulted, TimeSpan.FromSeconds(2)));
                Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
                Assert.True(worker.IsFaulted);
                Assert.AreSame(expected, worker.Fault);
                Assert.Throws<InvalidOperationException>(() => worker.Start());
            }
        }

        [Test]
        public void UnstartedWorkerCanBeStoppedButCannotBeRestarted()
        {
            var worker = new MNNRealtimeAudioCaptureWorker(new FakeCaptureSource(1), (_, __, ___) => { });
            Assert.True(worker.Stop(TimeSpan.Zero));
            Assert.True(worker.IsStopped);
            Assert.Throws<InvalidOperationException>(() => worker.Start());
            worker.Dispose();
        }

        [Test]
        public void CompletionIsSharedAndStoppingBeforeStartDoesNotFlushTheProcessor()
        {
            using var modelFrames = new MNNRealtimeModelInputBuffer(1);
            var processor = new FakeVoiceProcessor(Format, 512);
            using var worker = new MNNRealtimeAudioCaptureWorker(new FakeCaptureSource(1), processor, modelFrames);
            Task completion = worker.Completion;
            Assert.AreSame(completion, worker.Completion);
            Assert.False(completion.IsCompleted);
            Assert.True(worker.Stop(TimeSpan.Zero));
            Assert.AreEqual(TaskStatus.RanToCompletion, completion.Status);
            Assert.AreEqual(0, processor.CompleteCount);
            Assert.IsNull(worker.Fault);
            Assert.True(worker.Stop(TimeSpan.Zero));
            worker.Dispose();
            Assert.AreSame(completion, worker.Completion);
            Assert.Throws<InvalidOperationException>(() => worker.Start());
        }

        [Test]
        public void CompletionWaitsForTheFinalFlushAndStopTimeoutDoesNotCompleteIt()
        {
            using var entered = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            using var modelFrames = new MNNRealtimeModelInputBuffer(1);
            var processor = new FakeVoiceProcessor(Format, 512)
            {
                OnComplete = () =>
                {
                    entered.Set();
                    release.Wait();
                }
            };
            using var worker = new MNNRealtimeAudioCaptureWorker(new FakeCaptureSource(1), processor, modelFrames);
            worker.Start();
            try
            {
                Assert.False(worker.Stop(TimeSpan.Zero));
                Assert.True(entered.Wait(TimeSpan.FromSeconds(3)));
                Assert.False(worker.Completion.IsCompleted, "A pending EOS flush still owns processor state.");
                Assert.False(worker.IsStopped);
                Assert.False(worker.Stop(TimeSpan.Zero));
                Assert.False(worker.Completion.IsCompleted);
            }
            finally
            {
                release.Set();
                Assert.True(worker.Stop(TimeSpan.FromSeconds(3)));
            }
            Assert.AreEqual(TaskStatus.RanToCompletion, worker.Completion.Status);
            Assert.True(worker.IsStopped);
            Assert.AreEqual(1, processor.CompleteCount);
        }

        [Test]
        public void CompletionPublishesOriginalProcessorFailureWithoutRequiringStopOrPollingFault()
        {
            var source = new FakeCaptureSource(1);
            Assert.True(source.Publish(.5f, 0));
            var error = new InvalidOperationException("input processor failed");
            using var worker = new MNNRealtimeAudioCaptureWorker(source, (_, __, ___) => throw error);
            worker.Start();
            AwaitCompletion(worker.Completion);
            Assert.AreSame(error, Assert.Throws<InvalidOperationException>(() => worker.Completion.GetAwaiter().GetResult()));
            Assert.AreSame(error, worker.Fault);
            Assert.True(worker.IsFaulted);
            Assert.True(worker.Stop(TimeSpan.FromSeconds(3)));
            Assert.AreSame(error, Assert.Throws<InvalidOperationException>(() => worker.Completion.GetAwaiter().GetResult()));
        }

        [Test]
        public void FlushFailureFaultsCompletionAndIsNeverReportedAsAStoppedSuccess()
        {
            using var modelFrames = new MNNRealtimeModelInputBuffer(1);
            var error = new InvalidOperationException("EOS flush failed");
            var processor = new FakeVoiceProcessor(Format, 512) { OnComplete = () => throw error };
            using var worker = new MNNRealtimeAudioCaptureWorker(new FakeCaptureSource(1), processor, modelFrames);
            worker.Start();
            Assert.True(worker.Stop(TimeSpan.FromSeconds(3)));
            Assert.True(worker.Completion.IsFaulted);
            Assert.False(worker.IsStopped);
            Assert.True(worker.IsFaulted);
            Assert.AreSame(error, worker.Fault);
            Assert.AreSame(error, Assert.Throws<InvalidOperationException>(() => worker.Completion.GetAwaiter().GetResult()));
            Assert.AreEqual(1, processor.CompleteCount);
        }

        [TestCase(false), TestCase(true)]
        public void CompletionContinuationCanJoinAndDisposeWithoutRunningOnTheCaptureThread(bool fault)
        {
            var source = new FakeCaptureSource(1);
            Assert.True(source.Publish(.5f, 0));
            int captureThread = 0;
            var error = new InvalidOperationException("input failure");
            using var worker = new MNNRealtimeAudioCaptureWorker(source, (_, __, ___) =>
            {
                captureThread = Thread.CurrentThread.ManagedThreadId;
                if (fault)
                    throw error;
            });
            // ExecuteSynchronously deliberately asks for inline execution. The worker must refuse it.
            var observed = worker.Completion.ContinueWith(completion =>
            {
                Assert.AreNotEqual(captureThread, Thread.CurrentThread.ManagedThreadId);
                Assert.True(worker.Stop(TimeSpan.FromSeconds(3)), "An inline continuation would attempt to join itself.");
                worker.Dispose();
                if (fault)
                    Assert.AreSame(error, completion.Exception.InnerException);
                else
                    Assert.AreEqual(TaskStatus.RanToCompletion, completion.Status);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            worker.Start();
            if (!fault)
                Assert.True(worker.Stop(TimeSpan.FromSeconds(3)));
            AwaitCompletion(observed);
            observed.GetAwaiter().GetResult();
        }

        [Test]
        public void ConcurrentStartAndStopAlwaysResolveTheSameCompletion()
        {
            for (int iteration = 0; iteration < 32; ++iteration)
            {
                using var gate = new ManualResetEventSlim(false);
                using var worker = new MNNRealtimeAudioCaptureWorker(new FakeCaptureSource(1), (_, __, ___) => { });
                Task completion = worker.Completion;
                var start = Task.Run(() =>
                {
                    gate.Wait();
                    try
                    {
                        worker.Start();
                    }
                    catch (InvalidOperationException)
                    {
                        // Stop won the lifecycle lock.
                    }
                });
                var stop = Task.Run(() =>
                {
                    gate.Wait();
                    Assert.True(worker.Stop(TimeSpan.FromSeconds(3)));
                });
                gate.Set();
                AwaitCompletion(Task.WhenAll(start, stop));
                start.GetAwaiter().GetResult();
                stop.GetAwaiter().GetResult();
                Assert.AreSame(completion, worker.Completion);
                Assert.AreEqual(TaskStatus.RanToCompletion, completion.Status);
                Assert.True(worker.IsStopped);
                Assert.True(worker.Stop(TimeSpan.Zero));
            }
        }

        [Test]
        public void StandardPipelinePublishesWorkerFramesToModelQueueAndFlushesOnStop()
        {
            var source = new FakeCaptureSource(capacity: 2, channels: 2, sampleRate: 48000, maximumSamples: 2048);
            var input = new float[1024];
            for (int i = 0; i < input.Length; i += 2)
            {
                input[i] = .4f;
                input[i + 1] = .2f;
            }
            Assert.True(source.Publish(input, sequence: 0));
            using var modelFrames = new MNNRealtimeModelInputBuffer(2);
            var processor = new FakeVoiceProcessor(new MNNPcmFormat(48000, 1), maximumInputBlockFrames: 512);
            using (var worker = new MNNRealtimeAudioCaptureWorker(source, processor, modelFrames))
            {
                worker.Start();
                Assert.True(SpinWait.SpinUntil(() => !source.HasPendingCaptureFrames, TimeSpan.FromSeconds(2)));
                Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
                Assert.AreEqual(512, worker.ProcessedInputFrames);
                Assert.AreEqual(0, processor.ResetCount);
                var modelPcm = new float[512];
                Assert.True(modelFrames.TryRead(modelPcm, 0, out var metadata));
                Assert.AreEqual(0, metadata.StartSample);
                Assert.AreEqual(.75f, metadata.SpeechProbability);
                Assert.That(modelPcm, Is.All.EqualTo(.3f).Within(.0001f));
                Assert.False(modelFrames.HasPendingFrames);
            }
        }

        [Test]
        public void CaptureDiscontinuityResetsVadAndMarksTheNextModelFrame()
        {
            var source = new FakeCaptureSource(capacity: 2, maximumSamples: 512);
            Assert.True(source.Publish(new float[512], sequence: 0));
            Assert.True(source.Publish(new float[512], sequence: 1, discontinuity: true));
            using var modelFrames = new MNNRealtimeModelInputBuffer(2);
            var processor = new FakeVoiceProcessor(Format, maximumInputBlockFrames: 512);
            using (var worker = new MNNRealtimeAudioCaptureWorker(source, processor, modelFrames))
            {
                worker.Start();
                Assert.True(SpinWait.SpinUntil(() => !source.HasPendingCaptureFrames, TimeSpan.FromSeconds(2)));
                Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
                Assert.AreEqual(1, processor.ResetCount);
                Assert.True(modelFrames.TryRead(new float[512], 0, out var first));
                Assert.False(first.Discontinuity);
                Assert.True(modelFrames.TryRead(new float[512], 0, out var afterGap));
                Assert.True(afterGap.Discontinuity);
            }
        }

        [Test]
        public void ModelQueueBackpressureMarksTheNextAcceptedFrameAsDiscontinuous()
        {
            var source = new FakeCaptureSource(capacity: 2, maximumSamples: 512);
            Assert.True(source.Publish(new float[512], sequence: 0));
            Assert.True(source.Publish(new float[512], sequence: 1));
            using var modelFrames = new MNNRealtimeModelInputBuffer(1);
            var initial = new float[512];
            Assert.True(modelFrames.TryWrite(initial, 0, new MNNRealtimeVadUpdate(0, .1f, false, false, false)));
            var processor = new FakeVoiceProcessor(Format, maximumInputBlockFrames: 512);
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            using (var processed = new CountdownEvent(2))
            using (var worker = new MNNRealtimeAudioCaptureWorker(source, processor, modelFrames, _ =>
            {
                if (processed.CurrentCount == 2)
                {
                    entered.Set();
                    release.Wait();
                }
                processed.Signal();
            }))
            {
                worker.Start();
                Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
                Assert.True(modelFrames.TryRead(new float[512], 0, out _), "Remove the frame that filled the model queue.");
                release.Set();
                Assert.True(processed.Wait(TimeSpan.FromSeconds(2)));
                Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
                Assert.AreEqual(1, worker.DroppedModelFrames);
                Assert.True(modelFrames.TryRead(new float[512], 0, out var recovered));
                Assert.True(recovered.Discontinuity);
            }
        }

        [TestCase(-1, false), TestCase(0, false), TestCase(1, true)]
        public void CaptureAgeUsesFirstSampleTimeAndAnInclusiveBudget(long ticksPastBudget, bool expired)
        {
            long now = Stopwatch.Frequency * 10L;
            long budget = Stopwatch.Frequency;
            var source = new FakeCaptureSource(1);
            Assert.True(source.Publish(new float[4], 0, timestamp: now - budget - ticksPastBudget));
            int callbacks = 0;
            using var worker = ClockedWorker(source, (_, __, ___) => ++callbacks, () => now, TimeSpan.FromSeconds(1));
            worker.Start();
            Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
            Assert.False(worker.IsFaulted, worker.Fault?.ToString());
            Assert.AreEqual(expired ? 0 : 1, callbacks);
            Assert.AreEqual(expired ? 1 : 0, worker.ExpiredCaptureBlocks);
            Assert.AreEqual(expired ? 4 : 0, worker.ExpiredCaptureFrames);
            Assert.AreEqual(0, worker.DroppedCaptureBlocks, "Consumer expiry and producer overflow are different counters.");
        }

        [Test]
        public void ExpiredCaptureResetsVoiceStateAndMarksOnlyTheNextDeliveredModelFrame()
        {
            long now = Stopwatch.Frequency * 10L;
            var source = new FakeCaptureSource(3, maximumSamples: 512);
            Assert.True(source.Publish(new float[512], 0, timestamp: 0));
            Assert.True(source.Publish(new float[512], 1, timestamp: now));
            Assert.True(source.Publish(new float[512], 2, timestamp: now));
            using var modelFrames = new MNNRealtimeModelInputBuffer(2);
            var processor = new FakeVoiceProcessor(Format, 512);
            using var worker = new MNNRealtimeAudioCaptureWorker(source, null, processor, modelFrames, null,
                "Capture age test", TimeSpan.FromSeconds(1), () => now);
            worker.Start();
            Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
            Assert.False(worker.IsFaulted, worker.Fault?.ToString());
            Assert.AreEqual(1, processor.ResetCount);
            Assert.AreEqual(1, worker.ExpiredCaptureBlocks);
            Assert.AreEqual(512, worker.ExpiredCaptureFrames);
            Assert.AreEqual(2, worker.ProcessedBlocks);
            Assert.AreEqual(1024, worker.ProcessedInputFrames);
            Assert.True(modelFrames.TryRead(new float[512], 0, out var afterGap));
            Assert.True(afterGap.Discontinuity);
            Assert.True(modelFrames.TryRead(new float[512], 0, out var continuous));
            Assert.False(continuous.Discontinuity);
            Assert.False(modelFrames.HasPendingFrames);
        }

        [Test]
        public void ProcessingStallDiscardsTheNowExpiredBacklogAndPreservesFreshPcm()
        {
            long now = Stopwatch.Frequency * 10L;
            long later = now + Stopwatch.Frequency + 1;
            var source = new FakeCaptureSource(3);
            Assert.True(source.Publish(new[] { .1f, .1f, .1f, .1f }, 0, timestamp: now));
            Assert.True(source.Publish(new[] { .2f, .2f, .2f, .2f }, 1, timestamp: now));
            Assert.True(source.Publish(new[] { .3f, .3f, .3f, .3f }, 2, timestamp: later));
            var delivered = new List<MNNAudioFrameInfo>();
            var values = new List<float>();
            using var worker = ClockedWorker(source, (pcm, _, info) =>
            {
                delivered.Add(info);
                values.Add(pcm[0]);
                now = later; // Simulate time spent in a blocked or slow processor without sleeping.
            }, () => now, TimeSpan.FromSeconds(1));
            worker.Start();
            Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
            Assert.False(worker.IsFaulted, worker.Fault?.ToString());
            CollectionAssert.AreEqual(new[] { .1f, .3f }, values);
            Assert.False(delivered[0].Discontinuity);
            Assert.True(delivered[1].Discontinuity);
            Assert.AreEqual(2, delivered[1].Sequence);
            Assert.AreEqual(8, delivered[1].StartFrame);
            Assert.AreEqual(later, delivered[1].MonotonicTicks);
            Assert.AreEqual(1, worker.ExpiredCaptureBlocks);
        }

        [Test]
        public void TrailingExpiryResetsPartialVoiceStateBeforeShutdownFlush()
        {
            long now = Stopwatch.Frequency * 10L;
            var source = new FakeCaptureSource(2, maximumSamples: 512);
            Assert.True(source.Publish(new float[512], 0, timestamp: now));
            Assert.True(source.Publish(new float[512], 1, timestamp: now));
            using var modelFrames = new MNNRealtimeModelInputBuffer(2);
            var processor = new FakeVoiceProcessor(Format, 512);
            using var worker = new MNNRealtimeAudioCaptureWorker(source, null, processor, modelFrames,
                _ => now += Stopwatch.Frequency * 2L, "Trailing capture age test", TimeSpan.FromSeconds(1), () => now);
            worker.Start();
            Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
            Assert.False(worker.IsFaulted, worker.Fault?.ToString());
            Assert.AreEqual(1, worker.ProcessedBlocks);
            Assert.AreEqual(1, worker.ExpiredCaptureBlocks);
            Assert.AreEqual(1, processor.ResetCount);
            Assert.AreEqual(1, processor.CompleteCount);
            Assert.AreEqual(1, processor.ResetCountAtComplete, "Old partial input must be discarded before flushing EOS.");
        }

        [Test]
        public void InfiniteAgeBudgetPreservesHistoricalReplayWithoutReadingTheLiveClock()
        {
            var source = new FakeCaptureSource(2);
            Assert.True(source.Publish(new float[4], 0, timestamp: 0));
            Assert.True(source.Publish(new float[4], 1, timestamp: 1));
            int callbacks = 0;
            using var worker = ClockedWorker(source, (_, __, ___) => ++callbacks,
                () => throw new InvalidOperationException("Offline replay must not read the live clock."), Timeout.InfiniteTimeSpan);
            worker.Start();
            Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
            Assert.False(worker.IsFaulted, worker.Fault?.ToString());
            Assert.AreEqual(2, callbacks);
            Assert.AreEqual(0, worker.ExpiredCaptureBlocks);
        }

        [Test]
        public void FutureCaptureTimestampFaultsBeforeDeliveringPcm()
        {
            var source = new FakeCaptureSource(1);
            Assert.True(source.Publish(new float[4], 0, timestamp: 11));
            int callbacks = 0;
            using var worker = ClockedWorker(source, (_, __, ___) => ++callbacks, () => 10, TimeSpan.FromSeconds(1));
            worker.Start();
            Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
            Assert.True(worker.IsFaulted);
            Assert.IsInstanceOf<InvalidOperationException>(worker.Fault);
            Assert.AreEqual(0, callbacks);
            Assert.AreEqual(0, worker.ExpiredCaptureBlocks);
        }

        [TestCase(2, 4, 101, 1, 1)] // Skipped sequence.
        [TestCase(0, 4, 101, 1, 1)] // Duplicate sequence.
        [TestCase(1, 5, 101, 1, 1)] // Missing samples.
        [TestCase(1, 3, 101, 1, 1)] // Overlapping samples.
        [TestCase(1, 4, 99, 1, 1)] // Regressing timestamp.
        [TestCase(1, 4, 101, 2, 1)] // Stream changed.
        [TestCase(1, 4, 101, 1, 2)] // Format epoch changed.
        public void UnflaggedCaptureDiscontinuitiesReachTheConsumer(long sequence, long startFrame,
            long timestamp, long streamId, long formatEpoch)
        {
            var source = new FakeCaptureSource(3);
            Assert.True(source.Publish(new float[4], 0, timestamp: 100));
            Assert.True(source.Publish(new float[4], sequence, timestamp: timestamp, startFrame: startFrame,
                streamId: streamId, formatEpoch: formatEpoch));
            Assert.True(source.Publish(new float[4], sequence + 1, timestamp: timestamp + 1,
                streamId: streamId, formatEpoch: formatEpoch));
            var gaps = new List<bool>();
            using var worker = ClockedWorker(source, (_, __, info) => gaps.Add(info.Discontinuity),
                () => 1000, TimeSpan.FromSeconds(1));
            worker.Start();
            Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
            Assert.False(worker.IsFaulted, worker.Fault?.ToString());
            CollectionAssert.AreEqual(new[] { false, true, false }, gaps);
        }

        [TestCase(true), TestCase(false)]
        public void CapturePositionOverflowFaultsBeforePcmIsDelivered(bool sequenceOverflow)
        {
            var source = new FakeCaptureSource(1);
            Assert.True(source.Publish(new float[4], sequenceOverflow ? long.MaxValue : 0,
                timestamp: 100, startFrame: sequenceOverflow ? 0 : long.MaxValue - 1));
            int callbacks = 0;
            using var worker = ClockedWorker(source, (_, __, ___) => ++callbacks, () => 1000, TimeSpan.FromSeconds(1));
            worker.Start();
            Assert.True(worker.Stop(TimeSpan.FromSeconds(2)));
            Assert.True(worker.IsFaulted);
            Assert.IsInstanceOf<OverflowException>(worker.Fault);
            Assert.AreEqual(0, callbacks);
        }

        [TestCase(0), TestCase(-2)]
        public void InvalidCaptureAgeBudgetIsRejectedBeforeAThreadStarts(int milliseconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNRealtimeAudioCaptureWorker(
                new FakeCaptureSource(1), (_, __, ___) => { }, maximumCaptureAge: TimeSpan.FromMilliseconds(milliseconds)));
        }

        private static void AwaitCompletion(Task task)
        {
            Assert.AreEqual(0, Task.WaitAny(new[] { task }, TimeSpan.FromSeconds(5)), "Worker completion timed out.");
        }

        private static MNNRealtimeAudioCaptureWorker ClockedWorker(FakeCaptureSource source,
            Action<float[], int, MNNAudioFrameInfo> process, Func<long> clock, TimeSpan maximumCaptureAge) =>
            new MNNRealtimeAudioCaptureWorker(source, process, null, null, null,
                "Capture clock test", maximumCaptureAge, clock);

        private sealed class FakeCaptureSource : IMNNAudioCaptureSource
        {
            private readonly MNNAudioCaptureBuffer _buffer;
            private long _nextFrame;

            public MNNPcmFormat CaptureFormat { get; }
            public int MaximumCaptureSamples => _buffer.MaximumSamples;
            public long DroppedCaptureBlocks => _buffer.DroppedBlocks;
            public bool HasPendingCaptureFrames => _buffer.HasPendingFrames;

            public FakeCaptureSource(int capacity, int channels = 1, int sampleRate = 16000, int maximumSamples = 16)
            {
                CaptureFormat = new MNNPcmFormat(sampleRate, channels);
                _buffer = new MNNAudioCaptureBuffer(capacity, maximumSamples);
            }

            public bool Publish(float value, long sequence, bool discontinuity = false)
            {
                var samples = new[] { value, value, value, value };
                return Publish(samples, sequence, discontinuity);
            }

            public bool Publish(float[] samples, long sequence, bool discontinuity = false,
                long? timestamp = null, long? startFrame = null, long streamId = 1, long formatEpoch = 1)
            {
                var info = new MNNAudioFrameInfo(streamId, formatEpoch, sequence,
                    startFrame ?? _nextFrame, timestamp ?? Stopwatch.GetTimestamp(),
                    CaptureFormat, discontinuity);
                _nextFrame = unchecked(info.StartFrame + samples.Length / CaptureFormat.Channels);
                return _buffer.TryWrite(samples, 0, samples.Length, info);
            }

            public bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count) =>
                _buffer.TryRead(destination, offset, out info, out count);
        }

        private sealed class FakeVoiceProcessor : IMNNRealtimeVoiceInputProcessor
        {
            public MNNPcmFormat InputFormat { get; }
            public int MaximumInputBlockFrames { get; }
            public int ResetCount { get; private set; }
            public int CompleteCount { get; private set; }
            public int ResetCountAtComplete { get; private set; }
            public Action OnComplete { get; set; }

            public FakeVoiceProcessor(MNNPcmFormat inputFormat, int maximumInputBlockFrames)
            {
                InputFormat = inputFormat;
                MaximumInputBlockFrames = maximumInputBlockFrames;
            }

            public void Process(float[] monoPcm, int offset, int inputFrames,
                Action<MNNRealtimeVadUpdate> onUpdate, bool discontinuity = false,
                Action<float[], int, MNNRealtimeVadUpdate> onFrame = null)
            {
                if (discontinuity)
                    Reset();
                var update = new MNNRealtimeVadUpdate(0, .75f, true, false, false);
                onFrame?.Invoke(monoPcm, offset, update);
                onUpdate(update);
            }

            public bool Complete(Action<MNNRealtimeVadUpdate> onUpdate,
                Action<float[], int, MNNRealtimeVadUpdate> onFrame = null)
            {
                ++CompleteCount;
                ResetCountAtComplete = ResetCount;
                OnComplete?.Invoke();
                return false;
            }

            public void Reset()
            {
                ++ResetCount;
            }
        }
    }
}
