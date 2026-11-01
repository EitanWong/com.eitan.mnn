using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class SherpaVitsChineseIntegrationTests
    {
        [Test]
        public void AllFiveSpeakersSynthesizeAndTranscribeChinese()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_SHERPA_VITS_ROOT");
            const string phrase = "你好，这是语音合成测试。";
            var model = MNNSherpaVitsChinese.Load(root);
            try
            {
                CollectionAssert.AreEqual(new[]{"suyingxue", "gunian", "fushiyu", "bingjiao", "bazong"}, model.Voices);
                Assert.Throws<NotSupportedException>(() => model.Synthesize("你好𠮷"));
                Assert.Throws<NotSupportedException>(() => model.Synthesize("Hello"));
                Assert.Throws<NotSupportedException>(() => model.Synthesize("123"));
                Assert.Throws<ArgumentException>(() => model.Synthesize("？！"));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Synthesize(phrase, 5));
                Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrase, cancellationToken: new CancellationToken(true)));
                foreach (int speaker in Enumerable.Range(0, 5))
                {
                    string voice = model.Voices[speaker];
                    var audio = model.SynthesizeAsync(phrase, speaker).GetAwaiter().GetResult();
                    string path = AdditionalTtsValidation.ValidateAndSave(audio, "sherpa-vits-" + voice + ".wav", 16000);
                    AdditionalTtsValidation.Transcribe(path, true);
                }
                Assert.Greater(model.Synthesize("你好。", 4, 1.2f).Waveform.Length, 1600);
                Assert.Greater(model.Synthesize(phrase).Waveform.Length, 1600);
            }
            finally { model.Dispose(); model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Synthesize(phrase));
            using (var reload = MNNSherpaVitsChinese.Load(root))
                Assert.Greater(reload.Synthesize("你好。").Waveform.Length, 1600);
        }
    }
}
