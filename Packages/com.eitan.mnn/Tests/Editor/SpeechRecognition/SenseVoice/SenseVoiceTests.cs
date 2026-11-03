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
        public void FeaturesRetainBothEdgesUsingTheTrainingFrontend()
        {
            var samples = new float[2576];
            for (int i = 0; i < samples.Length; ++i) samples[i] = (i % 29 - 14) / 100f + (i % 7) / 20f;
            var features = MNNSenseVoiceFeatures.Extract(samples, out int frames, default);
            Assert.AreEqual(3, frames, "14 fbank frames require three centered LFR windows, including the tail.");
            // Independent kaldi-native-fbank + FunASR 7/6 LFR + published model CMVN.
            int[] indices = { 0, 79, 80, 239, 240, 319, 320, 559, 560, 639, 640, 1119, 1120, 1359, 1360, 1439, 1440, 1679 };
            float[] expected = { 0.64674741f, 1.66936219f, 0.64674741f, 1.66936219f, 0.64674741f, 1.66936219f, 0.541116893f, 1.66976094f, 0.100158811f, 1.66976094f, 0.409323096f, 1.66988194f, 0.504504323f, 1.66978395f, 0.620444477f, 1.66939175f, 0.457950056f, 1.67013097f };
            for (int i = 0; i < indices.Length; ++i)
                Assert.AreEqual(expected[i], features[indices[i]], .0003f, "feature " + indices[i]);
        }

        [Test]
        public void CausalWindowsKeepTheParaformerAdvanceContract()
        {
            var samples = new float[2576];
            for (int i = 0; i < samples.Length; ++i) samples[i] = (i % 29 - 14) / 100f + (i % 7) / 20f;
            var features = MNNSenseVoiceFeatures.Extract(samples, out int frames, default, centerLfr: false);
            Assert.AreEqual(2, frames);
            Assert.AreEqual(.6467474f, features[0], .0003f);
            Assert.AreEqual(.5411169f, features[80], .0003f);
        }

        [Test]
        public void OnlyThePinnedProviderProfilesAreAdvertised()
        {
            foreach (var provider in new[] {MNNModelSource.ModelScope, MNNModelSource.HuggingFace})
            {
                var model = new MNNRemoteModel {id = MNNSenseVoiceModel.Repository, name = "SenseVoiceSmall-MNN", provider = provider, repositoryId = MNNSenseVoiceModel.Repository};
                typeof(MNNModelCatalog).GetMethod("ApplyArtifactEvidence", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] {model, MNNSenseVoiceModel.Paths});
                Assert.AreEqual(provider == MNNModelSource.ModelScope, model.runtimeAvailable);
                Assert.AreEqual("SpeechRecognition", model.category);
                if (provider == MNNModelSource.ModelScope) Assert.AreEqual(MNNSenseVoiceModel.Sizes.Sum(), model.fileSize);
            }
            var hf = new MNNRemoteModel {id = "hf:" + MNNSenseVoiceModel.HuggingFaceRepository, name = "sensevoice_mnn", provider = MNNModelSource.HuggingFace, repositoryId = MNNSenseVoiceModel.HuggingFaceRepository};
            typeof(MNNModelCatalog).GetMethod("ApplyArtifactEvidence", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
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
