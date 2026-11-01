using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity.Tests
{
    public sealed class RealtimeSpeechTextChannelTests
    {
        private static readonly MNNPcmFormat Format = new MNNPcmFormat(16000, 1);

        [Test]
        public void BoundedChannelAppliesBackpressureAndPreservesSegmentOrderAndIds()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var response = output.BeginResponse();
            var channel = new MNNRealtimeSpeechTextChannel(output, response, capacity: 1);
            Assert.False(channel.Append("First. Second. "));
            Assert.AreEqual(1, channel.BackpressureCount);

            Assert.True(channel.TryRead(out var first));
            Assert.AreEqual(0, first.SegmentId);
            Assert.AreEqual("First.", first.Text);
            Assert.AreEqual(response, first.Response);
            Assert.True(channel.DrainPending());
            Assert.True(channel.Complete());

            Assert.True(channel.TryRead(out var second));
            Assert.AreEqual(1, second.SegmentId);
            Assert.AreEqual("Second.", second.Text);
            Assert.False(channel.TryRead(out _));
        }

        [Test]
        public void EosRemainsPendingUntilConsumerFreesCapacity()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var response = output.BeginResponse();
            var channel = new MNNRealtimeSpeechTextChannel(output, response, capacity: 1);
            Assert.True(channel.Append("Queued. tail"));
            Assert.False(channel.Complete());
            Assert.False(channel.IsCompleted);

            Assert.True(channel.TryRead(out var queued));
            Assert.AreEqual("Queued.", queued.Text);
            Assert.True(channel.Complete());
            Assert.True(channel.IsCompleted);
            Assert.True(channel.TryRead(out var tail));
            Assert.AreEqual("tail", tail.Text);
            Assert.True(channel.Complete());
        }

        [Test]
        public void InterruptedResponseRejectsNewTextWithoutPublishingIt()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var response = output.BeginResponse();
            var channel = new MNNRealtimeSpeechTextChannel(output, response);
            Assert.True(channel.Append("Old. "));
            output.Interrupt();
            Assert.False(channel.Append(" late"));
            Assert.AreEqual(1, channel.StaleAppendCount);
            Assert.True(channel.TryRead(out var oldSegment));
            Assert.AreEqual(response, oldSegment.Response);
            Assert.False(output.Accepts(oldSegment.Response));
        }

        [Test]
        public void WaitingConsumerWakesWhenTextArrivesOrTheChannelCompletes()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var response = output.BeginResponse();
            var channel = new MNNRealtimeSpeechTextChannel(output, response);
            Task waiting = channel.WaitForDataAsync();
            Assert.False(waiting.IsCompleted);

            Assert.True(channel.Append("你好。"));
            waiting.GetAwaiter().GetResult();
            Assert.True(channel.TryRead(out var segment));
            Assert.AreEqual("你好。", segment.Text);

            waiting = channel.WaitForDataAsync();
            Assert.True(channel.Complete());
            waiting.GetAwaiter().GetResult();
            Assert.True(channel.IsCompleted);
        }

        [Test]
        public void AsyncWaitingConsumerWakesOnInterruptionWithoutCancellingTheNextResponse()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var response = output.BeginResponse();
            var channel = new MNNRealtimeSpeechTextChannel(output, response);
            using var cleanup = new CancellationTokenSource();
            Task waiting = channel.WaitForDataAsync(cleanup.Token);
            try
            {
                Assert.False(waiting.IsCompleted);
                output.Interrupt();
                var next = output.BeginResponse();
                Assert.True(waiting.Wait(TimeSpan.FromSeconds(3)), "Interruption must wake an idle async consumer.");
                Assert.False(channel.IsCurrent);
                Assert.False(channel.IsCompleted, "Interruption must not report successful text EOS.");
                Assert.True(output.Accepts(next));
                var nextChannel = new MNNRealtimeSpeechTextChannel(output, next);
                Task nextWait = nextChannel.WaitForDataAsync(cleanup.Token);
                Assert.False(nextWait.IsCompleted, "An old response signal must not wake a new channel.");
                Assert.True(nextChannel.Complete());
                Assert.True(nextWait.Wait(TimeSpan.FromSeconds(3)));
            }
            finally
            {
                cleanup.Cancel();
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void InterruptedChannelDoesNotWaitEvenWhenInterruptionPrecedesSubscription(bool asynchronous, bool replaceResponse)
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var response = output.BeginResponse();
            var channel = new MNNRealtimeSpeechTextChannel(output, response);
            output.Interrupt();
            if (replaceResponse) output.BeginResponse();
            using var cleanup = new CancellationTokenSource();
            Task waiting = asynchronous ? channel.WaitForDataAsync(cleanup.Token)
                : Task.Run(() => channel.WaitForData(cleanup.Token));
            try
            {
                Assert.True(waiting.Wait(TimeSpan.FromSeconds(3)), "A stale channel cannot start waiting for new text.");
                Assert.False(channel.IsCompleted);
            }
            finally
            {
                cleanup.Cancel();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancelledWaitDoesNotConsumeFutureTextOrRetainAnInterruptSubscription(bool asynchronous)
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var response = output.BeginResponse();
            var channel = new MNNRealtimeSpeechTextChannel(output, response);
            using var cancellation = new CancellationTokenSource();
            Task waiting = asynchronous ? channel.WaitForDataAsync(cancellation.Token)
                : Task.Run(() => channel.WaitForData(cancellation.Token));
            cancellation.Cancel();
            Assert.True(SpinWait.SpinUntil(() => waiting.IsCompleted, TimeSpan.FromSeconds(3)));
            Assert.Catch<OperationCanceledException>(() => waiting.GetAwaiter().GetResult());
            Assert.True(channel.Append("Still active. "));
            Assert.True(channel.TryRead(out var segment));
            Assert.AreEqual("Still active.", segment.Text);
            output.Interrupt();
            Assert.False(channel.IsCurrent);
        }

        [Test]
        public void ConstructorAndInputValidationRemainExplicit()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var response = output.BeginResponse();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MNNRealtimeSpeechTextChannel(output, response, capacity: 0));
            Assert.Throws<System.ArgumentNullException>(() => new MNNRealtimeSpeechTextChannel(null, response));
            var channel = new MNNRealtimeSpeechTextChannel(output, response);
            Assert.Throws<System.ArgumentNullException>(() => channel.Append(null));
        }

        [Test]
        public void ConcurrentSpscProducerAndConsumerPreserveEverySegmentInOrder()
        {
            const int segmentCount = 2000;
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var response = output.BeginResponse();
            var channel = new MNNRealtimeSpeechTextChannel(output, response, capacity: 4, maximumSegmentCharacters: 64);
            Exception producerFailure = null;
            Exception consumerFailure = null;
            int consumed = 0;
            int abort = 0;
            bool producerDone = false;

            var consumer = new Thread(() =>
            {
                try
                {
                    long expectedId = 0;
                    while (!Volatile.Read(ref producerDone) || channel.HasQueuedSegments)
                    {
                        if (Volatile.Read(ref abort) != 0)
                            return;
                        if (!channel.TryRead(out var segment))
                        {
                            Thread.Yield();
                            continue;
                        }
                        Assert.AreEqual(expectedId, segment.SegmentId);
                        Assert.AreEqual("Sentence " + expectedId + "!", segment.Text);
                        Assert.AreEqual(response, segment.Response);
                        ++expectedId;
                        Interlocked.Increment(ref consumed);
                    }
                    Assert.AreEqual(segmentCount, expectedId);
                }
                catch (Exception error)
                {
                    consumerFailure = error;
                    Volatile.Write(ref abort, 1);
                }
            });

            var producer = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < segmentCount; ++i)
                    {
                        if (Volatile.Read(ref abort) != 0)
                            throw new InvalidOperationException("The consumer failed.");
                        string delta = "Sentence " + i + "! ";
                        while (!channel.Append(delta))
                        {
                            delta = string.Empty;
                            while (!channel.DrainPending())
                            {
                                if (Volatile.Read(ref abort) != 0)
                                    throw new InvalidOperationException("The consumer failed.");
                                Thread.Yield();
                            }
                        }
                    }
                    while (!channel.Complete())
                    {
                        while (!channel.DrainPending())
                            Thread.Yield();
                    }
                }
                catch (Exception error)
                {
                    producerFailure = error;
                    Volatile.Write(ref abort, 1);
                }
                finally
                {
                    Volatile.Write(ref producerDone, true);
                }
            });

            consumer.Start();
            producer.Start();
            Assert.True(producer.Join(10000), "Text producer did not complete.");
            Volatile.Write(ref abort, producerFailure == null ? 0 : 1);
            Assert.True(consumer.Join(10000), "Text consumer did not complete.");
            Assert.IsNull(producerFailure);
            Assert.IsNull(consumerFailure);
            Assert.AreEqual(segmentCount, consumed);
        }

        [Test]
        public void ConcurrentLedgerRegistrationAndSnapshotReadsRemainConsistent()
        {
            const int segmentCount = 1000;
            var ledger = new MNNConversationLedger(segmentCount);
            var responseOutput = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160, segmentCapacity: segmentCount);
            var response = responseOutput.BeginResponse();
            Exception writerFailure = null;
            Exception readerFailure = null;
            bool writerDone = false;

            var writer = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < segmentCount; ++i)
                        Assert.True(ledger.TryRegister(response, i, "Segment " + i));
                }
                catch (Exception error)
                {
                    writerFailure = error;
                }
                finally
                {
                    Volatile.Write(ref writerDone, true);
                }
            });

            var reader = new Thread(() =>
            {
                try
                {
                    while (!Volatile.Read(ref writerDone))
                    {
                        int count = ledger.Count;
                        if (count > 0)
                            Assert.AreEqual("Segment " + (count - 1), ledger.GetSegment(count - 1).Text);
                    }
                    Assert.AreEqual(segmentCount, ledger.Count);
                }
                catch (Exception error)
                {
                    readerFailure = error;
                }
            });

            writer.Start();
            reader.Start();
            Assert.True(writer.Join(10000), "Ledger writer did not complete.");
            Assert.True(reader.Join(10000), "Ledger reader did not complete.");
            Assert.IsNull(writerFailure);
            Assert.IsNull(readerFailure);
        }

        [Test]
        public void StableTextFlowsThroughTtsStreamAndPlaysBeforeResponseEos()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var response = output.BeginResponse();
            var textChannel = new MNNRealtimeSpeechTextChannel(output, response);
            var speechStream = new MNNRealtimeSpeechStream(output, response);
            Assert.True(textChannel.Append("First sentence. Second sentence."));
            Assert.True(textChannel.Append(" "));
            Assert.True(textChannel.TryRead(out var firstSegment));
            Assert.True(speechStream.BeginSegment(firstSegment));

            var firstBlock = new float[80];
            Array.Fill(firstBlock, .4f);
            Assert.True(speechStream.TryPublishPcm(firstBlock, 0, firstBlock.Length, endsSegment: true));
            var deviceBlock = new float[80];
            output.Render(deviceBlock, 0, deviceBlock.Length);
            CollectionAssert.AreEqual(firstBlock, deviceBlock);
            Assert.False(output.IsFaulted);
            Assert.False(output.CanBeginNextResponse(response));

            Assert.True(textChannel.TryRead(out var secondSegment));
            Assert.True(speechStream.BeginSegment(secondSegment));
            Assert.True(speechStream.TryPublishPcm(firstBlock, 0, firstBlock.Length, endsSegment: true));
            Assert.True(textChannel.Complete());
            Assert.True(speechStream.CompleteResponse());
        }

        [Test]
        public void SpeechStreamRejectsTextSegmentsFromAnotherResponseEpoch()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var oldResponse = output.BeginResponse();
            var oldChannel = new MNNRealtimeSpeechTextChannel(output, oldResponse);
            Assert.True(oldChannel.Append("Old sentence. "));
            Assert.True(oldChannel.TryRead(out var oldSegment));
            output.Interrupt();
            var currentResponse = output.BeginResponse();
            var currentStream = new MNNRealtimeSpeechStream(output, currentResponse);
            Assert.False(currentStream.BeginSegment(oldSegment));
            Assert.True(currentStream.IsCurrent);
        }
    }
}
