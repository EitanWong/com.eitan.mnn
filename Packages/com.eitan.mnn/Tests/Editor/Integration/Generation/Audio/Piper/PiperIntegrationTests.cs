using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class PiperIntegrationTests
    {
        [Test]
        public void StandardLessacExportSynthesizesAtItsConfiguredRate()
        {
            string root = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_TTS_ROOT"), "PiperLessac");
            string path;
            using (var model = MNNPiper.Load(root))
            {
                CollectionAssert.AreEqual(new[]{"en_US-lessac-high"}, model.Voices);
                Assert.AreEqual(22050, model.SampleRate);
                Assert.Throws<OperationCanceledException>(() => model.Synthesize("hello", cancellationToken: new CancellationToken(true)));
                path = AdditionalTtsValidation.ValidateAndSave(model.Synthesize("Paris is the capital of France."), "piper-lessac-en.wav", 22050);
                AdditionalTtsValidation.ValidateAndSave(model.Synthesize("Hello, this is another sentence."), "piper-lessac-repeat.wav", 22050);
            }
            AdditionalTtsValidation.Transcribe(path, false);
        }

        [Test]
        public void ThreeVoicesRepeatReloadDisposeAndAsr()
        {
            const string phrase = "The capital of France is Paris.";
            var model = MNNPiper.Load(GenerationTestData.Model("piper-voices-MNN"));
            string wav;
            try
            {
                CollectionAssert.AreEquivalent(new[]{"en_US-amy-low", "en_US-kathleen-low", "en_US-ryan-low"}, model.Voices);
                wav = AdditionalTtsValidation.ValidateAndSave(model.Synthesize(phrase), "piper-paris.wav", 16000);
                var repeat = model.Synthesize("Hello world.");
                Assert.Greater(repeat.Waveform.Length, 16000);
                foreach (string voice in model.Voices)
                    AdditionalTtsValidation.ValidateAndSave(model.Synthesize(phrase, voice), "piper-" + voice + ".wav", 16000);
                Assert.Throws<ArgumentException>(() => model.Synthesize(phrase, "missing-voice"));
                Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrase, cancellationToken: new CancellationToken(true)));
            }
            finally
            {
                model.Dispose();
                model.Dispose();
            }

            Assert.Throws<ObjectDisposedException>(() => model.Synthesize(phrase));
            using (var reload = MNNPiper.Load(GenerationTestData.Model("piper-voices-MNN")))
                Assert.Greater(reload.Synthesize("Hello.").Waveform.Length, 1000);
            AdditionalTtsValidation.Transcribe(wav, false);
            foreach (string voice in new[]{"en_US-kathleen-low", "en_US-ryan-low"})
                AdditionalTtsValidation.Transcribe(Path.Combine(AdditionalTtsValidation.Artifacts, "piper-" + voice + ".wav"), false);
        }
    }
}
