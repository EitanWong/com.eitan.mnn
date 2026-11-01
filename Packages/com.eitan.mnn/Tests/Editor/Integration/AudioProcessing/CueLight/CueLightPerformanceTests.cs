using System;
using System.Diagnostics;
using System.Globalization;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class CueLightPerformanceTests
    {
        [Test]
        public void WholeSixtySecondPcmCallsCompareBackendAndThreadOrders()
        {
            var pcm = CueLightSourceParityTests.Read("maximum-input"); var expected = CueLightSourceParityTests.Read("maximum-events");
            foreach (var order in new[] { new[] { 1, 2, 4, 8, -1 }, new[] { -1, 8, 4, 2, 1 }, new[] { 2, -1, 1, 8, 4 } })
            foreach (int selection in order)
            {
                var backend = selection < 0 ? MNNBackendType.Metal : MNNBackendType.CPU; int threads = Math.Abs(selection);
                using (var model = MNNCueLightTurnDetector.Load(CueLightSourceParityTests.Candidate, threads, backend))
                {
                    Assert.AreEqual(backend, model.Backend); model.Analyze(pcm); var timer = Stopwatch.StartNew(); var result = model.Analyze(pcm); timer.Stop();
                    double maximum = 0;
                    for (int i = 0; i < result.FrameCount; ++i)
                    {
                        var f = result.Frames[i]; maximum = Math.Max(maximum, Math.Abs((double)f.SpeechProbability - expected[i * 5]));
                        maximum = Math.Max(maximum, Math.Abs((double)f.EndOfTurnProbability - expected[i * 5 + 1]));
                        Assert.AreEqual((int)expected[i * 5 + 2], f.ResetCount); Assert.AreEqual(expected[i * 5 + 3] == 1, f.SnappyEndpoint); Assert.AreEqual(expected[i * 5 + 4] == 1, f.CarefulEndpoint);
                    }
                    Assert.Less(maximum, .0001);
                    TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, "CUELIGHT_WHOLE backend={0};threads={1};milliseconds={2:R};audio_seconds=60", backend, threads, timer.Elapsed.TotalMilliseconds));
                }
            }
        }
    }
}
