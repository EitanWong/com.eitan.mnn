using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class EmbeddingModelTests
    {
        private MNNEmbedding _model;
        [OneTimeSetUp]
        public void Load() => _model = MNNEmbedding.Load(MultimodalTestData.Model("Qwen3-Embedding-0.6B-MNN"), cacheDirectory: MultimodalTestData.Cache("embedding"));
        [OneTimeTearDown]
        public void Dispose()
        {
            _model?.Dispose();
            MultimodalTestData.CleanCache("embedding");
        }

        [Test]
        public void Embedding_IsFiniteAndSemanticallyUseful()
        {
            var query = _model.Encode("Instruct: Given a web search query, retrieve relevant passages that answer the query\nQuery: What is the capital of France?");
            var relevant = _model.Encode("Paris is the capital city of France.");
            var unrelated = _model.Encode("Bananas are yellow tropical fruit.");
            Assert.AreEqual(1024, _model.Dimension);
            Assert.AreEqual(_model.Dimension, query.Length);
            foreach (float value in query)
                Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value));
            double good = Cosine(query, relevant), bad = Cosine(query, unrelated);
            TestContext.WriteLine("Embedding cosine: relevant=" + good + ", unrelated=" + bad);
            Assert.Greater(good, bad + 0.05);
            CollectionAssert.AreEqual(relevant, _model.Encode("Paris is the capital city of France."));
        }

        [UnityTest]
        public IEnumerator AsyncEmbedding_ReturnsOwnedVector()
        {
            var task = _model.EncodeAsync("Hello world");
            while (!task.IsCompleted)
                yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.AreEqual(_model.Dimension, task.Result.Length);
        }

        [Test]
        public void InvalidText_IsRejected() => Assert.Throws<ArgumentNullException>(() => _model.Encode(null));
        [Test]
        public void DisposeAndReload_RejectsUseAfterDispose()
        {
            using (var other = MNNEmbedding.Load(MultimodalTestData.Model("Qwen3-Embedding-0.6B-MNN"), cacheDirectory: MultimodalTestData.Cache("embedding-lifecycle")))
            {
                Assert.That(other.Encode("Lifecycle test").Length, Is.GreaterThan(0));
                other.Dispose();
                other.Dispose();
                Assert.Throws<ObjectDisposedException>(() => other.Encode("After dispose"));
            }

            MultimodalTestData.CleanCache("embedding-lifecycle");
        }

        private static double Cosine(float[] a, float[] b)
        {
            double dot = 0, aa = 0, bb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                aa += a[i] * a[i];
                bb += b[i] * b[i];
            }

            Assert.Greater(aa, 0);
            Assert.Greater(bb, 0);
            return dot / Math.Sqrt(aa * bb);
        }
    }
}
