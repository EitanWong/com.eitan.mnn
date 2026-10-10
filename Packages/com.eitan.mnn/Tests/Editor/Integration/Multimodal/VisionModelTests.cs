using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class VisionModelTests
    {
        private MNNLlm _model;
        [OneTimeSetUp]
        public void Load() => _model = MNNLlm.Load(MultimodalTestData.Model("SmolVLM-256M-Instruct-MNN"), cacheDirectory: MultimodalTestData.Cache("vision"));
        [OneTimeTearDown]
        public void Dispose()
        {
            _model?.Dispose();
            MultimodalTestData.CleanCache("vision");
        }

        [Test]
        public void ImageInput_ChangesColorAnswer()
        {
            Assert.That(_model.Capabilities.HasFlag(MNNModelCapabilities.Vision));
            var red = _model.GenerateMultimodal("What is the main color in this image? Answer in one word.", imagePath: MultimodalTestData.Fixture("red.png"), maxNewTokens: 24);
            var blue = _model.GenerateMultimodal("What is the main color in this image? Answer in one word.", imagePath: MultimodalTestData.Fixture("blue.png"), maxNewTokens: 24);
            MultimodalTestData.Report("SmolVLM red", red);
            MultimodalTestData.Report("SmolVLM blue", blue);
            StringAssert.Contains("red", red.Text.ToLowerInvariant());
            StringAssert.Contains("blue", blue.Text.ToLowerInvariant());
            Assert.AreNotEqual(red.Text, blue.Text);
            Assert.Greater(red.VisionMicroseconds, 0);
            Assert.Greater(blue.VisionMicroseconds, 0);
        }

        [Test]
        public void UnsupportedAudio_IsRejected() => Assert.Throws<NotSupportedException>(() => _model.GenerateMultimodal("Transcribe.", audioPath: MultimodalTestData.Fixture("speech.wav")));
        [Test]
        public void InvalidMediaAndTokenLimits_AreRejected()
        {
            Assert.Throws<System.IO.FileNotFoundException>(() => _model.GenerateMultimodal("Describe.", imagePath: "/missing-mnn-test-image.png"));
            Assert.Throws<ArgumentOutOfRangeException>(() => _model.GenerateMultimodal("Describe.", maxNewTokens: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _model.GenerateMultimodal("Describe.", maxAudioTokens: 0));
        }

        [Test]
        public void CorruptImage_IsRejected()
        {
            string path = System.IO.Path.Combine(MultimodalTestData.Artifacts, "corrupt.png");
            System.IO.File.WriteAllText(path, "This is not an image.");
            try
            {
                Assert.Throws<MNNException>(() => _model.GenerateMultimodal("Describe.", imagePath: path));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
