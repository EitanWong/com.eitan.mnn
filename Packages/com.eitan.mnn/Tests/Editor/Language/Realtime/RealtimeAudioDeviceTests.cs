using System;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class RealtimeAudioDeviceTests
    {
        private GameObject _gameObject;
        private MNNRealtimeAudioDevice _device;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("Realtime audio device test");
            _device = _gameObject.AddComponent<MNNRealtimeAudioDevice>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void UnityDspBudgetIsAvailableBeforeBindingOrStartingOutput()
        {
            AudioSettings.GetDSPBufferSize(out int frames, out int buffers);
            double expected = frames * (double)buffers * 1000 / AudioSettings.outputSampleRate;
            Assert.AreEqual(expected, _device.GetUnityDspBufferMilliseconds());
            Assert.False(_device.IsOutputRunning);
        }

        [Test]
        public void FreshUnityBindingCanReadStartupStateBeforeNegotiatingOutput()
        {
            var input = new GameObject("Fresh microphone endpoint");
            try
            {
                var microphone = input.AddComponent<MNNUnityMicrophoneCapture>();
                var binding = new MNNUnityAudioDeviceBinding(microphone, _device);
                binding.ValidateFresh();
                var state = binding.ReadState();
                Assert.True(state.Active);
                Assert.False(state.OutputRunning);
                Assert.AreEqual(_device.GetUnityDspBufferMilliseconds(), state.PresentationTailMilliseconds);
                Assert.AreEqual(0, _device.MaximumCaptureSamples, "Startup inspection must not bind or allocate capture queues.");
            }
            finally { UnityEngine.Object.DestroyImmediate(input); }
        }

        [Test]
        public void CaptureGateRejectsBeforePublishingAndReopeningMarksGap()
        {
            _device.Bind(new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 160),
                new MNNPcmFormat(16000, 1));
            _device.CaptureEnabled = false;
            Assert.False(_device.TrySubmitCapture(new float[160], 0, 160, 10));
            Assert.False(_device.HasPendingCaptureFrames);
            Assert.AreEqual(0, _device.DroppedCaptureBlocks, "Gating is not queue overflow.");
            _device.CaptureEnabled = true;
            Assert.True(_device.TrySubmitCapture(new float[160], 0, 160, 20));
            Assert.True(_device.TryReadCapture(new float[4096], 0, out var frame, out _));
            Assert.True(frame.Discontinuity);
        }

        [Test]
        public void CapturePortCopiesAudioAndMarksOverflowAsDiscontinuity()
        {
            _device.Bind(new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 480),
                new MNNPcmFormat(48000, 2), captureQueueBlocks: 1, maximumCaptureSamplesPerBlock: 8);
            var input = new[] { .1f, .2f, .3f, .4f };
            Assert.True(_device.TrySubmitCapture(input, 0, 4, 100));
            Assert.False(_device.TrySubmitCapture(input, 0, 4, 200));
            Assert.AreEqual(1, _device.DroppedCaptureBlocks);

            var destination = new float[8];
            Assert.True(_device.TryReadCapture(destination, 0, out var first, out int count));
            Assert.AreEqual(4, count);
            Assert.AreEqual(new MNNPcmFormat(48000, 2), first.Format);
            Assert.AreEqual(0, first.StartFrame);
            Assert.False(first.Discontinuity);
            Assert.That(destination[0], Is.EqualTo(.1f));

            Assert.True(_device.TrySubmitCapture(input, 0, 4, 300));
            Assert.True(_device.TryReadCapture(destination, 0, out var recovered, out count));
            Assert.AreEqual(4, recovered.StartFrame, "The source timeline includes both frames dropped during overflow.");
            Assert.True(recovered.Discontinuity);
        }

        [Test]
        public void InvalidCapturePublicationMarksTheNextAcceptedBlockDiscontinuous()
        {
            _device.Bind(new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 480),
                new MNNPcmFormat(16000, 1), captureQueueBlocks: 2, maximumCaptureSamplesPerBlock: 16);
            Assert.False(_device.TrySubmitCapture(new float[2], 0, 3, 100));
            var input = new[] { .5f, -.5f };
            Assert.True(_device.TrySubmitCapture(input, 0, input.Length, 200));
            Assert.True(_device.TryReadCapture(new float[4], 0, out var frame, out _));
            Assert.True(frame.Discontinuity);
        }

        [Test]
        public void OutputCallbackIsSilentUntilStartedAndPullsFromTheSharedPipeline()
        {
            var pipeline = new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 240);
            var response = pipeline.BeginResponse();
            Assert.True(pipeline.TryRegisterSegment(response, 1, "live"));
            var pcm = new[] { .25f, .5f, .75f, 1f };
            Assert.True(pipeline.TryEnqueue(pcm, 0, pcm.Length, new MNNSpeechChunkInfo(response, 1, 0, true)));
            _device.Bind(pipeline, new MNNPcmFormat(24000, 1));

            var callback = new float[4];
            Array.Fill(callback, 1f);
            _device.RenderOutputPcm(callback);
            Assert.That(callback, Is.All.EqualTo(0f));
            Assert.AreEqual(0, _device.RenderedOutputFrames);
            Assert.AreEqual(4, pipeline.QueuedFrames);

            _device.StartOutput();
            _device.RenderOutputPcm(callback);
            Assert.That(callback, Is.EqualTo(pcm));
            Assert.GreaterOrEqual(_device.RenderedOutputFrames, 4);
            Assert.AreEqual(0, pipeline.QueuedFrames);
            _device.StopOutput();
            Assert.False(_device.IsOutputRunning);
        }

        [Test]
        public void UnityDspCallbackExpandsMonoToStereoWithoutReadAheadOrExtraReceipts()
        {
            var source = _gameObject.GetComponent<AudioSource>();
            source.priority = 96;
            var pipeline = new MNNRealtimeOutputPipeline(new MNNPcmFormat(AudioSettings.outputSampleRate, 1), 4);
            _device.Bind(pipeline, new MNNPcmFormat(16000, 1));
            var response = pipeline.BeginResponse();
            pipeline.TryRegisterSegment(response, 1, "DSP frames");
            var pcm = new[] { .25f, -.5f, .75f, -.25f };
            pipeline.TryEnqueue(pcm, 0, 4, new MNNSpeechChunkInfo(response, 1, 0, true));
            _device.StartOutput();
            Assert.AreEqual(0, _device.RenderedOutputFrames, "Starting output must not pre-read the streaming clip.");
            Assert.AreEqual(4, pipeline.QueuedFrames);
            var stereo = new float[8];
            typeof(MNNRealtimeAudioDevice).GetMethod("OnAudioFilterRead",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(_device, new object[] { stereo, 2 });
            Assert.That(stereo, Is.EqualTo(new[] { .25f, .25f, -.5f, -.5f, .75f, .75f, -.25f, -.25f }));
            Assert.AreEqual(4, _device.RenderedOutputFrames, "Presentation advances in frames, independently of Unity's output channel count.");
            Assert.AreEqual(0, pipeline.QueuedFrames);
            Assert.IsNull(_device.Fault);
            _device.StopOutput();
            Assert.AreEqual(96, source.priority);
        }

        [Test]
        public void RebindingWhileRunningAndOversizedCaptureAreRejectedWithoutCorruptingActivePorts()
        {
            _device.Bind(new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 240),
                new MNNPcmFormat(16000, 1), maximumCaptureSamplesPerBlock: 4);
            _device.StartOutput();
            Assert.Throws<InvalidOperationException>(() => _device.Bind(
                new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 240), new MNNPcmFormat(16000, 1)));
            Assert.False(_device.TrySubmitCapture(new float[6], 0, 6, 10));
            Assert.True(_device.TrySubmitCapture(new float[4], 0, 4, 20));
            Assert.True(_device.TryReadCapture(new float[4], 0, out var frame, out _));
            Assert.True(frame.Discontinuity);
            _device.StopOutput();
        }

        [Test]
        public void FailedRebindLeavesThePreviousOutputPipelineUsable()
        {
            var original = new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 240);
            var response = original.BeginResponse();
            Assert.True(original.TryRegisterSegment(response, 3, "Keep current route."));
            var pcm = new[] { .2f, .4f, .6f, .8f };
            Assert.True(original.TryEnqueue(pcm, 0, pcm.Length, new MNNSpeechChunkInfo(response, 3, 0, true)));
            _device.Bind(original, new MNNPcmFormat(16000, 1));

            Assert.Throws<ArgumentOutOfRangeException>(() => _device.Bind(
                new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 240),
                new MNNPcmFormat(16000, 1), captureQueueBlocks: 0));
            _device.StartOutput();
            var callback = new float[4];
            _device.RenderOutputPcm(callback);
            Assert.That(callback, Is.EqualTo(pcm));
            _device.StopOutput();
        }
    }
}
