using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class LsEendPerformanceTests
    {
        [Test]
        public void CompareSixtySecondWholeCallsWithReversedThreadOrders()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_LS_EEND");
            var bytes = File.ReadAllBytes(Path.Combine(root, "source-graphs-v208/reference/maximum-input.f32"));
            var pcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
            bytes = File.ReadAllBytes(Path.Combine(root, "source-graphs-v208/reference/maximum-posterior.f32"));
            var expected = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length);
            foreach (var order in new[] { new[] { 1, 2, 4, 8 }, new[] { 8, 4, 2, 1 }, new[] { 2, 8, 1, 4 } })
            foreach (int threads in order)
            using (var model = MNNLsEendDiarization.Load(Path.Combine(root, "candidate-v216"), threads, MNNBackendType.CPU))
            {
                model.Analyze(new float[1]);
                var timer = System.Diagnostics.Stopwatch.StartNew(); var result = model.Analyze(pcm); timer.Stop();
                double maximum = 0;
                for (int i = 0; i < expected.Length; ++i) maximum = Math.Max(maximum, Math.Abs((double)result.Probabilities[i] - expected[i]));
                Assert.Less(maximum, .0001);
                TestContext.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "LS_EEND_LONG threads={0};milliseconds={1:R};audio_seconds=60", threads, timer.Elapsed.TotalMilliseconds));
            }
        }
        [Test]
        public void MeasureCompletePcmWithBothBackendOrdersAndCpuThreads()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_LS_EEND");
            string candidate = Path.Combine(root, "candidate-v216");
            var bytes = File.ReadAllBytes(Path.Combine(root, "source-graphs-v208/reference/real-input.f32"));
            var pcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
            bytes = File.ReadAllBytes(Path.Combine(root, "source-graphs-v208/reference/real-posterior.f32"));
            var expected = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length);
            foreach (var backends in new[] { new[] { MNNBackendType.CPU, MNNBackendType.Metal }, new[] { MNNBackendType.Metal, MNNBackendType.CPU } })
            foreach (var backend in backends)
            foreach (int threads in backend == MNNBackendType.CPU ? new[] { 1, 2, 4, 8 } : new[] { 1 })
            {
                var load = System.Diagnostics.Stopwatch.StartNew();
                using (var model = MNNLsEendDiarization.Load(candidate, threads, backend))
                {
                    load.Stop(); model.Analyze(pcm);
                    var times = new double[5];
                    for (int repeat = 0; repeat < times.Length; ++repeat)
                    {
                        var timer = System.Diagnostics.Stopwatch.StartNew(); var result = model.Analyze(pcm); timer.Stop();
                        times[repeat] = timer.Elapsed.TotalMilliseconds;
                        Assert.AreEqual(expected.Length, result.Probabilities.Length);
                        double maximum = 0;
                        for (int i = 0; i < expected.Length; ++i) maximum = Math.Max(maximum, Math.Abs((double)result.Probabilities[i] - expected[i]));
                        Assert.Less(maximum, .0001);
                    }
                    Array.Sort(times);
                    Assert.AreEqual(backend, model.EncoderBackend); Assert.AreEqual(backend, model.DecoderBackend);
                    TestContext.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "LS_EEND_PERFORMANCE backend={0};threads={1};load_ms={2:R};median_ms={3:R};audio_seconds={4:R}",
                        backend, threads, load.Elapsed.TotalMilliseconds, times[2], pcm.Length / 8000.0));
                }
            }
        }
    }
}
