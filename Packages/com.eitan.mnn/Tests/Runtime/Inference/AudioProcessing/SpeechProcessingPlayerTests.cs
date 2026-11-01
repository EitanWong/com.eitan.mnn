using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("SpeechProcessingPlayer")]
    public sealed partial class SpeechProcessingPlayerTests
    {
        private static string Model(string name) => Path.Combine(Application.streamingAssetsPath, "MNN", "Models", name);
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void DtlnWaveformMatchesOnnxInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            var input = Read("MNN_TEST_DTLN_REFERENCE", "input");
            var expected = Read("MNN_TEST_DTLN_REFERENCE", "enhanced");
            using (var model = MNNDtln.Load(Model("DTLN-MNN"), 1, backend))
            {
                Verify(() => model.Enhance(input), expected, backend == MNNBackendType.CPU ? .0001 : .002);
                Assert.AreEqual(backend, model.Stage1Backend);
                Assert.AreEqual(backend, model.Stage2Backend);
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void DeepFilterWaveformMatchesLibDfOnnxInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            var input = Read("MNN_TEST_DEEPFILTERNET_REFERENCE", "input");
            var expected = Read("MNN_TEST_DEEPFILTERNET_REFERENCE", "enhanced");
            using (var model = MNNDeepFilterNet3.Load(Model("DeepFilterNet3-MNN"), 2, backend))
            {
                Verify(() => model.Enhance(input), expected, backend == MNNBackendType.CPU ? .00002 : .002);
                Assert.AreEqual(backend, model.EncoderBackend);
                Assert.AreEqual(backend, model.ErbDecoderBackend);
                Assert.AreEqual(backend, model.DfDecoderBackend);
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void WeSpeakerEmbeddingMatchesKaldiOnnxInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            var input = Read("MNN_TEST_WESPEAKER_REFERENCE", "input");
            var expected = Read("MNN_TEST_WESPEAKER_REFERENCE", "embedding");
            using (var model = MNNWeSpeakerEmbedding.Load(Model("WeSpeaker-ResNet34-LM-MNN"), 2, backend))
            {
                Verify(() => model.Encode(input), expected, .001);
                Assert.AreEqual(backend, model.Backend);
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void AvTargetWaveformMatchesPyTorchInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            var input = Read("MNN_TEST_AV_REFERENCE", "input");
            var visual = Read("MNN_TEST_AV_REFERENCE", "visual");
            var expected = Read("MNN_TEST_AV_REFERENCE", "enhanced");
            using (var model = MNNAvMossFormer2Tse.Load(Model("AV-MossFormer2-TSE-16K-MNN"), 2, backend))
            {
                Verify(() => model.Extract(input, visual, 75), expected, backend == MNNBackendType.CPU ? .0005 : .005);
                Assert.AreEqual(backend, model.Backend);
            }
        }
        private static void RequirePlayer(MNNBackendType backend)
        {
            if (Application.isEditor) Assert.Ignore("This fixture requires a built Player with staged models and independent references.");
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
        }
        private static float[] Read(string variable, string name)
        {
            string directory = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(directory)) Assert.Ignore("Set " + variable + " to the prepared oracle; no downloads.");
            var bytes = File.ReadAllBytes(Path.Combine(directory, name + ".f32"));
            var values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        private static void Verify(Func<float[]> run, float[] expected, double tolerance)
        {
            var actual = run();
            Assert.AreEqual(expected.Length, actual.Length);
            double error = Math.Sqrt(actual.Select((value, i) => Math.Pow((double)value - expected[i], 2)).Sum() / expected.Sum(value => (double)value * value));
            TestContext.WriteLine("Normalized output error=" + error + "; samples=" + actual.Length);
            Assert.That(error, Is.LessThan(tolerance));
            var repeated = run();
            Assert.That(actual.Select((value, i) => Math.Abs(value - repeated[i])).Max(), Is.LessThan(1e-5));
        }
    }
}
