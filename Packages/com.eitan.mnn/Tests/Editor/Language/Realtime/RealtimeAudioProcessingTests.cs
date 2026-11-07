using System;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class RealtimeAudioProcessingTests
    {
        [Test]
        public void FrameAccumulatorPreservesSamplesAcrossIrregularChunks()
        {
            var accumulator = new MNNPcmFrameAccumulator(512);
            var input = new float[4099];
            for (int i = 0; i < input.Length; ++i) input[i] = i / 8192f;
            var output = new float[512 * 4];
            var frames = new System.Collections.Generic.List<float>();
            int offset = 0;
            int[] chunks = { 1, 17, 510, 63, 1024, 7, 1024, 1024, 429 };
            foreach (int chunk in chunks)
            {
                int count = Math.Min(chunk, input.Length - offset);
                int consumed = 0;
                while (consumed < count)
                {
                    accumulator.Append(input, offset + consumed, count - consumed, output, 0, 4,
                        out int stepConsumed, out int produced);
                    consumed += stepConsumed;
                    for (int i = 0; i < produced * accumulator.FrameSamples; ++i) frames.Add(output[i]);
                }
                offset += count;
            }
            Assert.AreEqual(input.Length, offset);
            Assert.AreEqual(input.Length % 512, accumulator.PendingSamples);
            int fullSamples = input.Length / 512 * 512;
            CollectionAssert.AreEqual(input.Take(fullSamples), frames);

            var final = new float[512];
            Array.Fill(final, -1f);
            Assert.True(accumulator.TryComplete(final, 0, out int validSamples));
            Assert.AreEqual(input.Length % 512, validSamples);
            CollectionAssert.AreEqual(input.Skip(fullSamples), final.Take(validSamples));
            Assert.That(final.Skip(validSamples), Is.All.EqualTo(0f));
            Assert.False(accumulator.TryComplete(final, 0, out _));
            Assert.Throws<InvalidOperationException>(() => accumulator.Append(input, 0, 1, output, 0, 4, out _, out _));
        }

        [Test]
        public void FrameAccumulatorHonorsBackpressureAndCanResetAfterEmptyOrPartialCompletion()
        {
            var accumulator = new MNNPcmFrameAccumulator(4);
            var input = new[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f };
            var output = new float[8];
            accumulator.Append(input, 0, input.Length, output, 0, 0, out int consumed, out int produced);
            Assert.AreEqual(0, consumed);
            Assert.AreEqual(0, produced);
            accumulator.Append(input, 0, input.Length, output, 0, 1, out consumed, out produced);
            Assert.AreEqual(4, consumed);
            Assert.AreEqual(1, produced);
            CollectionAssert.AreEqual(new[] { 0f, 1f, 2f, 3f }, output.Take(4));
            accumulator.Append(input, consumed, input.Length - consumed, output, 0, 1, out int secondConsumed, out produced);
            Assert.AreEqual(4, secondConsumed);
            Assert.AreEqual(1, produced);
            CollectionAssert.AreEqual(new[] { 4f, 5f, 6f, 7f }, output.Take(4));
            Assert.AreEqual(0, accumulator.PendingSamples);
            accumulator.Append(input, consumed + secondConsumed, input.Length - consumed - secondConsumed,
                output, 0, 1, out int tailConsumed, out produced);
            Assert.AreEqual(2, tailConsumed);
            Assert.AreEqual(0, produced);
            Assert.AreEqual(2, accumulator.PendingSamples);
            Assert.True(accumulator.TryComplete(output, 0, out int valid));
            Assert.AreEqual(2, valid);
            Assert.AreEqual(8f, output[0]);
            Assert.AreEqual(9f, output[1]);
            Assert.AreEqual(0f, output[2]);

            accumulator.Reset();
            Assert.AreEqual(0, accumulator.PendingSamples);
            Assert.False(accumulator.TryComplete(output, 0, out valid));
            Assert.AreEqual(0, valid);
            accumulator.Reset();
            Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.Append(input, 0, 1, output, 0, -1, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.Append(input, input.Length, 1, output, 0, 1, out _, out _));
            Assert.Throws<ArgumentException>(() => accumulator.Append(input, 0, 4, input, 2, 1, out _, out _));
        }

        [Test]
        public void ResamplerRejectsAliasedRangesThatCouldOverwriteUnreadInput()
        {
            var resampler = new MNNStreamingResampler(new MNNPcmFormat(16000, 1), new MNNPcmFormat(16000, 1));
            var shared = new float[64];
            Assert.Throws<ArgumentException>(() => resampler.Process(shared, 0, 32, shared, 16, 32, out _, out _));
        }

        [TestCase(16000, 16000, 1)]
        [TestCase(48000, 16000, 1)]
        [TestCase(16000, 48000, 1)]
        [TestCase(44100, 16000, 2)]
        [TestCase(96000, 16000, 1)]
        [TestCase(192000, 8000, 8)]
        [TestCase(8000, 192000, 1)]
        public void StreamingResamplerIsIndependentOfInputAndOutputChunking(int inputRate, int outputRate, int channels)
        {
            const int frames = 4097;
            var source = new float[frames * channels];
            for (int frame = 0; frame < frames; ++frame)
            for (int channel = 0; channel < channels; ++channel)
                source[frame * channels + channel] = (float)(0.4 * Math.Sin(frame * 0.071) * (channel + 1) / channels);

            var whole = RunResampler(source, inputRate, outputRate, channels, frames, frames + 64, frames + 128);
            var chunked = RunResampler(source, inputRate, outputRate, channels, frames, 37, 53);
            CollectionAssert.AreEqual(whole, chunked);
            int expectedFrames = (int)Math.Floor((frames - 1) * (double)outputRate / inputRate) + 1;
            Assert.AreEqual(expectedFrames * channels, whole.Length);
            if (inputRate == outputRate)
                CollectionAssert.AreEqual(source, whole, "Equal-rate conversion must be an exact, zero-delay copy.");
        }

        // Exercise the public streaming path: out-of-band microphone energy must not fold into ASR features.
        [TestCase(32000)]
        [TestCase(44100)]
        [TestCase(48000)]
        [TestCase(96000)]
        [TestCase(192000)]
        public void DownsamplingPreservesSpeechBandAndRejectsAliasing(int inputRate)
        {
            Assert.That(ResampledToneGain(inputRate, 1000), Is.EqualTo(1).Within(.01), "1 kHz speech band");
            Assert.That(ResampledToneGain(inputRate, 6000), Is.EqualTo(1).Within(.01), "6 kHz consonant band");
            Assert.That(ResampledToneGain(inputRate, 9000), Is.LessThan(.001), "9 kHz must be rejected by at least 60 dB");
            Assert.That(ResampledToneGain(inputRate, 10000), Is.LessThan(.001), "10 kHz aliases to 6 kHz without filtering");
        }

        private static double ResampledToneGain(int inputRate, int frequency)
        {
            var input = new float[inputRate / 4];
            for (int i = 0; i < input.Length; ++i)
                input[i] = (float)(.5 * Math.Sin(2 * Math.PI * frequency * i / inputRate));
            var output = RunResampler(input, inputRate, 16000, 1, input.Length, 137, 71);
            // Exclude zero-padded boundaries, measuring total energy (including fractional-phase sidebands).
            double energy = 0;
            const int boundary = 256;
            for (int i = boundary; i < output.Length - boundary; ++i) energy += output[i] * output[i];
            double gain = Math.Sqrt(energy / (output.Length - 2 * boundary) * 2) / .5;
            TestContext.WriteLine($"{inputRate} -> 16000 Hz, {frequency} Hz tone gain: {20 * Math.Log10(gain):F3} dB");
            return gain;
        }

        [Test]
        public void CompleteCanDrainTailAcrossCallsAndEmptyStreamsStayEmpty()
        {
            var format = new MNNPcmFormat(16000, 1);
            var resampler = new MNNStreamingResampler(format, new MNNPcmFormat(48000, 1));
            var output = new float[16];
            resampler.Complete(output, 0, output.Length, out int first);
            Assert.AreEqual(0, first);
            Assert.True(resampler.IsCompleted);
            resampler.Complete(output, 0, output.Length, out int second);
            Assert.AreEqual(0, second);

            resampler.Reset();
            var input = new[] { 0.5f, -0.5f, 0.25f };
            resampler.Process(input, 0, input.Length, output, 0, output.Length,
                out int consumed, out int produced);
            Assert.AreEqual(input.Length, consumed);
            Assert.AreEqual(0, produced);
            int drained = 0;
            resampler.Complete(output, 0, 1, out int count);
            drained += count;
            while (count != 0)
            {
                resampler.Complete(output, 0, 1, out count);
                drained += count;
            }
            Assert.AreEqual(7, drained);
            Assert.Throws<InvalidOperationException>(() => resampler.Process(input, 0, 1, output, 0, 1, out _, out _));
        }

        [Test]
        public void ZeroCapacityDoesNotConsumeAndOutputBackpressurePreservesInputOwnership()
        {
            var resampler = new MNNStreamingResampler(new MNNPcmFormat(48000, 1), new MNNPcmFormat(16000, 1));
            var input = new float[512];
            Array.Fill(input, 0.125f);
            var output = new float[1];
            resampler.Process(input, 0, input.Length, output, 0, 0, out int consumed, out int produced);
            Assert.AreEqual(0, consumed);
            Assert.AreEqual(0, produced);

            resampler.Process(input, 0, input.Length, output, 0, 1, out consumed, out produced);
            Assert.AreEqual(1, produced);
            Assert.Less(consumed, input.Length);
            int nextConsumed = 0;
            while (nextConsumed < input.Length - consumed)
            {
                resampler.Process(input, consumed + nextConsumed, input.Length - consumed - nextConsumed,
                    output, 0, 1, out int stepConsumed, out _);
                Assert.Greater(stepConsumed, 0);
                nextConsumed += stepConsumed;
            }
            Assert.AreEqual(input.Length, consumed + nextConsumed);
        }

        [Test]
        public void ResetReplaysIdenticalStreamAndInvalidSamplesAreRejected()
        {
            var resampler = new MNNStreamingResampler(new MNNPcmFormat(44100, 1), new MNNPcmFormat(16000, 1));
            var input = new float[2048];
            for (int i = 0; i < input.Length; ++i) input[i] = (float)Math.Sin(i * .1) * .5f;
            var first = Run(resampler, input, input.Length, 2048);
            resampler.Reset();
            var second = Run(resampler, input, input.Length, 2048);
            CollectionAssert.AreEqual(first, second);
            resampler.Reset();
            Assert.Throws<ArgumentException>(() => resampler.Process(new[] {float.NaN}, 0, 1,
                new float[1], 0, 1, out _, out _));
        }

        [Test]
        public void BargeInRequiresPostAecNearEndAndUsesCandidateReleaseAndExplicitInterrupt()
        {
            var policy = new MNNBargeInPolicy(confirmationMilliseconds: 96, releaseMilliseconds: 64,
                minimumConfidence: .65f);
            var activeVad = new MNNRealtimeVadUpdate(0, .9f, true, true, false);
            Assert.AreEqual(MNNBargeInAction.None, policy.Process(activeVad, false, 1f, true, 32),
                "VAD alone cannot identify near-end speech during assistant playback.");
            Assert.AreEqual(MNNBargeInAction.None, policy.Process(activeVad, true, .5f, true, 32));
            Assert.AreEqual(MNNBargeInAction.Candidate, policy.Process(activeVad, true, .9f, true, 32));
            Assert.AreEqual(MNNBargeInAction.None, policy.Process(activeVad, true, .9f, true, 32));
            Assert.AreEqual(MNNBargeInAction.Confirmed, policy.Process(activeVad, true, .9f, true, 32));
            Assert.False(policy.HasCandidate);

            Assert.AreEqual(MNNBargeInAction.Candidate, policy.Process(activeVad, true, .9f, true, 32));
            Assert.AreEqual(MNNBargeInAction.None, policy.Process(activeVad, false, 0f, true, 32));
            Assert.AreEqual(MNNBargeInAction.Cancelled, policy.Process(activeVad, false, 0f, true, 32));
            Assert.AreEqual(MNNBargeInAction.Confirmed,
                policy.Process(activeVad, false, 0f, true, 1, explicitInterrupt: true));
            Assert.AreEqual(MNNBargeInAction.None, policy.Process(activeVad, true, 1f, false, 128));
        }

        [Test]
        public void BargeInRejectsInvalidFrameAndConfidence()
        {
            var policy = new MNNBargeInPolicy();
            var vad = new MNNRealtimeVadUpdate(0, 0, false, false, false);
            Assert.Throws<ArgumentOutOfRangeException>(() => policy.Process(vad, false, 0, false, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => policy.Process(vad, false, float.NaN, false, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNBargeInPolicy(minimumConfidence: 1.1f));
        }

        [Test]
        public void ModelInputBufferCopiesFramesPreservesVADAndMarksOverflowGapAcrossWraparound()
        {
            using var queue = new MNNRealtimeModelInputBuffer(capacity: 2);
            var first = new float[MNNRealtimeModelInputBuffer.FrameSamples];
            var second = new float[MNNRealtimeModelInputBuffer.FrameSamples];
            var third = new float[MNNRealtimeModelInputBuffer.FrameSamples];
            Array.Fill(first, .1f);
            Array.Fill(second, .2f);
            Array.Fill(third, .3f);
            Assert.True(queue.TryWrite(first, 0, new MNNRealtimeVadUpdate(0, .8f, true, true, false)));
            Assert.True(queue.TryWrite(second, 0, new MNNRealtimeVadUpdate(512, .7f, true, false, false)));
            Assert.False(queue.TryWrite(third, 0, new MNNRealtimeVadUpdate(1024, .6f, true, false, false)));
            Assert.AreEqual(1, queue.DroppedFrames);

            Array.Clear(first, 0, first.Length);
            var output = new float[MNNRealtimeModelInputBuffer.FrameSamples + 4];
            Assert.True(queue.TryRead(output, 2, out var metadata));
            Assert.AreEqual(0, metadata.StartSample);
            Assert.AreEqual(0, metadata.Sequence);
            Assert.AreEqual(.8f, metadata.SpeechProbability);
            Assert.True(metadata.SpeechStarted);
            Assert.False(metadata.Discontinuity);
            Assert.AreEqual(.1f, output[2]);

            Assert.True(queue.TryWrite(third, 0, new MNNRealtimeVadUpdate(1536, .1f, false, false, true)));
            Assert.True(queue.TryRead(output, 0, out metadata));
            Assert.AreEqual(1, metadata.Sequence);
            Assert.False(metadata.Discontinuity);
            Assert.True(queue.TryRead(output, 0, out metadata));
            Assert.AreEqual(2, metadata.Sequence);
            Assert.True(metadata.Discontinuity);
            Assert.True(metadata.SpeechEnded);
            Assert.AreEqual(.3f, output[0]);
            Assert.False(queue.HasPendingFrames);
        }

        [Test]
        public void ModelInputBufferRejectsMalformedFramesAndLeavesQueuedFrameOnShortReadBuffer()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNRealtimeModelInputBuffer(
                MNNRealtimeModelInputBuffer.MaximumCapacity + 1));
            using var queue = new MNNRealtimeModelInputBuffer(capacity: 1);
            var frame = new float[MNNRealtimeModelInputBuffer.FrameSamples];
            var vad = new MNNRealtimeVadUpdate(0, .2f, false, false, false);
            Assert.Throws<ArgumentOutOfRangeException>(() => queue.TryWrite(frame, 1, vad));
            frame[10] = float.NaN;
            Assert.Throws<ArgumentException>(() => queue.TryWrite(frame, 0, vad));
            frame[10] = 0;
            Assert.Throws<ArgumentOutOfRangeException>(() => queue.TryWrite(frame, 0,
                new MNNRealtimeVadUpdate(1, .2f, false, false, false)));
            Assert.True(queue.TryWrite(frame, 0, vad));
            Assert.Throws<ArgumentOutOfRangeException>(() => queue.TryRead(new float[511], 0, out _));
            Assert.True(queue.TryRead(new float[512], 0, out var info));
            Assert.AreEqual(0, info.Sequence);
        }

        [Test]
        public void ModelInputBufferWaitCoalescesNotificationsAndWakesConsumerWithoutPolling()
        {
            using var queue = new MNNRealtimeModelInputBuffer(capacity: 2);
            var frame = new float[MNNRealtimeModelInputBuffer.FrameSamples];
            var vad = new MNNRealtimeVadUpdate(0, .5f, true, true, false);
            Assert.False(queue.WaitForData(TimeSpan.Zero));

            var waitingConsumer = System.Threading.Tasks.Task.Run(() => queue.WaitForData(TimeSpan.FromSeconds(2)));
            Assert.True(queue.TryWrite(frame, 0, vad));
            Assert.True(waitingConsumer.GetAwaiter().GetResult());
            Assert.True(queue.TryWrite(frame, 0, new MNNRealtimeVadUpdate(512, .5f, true, false, false)));
            Assert.True(queue.WaitForData(TimeSpan.Zero), "A coalesced signal still represents queued data.");

            Assert.True(queue.TryRead(frame, 0, out _));
            Assert.True(queue.TryRead(frame, 0, out _));
            Assert.False(queue.HasPendingFrames);
            Assert.False(queue.WaitForData(TimeSpan.Zero), "A stale auto-reset signal must not report data after draining.");

            queue.WakeWaiter();
            Assert.False(queue.WaitForData(TimeSpan.Zero), "A control wake without a frame is not model input.");
            queue.Dispose();
            queue.Dispose();
            Assert.Throws<ObjectDisposedException>(() => queue.WaitForData(TimeSpan.Zero));
            Assert.Throws<ObjectDisposedException>(() => queue.WakeWaiter());
            Assert.Throws<ObjectDisposedException>(() => queue.TryRead(frame, 0, out _));
        }

        private static float[] RunResampler(float[] input, int inputRate, int outputRate, int channels,
            int frames, int inputBlockFrames, int outputBlockFrames)
        {
            var resampler = new MNNStreamingResampler(new MNNPcmFormat(inputRate, channels),
                new MNNPcmFormat(outputRate, channels));
            var result = new System.Collections.Generic.List<float>();
            var output = new float[outputBlockFrames * channels];
            int inputPosition = 0;
            while (inputPosition < frames)
            {
                int requested = Math.Min(inputBlockFrames, frames - inputPosition);
                int consumed = 0;
                while (consumed < requested)
                {
                    resampler.Process(input, (inputPosition + consumed) * channels, requested - consumed,
                        output, 0, outputBlockFrames, out int stepConsumed, out int stepProduced);
                    consumed += stepConsumed;
                    for (int i = 0; i < stepProduced * channels; ++i) result.Add(output[i]);
                }
                inputPosition += requested;
            }
            int tail;
            do
            {
                resampler.Complete(output, 0, outputBlockFrames, out tail);
                for (int i = 0; i < tail * channels; ++i) result.Add(output[i]);
            } while (tail == outputBlockFrames);
            return result.ToArray();
        }

        private static float[] Run(MNNStreamingResampler resampler, float[] input, int frames, int outputCapacity)
        {
            var result = new System.Collections.Generic.List<float>();
            var output = new float[outputCapacity];
            int consumed = 0;
            while (consumed < frames)
            {
                resampler.Process(input, consumed, frames - consumed, output, 0, output.Length,
                    out int stepConsumed, out int stepProduced);
                consumed += stepConsumed;
                for (int i = 0; i < stepProduced; ++i) result.Add(output[i]);
            }
            int tail;
            do
            {
                resampler.Complete(output, 0, output.Length, out tail);
                for (int i = 0; i < tail; ++i) result.Add(output[i]);
            } while (tail == output.Length);
            return result.ToArray();
        }
    }
}
