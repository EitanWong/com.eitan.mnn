using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class RerankerModelTests
    {
        private MNNReranker _model;
        [OneTimeSetUp]
        public void Load() => _model = MNNReranker.Load(MultimodalTestData.Model("Qwen3-Reranker-0.6B-MNN"), cacheDirectory: MultimodalTestData.Cache("reranker"));
        [OneTimeTearDown]
        public void Dispose()
        {
            _model?.Dispose();
            MultimodalTestData.CleanCache("reranker");
        }

        [Test]
        public void Score_RanksRelevantDocumentAboveUnrelatedDocument()
        {
            const string query = "What is the capital of France?";
            float good = _model.Score(query, "Paris is the capital of France.");
            float bad = _model.Score(query, "Bananas are yellow tropical fruit.");
            TestContext.WriteLine("Reranker: relevant=" + good + ", unrelated=" + bad);
            Assert.That(good, Is.InRange(0f, 1f));
            Assert.That(bad, Is.InRange(0f, 1f));
            Assert.Greater(good, bad + 0.1f);
            Assert.That(_model.Score(query, "Paris is the capital of France."), Is.EqualTo(good).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator AsyncScore_UsesWorkerThread()
        {
            var task = _model.ScoreAsync("What is the capital of France?", "Paris is the capital of France.");
            while (!task.IsCompleted)
                yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.That(task.Result, Is.InRange(0f, 1f));
        }

        [Test]
        public void InvalidInput_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => _model.Score(null, "document"));
            Assert.Throws<ArgumentNullException>(() => _model.Score("query", ""));
        }

        [Test]
        public void DisposeAndReload_RejectsUseAfterDispose()
        {
            using (var other = MNNReranker.Load(MultimodalTestData.Model("Qwen3-Reranker-0.6B-MNN"), cacheDirectory: MultimodalTestData.Cache("reranker-lifecycle")))
            {
                Assert.That(other.Score("Capital of France?", "Paris is the capital of France."), Is.InRange(0f, 1f));
                other.Dispose();
                other.Dispose();
                Assert.Throws<ObjectDisposedException>(() => other.Score("query", "document"));
            }

            MultimodalTestData.CleanCache("reranker-lifecycle");
        }
    }
}
