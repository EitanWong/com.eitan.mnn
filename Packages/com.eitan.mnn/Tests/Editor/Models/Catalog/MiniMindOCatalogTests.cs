using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("MiniMindO"), Explicit("Requires the complete local MiniMind-o installation; never downloads weights.")]
    public sealed class MiniMindOCatalogTests
    {
        [TestCase("MiniMind-o")]
        [TestCase("MiniMindO")]
        [TestCase("minimind")]
        public void BothCatalogsFindTheInstalledOmniExport(string query)
        {
            var runtime = MNNModelCatalog.CreateDefault().SearchModels(query).Single();
            var editor = MNNModelRepository.GetDefaultRepository().SearchModels(query).Single();
            Assert.AreEqual("OmniModel", runtime.category);
            Assert.AreEqual(runtime.id, editor.id);
            Assert.True(runtime.runtimeAvailable);
            Assert.True(editor.isInstalled);
            Assert.AreEqual(Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "MNN/Models/MiniMindO-113M-MNN")), Path.GetFullPath(editor.localPath));
            Assert.True(MNNMiniMindO.IsComplete(editor.localPath));
        }

        [Test]
        public void ConversationCatalogHasAMiniMindExportProtocolAndResolvesItsInstallation()
        {
            var entry = new MNNConversationModelCatalog().Entries.Single(item => MNNModelCatalog.MatchesSearch(item.remote, "MiniMind-o"));
            Assert.False(entry.catalogOnly);
            Assert.AreEqual("MiniMindO", entry.adapter.ToString());
            string root = Path.Combine(Application.streamingAssetsPath, "MNN/Models");
            string expected = Path.Combine(root, "MiniMindO-113M-MNN");
            Assert.True(MNNConversationModelFiles.IsExistingComplete(expected, entry.remote));
            Assert.AreEqual(Path.GetFullPath(expected), Path.GetFullPath(MNNConversationModelFiles.FindExistingDirectory(root, entry.remote, expected)));
            Assert.False(MNNConversationModelFiles.IsExistingComplete(Path.Combine(root, "MiniMindO-113M-Core-Experimental"), entry.remote));
            Assert.False(MNNConversationModelFiles.IsExistingComplete(Path.Combine(root, "MiniMindO-113M-Reference"), entry.remote));
        }
        [Test]
        public void DiscoveredRuntimeAdapterActuallyGeneratesSpeech()
        {
            var entry = new MNNConversationModelCatalog().Entries.Single(item => MNNModelCatalog.MatchesSearch(item.remote, "MiniMind-o"));
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN/Models/MiniMindO-113M-MNN");
            using (var provider = MNNConversationLocalProviders.Create(entry.adapter, directory,
                new MNNConversationLocalModelOptions { MaximumReplyTokens = 128 }))
            {
                provider.PrepareAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                string streamed = "";
                var result = ((IMNNConversationSpeechModelProvider)provider).GenerateSpeechAsync(
                    new[] { new MNNOpenAIMessage(MNNChatRole.User, "你好，请用一句话介绍你自己。") },
                    delta => { streamed += delta; return System.Threading.Tasks.Task.CompletedTask; },
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                Assert.IsNotEmpty(result.Text);
                Assert.AreEqual(result.Text, streamed);
                Assert.Greater(result.Waveform.Length, 0);
                Assert.AreEqual(24000, result.SampleRate);
                TestContext.WriteLine("RUNTIME_TEXT=" + result.Text + "\nPCM_SAMPLES=" + result.Waveform.Length);
            }
        }
        [Test]
        public void OnlineRefreshPreservesTheDedicatedExportAndRejectsMissingGraphs()
        {
            var remote = new MNNRemoteModel { provider = MNNModelSource.ModelScope,
                repositoryId = MNNMiniMindO.Repository, isRepository = true, revision = "master" };
            var profile = MNNModelCatalog.CreateDefault().SearchModels("MiniMind-o").Single();
            var paths = profile.mnnFiles.Concat(new[] { MNNMiniMindO.ConfigFileName, "tokenizer.json" }).ToArray();
            MNNModelCatalog.ApplyArtifactEvidence(remote, paths);
            Assert.True(remote.runtimeAvailable);
            var catalog = new MNNConversationModelCatalog();
            catalog.ApplyOnlineModels(new[] { remote }, System.Array.Empty<string>());
            var entry = catalog.Entries.Single(item => item.repository == MNNMiniMindO.Repository);
            Assert.False(entry.catalogOnly);
            Assert.AreEqual("MiniMindO", entry.adapter.ToString());
            MNNModelCatalog.ApplyArtifactEvidence(remote, paths.Where(path => path != "mimi_decoder.mnn.weight").ToArray());
            Assert.False(remote.runtimeAvailable);
        }
    }
}
