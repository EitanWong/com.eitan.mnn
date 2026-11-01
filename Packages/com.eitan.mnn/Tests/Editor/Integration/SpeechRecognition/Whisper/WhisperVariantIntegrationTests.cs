using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("Whisper")]
    public class WhisperVariantIntegrationTests
    {
        private string Root => Environment.GetEnvironmentVariable("MNN_TEST_WHISPER_ROOT");

        [TestCase("modelscope-tiny-en"), TestCase("developerabu-tiny"), TestCase("developerabu-base"), TestCase("developerabu-small")]
        public void EnglishReferenceResetsAndStudioUsesTheMatchingProfile(string id)
        {
            if (Root == null) Assert.Ignore("Set MNN_TEST_WHISPER_ROOT with existing pinned weights; no download.");
            var profile = MNNWhisperModel.Profiles.Single(p => p.Id == id);
            string directory = Path.Combine(Root, profile.Repository.Replace("/", "--"));
            string wave = Path.Combine(Root, "..", "..", "Zipformer", "Audio", "en-0.wav");
            MNNWhisperModel.Prepare(directory, profile.Repository);
            using (var recognizer = MNNWhisperRecognizer.Load(directory))
            {
                var first = recognizer.RecognizeWaveFile(wave);
                TestContext.WriteLine(id + ": " + first.Text);
                string lexical = System.Text.RegularExpressions.Regex.Replace(first.Text.ToUpperInvariant(), "[^A-Z ]", "");
                string reference = File.ReadAllLines(Path.Combine(Root, "..", "..", "Zipformer", "Audio", "en-trans.txt"))[0].Substring(6).Trim();
                Assert.AreEqual(reference, lexical, "Independent upstream word sequence.");
                Assert.AreEqual("", recognizer.Recognize(new float[16000], 16000).Text);
                Assert.Throws<OperationCanceledException>(() => recognizer.RecognizeWaveFile(wave, new CancellationToken(true)));
                var second = recognizer.RecognizeWaveFile(wave);
                Assert.AreEqual(first.Text, second.Text);
                CollectionAssert.AreEqual(first.TokenIds, second.TokenIds);
                Assert.IsEmpty(first.Timestamps);
                var model = MNNStudioModel.Read(directory);
                Assert.True(model.HasSpeechRecognitionPipeline);
                using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
                {
                    var result = backend.Run(new MNNStudioRequest {Audio = wave});
                    Assert.AreEqual(first.Text, result.Text);
                    Assert.That(result.BackendDescription, Does.Contain("Whisper " + profile.Name));
                }
            }
        }

        [Test]
        public void SmallChineseLanguagePrefixExecutesWithByteTokensAndStudioSelection()
        {
            if (Root == null) Assert.Ignore("Set MNN_TEST_WHISPER_ROOT.");
            const string id = "developerabu-small";
            var profile = MNNWhisperModel.Profiles.Single(p => p.Id == id);
            var config = profile.Config(); config.language = "zh";
            using (var recognizer = MNNWhisperRecognizer.Load(Path.Combine(Root, profile.Repository.Replace("/", "--")), config))
            {
                var result = recognizer.RecognizeWaveFile(Path.Combine(Root, "..", "..", "Zipformer", "Audio", "zh-en-0.wav"));
                TestContext.WriteLine(id + " zh: " + result.Text);
                Assert.That(result.Text, Does.Contain("昨天"));
                Assert.That(result.Text, Does.Contain("星期三"));
                Assert.IsNotEmpty(result.TokenIds);
                Assert.IsEmpty(result.Timestamps);
                recognizer.Language = "en";
                var english = recognizer.RecognizeWaveFile(Path.Combine(Root, "..", "..", "Zipformer", "Audio", "en-0.wav"));
                Assert.That(english.Text, Does.Contain("brothels"));
                string directory = Path.Combine(Root, profile.Repository.Replace("/", "--"));
                MNNWhisperModel.Prepare(directory, profile.Repository);
                var model = MNNStudioModel.Read(directory);
                Assert.True(model.SupportsWhisperChinese);
                using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
                {
                    var studio = backend.Run(new MNNStudioRequest {RecognitionLanguage = "zh", Audio = Path.Combine(Root, "..", "..", "Zipformer", "Audio", "zh-en-0.wav")});
                    Assert.AreEqual(result.Text, studio.Text);
                }
            }
        }
    }
}
