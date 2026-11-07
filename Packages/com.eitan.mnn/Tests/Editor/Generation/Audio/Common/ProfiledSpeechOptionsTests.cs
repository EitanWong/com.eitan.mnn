using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ProfiledSpeechOptionsTests
    {
        [TestCase("developerabu/pocket-tts-mnn", false)]
        [TestCase("developerabu/IndicF5-MNN", true)]
        [TestCase("yunfengwang/IndexTTS-2.5-mnn", false)]
        [TestCase("yunfengwang/fireredtts3-mnn", true)]
        [TestCase("Bbkblo/DhVaani-0.5-MNN", true)]
        public void ReferenceSpeechRequirementsAreAvailableBeforeDownload(string repository, bool transcript)
        {
            var model = new MNNRemoteModel { provider = MNNModelSource.HuggingFace, repositoryId = repository };
            Assert.True(MNNSpeechSynthesizer.SupportsRepository(model));
            Assert.True(MNNSpeechSynthesizer.RequiresReferenceAudio(model));
            Assert.AreEqual(transcript, MNNSpeechSynthesizer.RequiresReferenceText(model));
        }
        [Test]
        public void MissingReferenceRecordingFailsBeforeAnyNativeLoadAndPartialWeightsAreNotInstalled()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "ProfiledSpeechOptions", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, "gpt_prefill_fp16.mnn"), "incomplete fixture; not a graph");
                Assert.False(MNNSpeechSynthesizer.HasProfile(directory));
                var error = Assert.Throws<ArgumentException>(() => MNNSpeechSynthesizer.Load(directory));
                StringAssert.Contains("reference WAV", error.Message);
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
