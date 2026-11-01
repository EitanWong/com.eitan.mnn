using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class RealtimeProtocolTests
    {
        private static readonly MNNPcmFormat Mono16Khz = new MNNPcmFormat(16000, 1);

        [Test]
        public void PlaybackStartsSilentAndUsesFixedCapacityQueue()
        {
            var output = new MNNRealtimePlayback(Mono16Khz, framesPerBlock: 160, blockCapacity: 2);
            var destination = new float[80];
            Array.Fill(destination, 1f);
            output.Render(destination, 0, destination.Length);
            Assert.That(destination, Is.All.EqualTo(0f));
            Assert.AreEqual(0, output.UnderrunFrames);

            var response = output.BeginResponse();
            var audio = new float[160];
            Array.Fill(audio, 0.25f);
            var chunk = new MNNSpeechChunkInfo(response, 0, 0, true);
            Assert.True(output.TryEnqueue(audio, 0, audio.Length, chunk));
            Assert.True(output.TryEnqueue(audio, 0, audio.Length, chunk));
            Assert.False(output.TryEnqueue(audio, 0, audio.Length, chunk));
            Assert.AreEqual(320, output.QueuedFrames);
            Assert.AreEqual(20.0, output.QueuedAudioMilliseconds, 0.001);
            Assert.AreEqual(160, audio.Length, "Enqueue copies into bounded storage without changing caller ownership.");

            output.Render(destination, 0, destination.Length);
            Assert.That(destination, Is.All.EqualTo(0.25f));
            Assert.AreEqual(240, output.QueuedFrames);
            output.Render(destination, 0, destination.Length);
            Assert.That(destination, Is.All.EqualTo(0.25f));
            Assert.AreEqual(160, output.QueuedFrames);
            Assert.True(output.TryReadReceipt(out var first));
            Assert.AreEqual(0, first.SourceStartFrame);
            Assert.AreEqual(0, first.Chunk.ChunkSequence);
            Assert.False(first.EndsChunk);
            Assert.True(output.TryReadReceipt(out var second));
            Assert.AreEqual(80, second.SourceStartFrame);
            Assert.True(second.EndsChunk);
            Assert.True(second.EndsSegment);
        }

        [Test]
        public void InterruptInvalidatesQueuedAndLateAudioWithoutWaitingForInference()
        {
            var output = new MNNRealtimePlayback(Mono16Khz, framesPerBlock: 320);
            var old = output.BeginResponse();
            var audio = new float[240];
            Array.Fill(audio, 0.5f);
            Assert.True(output.TryEnqueue(audio, 0, audio.Length, new MNNSpeechChunkInfo(old, 0, 0, true)));
            Assert.AreEqual(240, output.QueuedFrames);

            output.Interrupt();
            Assert.AreEqual(240, output.QueuedFrames, "Invalidation gates audio immediately; the device consumer reclaims slots.");
            var silence = new float[80];
            output.Render(silence, 0, silence.Length);
            Assert.That(silence, Is.All.EqualTo(0f));
            Assert.AreEqual(0, output.QueuedFrames);
            Assert.False(output.TryEnqueue(audio, 0, audio.Length, new MNNSpeechChunkInfo(old, 1, 0, true)));
            Assert.False(output.TryReadReceipt(out _));
            Assert.AreEqual(1, output.DiscardedChunks);

            var current = output.BeginResponse();
            Assert.AreNotEqual(old, current);
            Assert.True(output.TryEnqueue(audio, 0, audio.Length, new MNNSpeechChunkInfo(current, 2, 0, true)));
            output.Render(silence, 0, silence.Length);
            Assert.That(silence, Is.All.EqualTo(0.5f));
        }

        [Test]
        public void QueueWatermarkCountsFramesForInterleavedChannelsAndReclaimsStaleBlocks()
        {
            var stereo = new MNNRealtimePlayback(new MNNPcmFormat(48000, 2), framesPerBlock: 480, blockCapacity: 2);
            var response = stereo.BeginResponse();
            var block = new float[960];
            Assert.True(stereo.TryEnqueue(block, 0, block.Length,
                new MNNSpeechChunkInfo(response, 0, 0, true)));
            Assert.AreEqual(480, stereo.QueuedFrames);
            Assert.AreEqual(10.0, stereo.QueuedAudioMilliseconds, 0.001);

            stereo.Interrupt();
            var render = new float[192];
            stereo.Render(render, 0, render.Length);
            Assert.That(render, Is.All.EqualTo(0f));
            Assert.AreEqual(0, stereo.QueuedFrames);
            Assert.AreEqual(0.0, stereo.QueuedAudioMilliseconds, 0.001);
            Assert.AreEqual(1, stereo.DiscardedChunks);
        }

        [Test]
        public void RenderClearsOnInterruptionRacingWithProducer()
        {
            var output = new MNNRealtimePlayback(Mono16Khz, framesPerBlock: 960, blockCapacity: 16);
            var response = output.BeginResponse();
            var block = new float[160];
            Array.Fill(block, 0.75f);
            var producer = Task.Run(() =>
            {
                for (int i = 0; i < 30000; ++i)
                    output.TryEnqueue(block, 0, block.Length, new MNNSpeechChunkInfo(response, i, 0, true));
            });
            var render = new float[960];
            for (int i = 0; i < 10000; ++i)
            {
                output.Render(render, 0, render.Length);
                if (i == 10)
                    output.Interrupt();
            }
            Assert.True(producer.Wait(TimeSpan.FromSeconds(5)), "Output producer remained bounded while rendering.");
            Assert.False(output.Accepts(response));
            output.Render(render, 0, render.Length);
            Assert.That(render, Is.All.EqualTo(0f));
        }

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(8, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(8, true)]
        public void InterruptReclaimsOnlyUnrenderedFramesFromPartialChunk(int channels, bool restartBeforeRender)
        {
            var output = new MNNRealtimePlayback(new MNNPcmFormat(16000, channels),
                framesPerBlock: 241, blockCapacity: 3);
            var old = output.BeginResponse();
            var block = new float[241 * channels];
            Array.Fill(block, .25f);
            Assert.True(output.TryEnqueue(block, 0, block.Length, new MNNSpeechChunkInfo(old, 0, 0, false)));
            Assert.True(output.TryEnqueue(block, 0, 37 * channels, new MNNSpeechChunkInfo(old, 0, 1, true)));

            var first = new float[121 * channels];
            output.Render(first, 0, first.Length);
            Assert.That(first, Is.All.EqualTo(.25f));
            Assert.AreEqual(157, output.QueuedFrames);
            Assert.True(output.TryReadReceipt(out var receipt));
            Assert.AreEqual(80, receipt.FrameCount);
            Assert.False(receipt.EndsChunk);
            Assert.True(output.TryReadReceipt(out receipt));
            Assert.AreEqual(80, receipt.SourceStartFrame);
            Assert.AreEqual(41, receipt.FrameCount);
            Assert.False(receipt.EndsChunk);
            Assert.False(output.TryReadReceipt(out _));

            output.Interrupt();
            Assert.AreEqual(157, output.QueuedFrames, "Only the render consumer reclaims invalidated slots.");
            MNNResponseId next = default;
            if (restartBeforeRender)
            {
                next = output.BeginResponse();
                Array.Fill(block, .5f);
                Assert.True(output.TryEnqueue(block, 0, 67 * channels, new MNNSpeechChunkInfo(next, 1, 0, true)));
                Assert.AreEqual(224, output.QueuedFrames);
            }

            var rendered = new float[216 * channels];
            Array.Fill(rendered, -1f);
            output.Render(rendered, 3 * channels, 211 * channels);
            for (int frame = 0; frame < 216; ++frame)
            {
                float expected = frame < 3 || frame >= 214 ? -1f :
                    restartBeforeRender && frame < 70 ? .5f : 0f;
                for (int channel = 0; channel < channels; ++channel)
                    Assert.AreEqual(expected, rendered[frame * channels + channel]);
            }
            Assert.AreEqual(0, output.QueuedFrames, "Previously rendered frames must not be subtracted again.");
            Assert.AreEqual(0.0, output.QueuedAudioMilliseconds);
            Assert.False(output.HasPendingAudio);
            Assert.AreEqual(2, output.DiscardedChunks);
            Assert.False(output.IsFaulted);
            if (restartBeforeRender)
            {
                Assert.True(output.TryReadReceipt(out receipt));
                Assert.AreEqual(next, receipt.Chunk.Response);
                Assert.AreEqual(0, receipt.SourceStartFrame);
                Assert.AreEqual(121, receipt.OutputStartFrame);
                Assert.AreEqual(67, receipt.FrameCount);
                Assert.True(receipt.EndsSegment);
            }
            Assert.False(output.TryReadReceipt(out _), "Discarded audio must never publish playback receipts.");
        }

        [Test]
        public void CaptureOverflowMarksNextPublishedFrameAsDiscontinuous()
        {
            var capture = new MNNAudioCaptureBuffer(capacity: 1, maximumSamples: 8);
            var data = new float[4];
            var first = new MNNAudioFrameInfo(1, 1, 0, 0, 10, Mono16Khz);
            var overflow = new MNNAudioFrameInfo(1, 1, 1, 4, 20, Mono16Khz);
            var recovered = new MNNAudioFrameInfo(1, 1, 2, 8, 30, Mono16Khz);
            Assert.True(capture.TryWrite(data, 0, data.Length, first));
            Assert.False(capture.TryWrite(data, 0, data.Length, overflow));
            Assert.AreEqual(1, capture.DroppedBlocks);

            var read = new float[8];
            Assert.True(capture.TryRead(read, 0, out var received, out var count));
            Assert.False(received.Discontinuity);
            Assert.AreEqual(4, count);
            Assert.True(capture.TryWrite(data, 0, data.Length, recovered));
            Assert.True(capture.TryRead(read, 0, out received, out count));
            Assert.True(received.Discontinuity);
        }

        [Test]
        public void LedgerCommitsOnlyACompleteSegmentPastPresentationWatermark()
        {
            var output = new MNNRealtimePlayback(Mono16Khz, framesPerBlock: 100);
            var response = output.BeginResponse();
            var ledger = new MNNConversationLedger(2);
            Assert.True(ledger.TryRegister(response, 7, "你好。"));
            var audio = new float[100];
            var chunk = new MNNSpeechChunkInfo(response, 7, 0, true);
            Assert.True(output.TryEnqueue(audio, 0, audio.Length, chunk));
            var render = new float[80];
            output.Render(render, 0, render.Length);
            Assert.True(output.TryReadReceipt(out var receipt));
            ledger.RecordSubmission(receipt);
            Assert.False(receipt.EndsChunk);
            output.Render(render, 0, render.Length);
            Assert.True(output.TryReadReceipt(out receipt));
            ledger.RecordSubmission(receipt);

            ledger.AcknowledgePresentation(99, estimated: false);
            Assert.False(ledger.GetSegment(0).IsPresented);
            ledger.AcknowledgePresentation(100, estimated: true);
            var presented = ledger.GetSegment(0);
            Assert.True(presented.IsPresented);
            Assert.True(presented.IsEstimatedPresentation);
            ledger.Interrupt(response);
            Assert.True(ledger.GetSegment(0).IsInterrupted);
            Assert.Throws<InvalidOperationException>(() => ledger.RecordSubmission(receipt));
        }

        [Test]
        public void BundledBackendFailsClosedForBothFullDuplexAndInterruptibleRoutes()
        {
            foreach (var route in new[] { MNNSpeechRoute.OmniSpeech, MNNSpeechRoute.IndependentTts })
            foreach (var mode in new[] { MNNRealtimeMode.InterruptibleConversation, MNNRealtimeMode.NativeDuplex })
            {
                var report = MNNRealtimePreflight.CheckBundledBackend(mode, route);
                Assert.False(report.CanStart, route + " / " + mode);
                Assert.Throws<NotSupportedException>(() => report.RequireSupported());
            }
        }

        [Test]
        public void PreflightAllowsOnlyExplicitHeadphoneRouteForInterruptibleModeWithoutEchoControl()
        {
            const MNNRealtimeFeature interruptibleModel = MNNRealtimeFeature.SupportedAbi |
                MNNRealtimeFeature.InProcessMnn | MNNRealtimeFeature.MemoryAudioInput |
                MNNRealtimeFeature.BoundedCancellation | MNNRealtimeFeature.ContextRecovery |
                MNNRealtimeFeature.StreamingSpeech | MNNRealtimeFeature.SustainedRtfBelowOne;
            var model = new MNNRealtimeCapabilities(interruptibleModel, "test-model/v1", "deterministic-test",
                sustainedRtf: .82, sustainedRtfWindowAudioSeconds: 8);
            var headphones = new MNNRealtimeCapabilities(MNNRealtimeFeature.DuplexDevice |
                MNNRealtimeFeature.PresentationClock | MNNRealtimeFeature.HeadphoneRoute,
                "test-headphones/v1", "explicit-headphone-route");

            var interruptible = MNNRealtimePreflight.Check(MNNRealtimeMode.InterruptibleConversation,
                MNNSpeechRoute.OmniSpeech, model, headphones);
            Assert.True(interruptible.CanStart);
            Assert.AreEqual(MNNRealtimeMode.InterruptibleConversation, interruptible.Mode);
            Assert.AreEqual(MNNSpeechRoute.OmniSpeech, interruptible.SpeechRoute);
            Assert.AreEqual("test-model/v1", interruptible.ModelContract);
            Assert.AreEqual("deterministic-test", interruptible.ModelEvidence);
            Assert.AreEqual(.82, interruptible.ModelSustainedRtf.Value, 0.0001);
            Assert.AreEqual(8, interruptible.ModelSustainedRtfWindowAudioSeconds, 0.0001);
            Assert.AreEqual("test-headphones/v1", interruptible.DeviceContract);
            Assert.AreEqual("explicit-headphone-route", interruptible.DeviceEvidence);
            Assert.AreEqual(MNNRealtimeFeature.None, interruptible.MissingDevice);

            var nativeDuplex = MNNRealtimePreflight.Check(MNNRealtimeMode.NativeDuplex,
                MNNSpeechRoute.OmniSpeech, model, headphones);
            Assert.False(nativeDuplex.CanStart);
            Assert.True((nativeDuplex.MissingDevice & MNNRealtimeFeature.EchoControl) != 0);
            Assert.True((nativeDuplex.MissingModel & MNNRealtimeFeature.ContinuousUnderstanding) != 0);

            var unqualifiedSpeaker = new MNNRealtimeCapabilities(MNNRealtimeFeature.DuplexDevice |
                MNNRealtimeFeature.PresentationClock, "test-speaker/v1", "no-echo-path");
            var speakerResult = MNNRealtimePreflight.Check(MNNRealtimeMode.InterruptibleConversation,
                MNNSpeechRoute.OmniSpeech, model, unqualifiedSpeaker);
            Assert.True((speakerResult.MissingDevice & MNNRealtimeFeature.EchoControl) != 0);
        }

        [Test]
        public void IndependentTtsPreflightRetainsSynthesizerContractAndEvidence()
        {
            var model = new MNNRealtimeCapabilities(MNNRealtimeFeature.SupportedAbi |
                MNNRealtimeFeature.InProcessMnn | MNNRealtimeFeature.MemoryAudioInput |
                MNNRealtimeFeature.BoundedCancellation | MNNRealtimeFeature.ContextRecovery |
                MNNRealtimeFeature.StableSpokenText, "omni-text/v2", "model-export-sha256");
            var device = new MNNRealtimeCapabilities(MNNRealtimeFeature.DuplexDevice |
                MNNRealtimeFeature.PresentationClock | MNNRealtimeFeature.EchoControl,
                "device/v3", "device-test-manifest");
            var tts = new MNNRealtimeCapabilities(MNNRealtimeFeature.SupportedAbi |
                MNNRealtimeFeature.InProcessMnn | MNNRealtimeFeature.IndependentTts |
                MNNRealtimeFeature.StreamingSpeech | MNNRealtimeFeature.BoundedCancellation |
                MNNRealtimeFeature.SustainedRtfBelowOne,
                "streaming-tts/v4", "tts-rtf-manifest", sustainedRtf: .71,
                sustainedRtfWindowAudioSeconds: 12);
            var vad = new MNNRealtimeCapabilities(MNNRealtimeFeature.SupportedAbi |
                MNNRealtimeFeature.InProcessMnn | MNNRealtimeFeature.MemoryAudioInput |
                MNNRealtimeFeature.VoiceActivityDetection | MNNRealtimeFeature.BoundedScheduling,
                "vad/v1", "vad-scheduling-manifest");

            var report = MNNRealtimePreflight.Check(MNNRealtimeMode.InterruptibleConversation,
                MNNSpeechRoute.IndependentTts, model, device, tts, vad: vad);

            Assert.True(report.CanStart);
            Assert.AreEqual(MNNSpeechRoute.IndependentTts, report.SpeechRoute);
            Assert.AreEqual("omni-text/v2", report.ModelContract);
            Assert.AreEqual("model-export-sha256", report.ModelEvidence);
            Assert.AreEqual("device/v3", report.DeviceContract);
            Assert.AreEqual("device-test-manifest", report.DeviceEvidence);
            Assert.AreEqual("streaming-tts/v4", report.TtsContract);
            Assert.AreEqual("tts-rtf-manifest", report.TtsEvidence);
            Assert.AreEqual(.71, report.TtsSustainedRtf.Value, 0.0001);
            Assert.AreEqual(12, report.TtsSustainedRtfWindowAudioSeconds, 0.0001);
        }

        [Test]
        public void SpeechPreflightRequiresEvidenceForSustainedRtfStrictlyBelowOne()
        {
            var model = new MNNRealtimeCapabilities(MNNRealtimeFeature.SupportedAbi |
                MNNRealtimeFeature.InProcessMnn | MNNRealtimeFeature.MemoryAudioInput |
                MNNRealtimeFeature.BoundedCancellation | MNNRealtimeFeature.ContextRecovery |
                MNNRealtimeFeature.StreamingSpeech,
                "omni/v1", "streaming-speech-only", sustainedRtf: 1.0,
                sustainedRtfWindowAudioSeconds: 5);
            var device = new MNNRealtimeCapabilities(MNNRealtimeFeature.DuplexDevice |
                MNNRealtimeFeature.PresentationClock | MNNRealtimeFeature.EchoControl,
                "device/v1", "device-manifest");

            var report = MNNRealtimePreflight.Check(MNNRealtimeMode.InterruptibleConversation,
                MNNSpeechRoute.OmniSpeech, model, device);

            Assert.False(report.CanStart);
            Assert.AreEqual(1.0, report.ModelSustainedRtf.Value, 0.0001);
            Assert.AreEqual(MNNRealtimeFeature.SustainedRtfBelowOne,
                report.MissingModel & MNNRealtimeFeature.SustainedRtfBelowOne);
        }

        [TestCase(1.0, 5.0)]
        [TestCase(1.2, 5.0)]
        [TestCase(.8, .99)]
        public void CapabilityCannotClaimSustainedRealtimeWithoutStrictMeasuredMargin(double rtf, double windowSeconds)
        {
            Assert.Throws<ArgumentException>(() => new MNNRealtimeCapabilities(
                MNNRealtimeFeature.StreamingSpeech | MNNRealtimeFeature.SustainedRtfBelowOne,
                "speech/v1", "measured-rtf", sustainedRtf: rtf,
                sustainedRtfWindowAudioSeconds: windowSeconds));
        }

        [Test]
        public void InvalidAudioFormatsChunksAndCapacitiesAreRejectedBeforePublication()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNPcmFormat(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNPcmFormat(16000, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNRealtimePlayback(Mono16Khz, 16000, blockCapacity: 4096));
            var output = new MNNRealtimePlayback(new MNNPcmFormat(16000, 2), 80);
            var response = output.BeginResponse();
            Assert.Throws<ArgumentOutOfRangeException>(() => output.TryEnqueue(new float[3], 0, 3,
                new MNNSpeechChunkInfo(response, 0, 0, true)));
            Assert.Throws<ArgumentException>(() => output.TryEnqueue(new[] {float.NaN, 0f}, 0, 2,
                new MNNSpeechChunkInfo(response, 0, 0, true)));
        }
    }
}
