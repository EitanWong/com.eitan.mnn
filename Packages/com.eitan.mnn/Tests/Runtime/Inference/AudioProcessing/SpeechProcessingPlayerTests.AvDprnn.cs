using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void AvDprnnTargetWaveformsMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNAvDprnnExtraction.Load(Model("AV-DPRNN-VoxCeleb2-16K-MNN"), 4, backend))
            {
                foreach (string name in new[] { "minimum", "encoder_before", "encoder_at", "encoder_after", "segment_before", "segment_at", "segment_after", "segment_step", "short", "noise", "long", "real", "quiet", "gain2", "silence", "constant", "shifted_visual", "mean_visual", "maximum", "minimum" })
                {
                    var input = Read("MNN_TEST_AV_DPRNN_REFERENCE", name + "-input");
                    var visual = Read("MNN_TEST_AV_DPRNN_REFERENCE", name + "-visual");
                    var expected = Read("MNN_TEST_AV_DPRNN_REFERENCE", name + "-target");
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        var actual = model.Extract(input, visual, visual.Length / (112 * 112));
                        Assert.AreEqual(expected.Length, actual.Length, name);
                        double energy = 0, error = 0;
                        for (int i = 0; i < actual.Length; ++i)
                        {
                            Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                            double value = expected[i];
                            energy += value * value;
                            double delta = actual[i] - value;
                            error += delta * delta;
                        }
                        double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
                        TestContext.WriteLine(name + "; repeat=" + repeat + "; waveform_nrmse=" + nrmse);
                        Assert.Less(nrmse, .001, name);
                    }
                }
                Assert.AreEqual(backend, model.Backend);
                Assert.AreEqual(MNNBackendType.CPU, model.AudioBackend);
                // Resampling must not invent a second frame at this duration boundary.
                var restored = model.Extract(new float[3839], new float[112 * 112], 1, 48000);
                Assert.AreEqual(3839, restored.Length);
                foreach (float sample in restored) Assert.AreEqual(0, sample);
            }
        }
    }
}
