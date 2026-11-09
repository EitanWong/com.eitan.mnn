using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("RealModel")]
    public sealed class PocketLlmIntegrationTests
    {
        private MNNLlm _model;

        [OneTimeSetUp]
        public void LoadOfficialExport()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_POCKET_MODEL_DIRECTORY") ??
                Path.Combine(Application.streamingAssetsPath, "MNN", "Models", MNNPocketModel.InstallationName);
            if (!File.Exists(Path.Combine(directory, "llm.mnn.weight")))
                Assert.Ignore("Install the official Pocket MNN export or set MNN_POCKET_MODEL_DIRECTORY.");
            _model = MNNLlm.Load(directory, cacheDirectory: Path.GetFullPath("TestArtifacts~/PocketLlmCache"));
        }

        [OneTimeTearDown]
        public void DisposeModel() => _model?.Dispose();

        [Test]
        public void MtokTokenizerHandlesChineseAndEnglish()
        {
            Assert.Greater(_model.CountTokens("你好，世界！"), 0);
            Assert.Greater(_model.CountTokens("Hello world!"), 0);
        }

        [Test]
        public void DownloaderReusesVerifiedDeploymentWithoutNetwork()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_POCKET_MODEL_DIRECTORY") ??
                Path.Combine(Application.streamingAssetsPath, "MNN", "Models", MNNPocketModel.InstallationName);
            using (var client = new System.Net.Http.HttpClient(new RejectNetwork()))
            using (var downloader = new MNNModelDownloader(client))
                Assert.AreEqual(MNNPocketModel.Bytes, downloader.DownloadRepositoryAsync(MNNPocketModel.Repository, directory).GetAwaiter().GetResult());
        }

        private sealed class RejectNetwork : System.Net.Http.HttpMessageHandler
        {
            protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(
                System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken token) =>
                throw new InvalidOperationException("Verified Pocket files must be reused without a network request: " + request.RequestUri);
        }

        [TestCase("中国的首都是哪里？只回答城市名称。", "北京")]
        [TestCase("What is the capital of France? Answer with one word.", "Paris")]
        [TestCase("2加2等于几？只回答数字。", "4")]
        public void OfficialTemplateGeneratesExpectedAnswer(string prompt, string expected)
        {
            var result = _model.Generate(prompt, 32);
            TestContext.WriteLine("Prompt: " + prompt + "\nOutput: " + result.Text + "\nTokens: " + result.GeneratedTokens);
            StringAssert.Contains(expected, result.Text);
            Assert.That(result.GeneratedTokens, Is.InRange(1, 32));
            StringAssert.DoesNotContain("<|im_end|>", result.Text);
        }

        [Test]
        public void ConversationProviderUsesTheOfficialChatTemplate()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_POCKET_MODEL_DIRECTORY") ??
                Path.Combine(Application.streamingAssetsPath, "MNN", "Models", MNNPocketModel.InstallationName);
            using (var provider = MNNConversationLocalProviders.Create(MNNConversationModelAdapter.Language, directory,
                new MNNConversationLocalModelOptions { Backend = MNNBackendType.CPU, Threads = 4, MaximumReplyTokens = 32,
                    DeterministicDecisions = true, CacheDirectory = Path.GetFullPath("TestArtifacts~/PocketConversationCache") }))
            {
                provider.PrepareAsync(default).GetAwaiter().GetResult();
                var text = new System.Text.StringBuilder();
                var messages = new[] { new MNNOpenAIMessage(MNNChatRole.User, "What is the capital of France? Answer with one word.") };
                ((IMNNConversationTextProvider)provider).GenerateAsync(messages, null, 16000, delta =>
                {
                    text.Append(delta);
                    return System.Threading.Tasks.Task.CompletedTask;
                }, default).GetAwaiter().GetResult();
                TestContext.WriteLine("Conversation output: " + text);
                StringAssert.Contains("Paris", text.ToString());
            }
        }
    }
}
