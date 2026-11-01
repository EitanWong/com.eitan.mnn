using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class RetrievalPlayerTests
    {
        [UnityTest, Category("MobileRealRetrieval")]
        public IEnumerator Embedding_CausalRetrievalAndIndependentRequests()
        {
            string root = RequireModel("Qwen3-Embedding-0.6B-MNN");
            var task = Task.Run(() =>
            {
                using (var model = MNNEmbedding.Load(root))
                {
                    const string document = "Paris is the capital city of France.";
                    var query = model.Encode("Instruct: Given a web search query, retrieve relevant passages that answer the query\nQuery: What is the capital of France?");
                    var good = model.Encode(document);
                    var bad = model.Encode("Bananas are yellow tropical fruit.");
                    double relevant = Cosine(query, good), unrelated = Cosine(query, bad);
                    TestContext.WriteLine($"Embedding: relevant={relevant}; unrelated={unrelated}; backend={model.Backend}");
                    Assert.AreEqual(1024, query.Length);
                    Assert.Greater(relevant, unrelated + 0.1);
                    model.Encode(new string('x', 1024));
                    CollectionAssert.AreEqual(good, model.Encode(document));
                    using (var independent = MNNEmbedding.Load(root))
                    {
                        independent.Encode("A separate request with a different length.");
                        CollectionAssert.AreEqual(good, independent.Encode(document));
                    }
                }
            });
            while (!task.IsCompleted) yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
        }

        [UnityTest, Category("MobileRealRetrieval")]
        public IEnumerator Reranker_RelevantDocumentScoresHigher()
        {
            string root = RequireModel("Qwen3-Reranker-0.6B-MNN");
            var task = Task.Run(() =>
            {
                using (var model = MNNReranker.Load(root))
                {
                    float good = model.Score("What is the capital of France?", "Paris is the capital of France.");
                    float bad = model.Score("What is the capital of France?", "Bananas are yellow tropical fruit.");
                    TestContext.WriteLine($"Reranker: relevant={good}; unrelated={bad}; backend={model.Backend}");
                    Assert.That(good, Is.InRange(0f, 1f));
                    Assert.That(bad, Is.InRange(0f, 1f));
                    Assert.Greater(good, bad + 0.1f);
                }
            });
            while (!task.IsCompleted) yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
        }

        private static string RequireModel(string name)
        {
#if !((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
            Assert.Ignore("Requires an ARM64 IL2CPP mobile Player.");
#endif
            string directory = Path.Combine(PlayerRepresentativeTests.MobileModelRoot(), "models", name);
            if (!File.Exists(Path.Combine(directory, "config.json")))
                Assert.Ignore("Stage the existing retrieval model in mobile-llm/models: " + name);
            return directory;
        }

        private static double Cosine(float[] a, float[] b)
        {
            Assert.AreEqual(a.Length, b.Length);
            double dot = 0, aa = 0, bb = 0;
            for (int i = 0; i < a.Length; ++i)
            {
                Assert.IsFalse(float.IsNaN(a[i]) || float.IsInfinity(a[i]));
                Assert.IsFalse(float.IsNaN(b[i]) || float.IsInfinity(b[i]));
                dot += a[i] * b[i]; aa += a[i] * a[i]; bb += b[i] * b[i];
            }
            Assert.Greater(aa * bb, 0);
            return dot / Math.Sqrt(aa * bb);
        }
    }
}
