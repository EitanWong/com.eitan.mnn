using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [TestFixture, Category("Integration"), Category("SenseVoice")]
    public class SenseVoiceIntegrationTests
    {
        [Test]
        public void GreedyOutputMatchesTheOriginalLogitsOnConsecutiveShapes()
        {
            if (Root == null) Assert.Ignore("Set MNN_TEST_SENSEVOICE_ROOT; no downloads.");
            string path = Path.Combine(Models(false), "model.mnn");
            byte[] original = File.ReadAllBytes(path);
            using (var reference = new MNNGenerationGraph(path, 1, backendType: MNNBackendType.CPU))
            using (var reduced = new MNNGenerationGraph(path, 1, MNNSenseVoiceGraph.WithGreedyOutput(original), backendType: MNNBackendType.CPU))
            {
                foreach (string name in new[] { "en", "zh-en", "en" })
                {
                    var audio = MNNWaveAudio.Read(Wave(name), out _);
                    var features = MNNSenseVoiceFeatures.Extract(audio, out int frames, default);
                    var inputs = new[] {
                        new MNNGenerationGraph.Input("speech", features, 1, frames, 560),
                        new MNNGenerationGraph.Input("speech_lengths", new[] { frames }, 1),
                        new MNNGenerationGraph.Input("language", new[] { 0 }, 1),
                        new MNNGenerationGraph.Input("text_norm", new[] { 14 }, 1) };
                    var logits = reference.RunOutputs(new[] { "logits" }, inputs)[0].Values;
                    var result = reduced.RunOutputs(new[] { MNNSenseVoiceGraph.OutputName }, inputs)[0];
                    Assert.AreEqual(typeof(int[]), result.Data.GetType());
                    var ids = (int[])result.Data;
                    Assert.AreEqual(frames + 4, ids.Length);
                    for (int f = 0; f < ids.Length; ++f)
                    {
                        int best = 0;
                        for (int k = 1; k < 25055; ++k)
                            if (logits[f * 25055 + k] > logits[f * 25055 + best]) best = k;
                        Assert.AreEqual(best, ids[f], name + " frame " + f);
                    }
                }
            }
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path), "The installed model remains unchanged.");
        }

        private string Root => Environment.GetEnvironmentVariable("MNN_TEST_SENSEVOICE_ROOT");
        private string Models(bool huggingFace) => Root == null ? null : Path.Combine(Root, huggingFace ? "xinliu--sensevoice_mnn" : "wo105090--SenseVoiceSmall-MNN");
        private string Wave(string name) => Path.Combine(Root, "..", "Zipformer", "Audio", name + "-0.wav");

        [TestCase("en"), TestCase("zh-en")]
        public void FeaturesMatchKaldiNativeFbankAndModelCmvn(string name)
        {
            if (Root == null) Assert.Ignore("Set MNN_TEST_SENSEVOICE_ROOT; no models are downloaded by tests.");
            var audio = MNNWaveAudio.Read(Wave(name), out int rate);
            Assert.AreEqual(16000, rate);
            var features = MNNSenseVoiceFeatures.Extract(audio, out int frames, CancellationToken.None);
            var reference = File.ReadAllBytes(Path.Combine(Root, name + "-sensevoice-training.f32"));
            Assert.AreEqual(frames * 560 * 4, reference.Length);
            double total = 0, max = 0;
            for (int i = 0; i < features.Length; ++i)
            {
                double error = Math.Abs(features[i] - BitConverter.ToSingle(reference, i * 4));
                total += error; max = Math.Max(max, error);
            }
            TestContext.WriteLine("feature max=" + max + " mean=" + total / features.Length);
            Assert.Less(max, .0003);
            Assert.Less(total / features.Length, .00001);
        }

        [TestCase(false), TestCase(true)]
        public void CpuRecognizesReferenceAndResetsBetweenRequests(bool huggingFace)
        {
            if (Root == null) Assert.Ignore("Set MNN_TEST_SENSEVOICE_ROOT.");
            using (var recognizer = MNNSenseVoiceRecognizer.Load(Models(huggingFace), MNNSenseVoiceModel.Config(huggingFace)))
            {
                Assert.AreEqual(MNNBackendType.CPU, recognizer.Backend);
                var first = recognizer.RecognizeWaveFile(Wave("en"));
                TestContext.WriteLine(first.Text);
                Assert.AreEqual("After early nightfall, the yellow lamps would light up here and there. The squalid quarter of the brothels.", first.Text);
                Assert.AreEqual(first.TokenIds.Count, first.Timestamps.Count);
                Assert.True(first.Timestamps.SequenceEqual(first.Timestamps.OrderBy(t => t)));
                Assert.AreEqual("", recognizer.Recognize(new float[16000], 16000).Text);
                Assert.Throws<OperationCanceledException>(() => recognizer.RecognizeWaveFile(Wave("en"), new CancellationToken(true)));
                var mixed = recognizer.RecognizeWaveFile(Wave("zh-en"));
                TestContext.WriteLine(mixed.Text);
                Assert.That(mixed.Text, Does.Contain("昨天"));
                Assert.That(mixed.Text, Does.Contain("星期三"));
                var second = recognizer.RecognizeWaveFile(Wave("en"));
                Assert.AreEqual(first.Text, second.Text);
                CollectionAssert.AreEqual(first.TokenIds, second.TokenIds);
                CollectionAssert.AreEqual(first.Timestamps, second.Timestamps);
            }
        }

        [TestCase(false), TestCase(true)]
        public void DownloadPreparationMakesStudioBackendRunnable(bool huggingFace)
        {
            if (Root == null) Assert.Ignore("Set MNN_TEST_SENSEVOICE_ROOT.");
            MNNSenseVoiceModel.Prepare(Models(huggingFace), huggingFace);
            var model = MNNStudioModel.Read(Models(huggingFace));
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.SpeechRecognition).CanRun(model));
            Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var result = backend.Run(new MNNStudioRequest {Prompt = "Transcribe", Audio = Wave("en")});
                Assert.That(result.Text, Does.Contain("brothels"));
                Assert.That(result.BackendDescription, Does.Contain("SenseVoiceSmall"));
            }
        }
    }
}
