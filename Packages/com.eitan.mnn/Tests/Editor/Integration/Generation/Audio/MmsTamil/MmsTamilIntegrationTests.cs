using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class MmsTamilIntegrationTests
    {
        [TestCase("int8")]
        [TestCase("fp16_int8")]
        [TestCase("fp32")]
        public void SynthesizesTamilAndTranscribesWithIndependentWhisper(string precision)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_MMS_TAMIL_ROOT");
            const string phrase = "வணக்கம் உலகம். இது ஒரு குரல் தொகுப்பு சோதனை.";
            string path;
            var model = MNNMmsTamil.Load(root, precision: precision);
            try
            {
                Assert.Throws<NotSupportedException>(() => model.Synthesize("你好"));
                Assert.Throws<ArgumentException>(() => model.Synthesize("!!!"));
                Assert.Throws<ArgumentException>(() => model.Synthesize("___'"));
                Assert.Throws<ArgumentException>(() => model.Synthesize(new string('அ', 301)));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Synthesize(phrase, float.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Synthesize(phrase, float.PositiveInfinity));
                Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrase, cancellationToken: new CancellationToken(true)));
                var audio = model.SynthesizeAsync(phrase).GetAwaiter().GetResult();
                path = AdditionalTtsValidation.ValidateAndSave(audio, "mms-tamil-" + precision + ".wav", 16000);
                Assert.Greater(model.Synthesize("வணக்கம்", 1.2f).Waveform.Length, 1600);
                AdditionalTtsValidation.ValidateAndSave(model.Synthesize(phrase), "mms-tamil-" + precision + "-repeat.wav", 16000);
            }
            finally { model.Dispose(); model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Synthesize(phrase));
            using (var model2 = MNNMmsTamil.Load(root, precision: precision))
                Assert.Greater(model2.Synthesize("வணக்கம்").Waveform.Length, 1600);
            using (var asr = MNNWhisperRecognizer.Load(Environment.GetEnvironmentVariable("MNN_TEST_TAMIL_ASR_ROOT")))
            {
                asr.Language = "ta";
                string transcript = asr.RecognizeWaveFile(path).Text;
                File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), transcript);
                TestContext.WriteLine(transcript);
                StringAssert.Contains("வணக்கம்", transcript);
                StringAssert.Contains("உலகம்", transcript);
            }
        }
    }
}
