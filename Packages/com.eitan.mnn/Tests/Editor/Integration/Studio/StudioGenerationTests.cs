using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class StudioGenerationTests
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Get(MNNChatStudio w, string name) => typeof(MNNChatStudio).GetField(name, Flags).GetValue(w);
        private static void Set(MNNChatStudio w, string name, object value) => typeof(MNNChatStudio).GetField(name, Flags).SetValue(w, value);
        private static object Invoke(MNNChatStudio w, string name, params object[] args) => typeof(MNNChatStudio).GetMethod(name, Flags).Invoke(w, args);

        [Test]
        public void DownloadedTtsModelsAreDiscoverableFromStreamingAssetsWithoutDownloadingAgain()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_LOCAL_MODEL_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            var errors = new List<string>();
            var models = MNNStudioModel.Discover(new[]{root}, errors);
            foreach (string name in new[]{"supertonic-tts-mnn", "Supertonic2", "Supertonic-3-INT8-MNN", "piper-voices-MNN", "PiperLessac",
                "Pocket-TTS-MNN", "IndicF5-MNN", "bert-vits2-MNN", "Cosy-Voice-MNN", "IndexTTS-2.5-mnn", "fireredtts3-mnn", "DhVaani", "Qwen3-TTS-Explicit",
                "MossNano", "MossNanoCached", "Kokoro-82M-v1.0-MNN", "kokoro-82m-mnn", "kokoro-multi-lang-v1_0-MNN",
                "kokoro-v1_1", "starsharp06sharp--kokoro-v1_1", "Jeryuk--kokoro-v1_0-MNN", "vits-tts-mnn", "mms-tts-tam-mnn", "sherpa-vits-zh-ll"})
            {
                var model = models.Find(value => value.Directory == Path.Combine(root, name));
                Assert.IsNotNull(model, name + ": " + string.Join("; ", errors));
                Assert.AreEqual(MNNStudioTask.SpeechSynthesis, model.Task, name);
                Assert.IsTrue(model.HasGenerationPipeline, name);
                Assert.IsTrue(MNNStudioTasks.Get((int)model.Task).CanRun(model), name);
            }
            foreach (string name in new[]{"Qwen3-TTS-INT8", "Qwen3-TTS-FP16"})
                Assert.IsFalse(MNNStudioModel.Read(Path.Combine(root, name)).HasGenerationPipeline, name + " remains an unvalidated Omni export.");
        }

        [UnityTest, Explicit("Requires local IndexTTS / FireRed model manifests and a reference WAV; uses a substitute backend.")]
        public IEnumerator ReferencePickerSurvivesModelRefreshAndEnablesSynthesis()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_REFERENCE_SPEECH_ROOT");
            string reference = Environment.GetEnvironmentVariable("MNN_TEST_REFERENCE_WAV");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            Assert.DoesNotThrow(() => MNNReferenceAudio.FromWaveFile(reference));
            foreach (string repository in new[]{"IndexTTS-2.5-mnn", "fireredtts3-mnn"})
            {
                var model = MNNStudioModel.Read(Path.Combine(root, repository));
                var window = StudioWindowFixture.Create<MNNChatStudio>();
                var backend = new ReferencePickerBackend();
                var session = new MNNStudioSession(backend);
                Action scheduled = null;
                string clipboard = EditorGUIUtility.systemCopyBuffer;
                try
                {
                    Set(window, "_autoTitle", false);
                    Set(window, "_taskIndex", (int)MNNStudioTask.SpeechSynthesis);
                    Set(window, "_available", new List<MNNStudioModel>{model});
                    Set(window, "_modelIndex", 0);
                    Set(window, "_query", "");
                    Set(window, "_dialogSchedule", new Action<Action>(action => scheduled = action));
                    Set(window, "_attachmentPicker", new Func<string, string, string, string>((title, directory, extension) =>
                    {
                        // Rediscovery creates new model objects for the same folder
                        // while the native dialog is pumping editor events.
                        Set(window, "_available", new List<MNNStudioModel>{MNNStudioModel.Read(model.Directory)});
                        window.SendEvent(new Event{type = EventType.Repaint});
                        return reference;
                    }));
                    Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.SpeechSynthesis).CanRun(model));
                    Assert.False((bool)Invoke(window, "CanRunTask"), "A missing reference must block synthesis.");
                    window.position = new Rect(60, 60, 1000, 900);
                    window.Show();
                    for (int frame = 0; frame < 5; ++frame)
                        yield return null;
                    ClickScreenRect(window, (Rect)Get(window, "_taskAudioAttachmentScreenRect"));
                    Assert.NotNull(scheduled);
                    scheduled();
                    Assert.AreEqual(reference, Get(window, "_audioPath"), repository + ": model rediscovery must preserve the picked WAV.");
                    Assert.IsNull(Get(window, "_error"));
                    if (model.IsFireRedTts)
                    {
                        Assert.False((bool)Invoke(window, "CanRunTask"), "FireRed needs the reference transcript too.");
                        StringAssert.Contains("transcript", (string)Invoke(window, "GetTaskRunBlockedReason"));
                        window.Focus();
                        for (int frame = 0; frame < 3; ++frame)
                        {
                            window.Repaint();
                            yield return null;
                        }
                        var transcriptRect = (Rect)Get(window, "_referenceTranscriptScreenRect");
                        Assert.GreaterOrEqual(transcriptRect.yMin, window.position.yMin);
                        Assert.LessOrEqual(transcriptRect.yMax, window.position.yMax, "The reference transcript must be visible in the synthesis form.");
                        ClickScreenRect(window, transcriptRect);
                        EditorGUIUtility.systemCopyBuffer = "你好，欢迎使用语音合成。";
                        window.SendEvent(new Event{type = EventType.ExecuteCommand, commandName = "Paste"});
                        Assert.AreEqual("你好，欢迎使用语音合成。", Get(window, "_referenceText"), "The visible transcript field must accept text input.");
                    }
                    Assert.True((bool)Invoke(window, "CanRunTask"), repository + ": valid inputs must enable Run.");
                    Set(window, "_session", session);
                    for (int frame = 0; frame < 3; ++frame)
                    {
                        window.Repaint();
                        yield return null;
                    }
                    ClickScreenRect(window, (Rect)Get(window, "_taskRunScreenRect"));
                    for (int frame = 0; frame < 60 && session.Busy; ++frame)
                    {
                        MNNStudioJobs.Update();
                        yield return null;
                    }
                    Assert.NotNull(backend.Request, repository + ": Run must start the synthesis request.");
                    Assert.AreEqual(reference, backend.Request.Audio);
                    Assert.AreEqual("你好，欢迎使用语音合成。", backend.Request.Prompt);
                    if (model.IsFireRedTts)
                        Assert.AreEqual("你好，欢迎使用语音合成。", backend.Request.ReferenceText);
                    LogAssert.NoUnexpectedReceived();
                }
                finally
                {
                    EditorGUIUtility.systemCopyBuffer = clipboard;
                    UnityEngine.Object.DestroyImmediate(window);
                    session.Dispose();
                }
            }
        }

        private sealed class ReferencePickerBackend : IMNNStudioBackend
        {
            internal MNNStudioRequest Request;
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                Request = request;
                return new MNNStudioResult();
            }

            public void Dispose() { }
        }

        [UnityTest]
        public IEnumerator UseExampleReplacesFocusedInstructionAndRemainsEditable()
        {
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            string clipboard = EditorGUIUtility.systemCopyBuffer;
            try
            {
                Set(window, "_autoTitle", false);
                Set(window, "_taskIndex", (int)MNNStudioTask.SpeechSynthesis);
                Set(window, "_available", new List<MNNStudioModel>{MNNStudioModel.Read(GenerationTestData.Model("supertonic-tts-mnn"))});
                Set(window, "_modelIndex", 0);
                window.position = new Rect(60, 60, 1000, 900);
                window.Show();
                window.Focus();
                for (int i = 0; i < 5; ++i)
                    yield return null;
                foreach (string draft in new[]{"", "Replace this draft."})
                {
                    Set(window, "_query", draft);
                    window.Repaint();
                    yield return null;
                    ClickScreenRect(window, (Rect)Get(window, "_taskInputScreenRect"));
                    window.SendEvent(new Event{type = EventType.KeyDown, character = 'x', keyCode = KeyCode.X});
                    Assert.That((string)Get(window, "_query"), Does.Contain("x"));
                    ClickScreenRect(window, (Rect)Get(window, "_taskExampleScreenRect"));
                    string expected = MNNStudioTasks.Get((int)MNNStudioTask.SpeechSynthesis).Example;
                    for (int i = 0; i < 4; ++i)
                    {
                        window.Repaint();
                        yield return null;
                        Assert.AreEqual(expected, Get(window, "_query"));
                    }
                    ClickScreenRect(window, (Rect)Get(window, "_taskInputScreenRect"));
                    window.SendEvent(new Event{type = EventType.ExecuteCommand, commandName = "SelectAll"});
                    window.SendEvent(new Event{type = EventType.ExecuteCommand, commandName = "Copy"});
                    Assert.AreEqual(expected, EditorGUIUtility.systemCopyBuffer, "The visible editor buffer must contain the example too.");
                    window.SendEvent(new Event{type = EventType.KeyDown, character = 'z', keyCode = KeyCode.Z});
                    Assert.AreEqual("z", Get(window, "_query"), "Typing must replace the selected example text.");
                    window.Repaint();
                    yield return null;
                    Assert.AreEqual("z", Get(window, "_query"));
                }
            }
            finally
            {
                EditorGUIUtility.systemCopyBuffer = clipboard;
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void ClickScreenRect(MNNChatStudio window, Rect screenRect)
        {
            Assert.Greater(screenRect.width, 0);
            var click = screenRect.center - window.position.position;
            window.SendEvent(new Event{type = EventType.MouseDown, button = 0, mousePosition = click});
            window.SendEvent(new Event{type = EventType.MouseUp, button = 0, mousePosition = click});
        }

        [UnityTest]
        public IEnumerator SupertonicWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true);
        [UnityTest]
        public IEnumerator SupertonicLongTextWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, longSpeech: true);
        [UnityTest]
        public IEnumerator DiffusionWorkspaceGeneratesDisplaysAndSavesActualImage() => Run(false);
        [UnityTest]
        public IEnumerator SanaWorkspaceEditsDisplaysAndSavesActualImage() => Run(false, true);
        [UnityTest]
        public IEnumerator PiperWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "piper-voices-MNN");
        [UnityTest]
        public IEnumerator BertVits2WorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "bert-vits2-MNN");
        [UnityTest, Explicit("Requires the complete local CosyVoice release.")]
        public IEnumerator CosyVoiceWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "Cosy-Voice-MNN");
        [UnityTest, Explicit("Requires the complete local CosyVoice release.")]
        public IEnumerator CosyVoiceEnglishWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "Cosy-Voice-MNN",
            inputText: "Every standard statement in English places the doer first, the action second, and the receiver third");
        [UnityTest, Explicit("Requires pinned local IndexTTS weights and reference WAV.")]
        public IEnumerator IndexTtsWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "IndexTTS-2.5-mnn");
        [UnityTest, Explicit("Requires pinned local FireRed weights and reference WAV.")]
        public IEnumerator FireRedWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "fireredtts3-mnn");
        [UnityTest]
        public IEnumerator KokoroWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "Kokoro-82M-v1.0-MNN", inputText: "你好，这是语音合成测试。");
        [UnityTest]
        public IEnumerator Kokoro11WorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "kokoro-v1_1", inputText: "The capital of France is Paris.");
        [UnityTest]
        public IEnumerator Supertonic3WorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "Supertonic-3-INT8-MNN");
        [UnityTest]
        public IEnumerator IndicVitsWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "vits-tts-mnn", inputText: MNNIndicVits.SpeakerExamples[18]);
        [UnityTest]
        public IEnumerator MmsTamilWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "mms-tts-tam-mnn", inputText: "வணக்கம் உலகம். இது ஒரு குரல் தொகுப்பு சோதனை.");
        [UnityTest]
        public IEnumerator MmsTamilFp16Int8WorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "mms-tts-tam-mnn", inputText: "வணக்கம் உலகம். இது ஒரு குரல் தொகுப்பு சோதனை.", ttsPrecision: "fp16_int8");
        [UnityTest]
        public IEnumerator MmsTamilFp32WorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "mms-tts-tam-mnn", inputText: "வணக்கம் உலகம். இது ஒரு குரல் தொகுப்பு சோதனை.", ttsPrecision: "fp32");
        [UnityTest]
        public IEnumerator SherpaVitsChineseWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "sherpa-vits-zh-ll", inputText: "你好，这是语音合成测试。");
        [UnityTest]
        public IEnumerator MossNanoWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "MossNano", inputText: "你好，这是语音合成测试。");

        [UnityTest]
        public IEnumerator MossNanoCachedWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "MossNanoCached", inputText: "你好，这是语音合成测试。");

        [UnityTest]
        public IEnumerator DhVaaniWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "DhVaani", inputText: "The capital of France is Paris.");

        [UnityTest]
        public IEnumerator Qwen3ExplicitWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "Qwen3-TTS-Explicit", inputText: "你好，这是语音合成测试。");

        [UnityTest]
        public IEnumerator PiperLessacWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "PiperLessac");

        [UnityTest]
        public IEnumerator KokoroSingleWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "Jeryuk--kokoro-v1_0-MNN", inputText: "你好，这是语音合成测试。");

        [UnityTest]
        public IEnumerator KokoroExpandedWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "starsharp06sharp--kokoro-v1_1", inputText: "你好，这是语音合成测试。");

        [UnityTest]
        public IEnumerator IndicF5WorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "IndicF5-MNN", inputText: "नमस्ते, यह आवाज़ का परीक्षण है।");

        [UnityTest]
        public IEnumerator PocketWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "Pocket-TTS-MNN", inputText: "The capital of France is Paris.");

        [UnityTest]
        public IEnumerator Qwen3ExplicitEnglishWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true,
            repository: "Qwen3-TTS-Explicit", inputText: "Paris is the capital of France.", speechLanguage: 1);

        private static IEnumerator Run(bool tts, bool sana = false, string repository = null, bool longSpeech = false, string inputText = null, string ttsPrecision = null, int speechLanguage = 0)
        {
            string artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT") ?? Path.GetFullPath("TestArtifacts~/StudioGeneration");
            Directory.CreateDirectory(artifacts);
            var model = MNNStudioModel.Read((repository == "IndicF5-MNN" || repository == "Pocket-TTS-MNN" || repository == "starsharp06sharp--kokoro-v1_1" || repository == "Jeryuk--kokoro-v1_0-MNN" || repository == "PiperLessac" || repository == "MossNano" || repository == "MossNanoCached" || repository == "DhVaani" || repository == "Qwen3-TTS-Explicit") ? Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_TTS_ROOT"), repository) : repository == "sherpa-vits-zh-ll" ? Environment.GetEnvironmentVariable("MNN_TEST_SHERPA_VITS_ROOT") : repository == "mms-tts-tam-mnn" ? Environment.GetEnvironmentVariable("MNN_TEST_MMS_TAMIL_ROOT") : repository == "kokoro-v1_1" ? Environment.GetEnvironmentVariable("MNN_TEST_KOKORO11_ROOT") : repository == "vits-tts-mnn" ? Environment.GetEnvironmentVariable("MNN_TEST_INDIC_VITS_ROOT") : repository == "Kokoro-82M-v1.0-MNN" ? Environment.GetEnvironmentVariable("MNN_TEST_KOKORO_ROOT") : repository == "Supertonic-3-INT8-MNN" ? Environment.GetEnvironmentVariable("MNN_TEST_SUPERTONIC3_ROOT") : repository == "IndexTTS-2.5-mnn" || repository == "fireredtts3-mnn" ? Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_REFERENCE_SPEECH_ROOT"), repository) : repository == "Cosy-Voice-MNN" ? Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE_ROOT") : GenerationTestData.Model(repository ?? (tts ? "supertonic-tts-mnn" : sana ? "MNN-Sana-Edit-V2" : "stable-diffusion-v1-5-mnn")));
            if (ttsPrecision != null) model.MmsTamilPrecision = ttsPrecision;
            Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            bool muted = EditorUtility.audioMasterMute;
            try
            {
                EditorUtility.audioMasterMute = false;
                Set(window, "_autoTitle", false);
                Set(window, "_historyPath", Path.Combine(artifacts, "studio-test-history.json"));
                Set(window, "_taskIndex", (int)model.Task);
                Set(window, "_available", new List<MNNStudioModel>{model});
                Set(window, "_modelIndex", 0);
                Set(window, "_query", (model.IsBertVits2 || model.IsCosyVoice || model.RequiresReferenceAudio) ? "你好，欢迎使用语音合成。" : tts ? "The capital of France is Paris." : sana ? "A bright blue bicycle parked beside a white wall, replace all red bicycle paint with blue paint." : "A red bicycle parked beside a white wall, photograph, daylight");
                if (inputText != null)
                    Set(window, "_query", inputText);
                Set(window, "_speechLanguage", speechLanguage);
                if (model.TtsProfile == MNNTtsModelProfile.IndicVits)
                    Set(window, "_ttsVoice", 18);
                if (model.RequiresReferenceAudio)
                {
                    // Missing reference must be rejected before a backend is started.
                    Set(window, "_audioPath", null);
                    Invoke(window, "RunTask"); Assert.IsNull(Get(window, "_session"));
                    Set(window, "_audioPath", Environment.GetEnvironmentVariable("MNN_TEST_REFERENCE_WAV"));
                    if (model.IsIndicF5)
                        Set(window, "_audioPath", Path.Combine(model.Directory, "samples", "hi_short_mnn_vocos_fp32.wav"));
                    if (model.IsPocket)
                        Set(window, "_audioPath", Path.Combine(model.Directory, "reference_sample.wav"));
                    if (model.IsDhVaani)
                        Set(window, "_audioPath", Path.Combine(artifacts, "moss-nano-cached-en.wav"));
                    if (model.RequiresReferenceTranscript)
                    {
                        Set(window, "_referenceText", ""); Invoke(window, "RunTask"); Assert.IsNull(Get(window, "_session"));
                        Set(window, "_referenceText", model.IsIndicF5 ? "नमस्ते, आप कैसे हैं?" : model.IsDhVaani ? "Paris is the capital of France." : "你好，欢迎使用语音合成。");
                    }
                }
                if (longSpeech)
                {
                    Set(window, "_ttsVoice", Array.IndexOf(model.TtsVoices, "F2"));
                    Set(window, "_query", "On Saturday morning, the family walked to the market beside the river. They bought fresh apples, warm bread, and milk. The price was $12.50, or 25% off. After lunch, the children read a story about a train traveling through the mountains. The capital of France is Paris.");
                }
                if (sana)
                    Set(window, "_imagePath", Path.Combine(artifacts, "sd15-red-bicycle.png"));
                Set(window, "_ttsSteps", 5);
                Set(window, "_imageSteps", 20);
                Set(window, "_generationSeed", 42);
                window.position = new Rect(60, 60, 900, 850);
                window.Show();
                for (int i = 0; i < 3; ++i)
                    yield return null;
                // Unity's test runner still fails on errors/exceptions. Routine
                // backend/cache/retry logs vary by device; verify generated audio,
                // rendering and playback below instead of their exact log order.
                Invoke(window, "RunTask");
                var session = (MNNStudioSession)Get(window, "_session");
                Assert.NotNull(session, (string)Get(window, "_error"));
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (session.Busy && timer.Elapsed.TotalSeconds < 360)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.False(session.Busy, "Generation timed out.");
                Assert.IsNull(session.Error);
                Assert.AreSame(session, Get(window, "_session"), "Studio window must retain the active session through completion.");
                Assert.NotNull(session.Result, "Session must deliver a result before Busy becomes false; window=" + (bool)window + ", task=" + Get(window, "_taskIndex"));
                var result = (MNNStudioResult)Get(window, "_retrieval");
                Assert.NotNull(result, (string)Get(window, "_error"));
                for (int i = 0; i < 8; ++i)
                {
                    window.Repaint();
                    yield return null;
                }

                if (tts)
                {
                    Assert.AreEqual(model.TtsProfile == MNNTtsModelProfile.MossNano ? 48000 : (model.TtsProfile == MNNTtsModelProfile.MmsTamil || model.TtsProfile == MNNTtsModelProfile.SherpaVitsChinese) ? 16000 : model.IsIndexTts ? 22050 : model.IsIndicF5 || model.IsPocket || model.IsQwen3Explicit || model.IsDhVaani || model.IsFireRedTts || model.IsCosyVoice || (model.TtsProfile == MNNTtsModelProfile.Kokoro || model.TtsProfile == MNNTtsModelProfile.IndicVits) ? 24000 : model.IsPiper ? (repository == "PiperLessac" ? 22050 : 16000) : 44100, result.SampleRate);
                    Assert.Greater(result.Waveform.Length, result.SampleRate);
                    Assert.AreEqual((double)result.Waveform.Length / result.SampleRate, result.AudioSeconds);
                    Assert.Greater(result.RealTimeFactor.Value, 0);
                    TestContext.WriteLine("Speech metrics: " + model.Name + ", generation=" + result.Seconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        "s, audio=" + result.AudioSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "s, RTF=" + result.RealTimeFactor.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                    string artifactName = "studio-" + (longSpeech ? "supertonic-long" : repository ?? "supertonic") + (model.IsQwen3Explicit ? (speechLanguage == 1 ? "-en" : "-zh") : inputText == null ? "" : "-english");
                    File.WriteAllText(Path.Combine(artifacts, artifactName + "-rtf.json"),
                        "{\"generationSeconds\":" + result.Seconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                        ",\"audioSeconds\":" + result.AudioSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                        ",\"rtf\":" + result.RealTimeFactor.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}");
                    if (longSpeech)
                    {
                        Assert.Greater(result.Waveform.Length, result.SampleRate * 10);
                        Assert.Less(System.Linq.Enumerable.Max(result.Waveform, value => Math.Abs(value)), .99f);
                    }
                    var audio = (MNNStudioAudio)Get(window, "_audio");
                    for (int i = 0; i < 120 && audio.IsLoading; ++i)
                        yield return null;
                    Assert.IsNull(audio.ConsumePlaybackError());
                    Assert.True(audio.IsPlaying, "Actual generated speech must play in the Editor.");
                    File.WriteAllBytes(Path.Combine(artifacts, artifactName + ".wav"), MNNStudioAudio.EncodeWave(result.Waveform, result.SampleRate));
                    if (model.IsQwen3Explicit)
                        AdditionalTtsValidation.Transcribe(Path.Combine(artifacts, artifactName + ".wav"), speechLanguage == 0);
                    audio.StopPreview();
                    Assert.False(audio.IsPlaying);
                }
                else
                {
                    Assert.NotNull(result.GeneratedImage);
                    var preview = (Texture2D)Get(window, "_generatedPreview");
                    Assert.NotNull(preview, "Image result must render in the Studio view.");
                    Assert.AreEqual(512, preview.width);
                    Assert.AreEqual(512, preview.height);
                    File.WriteAllBytes(Path.Combine(artifacts, sana ? "studio-sana.png" : "studio-sd15.png"), preview.EncodeToPNG());
                    Invoke(window, "ResetWorkspace");
                    Assert.True(preview == null, "Reset must destroy the generated texture.");
                }

                Assert.IsEmpty((List<MNNStudioMessage>)Get(window, "_messages"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                EditorUtility.audioMasterMute = muted;
            }
        }
    }
}
