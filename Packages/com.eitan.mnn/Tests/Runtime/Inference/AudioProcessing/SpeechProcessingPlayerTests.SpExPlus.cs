using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void SpExPlusTargetAndReferenceMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNSpExPlusExtraction.Load(Model("SpExPlus-WSJ02Mix-8K-MNN"), 4, backend))
            {
                foreach (string name in new[] { "minimum", "encoder_before", "encoder_at", "encoder_after", "pool_before", "pool_at", "pool_after", "long_reference", "maximum", "one_second", "odd", "real", "other_speaker", "quiet", "gain", "silence", "silent_reference", "dc", "inverted_reference", "reference_short", "impulse", "silence_both", "silence_short_reference", "silence_quiet_reference", "minimum" })
                {
                    var input = Read("MNN_TEST_SPEXPLUS_REFERENCE", name + "-input");
                    var reference = model.EncodeReference(Read("MNN_TEST_SPEXPLUS_REFERENCE", name + "-reference"));
                    CheckSpExArray(reference.ToArray(), Read("MNN_TEST_SPEXPLUS_REFERENCE", name + "-embedding"), name + ":embedding");
                    var exposed = reference.ToArray(); exposed[0] = float.NaN;
                    var expected = Read("MNN_TEST_SPEXPLUS_REFERENCE", name + "-target");
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        CheckSpExArray(model.Extract(input, reference), expected, name + ":target:" + repeat);
                        Assert.AreEqual(backend, model.Backend, name);
                        Assert.AreEqual(backend, model.ReferenceBackend, name);
                    }
                }
                var conditioning = model.EncodeReference(new float[1680], 48000);
                var restored = model.Extract(new float[121], conditioning, 48000); Assert.AreEqual(121, restored.Length);
                foreach (float sample in restored) Assert.False(float.IsNaN(sample) || float.IsInfinity(sample));
            }
        }

        private static void CheckSpExArray(float[] actual, float[] expected, string name)
        {
            Assert.AreEqual(expected.Length, actual.Length, name); double energy = 0, error = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                double value = expected[i]; energy += value * value; double delta = actual[i] - value; error += delta * delta;
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(name + ";nrmse=" + nrmse); Assert.Less(nrmse, .001, name);
        }
    }
}
