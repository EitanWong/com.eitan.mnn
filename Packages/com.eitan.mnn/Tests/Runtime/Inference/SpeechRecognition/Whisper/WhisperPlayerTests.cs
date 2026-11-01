using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("WhisperPlayer")]
    public class WhisperPlayerTests
    {
        [TestCase("developerabu-tiny-en"), TestCase("modelscope-tiny-en"), TestCase("developerabu-tiny"), TestCase("developerabu-base"), TestCase("developerabu-small")]
        public void CpuTranscribesResetsAndCancelsInActualPlayer(string id)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_WHISPER_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Provide --whisper-root with the existing pinned weights and WAV fixture.");
            string wave = Path.Combine(root, "..", "..", "Zipformer", "Audio", "en-0.wav");
            var profile = MNNWhisperModel.Profiles.Single(p => p.Id == id);
            using (var recognizer = MNNWhisperRecognizer.Load(Path.Combine(root, profile.Repository.Replace("/", "--"))))
            {
                var first = recognizer.RecognizeWaveFile(wave);
                string reference = File.ReadAllLines(Path.Combine(root, "..", "..", "Zipformer", "Audio", "en-trans.txt"))[0].Substring(6).Trim();
                Assert.AreEqual(reference, System.Text.RegularExpressions.Regex.Replace(first.Text.ToUpperInvariant(), "[^A-Z ]", ""));
                Assert.AreEqual(MNNBackendType.CPU, recognizer.Backend);
                Assert.IsEmpty(first.Timestamps);
                Assert.AreEqual("", recognizer.Recognize(new float[16000], 16000).Text);
                Assert.Throws<OperationCanceledException>(() => recognizer.RecognizeWaveFile(wave, new CancellationToken(true)));
                var second = recognizer.RecognizeWaveFile(wave);
                Assert.AreEqual(first.Text, second.Text);
                CollectionAssert.AreEqual(first.TokenIds, second.TokenIds);
                TestContext.WriteLine(first.Text);
            }
        }

        [Test]
        public void SmallChineseTranscriptionAndLanguageSwitchInActualPlayer()
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_WHISPER_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Provide --whisper-root with the existing pinned weights and WAV fixtures.");
            var profile = MNNWhisperModel.Profiles.Single(p => p.Id == "developerabu-small");
            using (var recognizer = MNNWhisperRecognizer.Load(Path.Combine(root, profile.Repository.Replace("/", "--"))))
            {
                recognizer.Language = "zh";
                var result = recognizer.RecognizeWaveFile(Path.Combine(root, "..", "..", "Zipformer", "Audio", "zh-en-0.wav"));
                Assert.That(result.Text, Does.Contain("昨天"));
                Assert.That(result.Text, Does.Contain("星期三"));
                recognizer.Language = "en";
                var english = recognizer.RecognizeWaveFile(Path.Combine(root, "..", "..", "Zipformer", "Audio", "en-0.wav"));
                Assert.That(english.Text, Does.Contain("brothels"));
                TestContext.WriteLine(result.Text);
            }
        }
    }
}
