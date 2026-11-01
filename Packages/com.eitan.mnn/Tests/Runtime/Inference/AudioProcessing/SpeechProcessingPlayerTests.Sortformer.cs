using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void SortformerActivityMatchesOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNSortformerDiarization.Load(Model("Sortformer-4spk-v2.1-MNN"), 4, backend))
            {
                foreach (string name in new[] { "speech", "quiet", "gain2", "silence", "noise", "tone", "minimum", "short", "boundary_before", "boundary_at", "boundary_after", "long", "maximum", "speech" })
                {
                    var input = Read("MNN_TEST_SORTFORMER_REFERENCE", name + "-input");
                    var expected = Read("MNN_TEST_SORTFORMER_REFERENCE", name + "-predictions-source");
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        var actual = model.Analyze(input);
                        Assert.AreEqual(expected.Length, actual.Probabilities.Length, name);
                        double maximum = 0;
                        for (int i = 0; i < expected.Length; ++i)
                        {
                            Assert.False(float.IsNaN(actual.Probabilities[i]) || float.IsInfinity(actual.Probabilities[i]), name);
                            maximum = Math.Max(maximum, Math.Abs((double)actual.Probabilities[i] - expected[i]));
                        }
                        TestContext.WriteLine(name + ";repeat=" + repeat + ";probability_max_absolute_error=" + maximum);
                        Assert.Less(maximum, .001, name);
                        Assert.AreEqual(expected.Length / 4, actual.FrameCount);
                    }
                }
                Assert.AreEqual(backend, model.PreencodeBackend);
                Assert.AreEqual(backend, model.CoreBackend);
            }
        }
    }
}
