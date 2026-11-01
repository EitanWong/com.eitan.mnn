using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("BertEmbeddingPlayer")]
    public class BertEmbeddingPlayerTests
    {
        [TestCase("darkmaniac7/bge-small-en-MNN")]
        [TestCase("developerabu/bge-small-en-v1.5-mnn")]
        [TestCase("darkmaniac7/minilm-l6-v2-mnn")]
        public void BertEncoderPerformsRealRetrievalOnCpuAndAuto(string repository)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_BERT_ROOT");
            if (string.IsNullOrWhiteSpace(root))
                Assert.Ignore("Provide the platform runner's --bert-root for the three pinned embedding models.");
            Assert.IsTrue(MNNPlatformSupport.IsSupported, MNNPlatformSupport.UnsupportedReason);
            string directory = Path.Combine(root, repository);
            Assert.IsTrue(File.Exists(Path.Combine(directory, MNNBertEmbeddingConfig.FileName)), "Prepare the pinned BERT profiles before building the Player.");
            foreach (var backend in new[] {MNNBackendType.CPU, MNNBackendType.Auto})
                using (var model = MNNEmbedding.Load(directory, backendType: backend))
                {
                    var query = model.EncodeQuery("What is the capital of France?");
                    var relevant = model.Encode("Paris is the capital of France.");
                    var unrelated = model.Encode("Bananas are yellow tropical fruit.");
                    double good = Dot(query, relevant), bad = Dot(query, unrelated);
                    Assert.AreEqual(384, model.Dimension);
                    Assert.Greater(good, bad + .05);
                    Assert.That(Dot(query, query), Is.EqualTo(1).Within(1e-5));
                    TestContext.WriteLine(repository + ": backend=" + model.Backend + "; relevant=" + good + "; unrelated=" + bad);
                }
        }

        private static double Dot(float[] first, float[] second)
        {
            Assert.AreEqual(first.Length, second.Length);
            double result = 0;
            for (int i = 0; i < first.Length; ++i)
            {
                Assert.IsFalse(float.IsNaN(first[i]) || float.IsInfinity(first[i]));
                result += (double)first[i] * second[i];
            }
            return result;
        }
    }
}
