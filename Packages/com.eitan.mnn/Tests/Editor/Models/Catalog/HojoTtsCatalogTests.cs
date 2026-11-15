using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class HojoTtsCatalogTests
    {
        [Test]
        public void InstallerAndCatalogUseTheSamePinnedRelease()
        {
            var model = MNNModelCatalog.CreateDefault().SearchModels("HojoAI").Single();
            Assert.AreEqual(model.revision, MNNModelRevisions.Resolve(model.provider, model.repositoryId));
            Assert.AreEqual(model.revision, MNNModelRevisions.Resolve(model.provider, model.repositoryId, "main"));
            Assert.Throws<System.ArgumentException>(() => MNNModelRevisions.Resolve(model.provider, model.repositoryId, "incompatible"));
            Assert.AreEqual("Hojo-TTS-Light-40M-V2-MNN", MNNModelCatalog.InstallFolderName(model));
        }
        [TestCase("Hojo")]
        [TestCase("Hojo-TTS-Light-40M-V2")]
        [TestCase("HojoAI")]
        public void BothCatalogsExposeTheSameDedicatedExport(string query)
        {
            var runtime = MNNModelCatalog.CreateDefault().SearchModels(query).Single();
            var editor = MNNModelRepository.GetDefaultRepository().SearchModels(query).Single();
            Assert.AreEqual("EitanWang/Hojo-TTS-Light-40M-V2-MNN", runtime.repositoryId);
            Assert.AreEqual(runtime.repositoryId, editor.repositoryId);
            Assert.AreEqual("SpeechSynthesis", runtime.category);
            Assert.AreEqual(runtime.revision, editor.revision);
            Assert.True(runtime.runtimeAvailable);
            Assert.True(MNNSpeechSynthesizer.SupportsRepository(runtime));
        }

        [Test]
        public void RegisteredPlansAreIndependentAndCatalogRefreshIsIdempotent()
        {
            var catalog = MNNModelCatalog.CreateDefault();
            catalog.IncludeRegisteredProfiles();
            foreach (var registration in MNNModelRegistry.All)
            {
                var model = catalog.Models.Single(item => item.provider == registration.Source && item.repositoryId == registration.Repository);
                var plan = registration.Files();
                Assert.AreEqual(plan.Sum(file => file.Size), model.fileSize);
                Assert.True(plan.All(file => file.Size > 0 && file.Sha256.Length == 64 && file.Revision == model.revision));
                Assert.AreEqual(plan.Length, plan.Select(file => file.Path).Distinct().Count());
                plan[0].Path = "caller-mutation";
                Assert.AreNotEqual("caller-mutation", registration.Files()[0].Path);
                Assert.AreEqual(registration.Revision, MNNModelRevisions.Resolve(registration.Source, registration.Repository));
            }
        }

        [Test]
        public void OnlineEvidenceRejectsAnExportMissingAnyRegisteredFile()
        {
            foreach (var registration in MNNModelRegistry.All)
            {
                var model = registration.CreateModel();
                var paths = registration.Files().Select(file => file.Path).ToArray();
                MNNModelCatalog.ApplyArtifactEvidence(model, paths);
                Assert.True(model.runtimeAvailable);
                foreach (string missing in paths)
                {
                    MNNModelCatalog.ApplyArtifactEvidence(model, paths.Where(path => path != missing).ToArray());
                    Assert.False(model.runtimeAvailable, missing);
                }
            }
        }
    }
}
