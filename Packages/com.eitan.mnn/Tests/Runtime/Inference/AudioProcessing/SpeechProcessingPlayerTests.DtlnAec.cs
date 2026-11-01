using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(128, MNNBackendType.CPU), TestCase(256, MNNBackendType.CPU), TestCase(512, MNNBackendType.CPU)]
        [TestCase(128, MNNBackendType.Metal), TestCase(256, MNNBackendType.Metal), TestCase(512, MNNBackendType.Metal)]
        public void AecWaveformsMatchOriginalTfliteInBuiltPlayer(int units, MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNDtlnAec.Load(Model("DTLN-aec-" + units + "-MNN"), 1, backend))
            {
                foreach (string name in new[] { "doubletalk", "farend_singletalk", "nearend_singletalk", "quiet", "mic_only", "reference_only", "silence", "noise", "short", "long", "doubletalk" })
                {
                    var expected = Read("MNN_TEST_DTLN_AEC_REFERENCE", name + "-" + units + "-enhanced");
                    var actual = model.Process(Read("MNN_TEST_DTLN_AEC_REFERENCE", name + "-mic"), Read("MNN_TEST_DTLN_AEC_REFERENCE", name + "-ref"));
                    Assert.AreEqual(expected.Length, actual.Length);
                    double error = 0, energy = 0, maximum = 0;
                    for (int i = 0; i < actual.Length; ++i)
                    {
                        Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]));
                        double delta = (double)actual[i] - expected[i];
                        error += delta * delta; energy += (double)expected[i] * expected[i];
                        maximum = Math.Max(maximum, Math.Abs(delta));
                    }
                    if (energy == 0) Assert.That(maximum, Is.LessThan(1e-6), name);
                    else Assert.That(Math.Sqrt(error / energy), Is.LessThan(.001), name);
                }
                if (backend == MNNBackendType.CPU)
                using (var defaults = MNNDtlnAec.Load(Model("DTLN-aec-" + units + "-MNN")))
                {
                    Verify(() => defaults.Process(Read("MNN_TEST_DTLN_AEC_REFERENCE", "doubletalk-mic"), Read("MNN_TEST_DTLN_AEC_REFERENCE", "doubletalk-ref")),
                        Read("MNN_TEST_DTLN_AEC_REFERENCE", "doubletalk-" + units + "-enhanced"), .001);
                    Assert.AreEqual(MNNBackendType.CPU, defaults.Backend);
                }
                Assert.AreEqual(backend, model.Backend);
                var mic = new float[128]; var reference = new float[128]; var output = new float[128];
                model.Reset();
                for (int i = 0; i < 8; ++i) model.ProcessHop(mic, reference, output);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; ++i) model.ProcessHop(mic, reference, output);
                Assert.Zero(GC.GetAllocatedBytesForCurrentThread() - before);
            }
        }
    }
}
