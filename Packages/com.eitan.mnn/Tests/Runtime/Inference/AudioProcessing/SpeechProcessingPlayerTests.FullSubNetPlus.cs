using System;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void FullSubNetPlusWaveformsMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNFullSubNet.Load(Model("FullSubNetPlus-MNN"), 1, backend))
            {
                foreach (string name in new[] { "speech", "natural_speech", "quiet", "gain2", "silence", "noise", "tone", "short", "long", "speech" })
                {
                    var input = Read("MNN_TEST_FULLSUBNETPLUS_REFERENCE", name + "-input");
                    var expected = Read("MNN_TEST_FULLSUBNETPLUS_REFERENCE", name + "-enhanced");
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        var actual = model.Enhance(input);
                        Assert.AreEqual(expected.Length, actual.Length);
                        Assert.True(actual.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), name);
                        double energy = expected.Sum(value => (double)value * value);
                        double error = Math.Sqrt(actual.Select((value, i) => Math.Pow((double)value - expected[i], 2)).Sum() / Math.Max(1e-30, energy));
                        TestContext.WriteLine(name + "; repeat=" + repeat + "; waveform_nrmse=" + error);
                        Assert.Less(error, .001, name);
                    }
                }
                Assert.AreEqual(backend, model.Backend);
                Assert.AreEqual("FullSubNet+", model.ModelName);
            }
        }
    }
}
