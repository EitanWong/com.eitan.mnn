using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class RealtimeOutputPipelineTests
    {
        private static readonly MNNPcmFormat Format = new MNNPcmFormat(16000, 1);

        [Test]
        public void StableTextMustBeRegisteredBeforeLiveAudioAndPresentationCommitsOnlyAfterWatermark()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 320);
            var response = output.BeginResponse();
            var samples = new float[160];
            Array.Fill(samples, .2f);
            var chunk = new MNNSpeechChunkInfo(response, 10, 0, true);
            Assert.Throws<InvalidOperationException>(() => output.TryEnqueue(samples, 0, samples.Length, chunk));
            Assert.True(output.TryRegisterSegment(response, 10, "Hello."));
            Assert.True(output.TryEnqueue(samples, 0, samples.Length, chunk));

            var deviceBlock = new float[80];
            output.Render(deviceBlock, 0, deviceBlock.Length);
            Assert.AreEqual(1, output.DrainReceipts(1));
            var submitted = output.GetSegment(0);
            Assert.AreEqual(80, submitted.SubmittedFrames);
            Assert.False(submitted.IsPresented);

            output.Render(deviceBlock, 0, deviceBlock.Length);
            Assert.AreEqual(1, output.DrainReceipts(1));
            output.AcknowledgePresentation(159, estimated: false);
            Assert.False(output.GetSegment(0).IsPresented);
            output.AcknowledgePresentation(160, estimated: true);
            var presented = output.GetSegment(0);
            Assert.True(presented.IsPresented);
            Assert.True(presented.IsEstimatedPresentation);
            Assert.False(presented.IsInterrupted);
        }

        [Test]
        public void InterruptMarksPriorTextAndRejectsLateAudioWithoutMarkingNextResponseInterrupted()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var first = output.BeginResponse();
            Assert.True(output.TryRegisterSegment(first, 0, "First response."));
            var audio = new float[160];
            Array.Fill(audio, .5f);
            Assert.True(output.TryEnqueue(audio, 0, audio.Length, new MNNSpeechChunkInfo(first, 0, 0, true)));

            Assert.AreEqual(first, output.Interrupt());
            Assert.True(output.GetSegment(0).IsInterrupted);
            Assert.False(output.TryRegisterSegment(first, 1, "Late text."));
            Assert.False(output.TryEnqueue(audio, 0, audio.Length, new MNNSpeechChunkInfo(first, 0, 1, true)));
            var silence = new float[80];
            output.Render(silence, 0, silence.Length);
            Assert.That(silence, Is.All.EqualTo(0f));
            Assert.AreEqual(0, output.DrainReceipts(10));

            var second = output.BeginResponse();
            Assert.AreNotEqual(first, second);
            Assert.True(output.TryRegisterSegment(second, 0, "Second response."));
            Assert.True(output.TryEnqueue(audio, 0, audio.Length, new MNNSpeechChunkInfo(second, 0, 0, true)));
            output.Render(silence, 0, silence.Length);
            Assert.AreEqual(1, output.DrainReceipts(10));
            Assert.False(output.GetSegment(1).IsInterrupted);
        }

        [Test]
        public void PresentationWatermarkMayArriveBeforeItsPlaybackReceiptIsDrained()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
            var response = output.BeginResponse();
            Assert.True(output.TryRegisterSegment(response, 0, "Already played."));
            var audio = new float[80];
            Assert.True(output.TryEnqueue(audio, 0, audio.Length, new MNNSpeechChunkInfo(response, 0, 0, true)));
            output.Render(audio, 0, audio.Length);

            output.AcknowledgePresentation(80, estimated: true);
            Assert.AreEqual(1, output.DrainReceipts(1));
            var segment = output.GetSegment(0);
            Assert.True(segment.IsPresented);
            Assert.True(segment.IsEstimatedPresentation);
        }

        [Test]
        public void PartialInterruptionPreservesSubmissionHistoryAndNextResponseWatermark()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 241, blockCapacity: 2);
            var old = output.BeginResponse();
            Assert.True(output.TryRegisterSegment(old, 0, "Interrupted sentence."));
            var audio = new float[241];
            Array.Fill(audio, .25f);
            Assert.True(output.TryEnqueue(audio, 0, audio.Length, new MNNSpeechChunkInfo(old, 0, 0, true)));
            var rendered = new float[121];
            output.Render(rendered, 0, rendered.Length);

            output.Interrupt();
            var next = output.BeginResponse();
            Assert.True(output.TryRegisterSegment(next, 0, "Next sentence."));
            Array.Fill(audio, .5f);
            Assert.True(output.TryEnqueue(audio, 0, 67, new MNNSpeechChunkInfo(next, 0, 0, true)));
            Assert.True(output.CompleteResponse(next));
            output.Render(rendered, 0, rendered.Length);
            Assert.AreEqual(3, output.DrainReceipts(10));
            output.AcknowledgePresentation(188, estimated: false);

            var interrupted = output.GetSegment(0);
            Assert.AreEqual(121, interrupted.SubmittedFrames);
            Assert.True(interrupted.IsInterrupted);
            Assert.False(interrupted.IsPresented, "The discarded remainder cannot commit the full spoken text.");
            var presented = output.GetSegment(1);
            Assert.AreEqual(67, presented.SubmittedFrames);
            Assert.True(presented.IsPresented);
            Assert.False(presented.IsInterrupted);
            Assert.True(output.CanBeginNextResponse(next));
            Assert.AreEqual(0, output.QueuedFrames);
            Assert.AreEqual(0.0, output.QueuedAudioMilliseconds);
        }

        [Test]
        public void LedgerCapacityAndReceiptTransportFaultRemainExplicit()
        {
            var bounded = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160,
                blockCapacity: 2, receiptCapacity: 1, segmentCapacity: 1);
            var response = bounded.BeginResponse();
            Assert.True(bounded.TryRegisterSegment(response, 0, "One."));
            Assert.False(bounded.TryRegisterSegment(response, 1, "Two."));
            var audio = new float[160];
            Assert.True(bounded.TryEnqueue(audio, 0, 160, new MNNSpeechChunkInfo(response, 0, 0, true)));
            var render = new float[320];
            bounded.Render(render, 0, render.Length);
            Assert.AreEqual(1, bounded.DrainReceipts(1));

            var receiptOverflow = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 80,
                blockCapacity: 2, receiptCapacity: 1);
            var secondResponse = receiptOverflow.BeginResponse();
            Assert.True(receiptOverflow.TryRegisterSegment(secondResponse, 0, "Overflow."));
            Assert.True(receiptOverflow.TryEnqueue(audio, 0, 80,
                new MNNSpeechChunkInfo(secondResponse, 0, 0, false)));
            Assert.True(receiptOverflow.TryEnqueue(audio, 80, 80,
                new MNNSpeechChunkInfo(secondResponse, 0, 1, true)));
            var twoCallbacks = new float[160];
            receiptOverflow.Render(twoCallbacks, 0, 80);
            receiptOverflow.Render(twoCallbacks, 80, 80);
            Assert.True(receiptOverflow.IsFaulted);
            Assert.Throws<InvalidOperationException>(() => receiptOverflow.BeginResponse());
            Assert.AreEqual(0, receiptOverflow.DrainReceipts(0));
            Assert.AreEqual(1, receiptOverflow.DrainReceipts(10));
            Assert.AreEqual(0, receiptOverflow.DrainReceipts(10));
        }

        [Test]
        public void InvalidReceiptDrainLimitAndPresentationRegressionAreRejected()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 80);
            Assert.Throws<ArgumentOutOfRangeException>(() => output.DrainReceipts(-1));
            output.AcknowledgePresentation(10, estimated: false);
            Assert.Throws<ArgumentOutOfRangeException>(() => output.AcknowledgePresentation(9, estimated: false));
        }

        [Test]
        public void StreamingSpeechCanRenderFirstBlockBeforeSynthesisAndResponseFinish()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160, blockCapacity: 2);
            var response = output.BeginResponse();
            var stream = new MNNRealtimeSpeechStream(output, response);
            Assert.True(stream.BeginSegment(1, "A stable first sentence."));
            var first = new float[80];
            Array.Fill(first, .25f);
            Assert.True(stream.TryPublishPcm(first, 0, first.Length, endsSegment: false));
            var rendered = new float[80];
            output.Render(rendered, 0, rendered.Length);
            Assert.That(rendered, Is.All.EqualTo(.25f));
            Assert.That(stream.FirstChunkLatencyMilliseconds, Is.GreaterThanOrEqualTo(0));

            var last = new float[80];
            Array.Fill(last, .5f);
            Assert.True(stream.TryPublishPcm(last, 0, last.Length, endsSegment: true));
            Assert.True(stream.CompleteResponse());
            output.Render(rendered, 0, rendered.Length);
            Assert.That(rendered, Is.All.EqualTo(.5f));
        }

        [Test]
        public void SustainedRealtimeHeadroomRequiresWeightedRtfStrictlyBelowOne()
        {
            long oneSecond = Stopwatch.Frequency;
            var fast = new MNNRealtimeSynthesisMeter();
            fast.Record(oneSecond / 2, 16000, 16000);
            Assert.That(fast.WindowRtf, Is.EqualTo(.5).Within(.001));
            Assert.True(fast.HasRealtimeHeadroom);
            var slow = new MNNRealtimeSynthesisMeter();
            slow.Record(oneSecond * 11 / 10, 16000, 16000);
            Assert.That(slow.WindowRtf, Is.EqualTo(1.1).Within(.001));
            Assert.False(slow.HasRealtimeHeadroom);
            Assert.That(slow.LifetimeRtf, Is.EqualTo(slow.WindowRtf).Within(.001));
        }

        [Test]
        public void StreamingSpeechRetainsChunkAfterBackpressureUntilRetry()
        {
            var output = new MNNRealtimeOutputPipeline(Format, framesPerBlock: 80, blockCapacity: 1);
            var response = output.BeginResponse();
            var stream = new MNNRealtimeSpeechStream(output, response);
            Assert.True(stream.BeginSegment(2, "Retry safely."));
            var pcm = new float[80];
            Assert.True(stream.TryPublishPcm(pcm, 0, pcm.Length, endsSegment: false));
            Assert.False(stream.TryPublishPcm(pcm, 0, pcm.Length, endsSegment: true));
            output.Render(pcm, 0, pcm.Length);
            Assert.True(stream.TryPublishPcm(pcm, 0, pcm.Length, endsSegment: true));
            Assert.True(stream.CompleteResponse());
        }

        [Test]
        public void SynthesisMetricsCanBeSnapshottedConcurrentlyWithoutTearing()
        {
            const int chunks = 3000;
            var meter = new MNNRealtimeSynthesisMeter(minimumWindowAudioSeconds: .1);
            Exception writerFailure = null;
            Exception readerFailure = null;
            bool writerDone = false;
            var writer = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < chunks; ++i)
                        meter.Record(Stopwatch.Frequency / 200, 160, 16000);
                }
                catch (Exception error) { writerFailure = error; }
                finally { Volatile.Write(ref writerDone, true); }
            });
            var reader = new Thread(() =>
            {
                try
                {
                    while (!Volatile.Read(ref writerDone))
                    {
                        double window = meter.WindowRtf;
                        double lifetime = meter.LifetimeRtf;
                        if (!double.IsPositiveInfinity(window)) Assert.That(window, Is.InRange(0.0, 1.0));
                        if (!double.IsPositiveInfinity(lifetime)) Assert.That(lifetime, Is.InRange(0.0, 1.0));
                        Assert.That(meter.WindowChunkCount, Is.InRange(0, 128));
                    }
                }
                catch (Exception error) { readerFailure = error; }
            });

            writer.Start();
            reader.Start();
            Assert.True(writer.Join(10000), "Synthesis metric writer did not complete.");
            Assert.True(reader.Join(10000), "Synthesis metric reader did not complete.");
            Assert.IsNull(writerFailure);
            Assert.IsNull(readerFailure);
            Assert.AreEqual(128, meter.WindowChunkCount);
            Assert.That(meter.WindowRtf, Is.EqualTo(.5).Within(.001));
            Assert.That(meter.LifetimeRtf, Is.EqualTo(.5).Within(.001));
            Assert.True(meter.HasRealtimeHeadroom);
        }
    }
}
