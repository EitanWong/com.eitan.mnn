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
            Assert.AreEqual(MNNModelCapabilities.Vision | MNNModelCapabilities.AudioInput | MNNModelCapabilities.SpeechOutput, _model.Capabilities);
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
        public void SpeechOutput_ProducesFiniteNonSilentWaveform()
        {
            var result = _model.GenerateMultimodal("Say hello in one short sentence.", maxNewTokens: 32, generateSpeech: true, maxAudioTokens: 192);
            MultimodalTestData.Report("Omni speech", result);
            MultimodalTestData.SaveWave("omni-generated.wav", result);
            // A nonzero waveform alone does not prove intelligible speech: transcribe it with a different model.
            using (var audio = MNNLlm.Load(MultimodalTestData.Model("LFM2.5-Audio-1.5B-MNN"), cacheDirectory: MultimodalTestData.Cache("speech-roundtrip")))
            {
                var transcript = audio.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: System.IO.Path.Combine(MultimodalTestData.Artifacts, "omni-generated.wav"), maxNewTokens: 64);
                MultimodalTestData.Report("Omni speech transcribed by LFM", transcript);
                StringAssert.Contains("hello", transcript.Text.ToLowerInvariant());
            }

            MultimodalTestData.CleanCache("speech-roundtrip");
        }

        [Test, Order(5)]
        public void RepeatedSpeech_AfterGc_ReturnsIndependentWaveforms()
        {
            var first = _model.GenerateMultimodal("Say hello in one short sentence.", maxNewTokens: 32, generateSpeech: true, maxAudioTokens: 192);
            var copy = (float[])first.Waveform.Clone();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var second = _model.GenerateMultimodal("Say hello in one short sentence.", maxNewTokens: 32, generateSpeech: true, maxAudioTokens: 192);
            MultimodalTestData.SaveWave("omni-repeat.wav", second);
            Assert.AreNotSame(first.Waveform, second.Waveform);
            CollectionAssert.AreEqual(copy, first.Waveform);
            // Clearing the native std::function must also allow subsequent text-only calls.
            Assert.IsNotEmpty(_model.Generate("Say hello.", 32).Text);
        }
    }
}
