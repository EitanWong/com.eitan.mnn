using System;
using System.Diagnostics;
using System.Globalization;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class VbxPerformanceTests
    {
        [Test]
        public void CompletePcmRealMaximumReturnCompareBackendAndThreadOrders()
        {
            foreach (var order in new[] { new[] { 1, 2, 4, 8, -1 }, new[] { -1, 8, 4, 2, 1 }, new[] { 2, -1, 1, 8, 4 } })
            foreach (int selected in order)
            {
                int threads = Math.Abs(selected); var backend = selected < 0 ? MNNBackendType.Metal : MNNBackendType.CPU;
                using (var model = MNNVbxEmbedding.Load(VbxReference.Candidate, threads, backend))
                {
                    model.Encode(VbxReference.Read("real-input"));
                    foreach (string name in new[] { "real", "maximum", "real" })
                    {
                        var pcm = VbxReference.Read(name + "-input"); var timer = Stopwatch.StartNew(); var result = model.Encode(pcm); timer.Stop();
                        Assert.AreEqual(backend, model.Backend); VbxReference.Check(VbxReference.Read(name + "-embedding"), result, backend + ";" + name);
                        TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, "VBX_WHOLE backend={0};threads={1};case={2};milliseconds={3:R};audio_seconds={4:R}", backend, threads, name, timer.Elapsed.TotalMilliseconds, pcm.Length / 16000.0));
                    }
                }
            }
        }
    }
}
