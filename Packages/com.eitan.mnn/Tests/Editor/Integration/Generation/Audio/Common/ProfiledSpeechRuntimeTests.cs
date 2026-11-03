using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    /// <summary>Reuses local deployment assets through shared speech facades. No automatic downloads.</summary>
    public sealed class ProfiledSpeechRuntimeTests
    {
        private static string Root
        {
            get
            {
                string root = Environment.GetEnvironmentVariable("MNN_TEST_AUDIO_MODELS");
                if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set MNN_TEST_AUDIO_MODELS to reuse local speech models.");
                return root;
            }
        }
        [TestCase("mms-tts-tam-mnn", "வணக்கம்", 16000, "int8")]
        [TestCase("vits-tts-mnn", "வணக்கம் உலகம்.", 24000, "")]
        [TestCase("Pocket-TTS-MNN", "Hello, how are you?", 24000, "")]
        public void SynthesisProfileProducesFiniteNonemptyAudio(string name, string text, int rate, string precision)
        {
            string directory = Path.Combine(Root, name);
            var options = new MNNSpeechSynthesisOptions { Precision = precision,
                ReferenceWavePath = name == "Pocket-TTS-MNN" ? Path.Combine(directory, "reference_sample.wav") : null };
            using (var model = MNNSpeechSynthesizer.Load(directory, options, 4, MNNBackendType.CPU))
            {
                Assert.That(model.SampleRate, Is.EqualTo(rate));
                if (name == "vits-tts-mnn") CollectionAssert.AreEqual(new[] { "ta" }, model.Languages);
                var result = model.Synthesize(text);
                Assert.That(result.SampleRate, Is.EqualTo(rate));
                Assert.That(result.Waveform.Length, Is.GreaterThan(rate / 10));
                Assert.That(result.Waveform.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
                Assert.That(result.Waveform.Any(value => Math.Abs(value) > .0001), Is.True);
            }
        }
        [TestCase("IndexTTS-2.5-mnn", 22050)]
        [TestCase("fireredtts3-mnn", 24000)]
        [TestCase("IndicF5-MNN", 24000)]
        [TestCase("DhVaani", 24000)]
        [TestCase("CosyVoice-300M-MNN", 22050)]
        public void AdditionalSynthesisProfilesExecuteTheirReferenceContract(string name, int rate)
        {
            string prompt = Environment.GetEnvironmentVariable("MNN_TEST_REFERENCE_WAV");
            string transcript = Environment.GetEnvironmentVariable("MNN_TEST_REFERENCE_TEXT");
            if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(transcript))
                Assert.Ignore("Provide a local reference WAV and its accurate transcript.");
            var options = new MNNSpeechSynthesisOptions { Language = "en", ReferenceWavePath = prompt,
                ReferenceText = transcript, Steps = 8 };
            using (var model = MNNSpeechSynthesizer.Load(Path.Combine(Root, name), options, 4, MNNBackendType.CPU))
            {
                Assert.That(model.SampleRate, Is.EqualTo(rate));
                var audio = model.Synthesize("The weather is sunny today.");
                Assert.That(audio.SampleRate, Is.EqualTo(rate));
                Assert.That(audio.Waveform.Length, Is.GreaterThan(rate / 10));
                Assert.That(audio.Waveform.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
                Assert.That(audio.Waveform.Any(value => Math.Abs(value) > .0001), Is.True);
                TestContext.WriteLine(name + " generated " + audio.Waveform.Length + " samples.");
            }
        }

        [TestCase("rap20171201--sherpa-mnn-whisper-tiny.en")]
        [TestCase("sherpa-mnn-streaming-zipformer-en-2023-02-21")]
        public void RecognitionProfileTranscribesKnownLocalSpeech(string name)
        {
            string root = Root;
            string wave = Environment.GetEnvironmentVariable("MNN_TEST_PROFILED_SPEECH_WAV");
            if (string.IsNullOrWhiteSpace(wave)) Assert.Ignore("Set MNN_TEST_PROFILED_SPEECH_WAV to a spoken English fixture.");
            var audio = MNNWaveAudio.Read(wave, out int rate);
            using (var model = MNNSpeechRecognizer.Load(Path.Combine(root, name), 4, MNNBackendType.CPU, "en"))
            {
                string transcript = model.Transcribe(audio, rate);
                Assert.That(transcript, Is.Not.Empty, "A speech fixture must yield actual recognized text.");
                string expected = Environment.GetEnvironmentVariable("MNN_TEST_PROFILED_SPEECH_WORD");
                if (!string.IsNullOrWhiteSpace(expected)) StringAssert.Contains(expected.ToLowerInvariant(), transcript.ToLowerInvariant());
                TestContext.WriteLine(name + ": " + transcript);
            }
        }
    }
}
