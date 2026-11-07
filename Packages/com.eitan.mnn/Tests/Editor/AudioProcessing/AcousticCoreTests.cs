using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class AcousticCoreTests
    {
        private static bool HasAllocationCounter()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[4096];
            long observed = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(probe);
            return observed >= probe.Length;
        }

        internal static void RequireAllocationCounter()
        {
            if (!HasAllocationCounter()) Assert.Ignore("Runtime does not expose a working allocation counter.");
        }

        [Test]
        public void ReferenceTimelineRejectsGapsAndOverwrittenSamples()
        {
            var timeline = new MNNAudioReferenceTimeline(256);
            var samples = new float[128];
            Array.Fill(samples, .25f);
            var output = new float[128];
            timeline.Write(100, samples, samples.Length);
            Assert.True(timeline.Read(100, output));
            CollectionAssert.AreEqual(samples, output);
            Assert.False(timeline.Read(99, output));
            Assert.That(output, Is.All.Zero);
            timeline.Write(500, samples, samples.Length);
            Assert.False(timeline.Read(100, output));
            timeline.Write(628, samples, samples.Length);
            timeline.Write(756, samples, samples.Length);
            Assert.False(timeline.Read(500, output));
            Assert.True(timeline.Read(628, output));
            timeline.Reset();
            Assert.False(timeline.Read(628, output));
            Assert.That(output, Is.All.Zero);
        }

        [Test]
        public void TimelineChecksRangesBeforeChangingHistory()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNAudioReferenceTimeline(3));
            var timeline = new MNNAudioReferenceTimeline();
            var samples = new[] { .25f, -.5f };
            var output = new float[2];
            timeline.Write(17, samples, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Write(100, samples, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Write(long.MaxValue, samples, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Write(-1, samples, 2));
            Assert.Throws<ArgumentNullException>(() => timeline.Read(17, null));
            Assert.True(timeline.Read(17, output));
            CollectionAssert.AreEqual(samples, output);
            Assert.AreEqual(19, timeline.EndSample);
            Assert.False(timeline.Read(long.MaxValue, output));
            Assert.That(output, Is.All.Zero);
        }

        [TestCase(float.NaN), TestCase(float.PositiveInfinity), TestCase(1.01f)]
        public void InvalidPcmCannotPoisonEchoState(float invalid)
        {
            var estimator = new MNNEchoDelayEstimator();
            var filter = new MNNAdaptiveEchoFilter();
            var mic = new float[128];
            var reference = new float[128];
            var residual = new float[128];
            mic[127] = invalid;
            Assert.Throws<ArgumentException>(() => estimator.Process(mic, reference));
            Assert.Throws<ArgumentException>(() => filter.Process(mic, reference, residual));
            Assert.Throws<ArgumentException>(() => filter.EstimateCoherence(mic, reference, 128));
            mic[127] = .5f;
            filter.Process(mic, reference, residual);
            CollectionAssert.AreEqual(mic, residual);
            Assert.AreEqual(1, filter.ResidualPowerRatio);
            Assert.False(estimator.IsLocked);
        }

        [Test]
        public void FilterRejectsInvalidShapeGainAndAliasesBeforeUpdating()
        {
            var filter = new MNNAdaptiveEchoFilter();
            var mic = new float[128];
            var reference = new float[128];
            var residual = new float[128];
            Assert.Throws<ArgumentException>(() => filter.Process(mic, new float[127], residual));
            Assert.Throws<ArgumentException>(() => filter.Process(mic, reference, mic));
            Assert.Throws<ArgumentException>(() => filter.Process(mic, reference, reference));
            Assert.Throws<ArgumentOutOfRangeException>(() => filter.Process(mic, reference, residual, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => filter.Process(mic, reference, residual, -.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => filter.Process(mic, reference, residual, 1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => filter.EstimateCoherence(mic, reference, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => filter.EstimateCoherence(mic, reference, MNNAdaptiveEchoFilter.TapCount + 1));
            mic[0] = .5f;
            filter.Process(mic, reference, residual);
            CollectionAssert.AreEqual(mic, residual);
        }

        [Test]
        public void AcousticProcessorPreservesRawAudioAndDrainsTheEnhancementTailOnce()
        {
            var delay = new float[384];
            int head = 0;
            Action<float[], int> enhance = (pcm, offset) =>
            {
                for (int i = offset; i < offset + 512; ++i)
                {
                    float input = pcm[i];
                    pcm[i] = delay[head];
                    delay[head] = input;
                    head = (head + 1) % delay.Length;
                }
            };
            var processor = new MNNAcousticInputProcessor(new MNNPcmFormat(16000, 1), 512,
                MNNRealtimeFrameVoiceActivity.Energy(.01f), enhance,
                () => { head = 0; Array.Clear(delay, 0, delay.Length); }, null);
            var pcmInput = new float[512];
            pcmInput[511] = .75f;
            var output = new List<float>();
            Action<MNNRealtimeVadUpdate> update = _ => { };
            Action<float[], int, MNNRealtimeVadUpdate> receive = (pcm, offset, vad) =>
            { for (int i = 0; i < 512; ++i) output.Add(pcm[offset + i]); };
            processor.Process(pcmInput, 0, 512, update, onFrame: receive);
            Assert.AreEqual(.75f, pcmInput[511]);
            Assert.True(processor.Complete(update, receive));
            Assert.AreEqual(.75f, output[511 + 384]);
            int count = output.Count;
            Assert.False(processor.Complete(update, receive));
            Assert.AreEqual(count, output.Count);
            processor.Reset();
            processor.Process(pcmInput, 0, 512, update);
            Assert.False(processor.IsFaulted);
        }

        [TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
        public void AcousticRoutesRecognitionAndVadIndependently(bool enhancedRecognition, bool enhancedVad)
        {
            var input = new float[512];
            Array.Fill(input, .25f);
            float raw = -1, enhanced = -1, recognition = -1;
            bool active = false;
            var processor = new MNNAcousticInputProcessor(new MNNPcmFormat(16000, 1), 512,
                MNNRealtimeFrameVoiceActivity.Energy(.01f), (pcm, offset) => Array.Clear(pcm, offset, 512),
                null, (pcm, offset, clean) => { raw = pcm[offset]; enhanced = clean[0]; },
                enhancedRecognition, enhancedVad);
            processor.Process(input, 0, input.Length, update => active = update.IsSpeechActive,
                onFrame: (pcm, offset, vad) => recognition = pcm[offset]);
            Assert.AreEqual(.25f, raw);
            Assert.Zero(enhanced);
            Assert.AreEqual(enhancedRecognition ? 0 : .25f, recognition);
            Assert.AreEqual(!enhancedVad, active);
            Assert.That(input, Is.All.EqualTo(.25f));
        }

        [Test]
        public void AcousticProcessorRequiresResetAfterAnEnhancerFaultAndRejectsNullVad()
        {
            Assert.Throws<ArgumentNullException>(() => new MNNAcousticInputProcessor(new MNNPcmFormat(16000, 1), 512, null, null, null, null));
            bool fail = true;
            var processor = new MNNAcousticInputProcessor(new MNNPcmFormat(16000, 1), 512,
                MNNRealtimeFrameVoiceActivity.Energy(.01f), (pcm, offset) =>
                { if (fail) pcm[offset + 511] = float.NaN; }, null, null);
            var input = new float[512];
            Action<MNNRealtimeVadUpdate> update = _ => { };
            Assert.Throws<InvalidOperationException>(() => processor.Process(input, 0, 512, update));
            Assert.True(processor.IsFaulted);
            fail = false;
            Assert.Throws<InvalidOperationException>(() => processor.Process(input, 0, 512, update));
            processor.Reset();
            processor.Process(input, 0, 512, update);
            Assert.False(processor.IsFaulted);
        }

        [Test]
        public void AcousticCompleteFaultCannotSilentlyLoseTheDelayedTail()
        {
            bool fail = false;
            var processor = new MNNAcousticInputProcessor(new MNNPcmFormat(16000, 1), 512,
                MNNRealtimeFrameVoiceActivity.Energy(.01f), (pcm, offset) =>
                { if (fail) throw new InvalidOperationException("tail failure"); }, null, null);
            Action<MNNRealtimeVadUpdate> update = _ => { };
            processor.Process(new float[512], 0, 512, update);
            fail = true;
            Assert.Throws<InvalidOperationException>(() => processor.Complete(update));
            Assert.True(processor.IsFaulted);
            Assert.Throws<InvalidOperationException>(() => processor.Complete(update));
            fail = false;
            processor.Reset();
            Assert.False(processor.IsFaulted);
        }

        [Test]
        public void InvalidDiscontinuityDoesNotResetTheExistingVoiceStream()
        {
            int resets = 0;
            var vad = new MNNRealtimeFrameVoiceActivity((samples, offset) => 1f, reset: () => ++resets);
            var processor = new MNNRealtimeVoiceInputProcessor(new MNNPcmFormat(16000, 1), vad, 512);
            var input = new float[512];
            Action<MNNRealtimeVadUpdate> update = _ => { };
            processor.Process(input, 0, 512, update);
            Assert.Throws<ArgumentOutOfRangeException>(() => processor.Process(input, 0, 513, update, discontinuity: true));
            Assert.Throws<ArgumentNullException>(() => processor.Process(input, 0, 512, null, discontinuity: true));
            input[511] = float.NaN;
            Assert.Throws<ArgumentException>(() => processor.Process(input, 0, 512, update, discontinuity: true));
            Assert.Zero(resets);
            input[511] = 0;
            long start = -1;
            processor.Process(input, 0, 512, result => start = result.StartSample);
            Assert.AreEqual(512, start);
            Assert.False(processor.IsFaulted);
        }

        [Test]
        public void DiscontinuityResetFailureRequiresASuccessfulResetBeforeReuse()
        {
            bool fail = true;
            var vad = new MNNRealtimeFrameVoiceActivity((samples, offset) => 1f, reset: () =>
            { if (fail) throw new InvalidOperationException("reset failure"); });
            var processor = new MNNRealtimeVoiceInputProcessor(new MNNPcmFormat(16000, 1), vad, 512);
            var input = new float[512];
            Action<MNNRealtimeVadUpdate> update = _ => { };
            Assert.Throws<InvalidOperationException>(() => processor.Process(input, 0, 512, update, discontinuity: true));
            Assert.True(processor.IsFaulted);
            fail = false;
            Assert.Throws<InvalidOperationException>(() => processor.Process(input, 0, 512, update));
            processor.Reset();
            processor.Process(input, 0, 512, update);
            Assert.False(processor.IsFaulted);
        }

        [Test]
        public void AcousticProcessorRejectsReentryAndRequiresResetAfterCallbackFailure()
        {
            MNNAcousticInputProcessor processor = null;
            bool reenter = true;
            Action<MNNRealtimeVadUpdate> update = _ => { };
            processor = new MNNAcousticInputProcessor(new MNNPcmFormat(16000, 1), 512,
                MNNRealtimeFrameVoiceActivity.Energy(.01f), null, null,
                (pcm, offset, enhanced) => { if (reenter) processor.Complete(update); });
            var input = new float[512];
            Assert.Throws<InvalidOperationException>(() => processor.Process(input, 0, 512, update));
            Assert.True(processor.IsFaulted);
            reenter = false;
            processor.Reset();
            processor.Process(input, 0, 512, update);
            Assert.False(processor.IsFaulted);
        }

        [Test]
        public void TimelineAndAcousticProcessingAllocateNothingAfterWarmup()
        {
            RequireAllocationCounter();
            var timeline = new MNNAudioReferenceTimeline();
            var samples = new float[512];
            var output = new float[512];
            var processor = new MNNAcousticInputProcessor(new MNNPcmFormat(16000, 1), 512,
                MNNRealtimeFrameVoiceActivity.Energy(.01f), (pcm, offset) => { }, null, null);
            Action<MNNRealtimeVadUpdate> update = _ => { };
            for (int i = 0; i < 16; ++i)
            {
                timeline.Write(i * 512, samples, 512);
                timeline.Read(i * 512, output);
                processor.Process(samples, 0, 512, update);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 16; i < 128; ++i)
            {
                timeline.Write(i * 512, samples, 512);
                timeline.Read(i * 512, output);
                processor.Process(samples, 0, 512, update);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Zero(allocated);
        }

        [Test]
        public void ManagedEchoHopBenchmarkReportsTheComposedPath()
        {
            const int warmup = 128, measured = 512, hop = 128, delaySamples = 512;
            bool hasAllocationCounter = HasAllocationCounter();
            var timeline = new MNNAudioReferenceTimeline();
            var delay = new MNNEchoDelayEstimator();
            var filter = new MNNAdaptiveEchoFilter();
            var random = new Random(367);
            var signal = new float[(warmup + measured) * hop];
            for (int i = 0; i < signal.Length; ++i) signal[i] = (float)(random.NextDouble() * .4 - .2);
            var reference = new float[hop];
            var microphone = new float[hop];
            var aligned = new float[hop];
            var residual = new float[hop];
            var elapsed = new double[measured];
            Action<int> process = block =>
            {
                int first = block * hop;
                Array.Copy(signal, first, reference, 0, hop);
                for (int i = 0; i < hop; ++i)
                    microphone[i] = first + i < delaySamples ? 0 : -.6f * signal[first + i - delaySamples];
                timeline.Write(first, reference, hop);
                delay.Process(microphone, reference);
                timeline.Read(first - delaySamples, aligned);
                float coherence = filter.EstimateCoherence(microphone, aligned,
                    MNNEchoDelayEstimator.AlignmentHeadroomSamples * 2);
                filter.Process(microphone, aligned, residual, coherence);
            };
            for (int block = 0; block < warmup; ++block) process(block);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int block = 0; block < measured; ++block)
            {
                long started = Stopwatch.GetTimestamp();
                process(warmup + block);
                elapsed[block] = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Array.Sort(elapsed);
            double total = 0;
            foreach (double milliseconds in elapsed) total += milliseconds;
            string allocation = hasAllocationCounter ? allocated.ToString() : "unavailable";
            TestContext.WriteLine($"MANAGED_ECHO_HOP samples={hop} audio_ms=8 warmup={warmup} measured={measured} mean_ms={total / measured:F3} p95_ms={elapsed[(int)(measured * .95)]:F3} p99_ms={elapsed[(int)(measured * .99)]:F3} allocation_bytes={allocation}");
            if (hasAllocationCounter) Assert.Zero(allocated);
            Assert.True(delay.IsLocked);
            Assert.That(delay.DelaySamples, Is.EqualTo(delaySamples).Within(16));
            Assert.Less(filter.ResidualPowerRatio, .001f);
            // Excludes neural inference, devices, resampling, scheduling and model memory.
            // Record this machine's result; do not assert a universal device timing promise.
        }
    }
}
