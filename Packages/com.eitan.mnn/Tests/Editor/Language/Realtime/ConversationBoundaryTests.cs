using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ConversationBoundaryTests
    {
        private sealed class Capture : IMNNAudioCaptureSource
        {
            public MNNPcmFormat CaptureFormat => new MNNPcmFormat(16000, 1);
            public int MaximumCaptureSamples => 512;
            public long DroppedCaptureBlocks => 0;
            public bool HasPendingCaptureFrames => false;
            public bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count)
            { info = default; count = 0; return false; }
        }

        [Test]
        public void WakeHandoffRejectsMissingFrames()
        {
            Assert.Throws<ArgumentNullException>(() => new MNNConversationWakeHandoff(null, 0, 0, false));
        }

        [TestCase(1)]
        [TestCase(511)]
        [TestCase(513)]
        [TestCase(16000 * 120 + 512)]
        public void WakeHandoffRejectsPartialOrOversizedFrames(int length)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MNNConversationWakeHandoff(new float[length], 16000 * 122, 0, false));
        }

        [TestCase(-512)]
        [TestCase(0)]
        [TestCase(511)]
        [TestCase(513)]
        public void WakeHandoffRejectsInconsistentCursor(long endSample)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MNNConversationWakeHandoff(new float[512], endSample, 0, false));
        }

        [Test]
        public void WakeHandoffRejectsNegativeCaptureRevision()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MNNConversationWakeHandoff(Array.Empty<float>(), 0, -1, true));
        }

        [TestCase(0, 0L, false)]
        [TestCase(0, 1024L, true)]
        [TestCase(512, 1536L, false)]
        [TestCase(16000 * 120, 16000 * 120L, false)]
        public void WakeHandoffPreservesWholeFramesAndStreamIdentity(int length, long endSample, bool lostPrefix)
        {
            var audio = new float[length];
            if (length > 0) audio[length - 1] = .25f;
            var handoff = new MNNConversationWakeHandoff(audio, endSample, 7, lostPrefix);
            Assert.AreSame(audio, handoff.Frames, "Ownership transfers without copying the command PCM again.");
            Assert.AreEqual(endSample, handoff.EndSample);
            Assert.AreEqual(7, handoff.CaptureRevision);
            Assert.AreEqual(lostPrefix, handoff.LostPrefix);
            if (length > 0) Assert.AreEqual(.25f, handoff.Frames[length - 1]);
        }

        [Test]
        public void RecognitionPreviewRejectsOversizedSnapshotWithoutReplacingAcceptedText()
        {
            var ports = new MNNConversationPorts(new Capture(), () => null);
            ports.SetRealtime(true);
            ports.PublishRecognitionPreview(ports.Revision, "accepted");
            ports.PublishRecognitionPreview(ports.Revision, new string('x', 65537));
            Assert.AreEqual("accepted", ports.TakeRecognitionPreview());
        }

        [Test]
        public void RecognitionPreviewAcceptsLimitAndAllowsExplicitClear()
        {
            var ports = new MNNConversationPorts(new Capture(), () => null);
            ports.SetRealtime(true);
            string text = new string('x', 65536);
            ports.PublishRecognitionPreview(ports.Revision, text);
            Assert.AreEqual(text, ports.TakeRecognitionPreview());
            ports.PublishRecognitionPreview(ports.Revision, "");
            Assert.AreEqual("", ports.TakeRecognitionPreview());
        }

        [Test]
        public void MissingRecognitionPreviewDoesNotErasePendingSnapshot()
        {
            var ports = new MNNConversationPorts(new Capture(), () => null);
            ports.SetRealtime(true);
            ports.PublishRecognitionPreview(ports.Revision, "accepted");
            ports.PublishRecognitionPreview(ports.Revision, null);
            Assert.AreEqual("accepted", ports.TakeRecognitionPreview());
        }
    }
}
