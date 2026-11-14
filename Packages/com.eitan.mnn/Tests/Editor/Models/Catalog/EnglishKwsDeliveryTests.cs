using System.IO;
using System.Linq;
using NUnit.Framework;
using System;
using MNN.Unity.Editor;

namespace MNN.Unity.Tests
{
    public sealed class EnglishKwsDeliveryTests
    {
        private const string Repository = "EitanWang/English-KWS-GigaSpeech-MNN";

        [Test]
        public void DeploymentPlanPinsEveryDependencyAndRejectsOtherRevisions()
        {
            var files = MNNEnglishKwsModel.Files();
            CollectionAssert.IsSubsetOf(new[] { "tokens.txt", "bpe.model", "LICENSE", "README.md", "NOTICE.md", "release-manifest.json" }, files.Select(file => file.Path));
            Assert.AreEqual(3, files.Count(file => file.Path.EndsWith(".mnn")));
            Assert.IsTrue(files.All(file => file.Size > 0 && file.Sha256.Length == 64 && file.Revision == MNNEnglishKwsModel.Revision));
            Assert.AreEqual(MNNEnglishKwsModel.Revision, MNNEnglishKwsModel.ResolveRevision("master"));
            Assert.Throws<ArgumentException>(() => MNNEnglishKwsModel.ResolveRevision("unverified"));
        }

        [Test]
        public void SelectedEnglishModelPackagesProfileAndVocabularyAndStagingRestoresThem()
        {
            string root = Path.GetFullPath("TestArtifacts~/EnglishKwsPackaging/" + Guid.NewGuid().ToString("N"));
            string models = Path.Combine(root, "Models");
            string name = MNNKeywordSpotterConfig.EnglishDirectoryName;
            Directory.CreateDirectory(Path.Combine(models, name));
            string[] dependencies = MNNEnglishKwsModel.Paths.Concat(new[] { MNNKeywordSpotterConfig.FileName }).ToArray();
            try
            {
                foreach (string path in dependencies) File.WriteAllText(Path.Combine(models, name, path), "packaged fixture " + path);
                File.WriteAllText(Path.Combine(models, name, "tokens.txt.meta"), "excluded metadata");
                Assert.IsEmpty(MNNModelBuildPlan.Create(models, MNNModelPackagingMode.None, null).IncludedFiles);
                var plan = MNNModelBuildPlan.Create(models, MNNModelPackagingMode.Selected, new[] { name });
                CollectionAssert.AreEquivalent(dependencies.Select(path => name + "/" + path), plan.IncludedFiles);
                var manifest = new MNNModelDeliveryManifest { files = plan.IncludedFiles.Select(path => new MNNModelDeliveryFile
                {
                    path = path, bundled = true, bytes = new FileInfo(Path.Combine(models, path)).Length,
                    sha256 = MNNModelDeliveryManifest.ComputeSha256(Path.Combine(models, path))
                }).ToArray() };
                var staging = new MNNModelBuildStaging(models, Path.Combine(root, "Staging"));
                staging.Begin(manifest);
                foreach (string path in plan.IncludedFiles) Assert.IsTrue(File.Exists(staging.SourceFile(path)));
                staging.Restore();
                foreach (string path in plan.IncludedFiles) Assert.IsTrue(File.Exists(Path.Combine(models, path)));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void FreshPlayerCatalogCanFindAndSelectEnglishWakeModel()
        {
            var model = MNNModelCatalog.CreateDefault().Models.SingleOrDefault(item => item.repositoryId == Repository);
            Assert.IsNotNull(model, "A fresh install must find English KWS without local model files or provider search indexing.");
            Assert.IsTrue(MNNModelCatalog.MatchesSearch(model, "English-KWS-GigaSpeech"));
            Assert.IsTrue(MNNModelCatalog.MatchesSearch(model, "唤醒词"));
            Assert.AreEqual(MNNModelCatalogCategory.KeywordSpotting.ToString(), model.category);
            Assert.IsTrue(model.runtimeAvailable);
            Assert.IsTrue(MNNConversationModelCatalog.TryGetAdapter(model, out var adapter));
            Assert.AreEqual(MNNConversationModelAdapter.KeywordSpotting, adapter);
        }

        [Test]
        public void DownloadUsesConfiguredEnglishInstallationDirectory()
        {
            var model = new MNNRemoteModel { provider = MNNModelSource.ModelScope, repositoryId = Repository,
                revision = "master", name = "English-KWS-GigaSpeech-MNN", isRepository = true, runtimeAvailable = true };
            Assert.AreEqual(MNNKeywordSpotterConfig.EnglishDirectoryName, MNNConversationModelCatalog.DefaultInstallationName(model));
        }
    }
}
