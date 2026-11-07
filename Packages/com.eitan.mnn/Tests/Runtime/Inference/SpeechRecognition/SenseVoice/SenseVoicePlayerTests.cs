using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("SenseVoicePlayer")]
    public class SenseVoicePlayerTests
    {
        [TestCase(false), TestCase(true)]
        public void CpuTranscribesWaveResetsAndCancelsInActualPlayer(bool huggingFace)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_SENSEVOICE_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Provide --sensevoice-root with existing verified weights and fixtures.");
            string wave = Path.Combine(root, "..", "Zipformer", "Audio", "en-0.wav");
            using (var model = MNNSenseVoiceRecognizer.Load(Path.Combine(root, huggingFace ? "xinliu--sensevoice_mnn" : "wo105090--SenseVoiceSmall-MNN"), MNNSenseVoiceModel.Config(huggingFace)))
            {
                var first = model.RecognizeWaveFile(wave);
                Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                Assert.AreEqual("After early nightfall, the yellow lamps would light up here and there. The squalid quarter of the brothels.", first.Text);
                Assert.AreEqual(first.TokenIds.Count, first.Timestamps.Count);
                Assert.AreEqual("", model.Recognize(new float[16000], 16000).Text);
                Assert.Throws<OperationCanceledException>(() => model.RecognizeWaveFile(wave, new CancellationToken(true)));
                var mixed = model.RecognizeWaveFile(Path.Combine(root, "..", "Zipformer", "Audio", "zh-en-0.wav"));
                Assert.That(mixed.Text, Does.Contain("昨天"));
                Assert.That(mixed.Text, Does.Contain("星期三"));
                var repeated = model.RecognizeWaveFile(wave);
                Assert.AreEqual(first.Text, repeated.Text);
                CollectionAssert.AreEqual(first.TokenIds, repeated.TokenIds);
                TestContext.WriteLine("SenseVoice CPU en: " + first.Text + "\nzh-en: " + mixed.Text);
            }
        }
    }
}
