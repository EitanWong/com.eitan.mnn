using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class MNNMiniMindOPreprocessingTests
    {
        private static object Fixture(string name) => MNNModelData.Read(Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Packages/com.eitan.mnn/Tests/Fixtures/MiniMindO", name + ".json")));

        [Test]
        public void SenseVoiceFrontendMatchesKaldiAndCheckpointCmvn()
        {
            var fixture = Fixture("frontend");
            int count = MNNModelData.Integer(MNNModelData.Get(fixture, "samples"));
            var samples = Enumerable.Range(0, count).Select(i => (float)(((i * 73) % 1009 - 504) / 504.0 * .1)).ToArray();
            var actual = MNNSenseVoiceFeatures.Extract(samples, out int frames, CancellationToken.None);
            var expected = MNNModelData.Floats(MNNModelData.Get(fixture, "values"));
            Assert.AreEqual(MNNModelData.Integer(MNNModelData.Get(fixture, "frames")), frames);
            Assert.AreEqual(expected.Length, actual.Length);
            float error = actual.Zip(expected, (a, b) => Math.Abs(a - b)).Max();
            TestContext.WriteLine("FRONTEND_MAX_ABS_ERROR=" + error);
            Assert.That(error, Is.LessThan(0.002f));
        }

        [Test]
        public void ImageResizeMatchesPinnedPillowProcessor()
        {
            var fixture = Fixture("image");
            var rgb = Enumerable.Range(0, 37 * 53 * 3).Select(i => (byte)((i * 37 + 11) % 256)).ToArray();
            var image = new MNNRgbImage(rgb, 37, 53);
            var resized = MNNImageSampling.ResizeBilinear(image, 256, 256, CancellationToken.None);
            using (var sha = SHA256.Create())
                Assert.AreEqual((string)MNNModelData.Get(fixture, "sha256"),
                    string.Concat(sha.ComputeHash(resized.ToArray()).Select(b => b.ToString("x2"))));
            var actual = MNNMiniMindO.PrepareImage(image);
            byte[] pixels = resized.ToArray();
            for (int c = 0; c < 3; ++c)
                for (int i = 0; i < 256 * 256; ++i)
                    Assert.That(actual[c * 256 * 256 + i], Is.EqualTo((pixels[i * 3 + c] / 255f - .5f) / .5f).Within(1e-6));
        }
    }
}
