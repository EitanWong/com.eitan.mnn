using System;
using System.Diagnostics;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class EchoDelayEstimatorTests
    {
        [TestCase(0), TestCase(1280), TestCase(3568), TestCase(7840)]
        public void DelayLocksToBroadbandEchoAndResetDiscardsRouteHistory(int delaySamples)
        {
            var estimator = new MNNEchoDelayEstimator();
            var random = new System.Random(94);
            var reference = new float[48000];
            for (int i = 0; i < reference.Length; ++i) reference[i] = (float)(random.NextDouble() * .5 - .25);
            var micHop = new float[128]; var refHop = new float[128];
            for (int offset = 0; offset < reference.Length; offset += 128)
            {
                for (int i = 0; i < 128; ++i)
                {
                    refHop[i] = reference[offset + i];
                    micHop[i] = offset + i >= delaySamples ? reference[offset + i - delaySamples] * -.6f : 0;
                }
                estimator.Process(micHop, refHop);
            }
            Assert.True(estimator.IsLocked);
            Assert.That(estimator.DelaySamples, Is.EqualTo(delaySamples).Within(16));
            estimator.Reset();
            Assert.False(estimator.IsLocked);
        }

        [TestCase(0), TestCase(127), TestCase(1203), TestCase(3569), TestCase(7991)]
        public void DelayAcquiresAsSoonAsTheCompleteEchoWindowIsAvailable(int delaySamples)
        {
            var estimator = new MNNEchoDelayEstimator();
            var random = new System.Random(852);
            var reference = new float[32000];
            var mic = new float[128]; var render = new float[128];
            for (int i = 0; i < reference.Length; ++i) reference[i] = (float)(random.NextDouble() * .4 - .2);
            int lockedAt = 0;
            for (int block = 0; block < reference.Length / 128; ++block)
            {
                for (int i = 0; i < 128; ++i)
                {
                    int sample = block * 128 + i;
                    render[i] = reference[sample];
                    mic[i] = sample >= delaySamples ? -.6f * reference[sample - delaySamples] : 0;
                }
                estimator.Process(mic, render);
                if (!estimator.IsLocked) continue;
                lockedAt = (block + 1) * 8;
                break;
            }
            Assert.Greater(lockedAt, 0);
            Assert.That(estimator.DelaySamples, Is.EqualTo(delaySamples).Within(16));
            // 192 ms correlation window + four further 64 ms votes + one 64 ms
            // scheduling interval. Short routes must not wait for the full 500 ms search history.
            Assert.LessOrEqual(lockedAt, (int)Math.Ceiling(delaySamples / 16.0) + 512);
            TestContext.WriteLine($"DELAY_ACQUISITION path={delaySamples / 16.0:F3} ms lock={lockedAt} ms");
        }

        [Test]
        public void SilenceAndUnrelatedNearEndDoNotAcquireDelay()
        {
            var estimator = new MNNEchoDelayEstimator();
            var mic = new float[128]; var reference = new float[128];
            var random = new System.Random(18);
            for (int block = 0; block < 400; ++block)
            {
                for (int i = 0; i < 128; ++i)
                {
                    mic[i] = (float)(random.NextDouble() - .5);
                    reference[i] = block < 200 ? 0 : (float)(random.NextDouble() - .5);
                }
                estimator.Process(mic, reference);
                Assert.False(estimator.IsLocked);
            }
        }

        [TestCase(500), TestCase(-500)]
        public void DelayTracksSlowSampleClockDriftWithoutSpuriousJumps(int partsPerMillion)
        {
            var estimator = new MNNEchoDelayEstimator();
            var random = new System.Random(743);
            const int blocks = 2500, initialDelay = 1203;
            var history = new float[blocks * 128];
            for (int i = 0; i < history.Length; ++i) history[i] = (float)(random.NextDouble() * .2 - .1);
            var mic = new float[128]; var render = new float[128];
            int previousDelay = initialDelay;
            for (int block = 0; block < blocks; ++block)
            {
                for (int i = 0; i < 128; ++i)
                {
                    int sample = block * 128 + i;
                    double position = sample - initialDelay - sample * partsPerMillion / 1e6;
                    int first = (int)Math.Floor(position);
                    render[i] = history[sample];
                    mic[i] = first >= 0 ? .6f * (float)(history[first] * (1 - (position - first)) + history[first + 1] * (position - first)) : 0;
                }
                estimator.Process(mic, render);
                if (block < 150) continue;
                Assert.True(estimator.IsLocked, "Observable slow drift must not discard the route.");
                double expectedDelay = initialDelay + (block + 1) * 128 * partsPerMillion / 1e6;
                Assert.That(estimator.DelaySamples, Is.EqualTo(expectedDelay).Within(80));
                Assert.LessOrEqual(Math.Abs(estimator.DelaySamples - previousDelay), 80, "No jumps to unrelated correlation peaks.");
                previousDelay = estimator.DelaySamples;
            }
        }

        [TestCase(false), TestCase(true)]
        public void DelaySearchMeetsCpuAndAllocationBudgets(bool checkAllocations)
        {
            if (checkAllocations) AcousticCoreTests.RequireAllocationCounter();
            var estimator = new MNNEchoDelayEstimator();
            var random = new System.Random(684);
            var mic = new float[128]; var reference = new float[128];
            var elapsed = new double[256];
            for (int i = 0; i < 128; ++i) mic[i] = reference[i] = (float)(random.NextDouble() * .2 - .1);
            for (int block = 0; block < 128; ++block) estimator.Process(mic, reference);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int block = 0; block < elapsed.Length; ++block)
            {
                long start = Stopwatch.GetTimestamp();
                estimator.Process(mic, reference);
                elapsed[block] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Array.Sort(elapsed);
            double total = 0; foreach (double time in elapsed) total += time;
            TestContext.WriteLine($"DELAY_BENCH mean={total / elapsed.Length:F3} ms p95={elapsed[243]:F3} ms p99={elapsed[253]:F3} ms checked allocations={checkAllocations} bytes={allocated}");
            if (checkAllocations) Assert.Zero(allocated);
            Assert.Less(elapsed[253], 8, "A search iteration cannot routinely exhaust the AEC hop's entire CPU budget.");
        }

        [Test]
        public void AmbiguousPeriodicPlaybackDoesNotQualifyAnEchoRoute()
        {
            var estimator = new MNNEchoDelayEstimator();
            var mic = new float[128]; var render = new float[128];
            for (int block = 0; block < 400; ++block)
            {
                for (int i = 0; i < 128; ++i)
                {
                    double phase = (block * 128 + i) * 2 * Math.PI * 250 / 16000;
                    render[i] = .2f * (float)Math.Sin(phase);
                    mic[i] = .1f * (float)Math.Sin(phase - 2 * Math.PI * 250 * .075);
                }
                estimator.Process(mic, render);
                if (block >= 40) Assert.False(estimator.IsLocked, "Periodic aliases do not establish a unique bulk delay.");
            }
        }

        [Test]
        public void LockedDelaySurvivesSilenceAndBriefDoubleTalkButExpiresWithContradictingExcitation()
        {
            var estimator = new MNNEchoDelayEstimator();
            var mic = new float[128]; var reference = new float[128];
            var random = new System.Random(5);
            for (int block = 0; block < 200; ++block)
            {
                for (int i = 0; i < 128; ++i) mic[i] = reference[i] = (float)(random.NextDouble() - .5);
                estimator.Process(mic, reference);
            }
            Assert.True(estimator.IsLocked);
            for (int block = 0; block < 200; ++block)
            {
                for (int i = 0; i < 128; ++i)
                {
                    reference[i] = (float)(random.NextDouble() - .5);
                    mic[i] = (float)(random.NextDouble() - .5);
                }
                estimator.Process(mic, reference);
            }
            Assert.True(estimator.IsLocked);
            Array.Clear(mic, 0, 128); Array.Clear(reference, 0, 128);
            for (int block = 0; block < 700; ++block) estimator.Process(mic, reference);
            Assert.True(estimator.IsLocked, "A quiet LLM/TTS wait must not discard the measured route before the next reply.");
            for (int block = 0; block < 700; ++block)
            {
                for (int i = 0; i < 128; ++i)
                {
                    reference[i] = (float)(random.NextDouble() - .5);
                    mic[i] = (float)(random.NextDouble() - .5);
                }
                estimator.Process(mic, reference);
            }
            Assert.False(estimator.IsLocked);
        }

    }
}
