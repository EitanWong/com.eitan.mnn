using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Explicit("Downloads the published MiniMind-o deployment and runs real native inference.")]
    public sealed class MiniMindOModelScopeTests
    {
        [Test]
        public void PublishedModelDownloadsThroughRuntimeInstallerAndGeneratesSpeech()
        {
            var model = MNNModelCatalog.CreateDefault().SearchModels("MiniMind-o").Single();
            string directory = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "MNN/Models/MiniMindO-113M-ModelScope-Verified"));
            using (var downloader = new MNNModelDownloader())
                MNNModelInstallation.EnsureAsync(directory, model, downloader: downloader).GetAwaiter().GetResult();
            Assert.True(MNNMiniMindO.IsComplete(directory));
            Assert.True(MNNModelInstallation.IsInstalled(directory, model));
            // Independent byte comparison with the locally validated export, including all external weights.
            string reference = Path.Combine(Application.streamingAssetsPath, "MNN/Models", MNNMiniMindO.InstallationName);
            foreach (string name in model.mnnFiles.Concat(new[] { "tokenizer.json", MNNMiniMindO.ConfigFileName }))
            {
                using (var sha = SHA256.Create())
                using (var expected = File.OpenRead(Path.Combine(reference, name)))
                using (var downloaded = File.OpenRead(Path.Combine(directory, name)))
                    CollectionAssert.AreEqual(sha.ComputeHash(expected), sha.ComputeHash(downloaded), name);
            }
            var entry = new MNNConversationModelCatalog().Entries.Single(item => item.repository == model.repositoryId);
            using (var provider = MNNConversationLocalProviders.Create(entry.adapter, directory,
                new MNNConversationLocalModelOptions { MaximumReplyTokens = 128 }))
            {
                provider.PrepareAsync(CancellationToken.None).GetAwaiter().GetResult();
                var reply = ((IMNNConversationSpeechModelProvider)provider).GenerateSpeechAsync(
                    new[] { new MNNOpenAIMessage(MNNChatRole.User, "你好，请用一句话介绍你自己。") }, null,
                    CancellationToken.None).GetAwaiter().GetResult();
                Assert.IsNotEmpty(reply.Text);
                Assert.Greater(reply.Waveform.Length, 0);
                TestContext.WriteLine("DOWNLOADED_DIRECTORY=" + directory + "\nREVISION=" + model.revision +
                    "\nVERIFIED_PAYLOAD_FILES=14\nTEXT=" + reply.Text + "\nPCM_SAMPLES=" + reply.Waveform.Length);
            }
        }
    }
}
