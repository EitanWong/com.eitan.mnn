using System;
using System.IO;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Explicit("Requires the verified local ModelScope TorryJi/bge-small-zh-v1.5-MNN files; no downloads.")]
    public class NativeBertEmbeddingIntegrationTests
    {
        private string Directory => Environment.GetEnvironmentVariable("MNN_TEST_NATIVE_BERT_ROOT")
            ?? throw new InvalidOperationException("Set MNN_TEST_NATIVE_BERT_ROOT to the verified Chinese BGE model directory.");

        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void OfficialMtokPipelineProducesUsefulChineseEmbeddings(MNNBackendType backend)
        {
            using (var model = MNNEmbedding.Load(Directory, backendType: backend))
            {
                var query = model.EncodeQuery("为这个句子生成表示以用于检索相关文章：中国的首都是哪里？");
                var relevant = model.Encode("北京是中华人民共和国的首都。");
                var unrelated = model.Encode("香蕉是一种黄色的热带水果。");
                Assert.AreEqual(512, model.Dimension);
                double good = Dot(query, relevant), bad = Dot(query, unrelated);
                Assert.Greater(good, bad + .05);
                Assert.That(Dot(query, query), Is.EqualTo(1).Within(1e-4));
                Assert.That(model.Encode("北京是中华人民共和国的首都。"), Is.EqualTo(relevant).Within(1e-5));
                TestContext.WriteLine("Chinese BGE mtok: backend=" + model.Backend + "; relevant=" + good + "; unrelated=" + bad);
            }
        }

        [Test]
        public void StudioRoutesAndExecutesTheInstalledChineseEncoder()
        {
            Assert.IsTrue(File.Exists(Path.Combine(Directory, "tokenizer.mtok")));
            var model = MNNStudioModel.Read(Directory);
            Assert.AreEqual(MNNStudioTask.Embedding, model.Task);
            using (var backend = MNNStudioNativeBackend.Load(model, 2, false))
            {
                var result = backend.Run(new MNNStudioRequest {Prompt = "中国的首都", Document = "北京是中国的首都。"});
                Assert.AreEqual(512, result.Vector.Length);
                Assert.Greater(result.Score, .5);
                TestContext.WriteLine("Chinese BGE Studio score=" + result.Score);
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
