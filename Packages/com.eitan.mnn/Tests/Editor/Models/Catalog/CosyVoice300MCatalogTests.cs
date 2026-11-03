using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class CosyVoice300MCatalogTests
    {
        private const string Repository = "EitanWang/CosyVoice-300M-MNN";

        [Test]
        public void PublishedDetectorsExposeTheirExistingRuntimeProtocols()
        {
            var models = MNNModelCatalog.CreateDefault().Models;
            var silero = models.Single(model => model.repositoryId == "EitanWang/Silero-v5.1-MNN");
            Assert.True(silero.runtimeAvailable, "The package implements this official Silero export.");
            CollectionAssert.Contains(silero.tasks, "voice-activity-detection");
            CollectionAssert.Contains(silero.mnnFiles, "silero_v5_16k_fp32.mnn");
            Assert.True(models.Single(model => model.name == "MarbleNet-VAD-MNN").runtimeAvailable);
            Assert.True(models.Single(model => model.name == "FSMN-VAD-16K-MNN").runtimeAvailable);
            Assert.True(models.Single(model => model.name == "NAMO-Turn-Detector-v1-Multilingual-MNN").requiresTranscript);
            Assert.True(models.Single(model => model.name == "TurnSense-SmolLM2-135M-MNN").requiresTranscript);
            Assert.False(models.Single(model => model.name == "SmartTurn-v3.2-FP32-MNN").requiresTranscript);
        }

        [Test]
        public void PublishedSpeechModelsAreAvailableWithoutHubIndexAndMergeWithoutDuplicates()
        {
            var catalog = MNNModelRepository.GetDefaultRepository();
            catalog.IncludePublishedProfiles();
            var models = catalog.models.Where(model => model.repositoryId != null && model.repositoryId.StartsWith("EitanWang/") &&
                model.repositoryId != Repository).ToArray();
            Assert.AreEqual(49, models.Length);
            Assert.AreEqual(49, models.Select(model => model.repositoryId).Distinct().Count());
            Assert.AreEqual("SpeechEnhancement", models.Single(model => model.name == "GTCRN-MNN").category);
            Assert.AreEqual("SpeakerRecognition", models.Single(model => model.name == "ECAPA-TDNN-MNN").category);
            Assert.AreEqual("TurnDetection", models.Single(model => model.name == "TurnSense-SmolLM2-135M-MNN").category);
            Assert.IsFalse(models.Any(model => model.name == "LiveKit-Turn-Detector-MNN"));
            Assert.IsTrue(models.All(model => model.isRepository));
        }

        [Test]
        public void PublishedSpeechProfileMergesExistingHubEntryIgnoringIdCase()
        {
            var catalog = new MNNModelRepository();
            catalog.models.Add(new MNNModelInfo {provider = MNNModelProvider.ModelScope,
                repositoryId = "eitanwang/gtcrn-mnn", name = "GTCRN-MNN", downloadCount = 123});
            catalog.IncludePublishedProfiles();
            var model = catalog.SearchModels("gtcrn-mnn").Single();
            Assert.AreEqual(123, model.downloadCount);
            Assert.AreEqual("SpeechEnhancement", model.category);
            CollectionAssert.Contains(model.mnnFiles, "gtcrn_fp32.mnn");
        }

        [Test]
        public void PublishedFinalProfileIsSearchableAndHasCompletePinnedPlan()
        {
            var catalog = MNNModelRepository.GetDefaultRepository();
            var matches = catalog.SearchModels("CosyVoice-300M-MNN");
            Assert.AreEqual(1, matches.Count);
            var model = matches.Single();
            Assert.AreEqual(Repository, model.repositoryId);
            Assert.AreEqual(MNNModelProvider.ModelScope, model.provider);
            Assert.AreEqual("SpeechSynthesis", model.category);
            Assert.IsTrue(model.runtimeAvailable);
            Assert.IsTrue(MNNStudioTasks.Get((int)MNNStudioTask.SpeechSynthesis).MatchesCatalog(model));
            Assert.AreEqual(40, model.revision.Length);
            var plan = MNNVerifiedTtsModel.Files(Repository);
            Assert.AreEqual(16, plan.Count(file => file.Path.EndsWith(".mnn") || file.Path.EndsWith(".mnn.weight")));
            Assert.AreEqual(7, plan.Count(file => file.Path.StartsWith("common/")));
            Assert.IsTrue(plan.Any(file => file.Path == "mnn_cosyvoice300m.json"));
            Assert.IsTrue(plan.Any(file => file.Path == "LICENSE"));
            Assert.IsTrue(plan.All(file => file.Hash.Length == 64 && file.Revision == model.revision && file.Bytes > 0));
            Assert.IsFalse(plan.Any(file => file.Path.EndsWith(".meta") || file.Path.Contains("experimental")));
            Assert.AreEqual(plan.Sum(file => file.Bytes), model.fileSize);
        }
    }
}
