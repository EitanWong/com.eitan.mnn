using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Explicit("Requires the three pinned local BERT model profiles. No automatic downloads.")]
    public class BertEmbeddingIntegrationTests
    {
        private string _root;
        [OneTimeSetUp]
        public void Prepare()
        {
            _root = Environment.GetEnvironmentVariable("MNN_TEST_BERT_ROOT");
            Assert.IsNotNull(_root, "Set MNN_TEST_BERT_ROOT to the existing model root.");
            foreach (string repository in MNNBertEmbeddingModel.Repositories)
                MNNBertEmbeddingModel.Prepare(Path.Combine(_root, repository), repository);
        }

        [TestCase(MNNBertEmbeddingModel.BgeRepository)]
        [TestCase(MNNBertEmbeddingModel.AlternateBgeRepository)]
        [TestCase(MNNBertEmbeddingModel.MiniLmRepository)]
        public void CpuEmbeddingsPreserveRetrievalQualityOwnedVectorsAndDynamicLength(string repository)
        {
            using (var model = MNNEmbedding.Load(Path.Combine(_root, repository), backendType: MNNBackendType.CPU))
            {
                Assert.AreEqual(384, model.Dimension);
                float[] query = model.EncodeQuery("What is the capital of France?");
                float[] relevant = model.Encode("Paris is the capital city of France.");
                float[] copy = (float[])relevant.Clone();
                float[] unrelated = model.Encode("Bananas grow on tropical plants and are yellow when ripe.");
                double good = Dot(query, relevant), bad = Dot(query, unrelated);
                TestContext.WriteLine(repository + ": relevant=" + good + ", unrelated=" + bad + ", backend=" + model.Backend);
                Assert.Greater(good, bad + .05);
                Assert.That(Dot(query, query), Is.EqualTo(1).Within(1e-5));
                CollectionAssert.AreEqual(copy, relevant, "Results must not alias the next native output.");
                Assert.That(model.Encode("Paris is the capital city of France."), Is.EqualTo(copy).Within(1e-5));
                Assert.AreEqual(384, model.Encode(string.Join(" ", Enumerable.Repeat("hello", 600))).Length);
                Assert.Throws<ArgumentNullException>(() => model.Encode(" "));
                model.Dispose();
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Encode("after disposal"));
            }
            using (var reloaded = MNNEmbedding.Load(Path.Combine(_root, repository), backendType: MNNBackendType.CPU))
                Assert.AreEqual(384, reloaded.Encode("Reloaded encoder.").Length);
        }

        [TestCase(MNNBertEmbeddingModel.BgeRepository)]
        [TestCase(MNNBertEmbeddingModel.MiniLmRepository)]
        [TestCase(MNNBertEmbeddingModel.AlternateBgeRepository)]
        public void AutoBackendPreservesRetrievalQuality(string repository)
        {
            using (var model = MNNEmbedding.Load(Path.Combine(_root, repository)))
            {
                var query = model.EncodeQuery("How do I reset a forgotten password?");
                double good = Dot(query, model.Encode("Use the password reset link to recover access to your account."));
                double bad = Dot(query, model.Encode("The spacecraft orbits the planet every ninety minutes."));
                TestContext.WriteLine(repository + ": relevant=" + good + ", unrelated=" + bad + ", backend=" + model.Backend);
                Assert.Greater(good, bad + .05);
            }
        }

        [Test]
        public void IndependentBgeExportsAgreeOnEmbeddingDirection()
        {
            using (var first = MNNEmbedding.Load(Path.Combine(_root, MNNBertEmbeddingModel.BgeRepository), backendType: MNNBackendType.CPU))
            using (var second = MNNEmbedding.Load(Path.Combine(_root, MNNBertEmbeddingModel.AlternateBgeRepository), backendType: MNNBackendType.CPU))
            {
                double similarity = Dot(first.Encode("A child is playing with a dog in the park."), second.Encode("A child is playing with a dog in the park."));
                TestContext.WriteLine("Independent BGE export cosine=" + similarity);
                Assert.Greater(similarity, .99);
            }
        }

        [UnityTest]
        public IEnumerator AsyncEncodingCancellationAndStudioUseTheSameRealPipeline()
        {
            string directory = Path.Combine(_root, MNNBertEmbeddingModel.MiniLmRepository);
            using (var model = MNNBertEmbedding.Load(directory, backendType: MNNBackendType.CPU))
            {
                var task = model.EncodeAsync("An asynchronous sentence embedding.");
                while (!task.IsCompleted) yield return null;
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.AreEqual(384, task.Result.Length);
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    Assert.Throws<OperationCanceledException>(() => model.Encode("cancelled", cancellation.Token));
                }
            }
            var studioModel = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.Embedding, studioModel.Task);
            using (var backend = MNNStudioNativeBackend.Load(studioModel, 2, false))
            {
                var result = backend.Run(new MNNStudioRequest {Prompt = "What is the capital of France?", Document = "Paris is the capital of France."});
                Assert.AreEqual(384, result.Vector.Length);
                Assert.Greater(result.Score, .5);
                TestContext.WriteLine("Actual Studio MiniLM score=" + result.Score);
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
