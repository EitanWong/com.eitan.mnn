using System;
using System.Diagnostics;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class MNNAdaptiveEchoFilterTests
    {
        [TestCase(0f, false), TestCase(.25f, false), TestCase(1f, false)]
        [TestCase(0f, true), TestCase(.25f, true), TestCase(1f, true)]
        public void ProcessingMeetsCpuAndAllocationBudgets(float adaptation, bool checkAllocations)
        {
            if (checkAllocations) AcousticCoreTests.RequireAllocationCounter();
            var filter = new MNNAdaptiveEchoFilter();
            var random = new Random(713);
            var mic = new float[128]; var reference = new float[128]; var residual = new float[128];
            for (int i = 0; i < mic.Length; ++i)
            {
                reference[i] = (float)(random.NextDouble() * .4 - .2);
                mic[i] = .6f * reference[i];
            }
            for (int i = 0; i < 32; ++i) filter.Process(mic, reference, residual,
                adaptation * filter.EstimateCoherence(mic, reference, MNNEchoDelayEstimator.AlignmentHeadroomSamples * 2));
            var elapsed = new double[256];
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < elapsed.Length; ++i)
            {
                long start = Stopwatch.GetTimestamp();
                float coherence = filter.EstimateCoherence(mic, reference, MNNEchoDelayEstimator.AlignmentHeadroomSamples * 2);
                filter.Process(mic, reference, residual, adaptation * coherence);
                elapsed[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Array.Sort(elapsed);
            double sum = 0;
            foreach (double milliseconds in elapsed) sum += milliseconds;
            TestContext.WriteLine($"FIR_BENCH adaptation={adaptation} mean={sum / elapsed.Length:F3} ms p95={elapsed[243]:F3} ms p99={elapsed[253]:F3} ms checked allocations={checkAllocations} bytes={allocated}");
            if (checkAllocations) Assert.Zero(allocated, "The processing worker must reuse its audio storage.");
            Assert.Less(elapsed[243], 8, "The FIR and its adaptation estimate must sustain 128 samples / 16 kHz; DTLN and the rest of the worker also need CPU time.");
        }

        [TestCase(.05f), TestCase(.7f), TestCase(-.9f)]
        public void ReacquiresAChangedRoomWithoutMutingIndependentSpeech(float echoGain)
        {
            var filter = new MNNAdaptiveEchoFilter();
            var random = new Random(181);
            var history = new float[4096];
            var mic = new float[128]; var reference = new float[128]; var residual = new float[128];
            double input = 0, output = 0;
            for (int block = 0; block < 750; ++block)
            {
                int direct = block < 250 ? 63 : 127;
                for (int i = 0; i < 128; ++i)
                {
                    int sample = block * 128 + i;
                    reference[i] = (float)(random.NextDouble() * .4 - .2);
                    history[sample % history.Length] = reference[i];
                    mic[i] = sample >= direct ? echoGain * history[(sample - direct) % history.Length] : 0;
                    if (sample >= 1807) mic[i] += .2f * echoGain * history[(sample - 1807) % history.Length];
                }
                filter.Process(mic, reference, residual);
                if (block < 350 || block >= 400) continue;
                for (int i = 0; i < 128; ++i) { input += mic[i] * mic[i]; output += residual[i] * residual[i]; }
            }
            double erle = 10 * Math.Log10(input / output);
            TestContext.WriteLine($"FIR_REACQUISITION gain={echoGain} ERLE after 800 ms={erle:F2} dB");
            Assert.Greater(erle, 20, "A moved direct path and late reflection must converge again within 800 ms.");
            Array.Clear(reference, 0, reference.Length);
            for (int block = 0; block < 17; ++block) filter.Process(reference, reference, residual);
            for (int i = 0; i < mic.Length; ++i) mic[i] = (float)(random.NextDouble() * .2 - .1);
            filter.Process(mic, reference, residual, 0);
            CollectionAssert.AreEqual(mic, residual, "Near speech must pass unchanged after the echo tail drains.");
        }

        [TestCase(-64), TestCase(0), TestCase(64), TestCase(72), TestCase(128), TestCase(136)]
        public void CausalAlignmentCoversAggregationAndHysteresisWithoutLosingNearSpeech(int lateBulkEstimate)
        {
            const int bulkDelay = 256, blocks = 512;
            var filter = new MNNAdaptiveEchoFilter();
            var random = new Random(431);
            var render = new float[blocks * 128];
            for (int i = 0; i < render.Length; ++i) render[i] = (float)(random.NextDouble() * .4 - .2);
            var mic = new float[128]; var reference = new float[128]; var residual = new float[128];
            var near = new float[128];
            int headroom = Math.Min(bulkDelay, MNNEchoDelayEstimator.AlignmentHeadroomSamples);
            double echoEnergy = 0, residualEnergy = 0, nearEnergy = 0, nearError = 0;
            for (int block = 0; block < blocks; ++block)
            {
                bool doubleTalk = block >= blocks - 64;
                for (int i = 0; i < 128; ++i)
                {
                    int sample = block * 128 + i;
                    int referenceSample = sample - bulkDelay + headroom;
                    int echoSample = sample - bulkDelay + lateBulkEstimate;
                    reference[i] = referenceSample >= 0 ? render[referenceSample] : 0;
                    near[i] = doubleTalk ? (float)(random.NextDouble() * .2 - .1) : 0;
                    mic[i] = (echoSample >= 0 ? .7f * render[echoSample] : 0) + near[i];
                }
                filter.Process(mic, reference, residual, doubleTalk ? 0 : 1);
                if (block < blocks / 2) continue;
                for (int i = 0; i < 128; ++i)
                {
                    if (doubleTalk)
                    {
                        nearEnergy += near[i] * (double)near[i];
                        double error = residual[i] - near[i]; nearError += error * error;
                    }
                    else { echoEnergy += mic[i] * (double)mic[i]; residualEnergy += residual[i] * (double)residual[i]; }
                }
            }
            Assert.Less(residualEnergy / echoEnergy, 1e-4,
                "The direct echo must remain causal throughout both estimator uncertainty bands and the coarse sample bin.");
            Assert.Less(nearError / nearEnergy, 1e-4,
                "Covering late alignment must retain the independent talker rather than mute microphone PCM.");
        }

        [TestCase(0), TestCase(64), TestCase(136), TestCase(272)]
        public void AdaptationRecognizesEchoAcrossTheCausalAlignmentRange(int lag)
        {
            var filter = new MNNAdaptiveEchoFilter();
            var random = new Random(933);
            const int blocks = 256;
            var signal = new float[blocks * 128];
            for (int i = 0; i < signal.Length; ++i) signal[i] = (float)(random.NextDouble() * .4 - .2);
            var mic = new float[128]; var reference = new float[128]; var residual = new float[128];
            double inputEnergy = 0, residualEnergy = 0;
            for (int block = 0; block < blocks; ++block)
            {
                for (int i = 0; i < 128; ++i)
                {
                    int sample = block * 128 + i;
                    reference[i] = signal[sample];
                    mic[i] = sample >= lag ? -.7f * signal[sample - lag] : 0;
                }
                float coherence = filter.EstimateCoherence(mic, reference,
                    MNNEchoDelayEstimator.AlignmentHeadroomSamples * 2);
                if (block >= 3) Assert.That(coherence, Is.EqualTo(1).Within(1e-6),
                    "Bulk-delay error must not be mistaken for an independent speaker or freeze path tracking.");
                filter.Process(mic, reference, residual, coherence);
                if (block < blocks / 2) continue;
                for (int i = 0; i < 128; ++i)
                { inputEnergy += mic[i] * (double)mic[i]; residualEnergy += residual[i] * (double)residual[i]; }
            }
            Assert.Less(residualEnergy / inputEnergy, 1e-4);
            Array.Clear(reference, 0, reference.Length); Array.Clear(mic, 0, mic.Length);
            for (int block = 0; block < 3; ++block) filter.Process(mic, reference, residual);
            Assert.Zero(filter.EstimateCoherence(mic, reference, MNNEchoDelayEstimator.AlignmentHeadroomSamples * 2));
        }

        [Test]
        public void FrozenAdaptationStillCancelsTheLearnedPathAndPreservesDoubleTalk()
        {
            var filter = new MNNAdaptiveEchoFilter();
            var random = new Random(675);
            var mic = new float[128]; var reference = new float[128]; var residual = new float[128]; var near = new float[128];
            double error = 0, nearEnergy = 0;
            for (int block = 0; block < 256; ++block)
            {
                bool frozen = block >= 128;
                for (int i = 0; i < 128; ++i)
                {
                    reference[i] = (float)(random.NextDouble() * .4 - .2);
                    near[i] = frozen ? (float)(random.NextDouble() * .4 - .2) : 0;
                    mic[i] = .7f * reference[i] + near[i];
                }
                filter.Process(mic, reference, residual, frozen ? 0 : 1);
                if (!frozen) continue;
                for (int i = 0; i < 128; ++i)
                {
                    double difference = residual[i] - near[i];
                    error += difference * difference; nearEnergy += near[i] * near[i];
                }
            }
            Assert.Less(error / nearEnergy, 1e-6,
                "Freezing coefficient updates must retain the echo prediction and pass the independent talker unchanged.");
        }

        [Test]
        public void ConvergesOnCorrelatedSpeechBandExcitation()
        {
            var filter = new MNNAdaptiveEchoFilter();
            var random = new Random(510);
            var history = new float[4096]; var mic = new float[128]; var reference = new float[128]; var residual = new float[128];
            float low = 0; double inputPower = 0, outputPower = 0;
            for (int block = 0; block < 1000; ++block)
            {
                for (int i = 0; i < 128; ++i)
                {
                    int index = block * 128 + i;
                    low = .95f * low + (float)(random.NextDouble() * .1 - .05);
                    reference[i] = low; history[index % history.Length] = low;
                    mic[i] = index >= 64 ? .7f * history[(index - 64) % history.Length] : 0;
                    if (index >= 240) mic[i] += .18f * history[(index - 240) % history.Length];
                    if (index >= 1040) mic[i] -= .12f * history[(index - 1040) % history.Length];
                }
                filter.Process(mic, reference, residual);
                if (block > 750) for (int i = 0; i < 128; ++i)
                { inputPower += mic[i] * mic[i]; outputPower += residual[i] * residual[i]; }
            }
            Assert.Less(outputPower / inputPower, 1e-3, "The adaptive filter must converge on correlated input without changing full-band output.");
        }

        [Test]
        public void LearnsCausalRoomAndKeepsAnIndependentTalker()
        {
            var filter = new MNNAdaptiveEchoFilter();
            var random = new Random(312);
            var history = new float[4096];
            var mic = new float[128]; var reference = new float[128]; var clean = new float[128];
            double inputEnergy = 0, residualEnergy = 0, nearEnergy = 0, nearError = 0;
            for (int block = 0; block < 800; ++block)
            {
                for (int i = 0; i < 128; ++i)
                {
                    int index = block * 128 + i;
                    reference[i] = (float)(random.NextDouble() * .4 - .2);
                    history[index % history.Length] = reference[i];
                    mic[i] = .7f * reference[i] + (index >= 976 ? .2f * history[(index - 976) % history.Length] : 0);
                }
                filter.Process(mic, reference, clean);
                if (block > 600) for (int i = 0; i < 128; ++i)
                { inputEnergy += mic[i] * mic[i]; residualEnergy += clean[i] * clean[i]; }
            }
            Assert.Less(residualEnergy / inputEnergy, 1e-6, "Known causal linear echo must converge, including reflections.");
            // Independent double talk must retain its coherent near-end component while the
            // normalized filter keeps adapting. This catches a filter that simply mutes output.
            double nearCross = 0, independentEnergy = 0, doubleTalkEnergy = 0;
            var independent = new float[128];
            for (int block = 0; block < 100; ++block)
            {
                for (int i = 0; i < 128; ++i)
                {
                    reference[i] = (float)(random.NextDouble() * .4 - .2);
                    independent[i] = (float)(random.NextDouble() * .2 - .1);
                    mic[i] = .7f * reference[i] + independent[i];
                }
                filter.Process(mic, reference, clean);
                for (int i = 0; i < 128; ++i)
                { nearCross += clean[i] * independent[i]; independentEnergy += independent[i] * independent[i]; doubleTalkEnergy += clean[i] * clean[i]; }
            }
            Assert.That(nearCross / independentEnergy, Is.InRange(.9, 1.1), "The independently generated talker must retain its gain.");
            Assert.Less(doubleTalkEnergy / independentEnergy, 3, "Adaptation must remain bounded through double talk.");
            // With no excitation the learned filter must neither learn the near-end voice nor erase it.
            for (int block = 0; block < 60; ++block)
            {
                Array.Clear(reference, 0, reference.Length);
                for (int i = 0; i < 128; ++i) mic[i] = (float)(random.NextDouble() * .2 - .1);
                filter.Process(mic, reference, clean);
                if (block > 20) for (int i = 0; i < 128; ++i)
                { nearEnergy += mic[i] * mic[i]; double error = mic[i] - clean[i]; nearError += error * error; }
            }
            Assert.Less(nearError / nearEnergy, 1e-12);
            Assert.AreEqual(1, filter.ResidualPowerRatio);
            filter.Reset();
            Array.Clear(reference, 0, reference.Length);
            filter.Process(mic, reference, clean);
            CollectionAssert.AreEqual(mic, clean);
        }
    }
}
