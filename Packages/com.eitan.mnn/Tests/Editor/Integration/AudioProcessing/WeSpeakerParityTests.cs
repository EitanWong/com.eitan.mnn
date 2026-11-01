using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class WeSpeakerParityTests
    {
        private static string Reference
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("MNN_TEST_WESPEAKER_REFERENCE");
                if (string.IsNullOrWhiteSpace(path)) Assert.Ignore("Set MNN_TEST_WESPEAKER_REFERENCE to the Kaldi/ONNX oracle.");
                return path;
            }
        }

        [Test, Explicit("Requires the independent Kaldi-native-fbank speech oracle.")]
        public void SpeakerFrontendMatchesKaldiFbank()
        {
            var audio = Read("input");
            var expected = Read("features");
            var actual = MNNKaldiFeatures.ExtractSpeaker(audio, out int frames, CancellationToken.None);
            Assert.AreEqual(expected.Length, frames * 80);
            double maximum = actual.Select((value, i) => Math.Abs((double)value - expected[i])).Max();
            TestContext.WriteLine("Feature max error=" + maximum);
            Assert.That(maximum, Is.LessThan(.001));
        }

        [Test, Explicit("Requires the WeSpeaker MNN graph and independent Kaldi/ONNX oracle.")]
        public void CpuEmbeddingMatchesKaldiAndOnnx() => Verify(MNNBackendType.CPU);

        [Test, Explicit("Requires Metal, the WeSpeaker MNN graph and independent Kaldi/ONNX oracle.")]
        public void MetalEmbeddingMatchesKaldiAndOnnx() => Verify(MNNBackendType.Metal);

        private static void Verify(MNNBackendType backend)
        {
            var audio = Read("input");
            var expected = Read("embedding");
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            string directory = Path.GetFullPath(Path.Combine("Assets", "StreamingAssets", "MNN", "Models", "WeSpeaker-ResNet34-LM-MNN"));
            using (var model = MNNWeSpeakerEmbedding.Load(directory, 2, backend))
            {
                var actual = model.Encode(audio);
                Assert.AreEqual(backend, model.Backend);
                Assert.AreEqual(256, actual.Length);
                double error = Math.Sqrt(actual.Select((value, i) => Math.Pow((double)value - expected[i], 2)).Sum());
                TestContext.WriteLine("Normalized embedding RMSE=" + error);
                Assert.That(error, Is.LessThan(.001));
                Assert.That(MNNWeSpeakerEmbedding.Similarity(actual, expected), Is.GreaterThan(.99999));
                var repeated = model.Encode(audio);
                Assert.That(actual.Select((value, i) => Math.Abs((double)value - repeated[i])).Max(), Is.LessThan(1e-6));
            }
        }

        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
