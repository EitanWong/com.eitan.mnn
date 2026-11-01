using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [TestFixture, Category("Integration"), Category("Zipformer")]
    public class ZipformerIntegrationTests
    {
        private string Root => Environment.GetEnvironmentVariable("MNN_TEST_ASR_ROOT");
        private string Models => Environment.GetEnvironmentVariable("MNN_TEST_ASR_MODELS");

        [TestCase(false), TestCase(true)]
        public void GreedyPipelineRecognizesAudioAndResetsState(bool bilingual)
        {
            if (Root == null || Models == null) Assert.Ignore("Set MNN_TEST_ASR_ROOT and MNN_TEST_ASR_MODELS; models are never downloaded by tests.");
            string name = bilingual ? MNNZipformerModel.Bilingual : MNNZipformerModel.English;
            string wave = Path.Combine(Root, "Audio", bilingual ? "zh-en-0.wav" : "en-0.wav");
            // These upstream samples are under 15 seconds; use the existing WAV parser for this fixture only.
            var audio = MNNReferenceAudio.FromWaveFile(wave);
            using (var recognizer = MNNZipformerRecognizer.Load(Path.Combine(Models, name), MNNZipformerModel.Config(bilingual)))
            {
                var first = recognizer.Recognize(audio.Samples, audio.SampleRate);
                TestContext.WriteLine(name + ": " + first.Text);
                Assert.That(first.Text, Does.Contain(bilingual ? "昨天" : "AFTER EARLY NIGHTFALL"));
                Assert.That(first.Text, Does.Contain(bilingual ? "星期三" : "BROTHELS"));
                if (!bilingual)
                    Assert.AreEqual("AFTER EARLY NIGHTFALL THE YELLOW LAMPS WOULD LIGHT UP HERE AND THERE THE SQUALID QUARTER OF THE BROTHELS", first.Text,
                        "Exact upstream test_wavs/trans.txt reference, not just a nonempty output gate.");
                Assert.That(first.TokenIds.Count, Is.GreaterThan(10));
                Assert.That(first.TokenIds.Count, Is.EqualTo(first.Timestamps.Count));
                Assert.True(first.Timestamps.SequenceEqual(first.Timestamps.OrderBy(x => x)));
                var blank = recognizer.Recognize(new float[16000], 16000);
                Assert.AreEqual("", blank.Text, "Silence must not leak previous tokens/cache state.");
                Assert.Throws<OperationCanceledException>(() => recognizer.Recognize(audio.Samples, audio.SampleRate, new CancellationToken(true)));
                var second = recognizer.Recognize(audio.Samples, audio.SampleRate);
                Assert.AreEqual(first.Text, second.Text);
                CollectionAssert.AreEqual(first.TokenIds, second.TokenIds);
                CollectionAssert.AreEqual(first.Timestamps, second.Timestamps);
            }
        }

        [TestCase(false), TestCase(true)]
        public void PreparedProfileRunsThroughStudioWorkerBackend(bool bilingual)
        {
            if (Root == null || Models == null) Assert.Ignore("Set MNN_TEST_ASR_ROOT and MNN_TEST_ASR_MODELS.");
            string repository = "MNN/" + (bilingual ? MNNZipformerModel.Bilingual : MNNZipformerModel.English);
            string directory = Path.Combine(Models, bilingual ? MNNZipformerModel.Bilingual : MNNZipformerModel.English);
            MNNZipformerModel.Prepare(directory, repository);
            var model = MNNStudioModel.Read(directory);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.SpeechRecognition).CanRun(model));
            Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var request = new MNNStudioRequest {Prompt = "Transcribe", Audio = Path.Combine(Root, "Audio", bilingual ? "zh-en-0.wav" : "en-0.wav")};
                var result = backend.Run(request);
                Assert.That(result.Text, Does.Contain(bilingual ? "星期三" : "BROTHELS"));
                Assert.That(result.BackendDescription, Does.Contain("Zipformer"));
            }
        }

        [TestCase("en"), TestCase("zh-en")]
        public void FeaturesMatchOfficialKaldiNativeFbankReference(string language)
        {
            if (Root == null) Assert.Ignore("Set MNN_TEST_ASR_ROOT with the explicit offline reference fixtures.");
            var audio = MNNReferenceAudio.FromWaveFile(Path.Combine(Root, "Audio", language + "-0.wav"));
            var pcm = (float[])audio.Samples.Clone();
            Array.Resize(ref pcm, pcm.Length + 16000);
            var features = MNNZipformerFeatures.Extract(pcm, out int frames, CancellationToken.None);
            var reference = File.ReadAllBytes(Path.Combine(Root, language + "-knf.f32"));
            Assert.AreEqual(frames * 80 * 4, reference.Length);
            double total = 0, max = 0;
            for (int i = 0; i < features.Length; ++i)
            {
                double error = Math.Abs(features[i] - BitConverter.ToSingle(reference, i * 4));
                total += error; max = Math.Max(max, error);
            }
            Assert.Less(max, .0003);
            Assert.Less(total / features.Length, .00001);
        }
    }
}
