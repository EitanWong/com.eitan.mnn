using System;
using System.Collections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class ConversationSharedStateTests
    {
        [Test]
        public void MailboxCoalescesOnlyAdjacentResponseSnapshotsAndRejectsStaleEpochs()
        {
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 512);
            var mailbox = new MNNConversationMailbox(output.Accepts, transcriptCapacity: 3);
            var response = output.BeginResponse();
            Assert.True(mailbox.TryPublishUserText("你好🎙"));
            Assert.True(mailbox.TryPublishAssistantSnapshot(response, new StringBuilder("first")));
            Assert.True(mailbox.TryPublishAssistantText(response, "latest🎙"));
            Assert.True(mailbox.TryPublishUserText("second"));
            Assert.False(mailbox.TryPublishUserText("full"));
            Assert.True(mailbox.TryReadTranscript(out var first)); Assert.AreEqual("你好🎙", first.Text);
            Assert.True(mailbox.TryReadTranscript(out var second)); Assert.AreEqual("latest🎙", second.Text);
            Assert.True(mailbox.TryReadTranscript(out var third)); Assert.AreEqual("second", third.Text);
            Assert.True(mailbox.TryPublishAssistantText(response, "old"));
            output.Interrupt();
            Assert.False(mailbox.TryReadTranscript(out _));
            Assert.False(mailbox.TryPublishAssistantText(response, "late"));
        }

        [Test]
        public void MailboxBoundsUnicodePreviewStageAndClear()
        {
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 512);
            var mailbox = new MNNConversationMailbox(output.Accepts, stageCapacity: 1);
            var response = output.BeginResponse();
            string unicode = new string('中', 65534) + "🎙";
            Assert.True(mailbox.TryPublishAssistantSnapshot(response, new StringBuilder(unicode)));
            Assert.True(mailbox.TryReadTranscript(out var text)); Assert.AreEqual(unicode, text.Text);
            mailbox.PublishRecognitionPreview(7, "valid");
            mailbox.PublishRecognitionPreview(7, unicode + "x");
            Assert.True(mailbox.TryReadRecognitionPreview(7, true, out var preview)); Assert.AreEqual("valid", preview);
            mailbox.PublishRecognitionPreview(7, "stale");
            Assert.True(mailbox.TryReadRecognitionPreview(8, true, out preview)); Assert.AreEqual("", preview);
            Assert.True(mailbox.TryPublishStage(response, MNNConversationStage.Speaking));
            Assert.False(mailbox.TryPublishStage(response, MNNConversationStage.Generating));
            mailbox.PublishInputWarning("warning");
            mailbox.Clear();
            Assert.False(mailbox.TryReadStage(out _));
            Assert.IsNull(mailbox.TakeInputWarning());
            Assert.False(mailbox.TryReadRecognitionPreview(7, true, out _));
        }

        [UnityTest]
        public IEnumerator MailboxBackpressureWaitIsCancelledWithoutPublishing()
        {
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 512);
            var mailbox = new MNNConversationMailbox(output.Accepts, transcriptCapacity: 1);
            var response = output.BeginResponse();
            mailbox.TryPublishUserText("occupied");
            using (var cancel = new CancellationTokenSource())
            {
                Task<bool> pending = mailbox.PublishAssistantSnapshotAsync(response, new StringBuilder("reply"), cancel.Token);
                Assert.False(pending.IsCompleted);
                cancel.Cancel();
                while (!pending.IsCompleted) yield return null;
                Assert.True(pending.IsCanceled);
                Assert.True(mailbox.TryReadTranscript(out var text)); Assert.AreEqual("occupied", text.Text);
                Assert.False(mailbox.TryReadTranscript(out _));
            }
        }

        [Test]
        public void InputRecordsInBlocksAndRejectsMissingAudioAndOverLimit()
        {
            var input = new MNNConversationInputState();
            input.BeginRecording();
            var frame = new float[512]; frame[10] = .3f;
            for (int i = 0; i < 10; ++i) Assert.True(input.Consume(frame));
            Assert.AreEqual(16000, input.RecordingAllocatedSamples);
            var audio = input.FinishRecording(false);
            Assert.AreEqual(5120, audio.Length); Assert.AreEqual(.3f, audio[10]);
            input.BeginRecording(); input.Consume(frame); input.Consume(frame, discontinuity: true);
            Assert.Throws<InvalidOperationException>(() => input.FinishRecording(false));
            input.BeginRecording();
            input.Consume(new float[MNNConversationInputState.MaximumSamples]);
            input.Consume(frame);
            Assert.AreEqual(MNNConversationInputState.MaximumSamples, input.RecordingAllocatedSamples);
            Assert.Throws<InvalidOperationException>(() => input.FinishRecording(false));
        }

        [Test]
        public void InputModeRevisionRejectsOldFramesAndCancelledRecordingsReleaseStorage()
        {
            var input = new MNNConversationInputState();
            input.SetRealtime(true);
            int revision = input.Revision;
            Assert.True(input.TryAcceptRealtime(revision, () => { }));
            input.BeginRecording();
            Assert.False(input.TryAcceptRealtime(revision, () => Assert.Fail("Old realtime input must not accept.")));
            input.Consume(new float[512], enqueuedMonotonicTicks: 1);
            Assert.AreEqual(0, input.Duration);
            input.Consume(new float[512]);
            Assert.IsNull(input.FinishRecording(true));
            Assert.False(input.Recording);
            Assert.AreEqual(0, input.RecordingAllocatedSamples);
        }
    }
}
