using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void DcunetEnhancementMatchesOriginalComplexCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNDcunetEnhancement.Load(Model("DCUNet-Libri1Mix-16K-MNN"), 4, backend))
            {
                foreach (string name in new[]
                {
                    "mixture", "speech", "quiet", "gain2", "silence", "constant", "noise", "tone",
                    "minimum", "maximum", "long", "boundary_before", "boundary_at", "boundary_after", "minimum_after", "hop_before", "hop_at", "hop_after", "mixture"
                })
                {
                    var input = Read("MNN_TEST_DCUNET_REFERENCE", name + "-input");
                    var expected = Read("MNN_TEST_DCUNET_REFERENCE", name + "-enhanced");
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        var actual = model.Enhance(input);
                        Assert.AreEqual(expected.Length, actual.Length, name);
                        double error = 0, energy = 0;
                        for (int i = 0; i < actual.Length; ++i)
                        {
                            Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                            double delta = (double)actual[i] - expected[i];
                            error += delta * delta;
                            energy += (double)expected[i] * expected[i];
                        }
                        double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
                        TestContext.WriteLine(name + ";repeat=" + repeat + ";waveform_nrmse=" + nrmse);
                        Assert.Less(nrmse, .001, name);
                        Assert.AreEqual(backend, model.Backend, "complete graph");
                    }
                }
            }
        }
    }
}
