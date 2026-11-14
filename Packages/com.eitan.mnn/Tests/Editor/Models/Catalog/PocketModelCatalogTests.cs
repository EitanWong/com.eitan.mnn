using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class PocketModelCatalogTests
    {
        [Test]
        public void DefaultCatalogIncludesTheFamilyAndOnlyTheMnnExportIsRunnable()
        {
            var models = MNNModelCatalog.CreateDefault().SearchModels("MNN-Pocket").ToArray();
            Assert.AreEqual(6, models.Length);
            var runnable = models.Single(model => model.runtimeAvailable);
            Assert.AreEqual("MNN/MNN-Pocket-0.3B-MNN", runnable.repositoryId);
            Assert.AreEqual("LargeLanguageModel", runnable.category);
            Assert.AreEqual("MNN-Pocket", runnable.family);
            Assert.AreEqual("92aaad61ae9ecc5a560402a70d1583c08f1c6442", runnable.revision);
            Assert.Greater(runnable.fileSize, 170000000);
            CollectionAssert.AreEquivalent(models.Select(model => model.repositoryId),
                MNN.Unity.Editor.MNNModelRepository.GetDefaultRepository().SearchModels("MNN-Pocket").Select(model => model.repositoryId));
        }

        [Test]
        public void ConversationDefaultResolvesTheInstructionModelToOfficialMnnAssets()
        {
            var entry = new MNNConversationModelCatalog().Entries.Single(model => model.modelId == "MNN/MNN-Pocket-0.3B");
            Assert.False(entry.catalogOnly);
            Assert.AreEqual(MNNConversationModelAdapter.Language, entry.adapter);
            Assert.AreEqual("MNN/MNN-Pocket-0.3B-MNN", entry.repository);
            Assert.AreEqual(entry.revision, entry.remote.revision);
            Assert.AreEqual(MNNPocketModel.InstallationName, MNNConversationModelCatalog.DefaultInstallationName(entry.remote));
            string root = System.IO.Path.GetFullPath("TestArtifacts~/PocketModels");
            string directory = MNNModelInstallation.DirectoryFor(root, entry.remote, MNNConversationModelCatalog.DefaultInstallationName(entry.remote));
            Assert.AreEqual(root, System.IO.Path.GetDirectoryName(directory), "Studio discovers one model folder per installation.");
        }

        [TestCase("MNN/MNN-Pocket-0.3B")]
        [TestCase("MNN/MNN-Pocket-0.3B-Base")]
        [TestCase("MNN/MNN-Pocket-0.3B-SFT")]
        [TestCase("MNN/MNN-Pocket-0.3B-Dec")]
        [TestCase("MNN/MNN-Pocket-0.3B-GGUF")]
        public void SourceVariantsStayCatalogOnly(string repository)
        {
            var model = MNNModelCatalog.CreateDefault().Models.Single(item => item.repositoryId == repository);
            Assert.False(MNNConversationModelCatalog.TryGetAdapter(model, out _));
            Assert.IsNotEmpty(model.supportSummary);
        }

        [Test]
        public void DownloadPlanIsPinnedCompleteAndOwnedByEachCaller()
        {
            var files = MNNPocketModel.Files();
            Assert.AreEqual(MNNPocketModel.Bytes, files.Sum(file => file.Size));
            CollectionAssert.AreEquivalent(new[] { "config.json", "llm_config.json", "llm.mnn", "llm.mnn.weight", "tokenizer.mtok", "LICENSE.txt" }, files.Select(file => file.Path));
            Assert.True(files.All(file => file.Revision == MNNPocketModel.Revision && file.Sha256.Length == 64));
            files[0].Sha256 = "caller changed";
            Assert.AreEqual(64, MNNPocketModel.Files()[0].Sha256.Length);
        }

        [Test]
        public void DiscoveryKeepsThePinnedDeploymentIdentityAndConversationAlias()
        {
            var discovered = new MNNRemoteModel { provider = MNNModelSource.ModelScope, repositoryId = MNNPocketModel.Repository,
                isRepository = true, name = "MNN-Pocket-0.3B-MNN", revision = "master" };
            MNNModelCatalog.ApplyArtifactEvidence(discovered, MNNPocketModel.Files().Select(file => file.Path).ToArray());
            var catalog = new MNNConversationModelCatalog();
            catalog.ApplyOnlineModels(new[] { discovered }, System.Array.Empty<string>());
            var matches = catalog.Entries.Where(entry => entry.repository == MNNPocketModel.Repository).ToArray();
            Assert.AreEqual(1, matches.Length);
            Assert.AreEqual(MNNPocketModel.ModelId, matches[0].modelId);
            Assert.AreEqual(MNNPocketModel.Revision, matches[0].revision);
            Assert.True(matches[0].online);
        }

        [Test]
        public void DownloaderRejectsUnqualifiedRevisionBeforeWritingFiles()
        {
            string directory = System.IO.Path.GetFullPath("TestArtifacts~/PocketRejectedRevision/" + System.Guid.NewGuid().ToString("N"));
            using (var downloader = new MNNModelDownloader())
                Assert.Throws<System.ArgumentException>(() => downloader.DownloadRepositoryAsync(MNNPocketModel.Repository,
                    directory, revision: "unknown-revision").GetAwaiter().GetResult());
            Assert.False(System.IO.Directory.Exists(directory));
        }

        [Test, Category("RealModel")]
        public void StudioDiscoversTheInstalledPocketModelFromTheModelsRoot()
        {
            string directory = System.Environment.GetEnvironmentVariable("MNN_POCKET_MODEL_DIRECTORY");
            if (string.IsNullOrEmpty(directory)) Assert.Ignore("Set MNN_POCKET_MODEL_DIRECTORY to the downloaded Pocket export.");
            var models = MNN.Unity.Editor.MNNStudioModel.Discover(new[] { System.IO.Path.GetDirectoryName(directory) });
            var pocket = models.Single(model => model.Directory == System.IO.Path.GetFullPath(directory));
            Assert.AreEqual(MNN.Unity.Editor.MNNStudioTask.Chat, pocket.Task);
        }
    }
}
