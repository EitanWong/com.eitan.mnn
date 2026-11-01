using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class RealtimeInputAndHistoryTests
    {
        [TestCase(0), TestCase(96), TestCase(240)]
        public void ConfiguredPreRollRetainsExactlyTheRequestedFrameWindow(int milliseconds)
        {
            var buffer = new MNNRealtimeUtteranceBuffer(32, 128, 2, milliseconds);
            var frame = new float[512];
            for (int i = 0; i < 12; ++i) { Array.Fill(frame, i); buffer.Append(frame, false); }
            Array.Fill(frame, 12); buffer.Append(frame, true);
            int frames = (milliseconds * 16 + 511) / 512;
            var audio = buffer.Snapshot();
            Assert.AreEqual((frames + 1) * 512, audio.Length);
            Assert.AreEqual(12 - frames, audio[0]);
            Assert.AreEqual(12, audio[audio.Length - 1]);
        }

        [Test]
        public void FrameAdapterKeepsProbabilitySeparateFromVadHangoverAndKeepsItsClock()
        {
            int detectorFrame = 0;
            var adapter = new MNNRealtimeFrameVoiceActivity((samples, offset) =>
            {
                ++detectorFrame;
                return new MNNRealtimeVadUpdate(0, detectorFrame == 1 ? .9f : .1f, true, true, false);
            });
            var frame = new float[512];
            Assert.True(adapter.ProcessFrame(frame, 0).SpeechStarted);
            var hangover = adapter.ProcessFrame(frame, 0);
            Assert.True(hangover.IsSpeechActive);
            Assert.AreEqual(.1f, hangover.SpeechProbability);
            Assert.False(hangover.SpeechStarted);
            Assert.AreEqual(512, hangover.StartSample, "A detector state reset must not reset the continuous adapter clock.");
            adapter.Reset();
            Assert.AreEqual(0, adapter.ProcessFrame(frame, 0).StartSample);
        }

        [Test]
        public void UtterancePreRollSilenceResumeAndGapHaveIndependentRevisions()
        {
            var buffer = new MNNRealtimeUtteranceBuffer(64, 128, 2);
            var frame = new float[512];
            for (int i = 0; i < 4; ++i) { frame[0] = i; buffer.Append(frame, false); }
            buffer.Append(frame, true);
            buffer.Append(frame, true);
            for (int i = 0; i < 4; ++i) buffer.Append(frame, false);
            Assert.True(buffer.IsCandidate);
            Assert.AreEqual(4 * 512, buffer.SilenceSamples);
            var copy = buffer.Snapshot();
            Assert.AreEqual(9 * 512, copy.Length);
            Assert.AreEqual(1, copy[0], "The most recent 96 ms pre-roll is retained.");
            long candidate = buffer.Revision;
            buffer.Append(frame, true);
            Assert.False(buffer.IsCandidate);
            Assert.AreEqual(0, buffer.SilenceSamples);
            Assert.Greater(buffer.Revision, candidate);
            buffer.Append(frame, false, true);
            Assert.False(buffer.HasSpeech);
            Assert.Throws<InvalidOperationException>(() => buffer.Snapshot());
        }
        [Test]
        public void EnergyVadReportsTransitionsAndReset()
        {
            var detector = MNNRealtimeFrameVoiceActivity.Energy(.1f);
            var frame = new float[512];
            Assert.False(detector.ProcessFrame(frame, 0).IsSpeechActive);
            for (int i = 0; i < frame.Length; ++i) frame[i] = .2f;
            var onset = detector.ProcessFrame(frame, 0);
            Assert.True(onset.SpeechStarted);
            Assert.False(detector.ProcessFrame(frame, 0).SpeechStarted);
            Array.Clear(frame, 0, frame.Length);
            Assert.True(detector.ProcessFrame(frame, 0).SpeechEnded);
            detector.Reset();
            Assert.AreEqual(0, detector.ProcessFrame(frame, 0).StartSample);
        }
        [Test]
        public void BufferedSpeechLedgerAcceptsLongRepliesAndKeepsItsTextBound()
        {
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 160);
            var response = output.BeginResponse();
            Assert.True(output.TryRegisterSegment(response, 0, new string('x', 65536)));
            Assert.Throws<ArgumentOutOfRangeException>(() => output.TryRegisterSegment(response, 1, new string('x', 65537)));
        }

        [Test]
        public void RetiredHistoryIgnoresLateReceiptsWithoutResettingPresentationClock()
        {
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 160);
            var response = output.BeginResponse();
            Assert.True(output.TryRegisterSegment(response, 0, "Spoken."));
            Assert.True(output.TryEnqueue(new float[160], 0, 160, new MNNSpeechChunkInfo(response, 0, 0, true)));
            output.Render(new float[160], 0, 160);
            output.AcknowledgePresentation(160, false);
            Assert.Throws<InvalidOperationException>(() => output.RetireHistoryThrough(response));
            output.Interrupt();
            output.RetireHistoryThrough(response);
            Assert.AreEqual(0, output.SegmentCount);
            Assert.AreEqual(2, output.DrainReceipts(256), "Render submission and presentation acknowledgement arrive independently.");
            Assert.Throws<ArgumentOutOfRangeException>(() => output.AcknowledgePresentation(159, false));
            var next = output.BeginResponse();
            Assert.True(output.TryRegisterSegment(next, 0, "New."));
        }
    }
}
