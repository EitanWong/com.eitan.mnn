using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class FftPlanTests
    {
        [TestCase(8), TestCase(32), TestCase(512), TestCase(640), TestCase(960), TestCase(1680)]
        public void MatchesIndependentDftAndRoundTrip(int length)
        {
            foreach (bool burst in new[] {false, true})
            {
                var real = new double[length]; var imaginary = new double[length];
                for (int i = 0; i < length; ++i) { real[i] = Math.Sin(i * .37) + .1 * Math.Cos(i); imaginary[i] = .3 * Math.Cos(i * .17); }
                var sourceReal = (double[])real.Clone(); var sourceImaginary = (double[])imaginary.Clone();
                var plan = new MNNFftPlan(length, burst);
                plan.Transform(real, imaginary);
                for (int k = 0; k < length; ++k)
                {
                    double r = 0, im = 0;
                    for (int t = 0; t < length; ++t)
                    {
                        double angle = -2 * Math.PI * k * t / length;
                        r += sourceReal[t] * Math.Cos(angle) - sourceImaginary[t] * Math.Sin(angle);
                        im += sourceReal[t] * Math.Sin(angle) + sourceImaginary[t] * Math.Cos(angle);
                    }
                    Assert.AreEqual(r, real[k], 1e-9, "real bin " + k);
                    Assert.AreEqual(im, imaginary[k], 1e-9, "imaginary bin " + k);
                }
                plan.Transform(real, imaginary, true);
                for (int i = 0; i < length; ++i)
                {
                    Assert.AreEqual(sourceReal[i], real[i], 1e-12);
                    Assert.AreEqual(sourceImaginary[i], imaginary[i], 1e-12);
                }
            }
        }
        [Test]
        public void SharedBluesteinPlanSupportsConcurrentTransforms()
        {
            var plan = new MNNFftPlan(640, false);
            System.Threading.Tasks.Parallel.For(0, 16, task =>
            {
                var real = new double[640];
                var imaginary = new double[640];
                for (int i = 0; i < real.Length; ++i) real[i] = Math.Sin(i * .07 + task);
                var original = (double[])real.Clone();
                for (int iteration = 0; iteration < 12; ++iteration)
                {
                    plan.Transform(real, imaginary);
                    plan.Transform(real, imaginary, true);
                }
                for (int i = 0; i < real.Length; ++i)
                    Assert.AreEqual(original[i], real[i], 1e-10, "Concurrent roundtrip sample " + i);
            });
        }
        [TestCase(640), TestCase(960), TestCase(1680)]
        public void BluesteinWarmTransformsDoNotAllocate(int length)
        {
            var plan = new MNNFftPlan(length);
            var real = new double[length]; var imaginary = new double[length];
            plan.Transform(real, imaginary);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; ++i) plan.Transform(real, imaginary);
            Assert.Zero(GC.GetAllocatedBytesForCurrentThread() - before);
        }
        [Test]
        public void RejectsInvalidBuffersAndLengths()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNFftPlan(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNFftPlan(513));
            var plan = new MNNFftPlan(512); var buffer = new double[512];
            Assert.Throws<ArgumentException>(() => plan.Transform(buffer, buffer));
            Assert.Throws<ArgumentException>(() => plan.Transform(buffer, new double[128]));
        }
        [Test]
        public void MeasuresPrepared512PointFftAndChecksNoManagedAllocation()
        {
            if (Environment.GetEnvironmentVariable("MNN_TEST_REQUIRE_BURST") == "1") Assert.True(MNNFftPlan.IsBurstAvailable, "Burst must actually initialize.");
            var real = new double[512]; var imaginary = new double[512];
            for (int i = 0; i < 512; ++i) real[i] = Math.Sin(i * .31);
            string report = "Burst available: " + MNNFftPlan.IsBurstAvailable + "\n";
            foreach (bool burst in new[] {false, true})
            {
                var plan = new MNNFftPlan(512, burst);
                for (int i = 0; i < 100; ++i) { plan.Transform(real, imaginary); plan.Transform(real, imaginary, true); }
                var timer = Stopwatch.StartNew(); long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; ++i) { plan.Transform(real, imaginary); plan.Transform(real, imaginary, true); }
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated; timer.Stop();
                report += (plan.UsesBurst ? "Burst" : "C#") + " 512 complex FFT us=" + (timer.Elapsed.TotalMilliseconds * 1000 / 20000).ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + " allocated=" + allocated + "\n";
                Assert.Zero(allocated, "Warm transforms must not allocate managed memory.");
            }
            TestContext.WriteLine(report);
            string output = Environment.GetEnvironmentVariable("MNN_TEST_DTLN_AEC_ROOT");
            if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output, MNNFftPlan.IsBurstAvailable ? "fft-burst.txt" : "fft-managed.txt"), report);
        }
    }
}
