using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class RealtimeModelInputBufferTests
    {
        [TestCase(-1, false), TestCase(0, false), TestCase(1, true)]
        public void QueueAgeBudgetIncludesItsExactBoundary(long ticksPastBudget, bool expired)
        {
            long now = Stopwatch.Frequency * 10L;
            long enqueued = now;
            using var queue = new MNNRealtimeModelInputBuffer(1, TimeSpan.FromSeconds(1), () => now);
            var frame = new float[512];
            Assert.True(queue.TryWrite(frame, 0, Vad(0)));
            now += Stopwatch.Frequency + ticksPastBudget;
            Assert.AreEqual(!expired, queue.TryRead(frame, 0, out var info));
            Assert.AreEqual(expired ? 1 : 0, queue.ExpiredFrames);
            Assert.AreEqual(0, queue.DroppedFrames);
            Assert.False(queue.HasPendingFrames);
            if (!expired)
                Assert.AreEqual(enqueued, info.EnqueuedMonotonicTicks);
        }

        [Test]
        public void ModelPauseDropsOldFramesPreservesFreshPcmAndMarksOnlyTheNextDelivery()
        {
            long now = 0;
            using var queue = new MNNRealtimeModelInputBuffer(3, TimeSpan.FromSeconds(1), () => now);
            var frame = new float[512];
            Array.Fill(frame, .1f);
            Assert.True(queue.TryWrite(frame, 0, Vad(0)));
            now = Stopwatch.Frequency + 1L;
            Array.Fill(frame, .2f);
            Assert.True(queue.TryWrite(frame, 0, Vad(512)));
            Array.Fill(frame, .3f);
            Assert.True(queue.TryWrite(frame, 0, Vad(1024)));
            var output = new float[516];
            Array.Fill(output, -.5f);
            Assert.True(queue.TryRead(output, 2, out var afterGap));
            Assert.AreEqual(1, afterGap.Sequence);
            Assert.AreEqual(512, afterGap.StartSample);
            Assert.AreEqual(now, afterGap.EnqueuedMonotonicTicks);
            Assert.AreEqual(.75f, afterGap.SpeechProbability);
            Assert.True(afterGap.IsSpeechActive);
            Assert.True(afterGap.SpeechStarted);
            Assert.False(afterGap.SpeechEnded);
            Assert.True(afterGap.Discontinuity);
            Assert.AreEqual(-.5f, output[1]);
            Assert.AreEqual(-.5f, output[514]);
            for (int i = 2; i < 514; ++i)
                Assert.AreEqual(.2f, output[i]);
            Assert.True(queue.TryRead(output, 0, out var continuous));
            Assert.AreEqual(2, continuous.Sequence);
            Assert.False(continuous.Discontinuity);
            for (int i = 0; i < 512; ++i)
                Assert.AreEqual(.3f, output[i]);
            Assert.AreEqual(1, queue.ExpiredFrames);
            Assert.AreEqual(0, queue.DroppedFrames);
        }

        [Test]
        public void AllExpiredFramesLeaveDestinationUntouchedAndGapSurvivesAnEmptyQueue()
        {
            long now = 0;
            using var queue = new MNNRealtimeModelInputBuffer(2, TimeSpan.FromSeconds(1), () => now);
            var frame = new float[512];
            Assert.True(queue.TryWrite(frame, 0, Vad(0)));
            Assert.True(queue.TryWrite(frame, 0, Vad(512)));
            now = Stopwatch.Frequency + 1L;
            Array.Fill(frame, -.5f);
            Assert.True(queue.WaitForData(TimeSpan.Zero), "Availability is independent of age eligibility.");
            Assert.False(queue.TryRead(frame, 0, out _));
            Assert.That(frame, Is.All.EqualTo(-.5f));
            Assert.False(queue.HasPendingFrames);
            Assert.False(queue.WaitForData(TimeSpan.Zero), "An old wake signal must not resurrect expired frames.");
            Assert.False(queue.TryRead(frame, 0, out _));
            Assert.AreEqual(2, queue.ExpiredFrames);
            Assert.True(queue.TryWrite(frame, 0, Vad(1024)));
            Assert.True(queue.TryRead(frame, 0, out var recovered));
            Assert.True(recovered.Discontinuity);
            Assert.AreEqual(2, recovered.Sequence);
            Assert.AreEqual(1024, recovered.StartSample);
        }

        [Test]
        public void DestinationValidationDoesNotRemoveEvenAnExpiredFrame()
        {
            long now = 0;
            using var queue = new MNNRealtimeModelInputBuffer(1, TimeSpan.FromSeconds(1), () => now);
            var frame = new float[512];
            Assert.True(queue.TryWrite(frame, 0, Vad(0)));
            now = Stopwatch.Frequency + 1L;
            Assert.Throws<ArgumentNullException>(() => queue.TryRead(null, 0, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => queue.TryRead(frame, 1, out _));
            Assert.True(queue.HasPendingFrames);
            Assert.AreEqual(0, queue.ExpiredFrames);
            Assert.False(queue.TryRead(frame, 0, out _));
            Assert.AreEqual(1, queue.ExpiredFrames);
        }

        [Test]
        public void ProducerOverflowAndConsumerExpiryKeepIndependentGapsAcrossWraparound()
        {
            long now = 0;
            using var queue = new MNNRealtimeModelInputBuffer(1, TimeSpan.FromSeconds(1), () => now);
            var frame = new float[512];
            Assert.True(queue.TryWrite(frame, 0, Vad(0)));
            Assert.False(queue.TryWrite(frame, 0, Vad(512)));
            now = Stopwatch.Frequency + 1L;
            Assert.False(queue.TryRead(frame, 0, out _));
            Assert.True(queue.TryWrite(frame, 0, Vad(1024)));
            Assert.True(queue.TryRead(frame, 0, out var recovered));
            Assert.True(recovered.Discontinuity);
            Assert.AreEqual(1, recovered.Sequence);
            Assert.True(queue.TryWrite(frame, 0, Vad(1536)));
            Assert.True(queue.TryRead(frame, 0, out var continuous));
            Assert.False(continuous.Discontinuity);
            Assert.AreEqual(1, queue.DroppedFrames);
            Assert.AreEqual(1, queue.ExpiredFrames);
        }

        [Test]
        public void OneReadExaminesOnlyItsWriteSnapshotWhenNewFramesArriveDuringExpiry()
        {
            long now = 0;
            var frame = new float[512];
            MNNRealtimeModelInputBuffer queue = null;
            Action duringRead = null;
            queue = new MNNRealtimeModelInputBuffer(3, TimeSpan.FromSeconds(1), () =>
            {
                var action = duringRead;
                duringRead = null;
                action?.Invoke();
                return now;
            });
            using (queue)
            {
                Assert.True(queue.TryWrite(frame, 0, Vad(0)));
                Assert.True(queue.TryWrite(frame, 0, Vad(512)));
                now = Stopwatch.Frequency + 1L;
                duringRead = () => Assert.True(queue.TryWrite(frame, 0, Vad(1024)));
                Assert.False(queue.TryRead(frame, 0, out _), "A read must not chase a moving producer indefinitely.");
                Assert.AreEqual(2, queue.ExpiredFrames);
                Assert.True(queue.HasPendingFrames);
                Assert.True(queue.WaitForData(TimeSpan.Zero));
                Assert.True(queue.TryRead(frame, 0, out var recovered));
                Assert.AreEqual(2, recovered.Sequence);
                Assert.True(recovered.Discontinuity);
            }
        }

        [Test]
        public void RegressingClockDoesNotConsumeTheAffectedFrameOrLosePendingGap()
        {
            long now = 0;
            using var queue = new MNNRealtimeModelInputBuffer(2, TimeSpan.FromSeconds(1), () => now);
            var frame = new float[512];
            Assert.True(queue.TryWrite(frame, 0, Vad(0)));
            now = Stopwatch.Frequency * 2L;
            Assert.True(queue.TryWrite(frame, 0, Vad(512)));
            now = Stopwatch.Frequency + 1L;
            Assert.Throws<InvalidOperationException>(() => queue.TryRead(frame, 0, out _));
            Assert.AreEqual(1, queue.ExpiredFrames);
            Assert.True(queue.HasPendingFrames);
            now = Stopwatch.Frequency * 2L;
            Assert.True(queue.TryRead(frame, 0, out var recovered));
            Assert.AreEqual(1, recovered.Sequence);
            Assert.True(recovered.Discontinuity);
        }

        [Test]
        public void NegativeEnqueueClockDoesNotPublishOrAdvanceTheSequence()
        {
            long now = -1;
            using var queue = new MNNRealtimeModelInputBuffer(1, TimeSpan.FromSeconds(1), () => now);
            var frame = new float[512];
            Assert.Throws<InvalidOperationException>(() => queue.TryWrite(frame, 0, Vad(0)));
            Assert.False(queue.HasPendingFrames);
            now = 100;
            Assert.True(queue.TryWrite(frame, 0, Vad(512)));
            now = -1;
            Assert.Throws<InvalidOperationException>(() => queue.TryRead(frame, 0, out _));
            Assert.True(queue.HasPendingFrames);
            now = 100;
            Assert.True(queue.TryRead(frame, 0, out var info));
            Assert.AreEqual(0, info.Sequence);
            Assert.False(info.Discontinuity);
        }

        [Test]
        public void DisablingExpiryStillTimestampsFramesForInputOwnership()
        {
            using var queue = new MNNRealtimeModelInputBuffer(1, Timeout.InfiniteTimeSpan,
                () => 12345);
            var frame = new float[512];
            Assert.True(queue.TryWrite(frame, 0, Vad(0)));
            Assert.True(queue.TryRead(frame, 0, out var info));
            Assert.AreEqual(12345, info.EnqueuedMonotonicTicks);
            Assert.AreEqual(0, queue.ExpiredFrames);
        }

        [TestCase(0), TestCase(-2)]
        public void InvalidAgeBudgetIsRejectedBeforeQueueUse(int milliseconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNRealtimeModelInputBuffer(
                1, TimeSpan.FromMilliseconds(milliseconds)));
        }

        [TestCase(false), TestCase(true)]
        public void ConcurrentProducerAndConsumerPreservePcmMetadataAndAccountForEveryFrame(bool expire)
        {
            const int attempts = 8192;
            long now = 0;
            using var queue = new MNNRealtimeModelInputBuffer(8,
                expire ? TimeSpan.FromMilliseconds(3) : Timeout.InfiniteTimeSpan,
                () => Interlocked.Read(ref now));
            int accepted = 0;
            int finished = 0;
            int delivered = 0;
            using var entered = new ManualResetEventSlim(false);
            using var start = new ManualResetEventSlim(false);
            var consumer = Task.Run(() =>
            {
                entered.Set();
                start.Wait();
                var output = new float[512];
                long nextSequence = 0;
                long lastPosition = -1;
                var bound = Stopwatch.StartNew();
                while (Volatile.Read(ref finished) == 0 || queue.HasPendingFrames)
                {
                    if (bound.Elapsed > TimeSpan.FromSeconds(5))
                        throw new TimeoutException("Model queue consumer did not finish.");
                    if (!queue.TryRead(output, 0, out var info))
                    {
                        Thread.Yield();
                        continue;
                    }
                    Assert.GreaterOrEqual(info.Sequence, nextSequence);
                    if (info.Sequence != nextSequence)
                        Assert.True(info.Discontinuity, "Expiry must be observable by the model owner.");
                    Assert.Greater(info.StartSample, lastPosition);
                    float value = (info.StartSample / 512 % 1000) / 1000f;
                    for (int i = 0; i < output.Length; ++i)
                        Assert.AreEqual(value, output[i], "PCM and metadata must belong to the same published slot.");
                    nextSequence = info.Sequence + 1;
                    lastPosition = info.StartSample;
                    ++delivered;
                }
            });
            try
            {
                Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
                start.Set();
                var frame = new float[512];
                for (int i = 0; i < attempts; ++i)
                {
                    Interlocked.Add(ref now, Math.Max(1, Stopwatch.Frequency / 1000));
                    Array.Fill(frame, (i % 1000) / 1000f);
                    if (queue.TryWrite(frame, 0, Vad(i * 512L)))
                        ++accepted;
                    if (i % 64 == 0)
                        Thread.Yield();
                }
            }
            finally
            {
                Volatile.Write(ref finished, 1);
                start.Set();
                consumer.GetAwaiter().GetResult();
            }
            Assert.Greater(accepted, 0);
            Assert.AreEqual(attempts, accepted + queue.DroppedFrames);
            Assert.AreEqual(accepted, delivered + queue.ExpiredFrames);
            if (!expire)
                Assert.AreEqual(0, queue.ExpiredFrames);
            Assert.False(queue.HasPendingFrames);
        }

        private static MNNRealtimeVadUpdate Vad(long position) =>
            new MNNRealtimeVadUpdate(position, .75f, true, true, false);
    }
}
