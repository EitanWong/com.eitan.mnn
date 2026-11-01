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
            Measure(FsEendReference.Candidate, FsEendReference.Directory, "FS_EEND_WHOLE");
        }
        [Test]
        public void CallhomeCompletePcmRealMaximumReturnCompareBackendAndThreadOrders()
        {
            Measure(FsEendReference.CallhomeCandidate, FsEendReference.CallhomeDirectory, "FS_CALLHOME_WHOLE");
        }
        private static void Measure(string directory, string reference, string label)
        {
            foreach (var order in new[] { new[] { 1, 2, 4, 8, -1 }, new[] { -1, 8, 4, 2, 1 }, new[] { 2, -1, 1, 8, 4 } })
            foreach (int selected in order)
            {
                int threads = Math.Abs(selected); var backend = selected < 0 ? MNNBackendType.Metal : MNNBackendType.CPU;
                using (var model = MNNFsEendDiarization.Load(directory, threads, backend))
                {
                    model.Analyze(FsEendReference.ReadFrom(reference, "real-input"));
                    foreach (string name in new[] { "real", "maximum", "real" })
                    {
                        var pcm = FsEendReference.ReadFrom(reference, name + "-input"); var timer = Stopwatch.StartNew(); var result = model.Analyze(pcm); timer.Stop();
                        Assert.AreEqual(backend, model.Backend); FsEendReference.Check(FsEendReference.ReadFrom(reference, name + "-posterior"), result.Probabilities, backend + ";" + name);
                        TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, label + " backend={0};threads={1};case={2};milliseconds={3:R};audio_seconds={4:R}", backend, threads, name, timer.Elapsed.TotalMilliseconds, pcm.Length / 8000.0));
                    }
                }
            }
        }
    }
}
