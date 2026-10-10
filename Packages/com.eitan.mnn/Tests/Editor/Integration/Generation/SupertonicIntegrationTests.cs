using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class SupertonicIntegrationTests
    {
        private static string Artifacts => Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT") ?? Path.GetFullPath("TestArtifacts~/GenerationOutputs");
        [Test]
        public void RealSpeechRepeatedCallsDisposalAndAsr()
        {
            Directory.CreateDirectory(Artifacts);
            const string phrase = "The capital of France is Paris.";
            MNNGeneratedAudio audio;
            var model = MNNSupertonic.Load(GenerationTestData.Model("supertonic-tts-mnn"));
            try
            {
                audio = model.Synthesize(phrase);
                Assert.AreEqual(44100, audio.SampleRate);
                Assert.That(audio.Waveform.Length, Is.InRange(44100, 44100 * 15));
                Assert.True(audio.Waveform.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) <= 1));
                Assert.Greater(Math.Sqrt(audio.Waveform.Select(v => (double)v * v).Average()), .005);
                var repeat = model.Synthesize(phrase);
                Assert.AreNotSame(audio.Waveform, repeat.Waveform);
                Assert.AreEqual(audio.Waveform.Length, repeat.Waveform.Length);
                Assert.Less(audio.Waveform.Zip(repeat.Waveform, (a, b) => Math.Abs(a - b)).Max(), .0001);
                var alternate = model.Synthesize("Hello world.", "F1", 5, 1, 7);
                Assert.Greater(alternate.Waveform.Length, 1000);
                Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrase, cancellationToken: new CancellationToken(true)));
            }
            finally
            {
                model.Dispose();
                model.Dispose();
            }

            Assert.Throws<ObjectDisposedException>(() => model.Synthesize(phrase));
            string wav = Path.Combine(Artifacts, "supertonic-paris.wav");
            File.WriteAllBytes(wav, MNNStudioAudio.EncodeWave(audio.Waveform, audio.SampleRate));
            using (var reload = MNNSupertonic.Load(GenerationTestData.Model("supertonic-tts-mnn")))
                Assert.Greater(reload.Synthesize("Hello.", steps: 2).Waveform.Length, 1000);
            string asrRoot = Environment.GetEnvironmentVariable("MNN_TEST_ASR_ROOT");
            Assert.That(asrRoot, Is.Not.Null.And.Not.Empty, "ASR model required for content validation.");
            string cache = Path.Combine(Artifacts, "asr-cache");
            using (var asr = MNNLlm.Load(asrRoot, cacheDirectory: cache))
            {
                var transcription = asr.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: wav, maxNewTokens: 64);
                File.WriteAllText(Path.Combine(Artifacts, "supertonic-asr.txt"), transcription.Text);
                StringAssert.Contains("paris", transcription.Text.ToLowerInvariant());
                StringAssert.Contains("france", transcription.Text.ToLowerInvariant());
            }

            if (Directory.Exists(cache))
                Directory.Delete(cache, true);
        }
    }
}
