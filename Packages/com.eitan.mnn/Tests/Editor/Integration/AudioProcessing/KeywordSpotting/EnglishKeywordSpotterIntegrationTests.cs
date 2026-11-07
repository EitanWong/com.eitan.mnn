using System;
using System.IO;
using MNN.Unity.Editor;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class EnglishKeywordSpotterIntegrationTests
    {
        [TestCase("Samantha", "siri")]
        [TestCase("Samantha", "hi-siri")]
        [TestCase("Samantha", "hey-siri")]
        [TestCase("Daniel", "siri")]
        [TestCase("Daniel", "hi-siri")]
        [TestCase("Daniel", "hey-siri")]
        public void EachIsolatedSiriPhraseWakesWithAllThreeWordsConfigured(string voice, string phrase)
        {
            string directory = Environment.GetEnvironmentVariable("MNN_ENGLISH_KWS_MODELS");
            string recordings = Environment.GetEnvironmentVariable("MNN_TEST_WAKE_FIX_AUDIO");
            if (directory == null || recordings == null) Assert.Ignore("Provide prepared English KWS and the isolated phrase recordings.");
            using (var model = MNNKeywordSpotter.Load(directory))
            {
                var wave = MNNWaveAudio.Read(Path.Combine(recordings, voice + "-" + phrase + ".wav"), out int rate);
                foreach (int lead in new[] { 0, 32000 })
                foreach (float gain in new[] { 1f, .15f })
                {
                    var pcm = new float[lead + wave.Length];
                    for (int i = 0; i < wave.Length; ++i) pcm[lead + i] = wave[i] * gain;
                    var matches = model.Detect(pcm, rate, "Siri\nHi Siri\nHey Siri");
                    string example = $"{voice} {phrase}, lead={lead}, gain={gain}";
                    Assert.IsNotEmpty(matches, example + " must trigger independently.");
                    Assert.That(matches.All(match => match.StartSeconds >= lead / (float)rate - .1f && match.EndSeconds <= pcm.Length / (float)rate + .1f),
                        example + " timestamps must exclude internal priming audio.");
                    model.Reset("Siri\nHi Siri\nHey Siri");
                    var streamed = new System.Collections.Generic.List<MNNKeywordDetection>();
                    for (int at = 0; at < pcm.Length + 16000; at += 512)
                    {
                        var block = new float[512];
                        if (at < pcm.Length) Array.Copy(pcm, at, block, 0, Math.Min(512, pcm.Length - at));
                        streamed.AddRange(model.AcceptWaveform(block));
                    }
                    Assert.AreEqual(MNNStudioNativeBackend.FormatKeywords(matches),
                        MNNStudioNativeBackend.FormatKeywords(streamed.ToArray()), example + " live and file detection must agree.");
                    TestContext.WriteLine(example + ": " + string.Join(",", matches.Select(match => match.Keyword + "=" + match.Confidence)));
                }
            }
        }

        [Test]
        public void StudioBackendDetectsEachSynthesizedSiriPhrase()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_ENGLISH_KWS_MODELS");
            string recordings = Environment.GetEnvironmentVariable("MNN_TEST_WAKE_FIX_AUDIO");
            if (directory == null || recordings == null) Assert.Ignore("Provide prepared English KWS and phrase recordings.");
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(directory), 2, false))
            {
                foreach (string voice in new[] { "Samantha", "Daniel" })
                foreach (string phrase in new[] { "siri", "hi-siri", "hey-siri" })
                {
                    var result = backend.Run(new MNNStudioRequest
                    {
                        Audio = Path.Combine(recordings, voice + "-" + phrase + ".wav"),
                        Keywords = "Siri\nHi Siri\nHey Siri"
                    });
                    Assert.IsNotEmpty(result.KeywordDetections, voice + " " + phrase);
                    Assert.That(result.BackendDescription, Does.Contain("English"));
                    Assert.That(result.Text, Does.Contain("Siri"));
                }
            }
        }

        [Test]
        public void PrimingAndKeywordBoostDoNotWakeOnSilenceOrUnrelatedEnglish()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_ENGLISH_KWS_MODELS");
            string recordings = Environment.GetEnvironmentVariable("MNN_TEST_WAKE_FIX_AUDIO");
            if (directory == null || recordings == null) Assert.Ignore("Provide prepared English KWS and phrase recordings.");
            using (var model = MNNKeywordSpotter.Load(directory))
            {
                model.Reset("Siri\nHi Siri\nHey Siri");
                Assert.IsEmpty(model.InputFinished(), "Priming alone is not user audio.");
                Assert.IsEmpty(model.Detect(new float[16000 * 10], 16000, "Siri\nHi Siri\nHey Siri"));
                foreach (string voice in new[] { "Samantha", "Daniel" })
                    Assert.IsEmpty(model.DetectWaveFile(Path.Combine(recordings, voice + "-negative.wav"), "Siri\nHi Siri\nHey Siri"));
                foreach (string recording in new[] { "0.wav", "1.wav" })
                    Assert.IsEmpty(model.DetectWaveFile(Path.Combine(directory, "test_wavs", recording), "Siri\nHi Siri\nHey Siri"));
            }
        }

        [Test]
        public void EnglishSiriRecordingTriggersTheDefaultWords()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_ENGLISH_KWS_MODELS");
            string audio = Environment.GetEnvironmentVariable("MNN_TEST_SIRI_WAV");
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(audio)) Assert.Ignore("Provide an English model and a Siri recording.");
            using (var model = MNNKeywordSpotter.Load(directory))
            {
                var matches = model.DetectWaveFile(audio, MNNKeywordSpotterConfig.EnglishDefaultWords);
                Assert.IsNotEmpty(matches);
                Assert.That(matches.All(match => match.Keyword == "Siri" || match.Keyword == "Hey Siri"));
                foreach (var match in matches) TestContext.WriteLine(match.Keyword + ": " + match.Confidence);
            }
        }
        [Test]
        public void EnglishOriginalRecordingsDetectTypedPhrasesAndRejectSilence()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_ENGLISH_KWS_MODELS");
            if (string.IsNullOrEmpty(directory)) Assert.Ignore("Set MNN_ENGLISH_KWS_MODELS to the prepared official GigaSpeech profile.");
            using (var model = MNNKeywordSpotter.Load(directory))
            {
                Assert.AreEqual(MNNKeywordLanguage.English, model.Language);
                var first = model.DetectWaveFile(Path.Combine(directory, "test_wavs/0.wav"), "light up");
                CollectionAssert.AreEqual(new[] { "light up" }, first.Select(item => item.Keyword));
                var second = model.DetectWaveFile(Path.Combine(directory, "test_wavs/1.wav"), "lovely child\nforever");
                CollectionAssert.AreEqual(new[] { "lovely child", "forever" }, second.Select(item => item.Keyword));
                Assert.IsEmpty(model.Detect(new float[32000], 16000, "Siri\nHey Siri"));
                Assert.Throws<ArgumentException>(() => model.Reset("你好"));
            }
        }
    }
}
