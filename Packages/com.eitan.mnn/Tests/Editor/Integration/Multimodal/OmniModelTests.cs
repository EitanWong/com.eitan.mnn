using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class OmniModelTests
    {
        private MNNLlm _model;
        [OneTimeSetUp]
        public void Load() => _model = MNNLlm.Load(MultimodalTestData.Model("Qwen2.5-Omni-3B-MNN"), cacheDirectory: MultimodalTestData.Cache("omni"));
        [OneTimeTearDown]
        public void Dispose()
        {
            _model?.Dispose();
            MultimodalTestData.CleanCache("omni");
        }

        [Test, Order(1)]
        public void ImageInput_RecognizesRed()
        {
            Assert.AreEqual(MNNModelCapabilities.Vision | MNNModelCapabilities.AudioInput, _model.Capabilities);
            var result = _model.GenerateMultimodal("What color is this image? Answer in one word.", imagePath: MultimodalTestData.Fixture("red.png"), maxNewTokens: 32);
            MultimodalTestData.Report("Omni vision", result);
            StringAssert.Contains("red", result.Text.ToLowerInvariant());
            Assert.Greater(result.VisionMicroseconds, 0);
        }

        [Test, Order(2)]
        public void AudioInput_TranscribesRecordedSentence()
        {
            var result = _model.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: MultimodalTestData.Fixture("speech.wav"), maxNewTokens: 64);
            MultimodalTestData.Report("Omni audio", result);
            StringAssert.Contains("paris", result.Text.ToLowerInvariant());
            Assert.Greater(result.AudioMicroseconds, 0);
            Assert.Greater(result.InputAudioSeconds, 1);
        }

        [Test, Order(3)]
        public void CombinedImageAudio_UsesBothInputs()
        {
            var result = _model.GenerateMultimodal("Name the color of the image, then name the city mentioned in the audio.", imagePath: MultimodalTestData.Fixture("blue.png"), audioPath: MultimodalTestData.Fixture("speech.wav"), maxNewTokens: 64);
            MultimodalTestData.Report("Omni combined", result);
            StringAssert.Contains("blue", result.Text.ToLowerInvariant());
            StringAssert.Contains("paris", result.Text.ToLowerInvariant());
            Assert.Greater(result.VisionMicroseconds, 0);
            Assert.Greater(result.AudioMicroseconds, 0);
        }

        [Test, Order(4)]
        public void SpeechOutput_RejectsUnsupportedCallbackWithoutDamagingModel()
        {
            Assert.Throws<PlatformNotSupportedException>(() => _model.GenerateMultimodal(
                "Say hello.", maxNewTokens: 32, generateSpeech: true));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.Throws<PlatformNotSupportedException>(() => _model.GenerateConversation(
                new[]{new MNNChatMessage(MNNChatRole.User, "Say hello.")}, generateSpeech: true));
            Assert.IsNotEmpty(_model.Generate("Say hello.", 32).Text);
        }
    }
}
