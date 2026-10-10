using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class ModelTaskValidationTests
    {
        [TestCase("Llm"), TestCase("Embedding"), TestCase("Reranker")]
        public void MissingWeights_FailsWithoutNativeCrash(string task)
        {
            string path = Path.Combine(MultimodalTestData.Artifacts, "InvalidModel", task);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "config.json"), "{}");
            try
            {
                Assert.Throws<MNNException>(() =>
                {
                    using (Load(task, path, Path.Combine(path, "cache")))
                    {
                    }
                });
            }
            finally
            {
                Directory.Delete(path, true);
            }
        }

        [TestCase("Llm"), TestCase("Embedding"), TestCase("Reranker")]
        public void MissingDirectory_IsRejectedBeforeNativeLoad(string task) => Assert.Throws<FileNotFoundException>(() => Load(task, Path.Combine(MultimodalTestData.Artifacts, "missing-model"), null));
        private static IDisposable Load(string task, string directory, string cache)
        {
            if (task == "Llm")
                return MNNLlm.Load(directory, cacheDirectory: cache);
            if (task == "Embedding")
                return MNNEmbedding.Load(directory, cacheDirectory: cache);
            return MNNReranker.Load(directory, cacheDirectory: cache);
        }
    }
}
