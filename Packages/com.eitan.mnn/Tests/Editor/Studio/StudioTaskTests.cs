using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class StudioTaskTests
    {
        private string _root;
        [SetUp]
        public void Setup()
        {
            _root = Path.GetFullPath(Path.Combine("TestArtifacts~", "StudioTasks", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        private string Model(string name, string metadata = null)
        {
            string directory = Path.Combine(_root, name);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "config.json"), "{}");
            if (metadata != null)
                File.WriteAllText(Path.Combine(directory, "llm_config.json"), metadata);
            return directory;
        }

        private static MNNStudioTaskInfo TaskInfo(MNNStudioTask task) => MNNStudioTasks.Get((int)task);
        private static FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static object Invoke(MNNChatStudio window, string name, params object[] args) => typeof(MNNChatStudio).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, args);

        [TestCase("VoiceActivityDetection", "mnn_silero_vad.json")]
        [TestCase("TurnDetection", "mnn_smart_turn.json")]
        [TestCase("SpeechEnhancement", "mnn_frcrn_se.json")]
        [TestCase("SpeechSeparation", "mnn_convtasnet.json")]
        [TestCase("SpeakerDiarization", "mnn_fs_eend.json")]
        public void AudioSubtasksRouteOnlyMatchingLocalProfiles(string taskName, string configName)
        {
            Assert.True(Enum.TryParse(taskName, out MNNStudioTask task), "Audio subtask must be available in Studio.");
            string directory = Model(taskName);
            File.WriteAllText(Path.Combine(directory, configName), "{}");
            var model = new MNNStudioModel(taskName, directory, MNNStudioTask.AudioProcessing, MNNModelCapabilities.AudioInput);
            Assert.True(TaskInfo(task).Accepts(model));
            foreach (string other in new[] { "VoiceActivityDetection", "TurnDetection", "SpeechEnhancement", "SpeechSeparation", "SpeakerDiarization" })
                if (other != taskName) Assert.False(TaskInfo((MNNStudioTask)Enum.Parse(typeof(MNNStudioTask), other)).Accepts(model), other);
        }

        [TestCase("VoiceActivityDetection", "Jeryuk/silero_vad-MNN")]
        [TestCase("TurnDetection", "Jeryuk/smart-turn-v3.2-gpu-MNN")]
        [TestCase("SpeechEnhancement", "manyeyes/frcrn-se-16k-onnx")]
        [TestCase("SpeechSeparation", "MNN/ConvTasNet-MNN")]
        [TestCase("SpeakerDiarization", "MNN/FS-EEND-Simu-MNN")]
        public void AudioSubtasksFilterTheModelManagerCatalog(string taskName, string repository)
        {
            var task = (MNNStudioTask)Enum.Parse(typeof(MNNStudioTask), taskName);
            var model = new MNNModelInfo { repositoryId = repository, category = "AudioProcessing" };
            Assert.True(TaskInfo(task).MatchesCatalog(model));
            Assert.False(TaskInfo(MNNStudioTask.KeywordSpotting).MatchesCatalog(model));
        }

        [TestCase("VoiceActivityDetection", "VoiceActivityDetection")]
        [TestCase("TurnDetection", "TurnDetection")]
        [TestCase("SpeechEnhancement", "SpeechEnhancement")]
        [TestCase("SpeechSeparation", "SpeechSeparation")]
        [TestCase("TargetSpeakerExtraction", "SpeechSeparation")]
        [TestCase("SpeakerDiarization", "SpeakerDiarization")]
        public void SpecificAudioCatalogCategoriesKeepTheirStudioWorkspace(string category, string taskName)
        {
            var task = (MNNStudioTask)Enum.Parse(typeof(MNNStudioTask), taskName);
            var model = new MNNModelInfo { name = "anonymous", category = category };
            Assert.True(TaskInfo(task).MatchesCatalog(model));
            Assert.True(TaskInfo(MNNStudioTask.AudioProcessing).MatchesCatalog(model));
            Assert.AreEqual(MNNStudioTask.AudioProcessing, MNNStudioTasks.FromCategory(category), "Keep the existing audio execution pipeline.");
            foreach (var other in new[] { MNNStudioTask.VoiceActivityDetection, MNNStudioTask.TurnDetection, MNNStudioTask.SpeechEnhancement, MNNStudioTask.SpeechSeparation, MNNStudioTask.SpeakerDiarization })
                if (other != task) Assert.False(TaskInfo(other).MatchesCatalog(model), other.ToString());
        }

        [UnityTest]
        public IEnumerator ChooseWaveDefersNestedGuiEventsAndPreservesDraft()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var model = MNNStudioModel.Read(Model("audio", "{\"is_audio\":true}"));
            string audio = Path.Combine(_root, "reference.wav");
            File.WriteAllBytes(audio, MNNStudioAudio.EncodeWave(new float[16000], 16000));
            Action scheduled = null;
            int pickerCalls = 0;
            Field("_dialogSchedule").SetValue(window, new Action<Action>(action => scheduled = action));
            Field("_attachmentPicker").SetValue(window, new Func<string, string, string, string>((title, directory, extension) =>
            {
                ++pickerCalls;
                Assert.AreEqual("wav", extension);
                // Model the nested redraw that a native file dialog can pump.
                window.SendEvent(new Event{type = EventType.Repaint});
                return audio;
            }));
            Field("_available").SetValue(window, new List<MNNStudioModel>{model});
            Field("_modelIndex").SetValue(window, 0);
            Field("_taskIndex").SetValue(window, (int)MNNStudioTask.SpeechRecognition);
            Field("_query").SetValue(window, "Keep this draft.");
            Field("_autoTitle").SetValue(window, false);
            try
            {
                window.position = new Rect(40, 40, 1000, 900);
                window.Show();
                window.Focus();
                for (int frame = 0; frame < 5; ++frame)
                {
                    window.Repaint();
                    yield return null;
                }

                var rect = (Rect)Field("_taskAudioAttachmentScreenRect").GetValue(window);
                Assert.Greater(rect.width, 0, "Choose WAV must be visible in the task workspace.");
                Vector2 click = rect.center - window.position.position;
                window.SendEvent(new Event{type = EventType.MouseDown, button = 0, mousePosition = click});
                window.SendEvent(new Event{type = EventType.MouseUp, button = 0, mousePosition = click});
                Assert.NotNull(scheduled, "Choose WAV must defer its native dialog until OnGUI has returned.");
                Assert.AreEqual(0, pickerCalls, "No native dialog may run inside the mouse event's layout scopes.");
                Action first = scheduled;
                Invoke(window, "PickAttachment", false);
                Assert.AreSame(first, scheduled, "Repeated clicks must not queue overlapping dialogs.");
                scheduled();
                Assert.AreEqual(1, pickerCalls);
                Assert.AreEqual(audio, Field("_audioPath").GetValue(window));
                Assert.AreEqual("Keep this draft.", Field("_query").GetValue(window));
                Assert.IsNull(Field("_error").GetValue(window));

                Field("_attachmentPicker").SetValue(window, new Func<string, string, string, string>((title, directory, extension) => ""));
                Invoke(window, "PickAttachment", false);
                scheduled();
                Assert.AreEqual(audio, Field("_audioPath").GetValue(window), "Cancel must keep the current attachment.");

                Field("_attachmentPicker").SetValue(window, new Func<string, string, string, string>((title, directory, extension) =>
                {
                    Field("_taskIndex").SetValue(window, (int)MNNStudioTask.Chat);
                    return audio;
                }));
                Field("_audioPath").SetValue(window, null);
                Invoke(window, "PickAttachment", false);
                scheduled();
                Assert.IsNull(Field("_audioPath").GetValue(window), "A dialog result must not attach to a workspace changed during the dialog.");

                bool openedAfterClose = false;
                Field("_attachmentPicker").SetValue(window, new Func<string, string, string, string>((title, directory, extension) =>
                {
                    openedAfterClose = true;
                    return audio;
                }));
                Invoke(window, "PickAttachment", false);
                window.Close();
                scheduled();
                Assert.False(openedAfterClose, "Closing the window must discard the pending dialog.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void GuiExitIsNotHandledAsAStudioError()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            try
            {
                var error = Assert.Throws<TargetInvocationException>(() => Invoke(window, "Try", new Action(() => throw new ExitGUIException())));
                Assert.IsInstanceOf<ExitGUIException>(error.InnerException);
                Assert.IsNull(Field("_error").GetValue(window));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void EveryCatalogCategoryHasAWorkspaceAndModelGuide()
        {
            foreach (MNNModelCategory category in Enum.GetValues(typeof(MNNModelCategory)))
            {
                if (category == MNNModelCategory.All)
                    continue;
                Assert.That(MNNStudioTasks.All.Where(task => task.Task != MNNStudioTask.Other).Any(task => task.MatchesCatalog(new MNNModelInfo{category = category.ToString()})) || category == MNNModelCategory.Other, "No workspace for " + category);
            }

            foreach (var task in MNNStudioTasks.All)
            {
                Assert.AreEqual(task, TaskInfo(task.Task));
                Assert.IsNotEmpty(task.Requirements);
                Assert.IsNotEmpty(task.Description);
            }

            Assert.That(TaskInfo(MNNStudioTask.Other).MatchesCatalog(new MNNModelInfo{category = "NewFutureTask"}));
        }

        [Test]
        public void PreciseCatalogCategoriesKeepTheirTaskAndTranslationRequiresALanguagePipeline()
        {
            foreach (var pair in new[] {
                new[] { "ImageMatting", "ImageMatting" }, new[] { "ImageRestoration", "ImageRestoration" },
                new[] { "Reconstruction3D", "Reconstruction3D" }, new[] { "ImageEmbedding", "ImageEmbedding" },
                new[] { "AudioGeneration", "AudioGeneration" }, new[] { "ModelDiagnostics", "Resources" } })
            {
                var task = (MNNStudioTask)Enum.Parse(typeof(MNNStudioTask), pair[1]);
                Assert.AreEqual(task, MNNStudioTasks.FromCategory(pair[0]));
                var model = new MNNStudioModel("resource", _root, task, MNNModelCapabilities.Text, pair[0]);
                Assert.True(TaskInfo(task).Accepts(model));
                Assert.False(TaskInfo(task).CanRun(model));
            }
            var language = new MNNStudioModel("translation", _root, MNNStudioTask.Chat, MNNModelCapabilities.Text, "Translation");
            Assert.True(TaskInfo(MNNStudioTask.Translation).CanRun(language));
            var graphs = new MNNStudioModel("opus-mt", _root, MNNStudioTask.Translation, MNNModelCapabilities.Text, "Translation");
            Assert.False(TaskInfo(MNNStudioTask.Translation).CanRun(graphs));
        }

        [Test]
        public void MediaWorkspacesFilterByCapabilitiesAndKeepBackendType()
        {
            var text = MNNStudioModel.Read(Model("text", "{}"));
            var vision = MNNStudioModel.Read(Model("vision", "{\"is_visual\":true}"));
            var audio = MNNStudioModel.Read(Model("audio", "{\"is_audio\":true}"));
            var omni = MNNStudioModel.Read(Model("omni", "{\"is_visual\":true,\"is_audio\":true}"));
            Assert.False(TaskInfo(MNNStudioTask.SpeechRecognition).Accepts(text));
            Assert.True(TaskInfo(MNNStudioTask.SpeechRecognition).CanRun(audio));
            Assert.True(TaskInfo(MNNStudioTask.OCR).CanRun(vision));
            Assert.False(TaskInfo(MNNStudioTask.Omni).Accepts(audio));
            Assert.True(TaskInfo(MNNStudioTask.Omni).CanRun(omni));
            Assert.AreEqual(MNNStudioTask.Chat, audio.Task, "Workspace selection must not change native loading to standalone ASR.");
        }

        [TestCase("supertonic-tts-mnn", (int)MNNStudioTask.SpeechSynthesis)]
        [TestCase("bert-vits2-MNN", (int)MNNStudioTask.SpeechSynthesis)]
        [TestCase("whisper-MNN", (int)MNNStudioTask.SpeechRecognition)]
        [TestCase("stable-diffusion-v1-5-mnn", (int)MNNStudioTask.ImageGeneration)]
        [TestCase("MNN-Sana-Edit-V2", (int)MNNStudioTask.ImageGeneration)]
        public void DedicatedPipelinesNeverFallBackToChat(string name, int taskIndex)
        {
            var task = (MNNStudioTask)taskIndex;
            var model = MNNStudioModel.Read(Model(name, "{}"));
            Assert.AreEqual(task, model.Task);
            Assert.False(TaskInfo(MNNStudioTask.Chat).CanRun(model));
            Assert.False(TaskInfo(task).CanRun(model));
        }

        [Test]
        public void DiscoveryAcceptsModelFolderAndInstalledRepositoriesWithoutLlmConfig()
        {
            string tts = Path.Combine(_root, "renamed-local-folder");
            Directory.CreateDirectory(tts);
            File.WriteAllText(Path.Combine(tts, MNNModelDownloadTasks.InstallMarker), "MNN/supertonic-tts-mnn");
            string vision = Model("vl", "{\"is_visual\":true}");
            var models = MNNStudioModel.Discover(new[]{tts, vision, _root});
            Assert.AreEqual(2, models.Count);
            Assert.AreEqual(MNNStudioTask.SpeechSynthesis, models.Single(model => model.Directory == tts).Task);
            Assert.True(TaskInfo(MNNStudioTask.Vision).CanRun(models.Single(model => model.Directory == vision)));
        }

        [Test]
        public void OfficialSpeechTasksAreSeparateFromAudioLanguageModels()
        {
            Assert.AreEqual("SpeechSynthesis", MNNModelRepository.Classify("MNN/piper-voices-MNN", tasks: new[]{"text-to-speech"}));
            Assert.AreEqual("SpeechRecognition", MNNModelRepository.Classify("MNN/custom", tasks: new[]{"automatic-speech-recognition"}));
            Assert.AreEqual("SpeechRecognition", MNNModelRepository.Classify("MNN/sherpa-mnn-streaming-zipformer-en-2023-02-21"));
            Assert.AreEqual("SpeechRecognition", MNNModelRepository.Classify("MNN/sherpa-mnn-streaming-zipformer-bilingual-zh-en-2023-02-20"));
            Assert.AreEqual("AudioLanguageModel", MNNModelRepository.Classify("MNN/LFM2.5-Audio-1.5B-MNN"));
            Assert.True(TaskInfo(MNNStudioTask.SpeechRecognition).MatchesCatalog(new MNNModelInfo{category = "SpeechRecognition"}));
            Assert.False(TaskInfo(MNNStudioTask.SpeechSynthesis).MatchesCatalog(new MNNModelInfo{category = "AudioLanguageModel"}));
            CollectionAssert.Contains(TaskInfo(MNNStudioTask.SpeechRecognition).Repositories, "MNN/sherpa-mnn-streaming-zipformer-bilingual-zh-en-2023-02-20");
            CollectionAssert.Contains(TaskInfo(MNNStudioTask.SpeechRecognition).Repositories, "MNN/sherpa-mnn-streaming-zipformer-en-2023-02-21");
            StringAssert.Contains("tokens.txt", TaskInfo(MNNStudioTask.SpeechRecognition).Requirements);
            StringAssert.Contains("mnn_zipformer.json", TaskInfo(MNNStudioTask.SpeechRecognition).UnavailableReason);
        }

        [Test]
        public void RenamedSherpaZipformerIsDiscoveredAsPreparationOnlyAsr()
        {
            string directory = Path.Combine(_root, "renamed-asr-folder");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "config.json"), "{\"modelType\":\"zipformer\"}");
            var model = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.SpeechRecognition, model.Task);
            Assert.AreEqual("SpeechRecognition", model.Category);
            Assert.False(TaskInfo(MNNStudioTask.Chat).CanRun(model));
            Assert.False(TaskInfo(MNNStudioTask.SpeechRecognition).CanRun(model));
        }

        [Test]
        public void RenamedCodeRepositoryKeepsItsWorkspaceAndEmptyFolderIsRejected()
        {
            string directory = Model("my-local-model", "{}");
            File.WriteAllText(Path.Combine(directory, MNNModelDownloadTasks.InstallMarker), "MNN/Qwen3-Coder-MNN");
            var model = MNNStudioModel.Read(directory);
            Assert.True(TaskInfo(MNNStudioTask.Code).CanRun(model));
            string empty = Path.Combine(_root, "empty");
            Directory.CreateDirectory(empty);
            Assert.Throws<ArgumentException>(() => MNNStudioModel.Read(empty));
        }

        [Test]
        public void SwitchingReferenceSpeechModelsKeepsRecordingButOtherTtsClearsIt()
        {
            string indexDirectory = Path.Combine(_root, "IndexTTS-2.5-mnn");
            string fireDirectory = Path.Combine(_root, "fireredtts3-mnn");
            string otherDirectory = Path.Combine(_root, "supertonic-tts-mnn");
            Directory.CreateDirectory(indexDirectory);
            Directory.CreateDirectory(fireDirectory);
            Directory.CreateDirectory(otherDirectory);
            string reference = Path.Combine(_root, "reference.wav");
            File.WriteAllBytes(reference, MNNStudioAudio.EncodeWave(new[]{.1f, -.1f}, 16000));

            var window = UnityEngine.ScriptableObject.CreateInstance<MNNChatStudio>();
            try
            {
                Field("_available").SetValue(window, new List<MNNStudioModel>
                {
                    new MNNStudioModel("IndexTTS-2.5-mnn", indexDirectory, MNNStudioTask.SpeechSynthesis, MNNModelCapabilities.Text),
                    new MNNStudioModel("fireredtts3-mnn", fireDirectory, MNNStudioTask.SpeechSynthesis, MNNModelCapabilities.Text),
                    new MNNStudioModel("supertonic-tts-mnn", otherDirectory, MNNStudioTask.SpeechSynthesis, MNNModelCapabilities.Text)
                });
                Field("_taskIndex").SetValue(window, (int)MNNStudioTask.SpeechSynthesis);
                Field("_audioPath").SetValue(window, reference);
                Invoke(window, "SelectModel", 1);
                Assert.AreEqual(reference, Field("_audioPath").GetValue(window), "Reference audio should carry over between reference-conditioned models.");
                Invoke(window, "SelectModel", 2);
                Assert.IsNull(Field("_audioPath").GetValue(window), "An unrelated synthesis model must not retain a stale reference attachment.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ReferenceSpeechAcceptsValidWavAndExplainsDurationLimits()
        {
            string indexDirectory = Path.Combine(_root, "IndexTTS-2.5-mnn");
            Directory.CreateDirectory(indexDirectory);
            string valid = Path.Combine(_root, "reference.wav");
            string tooShort = Path.Combine(_root, "short.wav");
            var validSamples = Enumerable.Repeat(.1f, 16000).ToArray();
            var shortSamples = Enumerable.Repeat(.1f, 7840).ToArray();
            File.WriteAllBytes(valid, MNNStudioAudio.EncodeWave(validSamples, 16000));
            File.WriteAllBytes(tooShort, MNNStudioAudio.EncodeWave(shortSamples, 16000));
            var window = UnityEngine.ScriptableObject.CreateInstance<MNNChatStudio>();
            try
            {
                Field("_available").SetValue(window, new List<MNNStudioModel>{new MNNStudioModel("IndexTTS-2.5-mnn", indexDirectory, MNNStudioTask.SpeechSynthesis, MNNModelCapabilities.Text)});
                Field("_modelIndex").SetValue(window, 0);
                Field("_taskIndex").SetValue(window, (int)MNNStudioTask.SpeechSynthesis);
                Invoke(window, "Attach", valid, false);
                Assert.AreEqual(valid, Field("_audioPath").GetValue(window));
                var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => Invoke(window, "Attach", tooShort, false));
                Assert.IsInstanceOf<ArgumentException>(error.InnerException);
                StringAssert.Contains("0.5–15 seconds", error.InnerException.Message);
                Assert.AreEqual(valid, Field("_audioPath").GetValue(window), "A rejected replacement must preserve the current valid reference.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private sealed class Backend : IMNNStudioBackend
        {
            internal MNNStudioRequest Request;
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                Request = request;
                return new MNNStudioResult{Text = "Transcript"};
            }

            public void Dispose()
            {
            }
        }

        [UnityTest]
        public IEnumerator TranscriptionUsesAudioAndDedicatedResultWithoutChatHistory()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var backend = new Backend();
            var session = new MNNStudioSession(backend);
            string audio = Path.Combine(_root, "input.wav");
            File.WriteAllBytes(audio, MNNStudioAudio.EncodeWave(new[]{.1f, -.1f}, 16000));
            try
            {
                Field("_available").SetValue(window, new List<MNNStudioModel>{MNNStudioModel.Read(Model("audio", "{\"is_audio\":true}"))});
                Field("_modelIndex").SetValue(window, 0);
                Field("_taskIndex").SetValue(window, (int)MNNStudioTask.SpeechRecognition);
                Field("_query").SetValue(window, "");
                Field("_audioPath").SetValue(window, audio);
                Field("_session").SetValue(window, session);
                Field("_autoTitle").SetValue(window, false);
                session.Completed += () => Invoke(window, "OnCompleted");
                Assert.True((bool)Invoke(window, "CanRunTask"));
                Invoke(window, "RunTask");
                // Batchmode test frames can finish before a queued worker is scheduled.
                double deadline = UnityEditor.EditorApplication.timeSinceStartup + 5;
                while (session.Busy && UnityEditor.EditorApplication.timeSinceStartup < deadline)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.False(session.Busy);
                Assert.IsNotNull(backend.Request);
                Assert.AreEqual(audio, backend.Request.Audio);
                Assert.IsNull(backend.Request.History);
                Assert.False(backend.Request.Speech);
                Assert.AreEqual(1, backend.Request.Conversation.Length);
                StringAssert.Contains("verbatim", backend.Request.Prompt);
                Assert.AreEqual("Transcript", ((MNNStudioResult)Field("_retrieval").GetValue(window)).Text);
                Assert.IsEmpty((List<MNNStudioMessage>)Field("_messages").GetValue(window));
                Field("_audioPath").SetValue(window, null);
                Assert.False((bool)Invoke(window, "CanRunTask"), "Transcription requires audio even when its default instruction exists.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                session.Dispose();
            }
        }
    }
}
