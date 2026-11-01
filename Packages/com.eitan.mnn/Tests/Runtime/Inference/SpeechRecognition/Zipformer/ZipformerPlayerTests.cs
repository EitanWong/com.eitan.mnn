using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("ZipformerPlayer")]
    public class ZipformerPlayerTests
    {
        [TestCase(false, MNNBackendType.CPU), TestCase(true, MNNBackendType.CPU)]
        [TestCase(false, MNNBackendType.Auto), TestCase(true, MNNBackendType.Auto)]
        public void TranscribesWaveResetsAndCancelsInActualPlayer(bool bilingual, MNNBackendType backend)
        {
            string fixtures = Environment.GetEnvironmentVariable("MNN_PLAYER_ASR_ROOT");
            string models = Environment.GetEnvironmentVariable("MNN_PLAYER_ASR_MODELS");
            if (string.IsNullOrWhiteSpace(fixtures) || string.IsNullOrWhiteSpace(models))
                Assert.Ignore("Provide --asr-root and --asr-models with existing verified Zipformer models and WAV fixtures.");
            string directory = Path.Combine(models, bilingual ? MNNZipformerModel.Bilingual : MNNZipformerModel.English);
            string wave = Path.Combine(fixtures, "Audio", bilingual ? "zh-en-0.wav" : "en-0.wav");
            using (var model = MNNZipformerRecognizer.Load(directory, MNNZipformerModel.Config(bilingual), backendType: backend))
            {
                var first = model.RecognizeWaveFile(wave);
                Assert.That(first.Text, Does.Contain(bilingual ? "昨天" : "AFTER EARLY NIGHTFALL"));
                Assert.That(first.Text, Does.Contain(bilingual ? "星期三" : "BROTHELS"));
                if (!bilingual)
                    Assert.AreEqual("AFTER EARLY NIGHTFALL THE YELLOW LAMPS WOULD LIGHT UP HERE AND THERE THE SQUALID QUARTER OF THE BROTHELS", first.Text);
                Assert.AreEqual(first.TokenIds.Count, first.Timestamps.Count);
                Assert.AreEqual("", model.Recognize(new float[16000], 16000).Text);
                Assert.Throws<OperationCanceledException>(() => model.RecognizeWaveFile(wave, new CancellationToken(true)));
                var repeated = model.RecognizeWaveFile(wave);
                Assert.AreEqual(first.Text, repeated.Text);
                CollectionAssert.AreEqual(first.TokenIds, repeated.TokenIds);
                TestContext.WriteLine("Zipformer " + (bilingual ? "zh-en" : "en") + " backend=" + model.Backend + ": " + first.Text);
            }
        }
    }
}
