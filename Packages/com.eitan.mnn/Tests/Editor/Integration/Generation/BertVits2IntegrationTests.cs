using System;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class BertVits2IntegrationTests
    {
        [Test]
        public void ChineseSpeechRepeatReloadDisposeAndAsr()
        {
            const string phrase = "你好，欢迎使用语音合成。";
            string wav;
            var model = MNNBertVits2.Load(GenerationTestData.Model("bert-vits2-MNN"));
            try
            {
                wav = AdditionalTtsValidation.ValidateAndSave(model.Synthesize(phrase), "bertvits2-chinese.wav", 44100);
                var repeat = model.Synthesize(phrase);
                AdditionalTtsValidation.ValidateAndSave(repeat, "bertvits2-repeat.wav", 44100);
                AdditionalTtsValidation.ValidateAndSave(model.Synthesize("今天是二十三号。"), "bertvits2-number.wav", 44100);
                Assert.Throws<NotSupportedException>(() => model.Synthesize("Hello world"));
                Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrase, new CancellationToken(true)));
            }
            finally
            {
                model.Dispose();
                model.Dispose();
            }

            Assert.Throws<ObjectDisposedException>(() => model.Synthesize(phrase));
            using (var reload = MNNBertVits2.Load(GenerationTestData.Model("bert-vits2-MNN")))
                Assert.Greater(reload.Synthesize("你好。").Waveform.Length, 1000);
            AdditionalTtsValidation.Transcribe(wav, true);
        }
    }
}
