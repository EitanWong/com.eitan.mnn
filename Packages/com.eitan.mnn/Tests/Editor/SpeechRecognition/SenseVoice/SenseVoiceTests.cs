using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class SenseVoiceTests
    {
        [Test]
        public void OnlyThePinnedProviderProfilesAreAdvertised()
        {
            foreach (var provider in new[] {MNNModelProvider.ModelScope, MNNModelProvider.HuggingFace})
            {
                var model = new MNNModelInfo {id = MNNSenseVoiceModel.Repository, name = "SenseVoiceSmall-MNN", provider = provider, repositoryId = MNNSenseVoiceModel.Repository};
                typeof(MNNModelRepository).GetMethod("ApplyArtifactEvidence", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] {model, MNNSenseVoiceModel.Paths});
                Assert.AreEqual(provider == MNNModelProvider.ModelScope, model.runtimeAvailable);
                Assert.AreEqual("SpeechRecognition", model.category);
                if (provider == MNNModelProvider.ModelScope) Assert.AreEqual(MNNSenseVoiceModel.Sizes.Sum(), model.fileSize);
            }
            var hf = new MNNModelInfo {id = "hf:" + MNNSenseVoiceModel.HuggingFaceRepository, name = "sensevoice_mnn", provider = MNNModelProvider.HuggingFace, repositoryId = MNNSenseVoiceModel.HuggingFaceRepository};
            typeof(MNNModelRepository).GetMethod("ApplyArtifactEvidence", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] {hf, MNNSenseVoiceModel.HuggingFacePaths});
            Assert.True(hf.runtimeAvailable);
            Assert.AreEqual(MNNSenseVoiceModel.HuggingFaceRevision, hf.revision);
            Assert.AreEqual(MNNSenseVoiceModel.HuggingFaceSizes.Sum(), hf.fileSize);
            Assert.False(MNNSenseVoiceModel.IsRepository("other/sensevoice_mnn"));
        }

        [Test]
        public void RejectsInvalidProfileAndShortFeaturesBeforeNativeInference()
        {
            string directory = Path.Combine("TestArtifacts~", "SenseVoiceTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.False(MNNSenseVoiceModel.IsComplete(directory));
                Assert.Throws<ArgumentException>(() => MNNSenseVoiceRecognizer.Load(directory, new MNNSenseVoiceConfig {language = "invalid"}));
                Assert.Throws<FileNotFoundException>(() => MNNSenseVoiceModel.Prepare(directory));
                Assert.Throws<ArgumentOutOfRangeException>(() => MNNSenseVoiceRecognizer.Load(directory, threadCount: 0));
                Assert.Throws<ArgumentException>(() => MNNSenseVoiceFeatures.Extract(new float[100], out _, default));
            }
            finally {Directory.Delete(directory, true);}
        }
    }
}
