using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class TurnModelCatalogTests
    {
        [TestCase("SmartTurn-v3.2-FP32-MNN", false)]
        [TestCase("SmartTurn-v3.2-FP16-MNN", false)]
        [TestCase("Bairong-TurnSense-1.0-MNN", false)]
        [TestCase("Bairong-TurnSense-1.1-MNN", false)]
        [TestCase("Easy-Turn-MNN", false)]
        [TestCase("TEN-Turn-Detection-7B-MNN", true)]
        [TestCase("DualTurn-Endpointing-MNN", false)]
        [TestCase("LiveKit-Turn-Detector-Multilingual-MNN", true)]
        [TestCase("LiveKit-Turn-Detector-MNN", true)]
        [TestCase("NAMO-Turn-Detector-v1-Multilingual-MNN", true)]
        [TestCase("VoxTurn-v1-MNN", false)]
        [TestCase("CueLight-Nano-MNN", false)]
        [TestCase("CueLight-Nano-SSM-MNN", false)]
        [TestCase("TurnSense-SmolLM2-135M-MNN", true)]
        public void PublishedTurnsSharePinnedCatalogInstallationAndRuntimeAdapter(string name, bool text)
        {
            var registration = MNNModelRegistry.Find(MNNModelSource.ModelScope, "EitanWang/" + name);
            Assert.IsNotNull(registration, "A published model must use the shared deployment registration.");
            var runtime = MNNModelCatalog.CreateDefault().SearchModels(name).Single(m => m.repositoryId == registration.Repository);
            var editor = MNNModelRepository.GetDefaultRepository().SearchModels(name).Single(m => m.repositoryId == registration.Repository);
            Assert.AreEqual(registration.Revision, runtime.revision); Assert.AreEqual(runtime.revision, editor.revision);
            Assert.AreEqual("TurnDetection", runtime.category); Assert.AreEqual(text, runtime.requiresTranscript);
            Assert.True(MNNConversationModelCatalog.TryGetAdapter(runtime, out var adapter));
            Assert.AreEqual(text ? MNNConversationModelAdapter.TextTurn : MNNConversationModelAdapter.AudioTurn, adapter);
            Assert.AreEqual(name, MNNConversationModelCatalog.DefaultInstallationName(runtime));
            Assert.AreEqual(name, MNNModelCatalog.InstallFolderName(runtime));
            var files = registration.Files();
            foreach (var missing in files)
            {
                var candidate = registration.CreateModel();
                MNNModelCatalog.ApplyArtifactEvidence(candidate, files.Where(f => f.Path != missing.Path).Select(f => f.Path).ToArray());
                Assert.False(candidate.runtimeAvailable, missing.Path);
            }
        }
        [TestCase("Smart Turn", "SmartTurn-v3.2-FP16-MNN")]
        [TestCase("TurnSense 1.0", "Bairong-TurnSense-1.0-MNN")]
        [TestCase("TurnSense 1.1", "Bairong-TurnSense-1.1-MNN")]
        [TestCase("Easy Turn", "Easy-Turn-MNN")]
        [TestCase("TEN Turn Detection", "TEN-Turn-Detection-7B-MNN")]
        [TestCase("DualTurn", "DualTurn-Endpointing-MNN")]
        [TestCase("LiveKit multilingual", "LiveKit-Turn-Detector-Multilingual-MNN")]
        [TestCase("LiveKit English", "LiveKit-Turn-Detector-MNN")]
        public void DocumentedSearchNamesResolveInBothCatalogs(string query, string name)
        {
            string repository = "EitanWang/" + name;
            Assert.True(MNNModelCatalog.CreateDefault().SearchModels(query).Any(m => m.repositoryId == repository));
            Assert.True(MNNModelRepository.GetDefaultRepository().SearchModels(query).Any(m => m.repositoryId == repository));
        }

        [Test]
        public void ExistingSmartTurnSelectionUsesTheVerifiedFp16Deployment()
        {
            var entry = new MNNConversationModelCatalog().Entries.Single(e => e.modelId == "SmartTurn-v3.2-MNN");
            Assert.AreEqual("EitanWang/SmartTurn-v3.2-FP16-MNN", entry.repository);
            Assert.AreEqual(MNNModelRegistry.Find(MNNModelSource.ModelScope, entry.repository).Revision, entry.revision);
            Assert.AreEqual(MNNConversationModelAdapter.AudioTurn, entry.adapter);
        }

        [Test]
        public void PinnedTurnInstallationsRejectMissingOrTruncatedExports()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty, "Set MNN_TEST_MODELS_ROOT to the verified local exports.");
            var registrations = MNNModelRegistry.All.Where(r => r.CreateModel().category == "TurnDetection").ToArray();
            Assert.GreaterOrEqual(registrations.Length, 6);
            foreach (var registration in registrations)
            {
                Assert.True(registration.IsComplete(Path.Combine(root, registration.InstallationName)), registration.Repository);
                string temporary = Path.GetFullPath("TestArtifacts~/TurnModelCatalog/" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporary);
                try
                {
                    Assert.False(registration.IsComplete(temporary));
                    var graph = registration.Files().First(f => f.Path.EndsWith(".mnn", StringComparison.Ordinal));
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(temporary, graph.Path)));
                    File.WriteAllBytes(Path.Combine(temporary, graph.Path), new byte[16]);
                    Assert.False(registration.IsComplete(temporary));
                }
                finally { Directory.Delete(temporary, true); }
            }
        }
    }
}
