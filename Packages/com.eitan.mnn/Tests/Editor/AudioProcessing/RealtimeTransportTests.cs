using System;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class RealtimeTransportTests
    {
        [Test]
        public void CaptureCopiesBorrowedFramesAndCannotReplayAcrossGateEpochs()
        {
            var format = new MNNPcmFormat(16000, 1);
            var port = new MNNRealtimeCapturePort(4) { Accepting = true };
            var producer = port.CreateProducer(); producer.Initialize(format);
            var pcm = new float[400]; Array.Fill(pcm, .25f);
            producer.Submit(pcm, format);
            Array.Clear(pcm, 0, pcm.Length);
            var read = new float[port.MaximumCaptureSamples];
            for (int i = 0; i < 3; ++i)
            {
                Assert.True(port.TryReadCapture(read, 0, out var info, out int count));
                Assert.AreEqual(i * 160, info.StartFrame);
                Assert.AreEqual(i == 2 ? 80 : 160, count);
                Assert.AreEqual(.25f, read[count - 1]);
            }
            producer.Submit(pcm, format);
            port.Accepting = false; port.Accepting = true;
            Assert.False(port.TryReadCapture(read, 0, out _, out _));
            producer.Submit(pcm, format);
            Assert.True(port.TryReadCapture(read, 0, out var current, out _));
            Assert.True(current.Discontinuity);
            port.PauseCaptureAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.False(port.Accepting);
        }

        [Test]
        public void RenderPullHandlesLargeBuffersAndInterruptionWithoutConfirmingPresentation()
        {
            var format = new MNNPcmFormat(16000, 2);
            var output = new MNNRealtimeOutputPipeline(format, 160);
            var pull = new MNNRealtimeOutputPull(output); pull.Initialize(format);
            var response = output.BeginResponse();
            Assert.True(output.TryRegisterSegment(response, 0, "spoken"));
            var pcm = new float[320]; Array.Fill(pcm, .25f);
            Assert.True(output.TryEnqueue(pcm, 0, pcm.Length, new MNNSpeechChunkInfo(response, 0, 0, true)));
            var render = new float[960]; pull.Render(render, format);
            Assert.AreEqual(.25f, render[319]); Assert.Zero(render[320]);
            Assert.AreEqual(480, pull.RenderedFrames);
            output.DrainReceipts(20);
            Assert.False(output.GetSegment(0).IsPresented);
            output.Interrupt(); pull.Render(render, format);
            Assert.That(render, Is.All.Zero);
            pull.Disable(); Array.Fill(render, 1); pull.Render(render, format);
            Assert.That(render, Is.All.Zero);
        }

        [Test]
        public void CaptureAndRenderTransportAllocateNothingAfterWarmup()
        {
            long probeStart = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[4096]; GC.KeepAlive(probe);
            if (GC.GetAllocatedBytesForCurrentThread() - probeStart < probe.Length)
                Assert.Ignore("The runtime allocation counter is unavailable; use the recorded Mono harness.");
            var format = new MNNPcmFormat(16000, 1);
            var port = new MNNRealtimeCapturePort(4) { Accepting = true };
            using (var producer = port.CreateProducer())
            using (var pull = new MNNRealtimeOutputPull(new MNNRealtimeOutputPipeline(format, 160)))
            {
                producer.Initialize(format); pull.Initialize(format);
                var pcm = new float[160]; var read = new float[1920];
                for (int i = 0; i < 100; ++i)
                { producer.Submit(pcm, format); port.TryReadCapture(read, 0, out _, out _); pull.Render(pcm, format); }
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; ++i)
                { producer.Submit(pcm, format); port.TryReadCapture(read, 0, out _, out _); pull.Render(pcm, format); }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Zero(allocated, "Borrowed-span capture and render transport must not allocate per callback.");
                TestContext.WriteLine("Capture + queue + output pull, 1000 callbacks: " + allocated + " managed bytes.");
            }
        }
    }
}
