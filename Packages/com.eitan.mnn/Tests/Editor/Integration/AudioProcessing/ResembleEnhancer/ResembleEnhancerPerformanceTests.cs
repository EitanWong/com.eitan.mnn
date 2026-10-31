using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    // Explicitly selected only after all correctness and shape-history gates pass.
    public sealed class ResembleEnhancerPerformanceTests
    {
        [Test]
        public void CompletePcmLatencyUsesBothInterleavedBackendOrders()
        {
            var orders = new[]
            {
                new[] { MNNBackendType.CPU, MNNBackendType.Metal },
                new[] { MNNBackendType.Metal, MNNBackendType.CPU }
            };
            foreach (var order in orders)
            foreach (var backend in order)
            {
                var load = Stopwatch.StartNew();
                using (var model = MNNResembleEnhancer.Load(ResembleEnhancerReference.Model, backendType: backend))
                {
                    load.Stop();
                    TestContext.WriteLine("CFM_LOAD;backend=" + backend + ";ms=" + Number(load.Elapsed.TotalMilliseconds));
                    foreach (string name in new[] { "real", "maximum", "real" })
                    {
                        float[] input = ResembleEnhancerReference.Read(name + "-input");
                        var first = Stopwatch.StartNew();
                        float[] output = model.Enhance(input);
                        first.Stop();
                        ResembleEnhancerReference.Check(name + " first PCM", output, ResembleEnhancerReference.Read(name + "-target"));
                        var times = new double[3];
                        for (int repeat = 0; repeat < times.Length; ++repeat)
                        {
                            var timer = Stopwatch.StartNew();
                            output = model.Enhance(input);
                            timer.Stop();
                            times[repeat] = timer.Elapsed.TotalMilliseconds;
                            ResembleEnhancerReference.Check(name + " warm PCM", output, ResembleEnhancerReference.Read(name + "-target"));
                        }
                        ResembleEnhancerReference.CheckBackends(model, backend);
                        TestContext.WriteLine("CFM_PCM;backend=" + backend + ";case=" + name +
                            ";first_ms=" + Number(first.Elapsed.TotalMilliseconds) + ";warm_ms=" + string.Join(",", times.Select(Number)));
                    }
                }
            }
        }

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
