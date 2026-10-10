using NUnit.Framework;
using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Category("RealModel"), Category("RepresentativeModel")]
    public class MNNLlmIntegrationTests
    {
        private MNNLlm _model;
        private string _cache;
        [OneTimeSetUp]
        public void LoadDownloadedModel()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_DIRECTORY") ?? Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "Qwen3.5-0.8B-MNN");
            if (!File.Exists(Path.Combine(directory, "config.json")))
                Assert.Ignore("Set MNN_TEST_MODEL_DIRECTORY to the downloaded Qwen3.5 directory.");
            _cache = Path.Combine(Path.GetFullPath("TestArtifacts~/MNNValidation"), "LlmCache");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            _model = MNNLlm.Load(directory, cacheDirectory: _cache);
            TestContext.WriteLine("Qwen3.5 load seconds: " + timer.Elapsed.TotalSeconds);
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            _model?.Dispose();
            if (_cache != null && Directory.Exists(_cache))
                Directory.Delete(_cache, true);
        }

        [Test]
        public void Tokenizer_HandlesChineseAndEnglish()
        {
            Assert.Greater(_model.CountTokens("你好，世界！"), 0);
            Assert.Greater(_model.CountTokens("Hello world"), 0);
        }

        [Test]
        public void Generate_AnswersChineseQuestion()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var result = _model.Generate("中国的首都是哪里？只回答城市名称。", 32);
            TestContext.WriteLine("Prompt: 中国的首都是哪里？只回答城市名称。\nOutput: " + result.Text + "\nTokens: " + result.GeneratedTokens + "\nSeconds: " + timer.Elapsed.TotalSeconds);
            StringAssert.Contains("北京", result.Text);
            Assert.That(result.GeneratedTokens, Is.InRange(1, 32));
        }

        [Test]
        public void Generate_ResetProducesRepeatableIndependentAnswers()
        {
            const string prompt = "What is 1 + 1? Answer with just the number.";
            var first = _model.Generate(prompt, 16);
            var second = _model.Generate(prompt, 16);
            TestContext.WriteLine("Arithmetic output: " + first.Text);
            StringAssert.Contains("2", first.Text);
            Assert.AreEqual(first.Text, second.Text);
        }

        [TestCase(1), TestCase(4)]
        public void DefaultPrecision_AnswersArithmeticAcrossThreadCounts(int threads)
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_DIRECTORY") ?? Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "Qwen3.5-0.8B-MNN");
            using (var model = MNNLlm.Load(directory, threads, cacheDirectory: Path.Combine(_cache, "precision" + threads)))
            {
                for (int run = 0; run < 3; run++)
                {
                    var result = model.Generate("What is 1 + 1? Answer with just the number.", 16);
                    TestContext.WriteLine("threads=" + threads + ", run=" + run + ", output=" + result.Text);
                    StringAssert.Contains("2", result.Text);
                }
            }
        }

        [Test]
        public void Generate_ProcessesLongerChineseContext()
        {
            var prompt = new System.Text.StringBuilder();
            for (int i = 0; i < 64; i++)
                prompt.Append("记录：天空是蓝色的，草地是绿色的。\n");
            prompt.Append("中国的首都是哪里？只回答城市名称。");
            Assert.Greater(_model.CountTokens(prompt.ToString()), 256);
            var result = _model.Generate(prompt.ToString(), 32);
            TestContext.WriteLine("Long-context output: " + result.Text);
            StringAssert.Contains("北京", result.Text);
        }

        [Test]
        public void DisposeAndReload_ReleaseNativeHandle_RejectUseAfterDispose()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_DIRECTORY") ?? Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "Qwen3.5-0.8B-MNN");
            for (int i = 0; i < 2; i++)
            {
                var model = MNNLlm.Load(directory, cacheDirectory: Path.Combine(_cache, "reload" + i));
                Assert.Greater(model.CountTokens("你好"), 0);
                model.Dispose();
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.CountTokens("hello"));
                Assert.Throws<ObjectDisposedException>(() => model.Generate("hello"));
            }
        }

        [Test]
        public void Generate_EnforcesTokenBudget()
        {
            var result = _model.Generate("Count from 1 to 100.", 1);
            Assert.That(result.GeneratedTokens, Is.InRange(1, 1));
            Assert.IsTrue(result.ReachedTokenLimit);
        }

        [UnityTest]
        public IEnumerator GenerateAsync_ProducesAnswer()
        {
            var task = _model.GenerateAsync("What is 2 + 2? Answer with just the number.", 16);
            while (!task.IsCompleted)
                yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            StringAssert.Contains("4", task.Result.Text);
        }

        [Test]
        public void Arguments_AreValidated()
        {
            Assert.Throws<ArgumentNullException>(() => _model.CountTokens(null));
            Assert.Throws<ArgumentNullException>(() => _model.Generate(""));
            Assert.Throws<ArgumentOutOfRangeException>(() => _model.Generate("hello", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNLlm.Load(".", 0));
        }
    }
}
