using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("Qwen3Asr")]
    public class Qwen3AsrIntegrationTests
    {
        private string Root => Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_ASR_ROOT");
        private string Model => Path.Combine(Root, MNNQwen3AsrModel.Repository.Replace("/", "--"));
        private string Wave(string name) => Path.Combine(Root, "..", "..", "Zipformer", "Audio", name);
        private void RequireModel()
        {
            if (Root == null) Assert.Ignore("Set MNN_TEST_QWEN3_ASR_ROOT with existing pinned weights; no downloads.");
            MNNQwen3AsrModel.Prepare(Model);
        }

        [Test]
        public void EnglishAndBilingualTranscriptsResetAndUseOwnedActualTokenIds()
        {
            RequireModel();
            using (var recognizer = MNNQwen3AsrRecognizer.Load(Model, cacheDirectory: Path.Combine(Root, "TestsCache"), backendType: MNNBackendType.CPU))
            {
                bool callbackChecked = false;
                var first = recognizer.RecognizeWaveFile(Wave("en-0.wav"), onProgress: update =>
                {
                    if (callbackChecked) return;
                    callbackChecked = true;
                    Assert.Throws<InvalidOperationException>(() => recognizer.Dispose());
                    Assert.Throws<InvalidOperationException>(() => recognizer.Recognize(new float[16000], 16000));
                });
                Assert.True(callbackChecked);
                AssertEnglish(first.Text);
                Assert.IsNotEmpty(first.TokenIds);
                var saved = first.TokenIds.ToArray();
                var bilingual = recognizer.RecognizeWaveFile(Wave("zh-en-0.wav"));
                Assert.AreEqual("昨天是 Monday。Today is 礼拜二。The day after tomorrow 是星期三。", bilingual.Text);
                Assert.IsEmpty(bilingual.Timestamps);
                var second = recognizer.RecognizeWaveFile(Wave("en-0.wav"));
                Assert.AreEqual(first.Text, second.Text);
                CollectionAssert.AreEqual(saved, first.TokenIds);
                CollectionAssert.AreEqual(first.TokenIds, second.TokenIds);
                Assert.AreEqual(MNNBackendType.CPU, recognizer.Backend);
                Assert.AreEqual(MNNBackendType.CPU, recognizer.MediaBackend);
                Assert.IsEmpty(Directory.GetFiles(Path.Combine(Root, "TestsCache"), "input-*.wav"));
            }
        }

        [Test]
        public void SilenceInvalidAudioAndCancellationRecoverWithoutLeakingTemporaryFiles()
        {
            RequireModel();
            using (var recognizer = MNNQwen3AsrRecognizer.Load(Model, cacheDirectory: Path.Combine(Root, "CancelCache"), backendType: MNNBackendType.CPU))
            {
                Assert.AreEqual("", recognizer.Recognize(new float[16000], 16000).Text);
                Assert.Throws<ArgumentException>(() => recognizer.Recognize(new float[1599], 16000));
                Assert.Throws<ArgumentException>(() => recognizer.Recognize(new float[480001], 16000));
                var invalid = new float[1600]; invalid[10] = float.NaN;
                Assert.Throws<ArgumentException>(() => recognizer.Recognize(invalid, 16000));
                Assert.Throws<OperationCanceledException>(() => recognizer.RecognizeWaveFile(Wave("en-0.wav"), new CancellationToken(true)));
                using (var cancellation = new CancellationTokenSource())
                {
                    int updates = 0;
                    Assert.Throws<OperationCanceledException>(() => recognizer.RecognizeWaveFile(Wave("en-0.wav"), cancellation.Token,
                        update => { ++updates; cancellation.Cancel(); }));
                    Assert.AreEqual(1, updates, "Cancellation is observed between tokens.");
                }
                Assert.IsEmpty(Directory.GetFiles(Path.Combine(Root, "CancelCache"), "input-*.wav"));
                AssertEnglish(recognizer.RecognizeWaveFile(Wave("en-0.wav")).Text);
            }
        }

        [Test]
        public void StudioRoutesPreparedAudioProfileAndReportsItsBackend()
        {
            RequireModel();
            var model = MNNStudioModel.Read(Model);
            Assert.True(model.HasSpeechRecognitionPipeline);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.SpeechRecognition).CanRun(model));
            Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var result = backend.Run(new MNNStudioRequest {Audio = Wave("zh-en-0.wav")});
                Assert.AreEqual("昨天是 Monday。Today is 礼拜二。The day after tomorrow 是星期三。", result.Text);
                Assert.Greater(result.Tokens, 0);
                var expected = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
                Assert.That(result.BackendDescription, Does.Contain("Qwen3-ASR").And.Contain(expected + " / audio " + expected));
            }
        }

        private void AssertEnglish(string text)
        {
            string reference = File.ReadAllLines(Wave("en-trans.txt"))[0].Substring(6).Trim();
            string lexical = System.Text.RegularExpressions.Regex.Replace(text.ToUpperInvariant(), "[^A-Z]+", " ").Trim();
            Assert.AreEqual(reference, lexical, "Independent upstream word sequence.");
            TestContext.WriteLine(text);
        }
    }
}
