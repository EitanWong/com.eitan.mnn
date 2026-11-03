using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class EnglishKeywordSpotterIntegrationTests
    {
        [Test]
        public void EnglishSiriRecordingTriggersTheDefaultWords()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_ENGLISH_KWS_MODELS");
            string audio = Environment.GetEnvironmentVariable("MNN_TEST_SIRI_WAV");
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(audio)) Assert.Ignore("Provide an English model and a Siri recording.");
            using (var model = MNNKeywordSpotter.Load(directory))
            {
                var matches = model.DetectWaveFile(audio, MNNKeywordSpotterConfig.EnglishDefaultWords);
                Assert.IsNotEmpty(matches);
                Assert.That(matches.All(match => match.Keyword == "Siri" || match.Keyword == "Hey Siri"));
                foreach (var match in matches) TestContext.WriteLine(match.Keyword + ": " + match.Confidence);
            }
        }
        [Test]
        public void EnglishOriginalRecordingsDetectTypedPhrasesAndRejectSilence()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_ENGLISH_KWS_MODELS");
            if (string.IsNullOrEmpty(directory)) Assert.Ignore("Set MNN_ENGLISH_KWS_MODELS to the prepared official GigaSpeech profile.");
            using (var model = MNNKeywordSpotter.Load(directory))
            {
                Assert.AreEqual(MNNKeywordLanguage.English, model.Language);
                var first = model.DetectWaveFile(Path.Combine(directory, "test_wavs/0.wav"), "light up");
                CollectionAssert.AreEqual(new[] { "light up" }, first.Select(item => item.Keyword));
                var second = model.DetectWaveFile(Path.Combine(directory, "test_wavs/1.wav"), "lovely child\nforever");
                CollectionAssert.AreEqual(new[] { "lovely child", "forever" }, second.Select(item => item.Keyword));
                Assert.IsEmpty(model.Detect(new float[32000], 16000, "Siri\nHey Siri"));
                Assert.Throws<ArgumentException>(() => model.Reset("你好"));
            }
        }
    }
}
