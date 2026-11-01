using System;
using System.Diagnostics;
using System.Globalization;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class FsEendPerformanceTests
    {
        [Test]
        public void CompletePcmRealMaximumReturnCompareBackendAndThreadOrders()
        {
            foreach (var order in new[] { new[] { 1, 2, 4, 8, -1 }, new[] { -1, 8, 4, 2, 1 }, new[] { 2, -1, 1, 8, 4 } })
            foreach (int selected in order)
            {
                int threads = Math.Abs(selected); var backend = selected < 0 ? MNNBackendType.Metal : MNNBackendType.CPU;
                using (var model = MNNFsEendDiarization.Load(FsEendReference.Candidate, threads, backend))
                {
                    model.Analyze(FsEendReference.Read("real-input"));
                    foreach (string name in new[] { "real", "maximum", "real" })
                    {
                        var pcm = FsEendReference.Read(name + "-input"); var timer = Stopwatch.StartNew(); var result = model.Analyze(pcm); timer.Stop();
                        Assert.AreEqual(backend, model.Backend); FsEendReference.Check(FsEendReference.Read(name + "-posterior"), result.Probabilities, backend + ";" + name);
                        TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, "FS_EEND_WHOLE backend={0};threads={1};case={2};milliseconds={3:R};audio_seconds={4:R}", backend, threads, name, timer.Elapsed.TotalMilliseconds, pcm.Length / 8000.0));
                    }
                }
            }
        }
    }
}
