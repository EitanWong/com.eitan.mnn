using System;
using System.IO;
using System.Threading;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class IndicVitsIntegrationTests
    {
        [Test]
        public void InvalidControlsCancellationAndDisposalDoNotPoisonTheNextRequest()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_INDIC_VITS_ROOT");
            const string phrase = "வணக்கம் உலகம்.";
            float[] first;
            var model = MNNIndicVits.Load(root);
            try
            {
                first = model.Synthesize(phrase).Waveform;
                foreach (int id in new[] { -1, 20, 1023 })
                    Assert.Throws<ArgumentOutOfRangeException>(() => model.Synthesize(phrase, speakerId: id));
                foreach (int id in new[] { -1, 32 })
                    Assert.Throws<ArgumentOutOfRangeException>(() => model.Synthesize(phrase, emotionId: id));
                Assert.Throws<ArgumentException>(() => model.Synthesize("..."));
                Assert.Throws<ArgumentException>(() => model.Synthesize(new string('a', 301)));
                Assert.Throws<NotSupportedException>(() => model.Synthesize(phrase + "𠮷"));
                Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrase, cancellationToken: new CancellationToken(true)));
                var recovered = model.SynthesizeAsync(phrase).GetAwaiter().GetResult();
                Assert.Greater(recovered.Waveform.Length, 2400);
                Assert.IsFalse(ReferenceEquals(first, recovered.Waveform));
            }
            finally
            {
                model.Dispose();
                model.Dispose();
            }
            Assert.Throws<ObjectDisposedException>(() => model.Synthesize(phrase));
            using (var reloaded = MNNIndicVits.Load(root))
                Assert.Greater(reloaded.Synthesize(phrase).Waveform.Length, 2400);
            Assert.IsTrue(first.All(v => !float.IsNaN(v) && !float.IsInfinity(v)));
        }

        [Test]
        public void DocumentedSpeakersAndEmotionsProduceNonSilentWaveforms()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_INDIC_VITS_ROOT");
            using (var model = MNNIndicVits.Load(root))
            {
                var studio = MNNStudioModel.Read(root);
                Assert.IsTrue(studio.HasGenerationPipeline);
                Assert.AreEqual(MNNSpeechModelKind.IndicVits, studio.TtsProfile);
                CollectionAssert.AreEqual(model.Voices, studio.TtsVoices);
                for (int speaker = 0; speaker < model.Voices.Length; ++speaker)
                {
                    var audio = model.Synthesize(MNNIndicVits.SpeakerExamples[speaker], speaker);
                    Assert.Greater(audio.Waveform.Length, 2400);
                    Assert.Greater(Math.Sqrt(audio.Waveform.Select(v => (double)v * v).Average()), .005, model.Voices[speaker]);
                    File.WriteAllBytes(Path.Combine(AdditionalTtsValidation.Artifacts, "indic-vits-" + model.Voices[speaker] + ".wav"), MNNStudioAudio.EncodeWave(audio.Waveform, audio.SampleRate));
                }
                float[] previous = null;
                for (int i = 0; i < MNNIndicVits.StyleIds.Length; ++i)
                {
                    var audio = model.Synthesize(MNNIndicVits.SpeakerExamples[18], 18, MNNIndicVits.StyleIds[i]);
                    Assert.Greater(Math.Sqrt(audio.Waveform.Select(v => (double)v * v).Average()), .005, MNNIndicVits.StyleNames[i]);
                    if (previous != null)
                        Assert.IsFalse(previous.SequenceEqual(audio.Waveform), "Emotion control must affect output.");
                    previous = audio.Waveform;
                    File.WriteAllBytes(Path.Combine(AdditionalTtsValidation.Artifacts, "indic-vits-emotion-" + MNNIndicVits.StyleNames[i] + ".wav"), MNNStudioAudio.EncodeWave(audio.Waveform, audio.SampleRate));
                }
            }
        }

        [Test]
        public void TamilSpeechWithExplicitSpeakerAndEmotion()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_INDIC_VITS_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            using (var model = MNNIndicVits.Load(root))
            {
                Assert.Throws<OperationCanceledException>(() => model.Synthesize("வணக்கம் உலகம்.", cancellationToken: new CancellationToken(true)));
                Assert.Throws<NotSupportedException>(() => model.Synthesize("你好"));
                string path = AdditionalTtsValidation.ValidateAndSave(model.Synthesize("வணக்கம் உலகம். இது ஒரு குரல் தொகுப்பு சோதனை.", 18), "indic-vits-tamil.wav", 24000);
                using (var asr = MNNWhisperRecognizer.Load(Environment.GetEnvironmentVariable("MNN_TEST_TAMIL_ASR_ROOT")))
                {
                    asr.Language = "ta";
                    string text = asr.RecognizeWaveFile(path).Text;
                    File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), text);
                    TestContext.WriteLine(text);
                    StringAssert.Contains("வணக்கம்", text);
                    StringAssert.Contains("உலகம்", text);
                }
            }
        }
    }
}
