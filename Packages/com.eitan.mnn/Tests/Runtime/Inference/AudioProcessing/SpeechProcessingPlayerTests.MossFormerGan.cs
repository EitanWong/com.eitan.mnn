using System;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void MossFormerGanWaveformsMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNMossFormerGan.Load(Model("MossFormerGAN-SE-16K-MNN"), 1, backend))
            {
                foreach (string name in new[] { "speech", "natural_speech", "quiet", "gain2", "noise", "tone", "short", "long", "minimum", "speech" })
                {
                    var input = Read("MNN_TEST_MOSSFORMERGAN_REFERENCE", name + "-input");
                    var expected = Read("MNN_TEST_MOSSFORMERGAN_REFERENCE", name + "-enhanced");
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
                Assert.That(model.Enhance(new float[16000]), Is.All.EqualTo(0));
                Assert.AreEqual(backend, model.Backend);
            }
        }
    }
}
