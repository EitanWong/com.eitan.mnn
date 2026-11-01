using System;
using System.Diagnostics;
using System.Globalization;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class CueLightSsmPerformanceTests
    {
        [Test]
        public void WholeSixtySecondSsmCallsCompareCpuThreadOrders()
        {
            var pcm = CueLightSsmSourceTests.Read("maximum-input"); var expected = CueLightSsmSourceTests.Read("maximum-events");
            foreach (var order in new[] { new[] { 1, 2, 4, 8 }, new[] { 8, 4, 2, 1 }, new[] { 2, 1, 8, 4 } })
            foreach (int threads in order)
            {
                using (var model = MNNCueLightTurnDetector.Load(CueLightSsmSourceTests.Candidate, threads, MNNBackendType.CPU))
                {
                    Assert.AreEqual(MNNBackendType.CPU, model.Backend); model.Analyze(pcm);
                    var timer = Stopwatch.StartNew(); var result = model.Analyze(pcm); timer.Stop();
                    for (int i = 0; i < result.FrameCount; ++i) CueLightSsmSourceTests.AssertFrame(expected, i, result.Frames[i]);
                    TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, "CUELIGHT_SSM_WHOLE backend=CPU;threads={0};milliseconds={1:R};audio_seconds=60", threads, timer.Elapsed.TotalMilliseconds));
                }
            }
        }
    }
}
