using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Reflection;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [TestFixture, Category("Integration"), Category("KWS")]
    public class KeywordSpotterIntegrationTests
    {
        private string Models => Environment.GetEnvironmentVariable("MNN_TEST_KWS_MODELS");
        [SetUp]
        public void RequireModels()
        {
            if (Models == null) Assert.Ignore("Set MNN_TEST_KWS_MODELS to the prepared Wenetspeech model folder. Tests do not download assets.");
        }

        [Test]
        public void OrdinaryWakeWordsMatchPublishedSpecificationsAndRecoverAfterInvalidInput()
        {
            string assets = MNNKwsModel.AssetDirectory(Models);
            string specification = File.ReadAllText(Path.Combine(assets, "test_wavs/test_keywords.txt"));
            const string words = "文森特卡索\n周望军\n朱丽楠\n蒋友伯\n女儿\n法国\n见面会\n落实";
            using (var spotter = MNNKeywordSpotter.Load(Models))
            {
                string audio = Path.Combine(assets, "test_wavs/3.wav");
                var expected = spotter.DetectWaveFile(audio, specification);
                var actual = spotter.DetectWaveFile(audio, words);
                CollectionAssert.AreEqual(new[] { "文森特卡索", "法国" }, actual.Select(match => match.Keyword));
                Assert.AreEqual(MNNStudioNativeBackend.FormatKeywords(expected), MNNStudioNativeBackend.FormatKeywords(actual));
                Assert.AreEqual(MNNKeywordLanguage.Chinese, spotter.Language);
                Assert.Throws<ArgumentException>(() => spotter.Reset("hello assistant"));
                spotter.Reset(words);
                Assert.IsEmpty(spotter.AcceptWaveform(new float[16000]));
                Assert.AreEqual(MNNStudioNativeBackend.FormatKeywords(expected),
                    MNNStudioNativeBackend.FormatKeywords(spotter.DetectWaveFile(audio, words)));
            }
        }

        [Test]
        public void PublishedRecordingsDetectKeywordsAndRejectSilenceAndUnrelatedWords()
        {
            MNNKwsModel.Prepare(Models);
            string assets = MNNKwsModel.AssetDirectory(Models);
            string keywords = File.ReadAllText(Path.Combine(assets, "test_wavs/test_keywords.txt"));
            using (var spotter = MNNKeywordSpotter.Load(Models))
            {
                string[][] expected = {Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                    new[] {"文森特卡索", "法国"}, new[] {"蒋友伯", "女儿"}, new[] {"周望军", "落实"}, new[] {"朱丽楠", "见面会"}};
                for (int i = 0; i < 7; ++i)
                {
                    var matches = spotter.DetectWaveFile(Path.Combine(assets, "test_wavs/" + i + ".wav"), keywords);
                    TestContext.WriteLine(i + ": " + MNNStudioNativeBackend.FormatKeywords(matches));
                    CollectionAssert.AreEqual(expected[i], matches.Select(match => match.Keyword), "Published recording " + i);
                    Assert.True(matches.All(match => match.Confidence >= .25f && match.Confidence <= 1 && match.StartSeconds >= 0 && match.EndSeconds > match.StartSeconds));
                }
                Assert.IsEmpty(spotter.Detect(new float[32000], 16000, keywords));
                Assert.IsEmpty(spotter.DetectWaveFile(Path.Combine(assets, "test_wavs/3.wav"), MNNKwsModel.DefaultKeywords), "An unrelated recording must not trigger common wake phrases.");
            }
        }

        [UnityTest]
        public IEnumerator StudioWindowLoadsDemoRunsWorkerAndDisplaysKeywordResults() => RunStudioKeywordDemo(false);

        [UnityTest]
        public IEnumerator StudioWindowLoadsEnglishDemoAndDisplaysKeywordResults() => RunStudioKeywordDemo(true);

        private IEnumerator RunStudioKeywordDemo(bool english)
        {
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            object Invoke(string name) => typeof(MNNChatStudio).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
            string directory = english ? Environment.GetEnvironmentVariable("MNN_ENGLISH_KWS_MODELS") : Models;
            if (directory == null) Assert.Ignore("Provide the prepared model for this language.");
            var model = MNNStudioModel.Read(directory);
            string assets = MNNKwsModel.AssetDirectory(directory);
            LogAssert.Expect(LogType.Log, "[MNN] Loaded model from: " + Path.Combine(assets, MNNKwsModel.Encoder));
            LogAssert.Expect(LogType.Log, "[MNN] Created session: requested=CPU, actual=CPU, threads=2");
            LogAssert.Expect(LogType.Log, "[MNN] Loaded model from: " + Path.Combine(assets, MNNKwsModel.Joiner));
            LogAssert.Expect(LogType.Log, "[MNN] Created session: requested=CPU, actual=CPU, threads=2");
            var session = new MNNStudioSession(MNNStudioNativeBackend.Load(model, 2, false));
            try
            {
                Field("_available").SetValue(window, new List<MNNStudioModel> {model});
                Field("_modelNames").SetValue(window, new[] {model.DisplayName});
                Field("_modelIndex").SetValue(window, 0);
                Field("_taskIndex").SetValue(window, (int)MNNStudioTask.KeywordSpotting);
                Field("_query").SetValue(window, "");
                Field("_audioPath").SetValue(window, null);
                Field("_session").SetValue(window, session);
                Field("_autoTitle").SetValue(window, false);
                Assert.False((bool)Invoke("CanRunTask"));
                Invoke("LoadKeywordDemo");
                Assert.True((bool)Invoke("CanRunTask"));
                session.Completed += () => Invoke("OnCompleted");
                // Render the native IMGUI workspace at two supported editor sizes.
                window.position = new Rect(60, 60, 980, 820);
                window.ShowUtility();
                yield return null;
                window.Repaint();
                yield return null;
                Invoke("RunTask");
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (session.Busy && DateTime.UtcNow < deadline) { MNNStudioJobs.Update(); yield return null; }
                Assert.False(session.Busy);
                Assert.IsNull(session.Error);
                var result = (MNNStudioResult)Field("_retrieval").GetValue(window);
                Assert.That(result.Text, Does.Contain(english ? "light up" : "文森特卡索"));
                Assert.AreEqual(english ? 1 : 2, result.KeywordDetections.Length);
                Invoke("StopKeywordListening");
                Assert.That(result.Text, Does.Contain(english ? "light up" : "法国"), "Stopping an inactive listener must preserve the recording result.");
                Assert.IsEmpty((List<MNNStudioMessage>)Field("_messages").GetValue(window));
                window.position = new Rect(60, 60, 660, 620);
                window.Repaint();
                yield return null;
                LogAssert.NoUnexpectedReceived();
            }
            finally { UnityEngine.Object.DestroyImmediate(window); session.Dispose(); }
        }

        [Test]
        public void StreamingAcrossIrregularBlocksMatchesWholeRecordingAndResetRecovers()
        {
            string assets = MNNKwsModel.AssetDirectory(Models);
            string keywords = File.ReadAllText(Path.Combine(assets, "test_wavs/test_keywords.txt"));
            var pcm = MNNWaveAudio.Read(Path.Combine(assets, "test_wavs/3.wav"), out int rate);
            Assert.AreEqual(16000, rate);
            using (var spotter = MNNKeywordSpotter.Load(Models))
            {
                var expected = spotter.Detect(pcm, rate, keywords);
                Assert.IsNotEmpty(expected);
                spotter.Reset(keywords);
                var actual = new List<MNNKeywordDetection>();
                int offset = 0, step = 0;
                int[] blocks = {1, 173, 997, 31, 640, 1201, 257};
                while (offset < pcm.Length)
                {
                    var block = new float[Math.Min(blocks[step++ % blocks.Length], pcm.Length - offset)];
                    Array.Copy(pcm, offset, block, 0, block.Length); offset += block.Length;
                    actual.AddRange(spotter.AcceptWaveform(block));
                }
                actual.AddRange(spotter.InputFinished());
                Assert.AreEqual(MNNStudioNativeBackend.FormatKeywords(expected), MNNStudioNativeBackend.FormatKeywords(actual.ToArray()));
                Assert.IsEmpty(spotter.InputFinished());
                Assert.Throws<InvalidOperationException>(() => spotter.AcceptWaveform(new float[1]));
                spotter.Reset(keywords);
                Assert.Throws<ArgumentException>(() => spotter.AcceptWaveform(new[] {float.NaN}));
                Assert.Throws<OperationCanceledException>(() => spotter.AcceptWaveform(pcm, new CancellationToken(true)));
                Assert.AreEqual(MNNStudioNativeBackend.FormatKeywords(expected), MNNStudioNativeBackend.FormatKeywords(spotter.Detect(pcm, rate, keywords)));
            }
        }

        [Test]
        public void StudioDiscoversAndRunsRenamedProfileThroughKeywordTask()
        {
            var model = MNNStudioModel.Read(Models);
            Assert.True(model.IsKeywordSpotter);
            Assert.AreEqual(MNNStudioTask.KeywordSpotting, model.Task);
            Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
            string assets = MNNKwsModel.AssetDirectory(Models);
            using (var backend = MNNStudioNativeBackend.Load(model, 2, false))
            {
                var result = backend.Run(new MNNStudioRequest {Prompt = "Detect wake words", Audio = Path.Combine(assets, "test_wavs/3.wav"),
                    Keywords = File.ReadAllText(Path.Combine(assets, "test_wavs/test_keywords.txt"))});
                Assert.IsNotEmpty(result.KeywordDetections);
                Assert.That(result.BackendDescription, Does.Contain("keyword spotting"));
                TestContext.WriteLine(result.Text);
            }
        }

        [Test]
        public void ResamplingAsyncOwnershipAndInterruptedStreamRecover()
        {
            string assets = MNNKwsModel.AssetDirectory(Models);
            string keywords = File.ReadAllText(Path.Combine(assets, "test_wavs/test_keywords.txt"));
            var pcm = MNNWaveAudio.Read(Path.Combine(assets, "test_wavs/3.wav"), out int rate);
            using (var spotter = MNNKeywordSpotter.Load(Models))
            {
                var resampled = MNNSpeechDsp.Resample(pcm, rate, 44100, CancellationToken.None);
                var matches = spotter.Detect(resampled, 44100, keywords);
                CollectionAssert.AreEqual(new[] {"文森特卡索", "法国"}, matches.Select(match => match.Keyword));
                Assert.Throws<ArgumentOutOfRangeException>(() => spotter.Detect(pcm, 7999, keywords));
                Assert.Throws<ArgumentException>(() => spotter.Detect(new[] {float.PositiveInfinity}, 16000, keywords));
                Assert.Throws<ArgumentOutOfRangeException>(() => spotter.Reset(keywords, float.NaN));
                spotter.Reset(keywords);
                using (var cancel = new CancellationTokenSource())
                {
                    cancel.CancelAfter(20);
                    Assert.Throws<OperationCanceledException>(() => spotter.AcceptWaveform(new float[16000 * 30], cancel.Token));
                }
                Assert.Throws<InvalidOperationException>(() => spotter.AcceptWaveform(new float[160]));
                var pending = spotter.DetectAsync(pcm, rate, keywords);
                Array.Clear(pcm, 0, pcm.Length);
                CollectionAssert.AreEqual(new[] {"文森特卡索", "法国"}, pending.GetAwaiter().GetResult().Select(match => match.Keyword));
            }
        }
    }
}
