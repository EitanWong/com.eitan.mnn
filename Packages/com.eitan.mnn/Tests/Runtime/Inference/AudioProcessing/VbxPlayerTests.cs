using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("NativeInference"), Category("SpeechModels")]
    public sealed class VbxPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void OriginalCompletePcmRawVectorsMatchInBuiltPlayer(MNNBackendType backend)
        {
            Assert.False(Application.isEditor, "Run in a built Player."); MNNPlatformSupport.RequireSupported();
            string root = Environment.GetEnvironmentVariable("MNN_TEST_VBX"); Assert.That(root, Is.Not.Null.And.Not.Empty);
            string reference = Path.Combine(root, "source-v341/reference");
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "VBx-ResNet101-16K-MNN");
            var cases = new[] { "minimum", "short", "real", "quiet", "gain2", "silence", "constant", "hop_before", "hop_at", "hop_after",
                "cmn_before", "cmn_at", "cmn_after", "segment_before", "segment_at", "segment_after", "jump_before", "jump_at", "jump_after", "maximum" };
            using (var model = MNNVbxEmbedding.Load(directory, backendType: backend))
            {
                Assert.AreEqual("VBx-ResNet101-16K-MNN", model.ModelName);
                foreach (string name in cases.Concat(new[] { cases[0] }))
                {
                    var pcm = Read(Path.Combine(reference, name + "-input.f32")); var expected = Read(Path.Combine(reference, name + "-embedding.f32"));
                    var actual = model.Encode(pcm); Check(name, expected, actual); CollectionAssert.AreEqual(actual, model.Encode(pcm), name + " repeat");
                    Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.Metal : backend, model.Backend);
                }
                Assert.Throws<OperationCanceledException>(() => model.Encode(new float[1600], cancellationToken: new CancellationToken(true)));
                model.Dispose(); Assert.Throws<ObjectDisposedException>(() => model.Encode(new float[1600]));
            }
        }
        private static float[] Read(string path)
        {
            var bytes = File.ReadAllBytes(path); var result = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length); return result;
        }
        private static void Check(string name, float[] expected, float[] actual)
        {
            Assert.AreEqual(256, actual.Length); double error = 0, norm = 0, actualNorm = 0, product = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]));
                double e = expected[i], a = actual[i]; error += (a - e) * (a - e); norm += e * e; actualNorm += a * a; product += e * a;
            }
            double nrmse = Math.Sqrt(error / norm), cosine = product / Math.Sqrt(norm * actualNorm);
            TestContext.WriteLine(name + ";nrmse=" + nrmse.ToString("R", CultureInfo.InvariantCulture) + ";cosine=" + cosine.ToString("R", CultureInfo.InvariantCulture));
            Assert.Less(nrmse, .001); Assert.GreaterOrEqual(cosine, .99999);
        }
    }
}
