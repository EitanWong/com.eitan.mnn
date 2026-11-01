using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class NativeMtokEmbeddingPlayerTests
    {
        [Test]
        public void ChineseBgeUsesOfficialMtokTokenizerAndRetrievesOnCpuAndAuto()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_PLAYER_NATIVE_BERT_ROOT");
            if (string.IsNullOrWhiteSpace(directory))
                Assert.Ignore("Provide --native-bert-root with the verified ModelScope TorryJi Chinese BGE model.");
            Assert.IsTrue(MNNPlatformSupport.IsSupported, MNNPlatformSupport.UnsupportedReason);
            foreach (var backend in new[] {MNNBackendType.CPU, MNNBackendType.Auto})
                using (var model = MNNEmbedding.Load(directory, backendType: backend))
                {
                    var query = model.EncodeQuery("为这个句子生成表示以用于检索相关文章：中国的首都是哪里？");
                    double good = Dot(query, model.Encode("北京是中华人民共和国的首都。"));
                    double bad = Dot(query, model.Encode("香蕉是一种黄色的热带水果。"));
                    Assert.AreEqual(512, model.Dimension);
                    Assert.Greater(good, bad + .05);
                    Assert.That(Dot(query, query), Is.EqualTo(1).Within(1e-4));
                    TestContext.WriteLine("Chinese BGE mtok: backend=" + model.Backend + "; relevant=" + good + "; unrelated=" + bad);
                }
        }

        private static double Dot(float[] first, float[] second)
        {
            Assert.AreEqual(512, first.Length);
            Assert.AreEqual(first.Length, second.Length);
            double sum = 0;
            for (int i = 0; i < first.Length; ++i)
            {
                Assert.IsFalse(float.IsNaN(first[i]) || float.IsInfinity(first[i]));
                sum += (double)first[i] * second[i];
            }
            return sum;
        }
    }
}
