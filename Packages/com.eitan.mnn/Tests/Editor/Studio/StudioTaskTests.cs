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
            StringAssert.Contains("runtime is not included", TaskInfo(MNNStudioTask.SpeechRecognition).UnavailableReason);
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
                for (int frame = 0; frame < 60 && session.Busy; ++frame)
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
