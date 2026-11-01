using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Qwen3AsrPlayer")]
    public class Qwen3AsrPlayerTests
    {
        [TestCase(false, false), TestCase(true, false), TestCase(false, true), TestCase(true, true)]
        public void TranscribesResetsAndCancelsThroughOfficialAudioApiInActualPlayer(bool bilingual, bool auto)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_QWEN3_ASR_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Provide --qwen3-asr-root with existing pinned weights and WAV fixtures.");
            string model = Path.Combine(root, MNNQwen3AsrModel.Repository.Replace("/", "--"));
            string audio = Path.Combine(root, "..", "..", "Zipformer", "Audio");
            using (var recognizer = MNNQwen3AsrRecognizer.Load(model, backendType: auto ? MNNBackendType.Auto : MNNBackendType.CPU))
            {
                string wave = Path.Combine(audio, bilingual ? "zh-en-0.wav" : "en-0.wav");
                bool callbackChecked = false;
                var first = recognizer.RecognizeWaveFile(wave, onProgress: update =>
                {
                    if (callbackChecked) return;
                    callbackChecked = true;
                    Assert.Throws<InvalidOperationException>(() => recognizer.Dispose());
                    Assert.Throws<InvalidOperationException>(() => recognizer.Recognize(new float[16000], 16000));
                });
                Assert.True(callbackChecked);
                if (bilingual) Assert.AreEqual("昨天是 Monday。Today is 礼拜二。The day after tomorrow 是星期三。", first.Text);
                else Assert.AreEqual(File.ReadAllLines(Path.Combine(audio, "en-trans.txt"))[0].Substring(6).Trim(),
                    System.Text.RegularExpressions.Regex.Replace(first.Text.ToUpperInvariant(), "[^A-Z]+", " ").Trim());
                var expected = auto && MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
                Assert.AreEqual(expected, recognizer.Backend);
                Assert.AreEqual(expected, recognizer.MediaBackend);
                Assert.IsNotEmpty(first.TokenIds);
                int[] saved = first.TokenIds.ToArray();
                Assert.IsEmpty(first.Timestamps);
                Assert.AreEqual("", recognizer.Recognize(new float[16000], 16000).Text);
                Assert.Throws<OperationCanceledException>(() => recognizer.RecognizeWaveFile(wave, new CancellationToken(true)));
                using (var cancellation = new CancellationTokenSource())
                    Assert.Throws<OperationCanceledException>(() => recognizer.RecognizeWaveFile(wave, cancellation.Token, update => cancellation.Cancel()));
                var second = recognizer.RecognizeWaveFile(wave);
                Assert.AreEqual(first.Text, second.Text);
                CollectionAssert.AreEqual(saved, first.TokenIds);
                CollectionAssert.AreEqual(first.TokenIds, second.TokenIds);
                TestContext.WriteLine(first.Text);
            }
        }
    }
}
