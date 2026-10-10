using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class StudioHistoryTests
    {
        private sealed class LifecycleBackend : IMNNStudioBackend
        {
            internal int Disposals;
            public MNNStudioResult Run(MNNStudioRequest request) => new MNNStudioResult{Text = "ok"};
            public void Dispose()
            {
                ++Disposals;
            }
        }

        [Test]
        public void ModelSession_IsReusedForNewChatAndReleasedOnModelChange()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var backend = new LifecycleBackend();
            var session = new MNNStudioSession(backend);
            try
            {
                Set(window, "_session", session);
                Set(window, "_sessionModelDirectory", "model-a");
                Invoke(window, "ResetWorkspace");
                Assert.AreEqual(0, backend.Disposals, "Starting a new chat on the same model should keep the loaded instance.");
                Invoke(window, "ResetWorkspaceForModelChange");
                Assert.AreEqual(1, backend.Disposals, "Changing model or task must release its native instance.");
                Assert.IsNull(Get(window, "_session"));
            }
            finally
            {
                session.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ModelSwitch_PreservesConversationAndDraftAndIdleReleaseIsLazy()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var backend = new LifecycleBackend();
            var session = new MNNStudioSession(backend);
            string root = Path.GetFullPath(Path.Combine("TestArtifacts~", "StudioHistory", Guid.NewGuid().ToString("N")));
            try
            {
                Set(window, "_historyPath", Path.Combine(root, "history.json"));
                Set(window, "_history", new MNNStudioHistory());
                var models = new List<MNNStudioModel>{new MNNStudioModel("A", "model-a", MNNStudioTask.Chat, MNNModelCapabilities.Vision), new MNNStudioModel("B", "model-b", MNNStudioTask.Chat, MNNModelCapabilities.Text)};
                Set(window, "_models", models);
                Set(window, "_available", models);
                Set(window, "_modelIndex", 0);
                Set(window, "_session", session);
                Set(window, "_sessionModelDirectory", "model-a");
                var messages = (List<MNNStudioMessage>)Get(window, "_messages");
                messages.Add(new MNNStudioMessage(true, "Remember Ada"));
                messages.Add(new MNNStudioMessage(false, "OK"));
                Set(window, "_draft", "my draft");
                Set(window, "_contextSummary", "Ada");
                Set(window, "_summarizedMessages", 2);
                Set(window, "_imagePath", "pending.png");
                Invoke(window, "RememberConversation");
                string id = (string)Get(window, "_activeChatId");
                Invoke(window, "SelectModel", 1);
                Assert.AreEqual(1, backend.Disposals);
                Assert.IsNull(Get(window, "_session"));
                Assert.AreEqual(2, messages.Count);
                Assert.AreEqual("my draft", Get(window, "_draft"));
                Assert.AreEqual("Ada", Get(window, "_contextSummary"));
                Assert.AreEqual(2, Get(window, "_summarizedMessages"));
                Assert.AreEqual(id, Get(window, "_activeChatId"));
                Assert.IsNull(Get(window, "_imagePath"));
                Assert.AreEqual("model-b", ((MNNStudioHistory)Get(window, "_history")).Conversations[0].ModelDirectory);
                var idle = new LifecycleBackend();
                Set(window, "_session", new MNNStudioSession(idle));
                Set(window, "_sessionLastUsed", 10d);
                var unload = typeof(MNNChatStudio).GetMethod("TryUnloadIdleModel", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsFalse((bool)unload.Invoke(window, new object[]{309d}));
                Assert.AreEqual(0, idle.Disposals);
                Set(window, "_voiceLoop", true);
                Assert.IsFalse((bool)unload.Invoke(window, new object[]{310d}));
                Set(window, "_voiceLoop", false);
                Assert.IsTrue((bool)unload.Invoke(window, new object[]{310d}));
                Assert.AreEqual(1, idle.Disposals);
                Assert.AreEqual(2, messages.Count);
            }
            finally
            {
                session.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void History_RetainsFullTranscriptBeyond100MessagesAndMemoryCoverageAcrossReload()
        {
            string root = Path.GetFullPath(Path.Combine("TestArtifacts~", "StudioHistory", Guid.NewGuid().ToString("N")));
            string path = Path.Combine(root, "history.json");
            try
            {
                var messages = new List<MNNStudioMessage>();
                for (int i = 0; i < 150; ++i)
                    messages.Add(new MNNStudioMessage(i % 2 == 0, "message-" + i));
                var history = new MNNStudioHistory();
                history.Remember(MNNStudioConversation.Capture("id", "model", messages, "draft", null, null, "name is Ada", 140));
                history.Save(path);
                var chat = MNNStudioHistory.Load(path).Conversations[0];
                Assert.AreEqual(150, chat.Messages.Count);
                Assert.AreEqual("message-0", chat.Messages[0].Text);
                Assert.AreEqual(140, chat.SummarizedMessages);
                var restored = new List<MNNStudioMessage>(chat.RestoreMessages());
                var prepared = MNNStudioContext.Prepare(restored, "next", chat.ContextSummary, chat.SummarizedMessages, 8192, 256, 0, text => text.Length, prompt =>
                {
                    Assert.Fail("No unnecessary recompression");
                    return "";
                }, null, default);
                StringAssert.Contains("Ada", prepared.Conversation[0].Content);
                Assert.IsFalse(Array.Exists(prepared.Conversation, message => message.Content.Contains("message-0")));
                Assert.IsTrue(Array.Exists(prepared.Conversation, message => message.Content.Contains("message-140")));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void FoldersDragOrderTitlesAndDirectDelete_RoundTripWithoutResorting()
        {
            string root = Path.GetFullPath(Path.Combine("Library", "MNN", "StudioHistoryTests", Guid.NewGuid().ToString("N")));
            string path = Path.Combine(root, "history.json");
            try
            {
                var history = new MNNStudioHistory();
                for (int i = 0; i < 4; ++i)
                    history.Remember(MNNStudioConversation.Capture(i.ToString(), "model", new List<MNNStudioMessage>(), "Draft " + i, null, null));
                var first = history.CreateFolder("工作");
                var second = history.CreateFolder("Research");
                history.MoveChat("0", first.Id);
                history.MoveChat("1", first.Id, "0");
                history.MoveChat("2", null, "3");
                history.MoveFolder(second.Id, first.Id);
                history.Rename("1", "LLM generated title");
                history.Remember(MNNStudioConversation.Capture("1", "model", new List<MNNStudioMessage>(), "Updated draft", null, null));
                Assert.AreEqual("LLM generated title", history.Conversations.Find(item => item.Id == "1").Title);
                history.Save(path);
                history = MNNStudioHistory.Load(path);
                CollectionAssert.AreEqual(new[]{"2", "3", "1", "0"}, history.Conversations.ConvertAll(item => item.Id));
                Assert.AreEqual(second.Id, history.Folders[0].Id);
                Assert.AreEqual(first.Id, history.Conversations[2].FolderId);
                history.Delete("1");
                history.Delete(first.Id, true);
                history.Save(path);
                history = MNNStudioHistory.Load(path);
                Assert.IsFalse(history.Conversations.Exists(item => item.Id == "1"));
                Assert.AreEqual(3, history.Conversations.Count);
                Assert.IsNull(history.Conversations.Find(item => item.Id == "0").FolderId);
                Assert.AreEqual(1, history.Folders.Count);
                Assert.Throws<ArgumentException>(() => history.MoveChat("0", "missing"));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void ModelTitlePrompt_IsBoundedAndCleanedWithoutLeakingReasoning()
        {
            var chat = MNNStudioConversation.Capture("id", "model", new[]{new MNNStudioMessage(true, new string ('a', 2000)), new MNNStudioMessage(false, new string ('b', 2000))}, "", null, null);
            var prompt = MNNStudioNaming.Prompt(chat);
            Assert.AreEqual(MNNChatRole.System, prompt[0].Role);
            Assert.Less(prompt[1].Content.Length, 1800);
            Assert.AreEqual("彩虹的形成", MNNStudioNaming.CleanTitle("<think>private reasoning</think>\n标题：彩虹的形成\nExtra explanation"));
            Assert.AreEqual("", MNNStudioNaming.CleanTitle("<think>unfinished reasoning"));
            Assert.AreEqual("A useful title", MNNStudioNaming.CleanTitle("\"A useful title\""));
        }

        [Test]
        public void History_RoundTripsRolesDraftAndModelWithoutWaveforms()
        {
            string root = Path.GetFullPath(Path.Combine("Library", "MNN", "StudioHistoryTests", Guid.NewGuid().ToString("N")));
            string path = Path.Combine(root, "history.json");
            try
            {
                var messages = new List<MNNStudioMessage>{new MNNStudioMessage(true, "你好\nExplain this image", "image.png"), new MNNStudioMessage(false, "An answer", result: new MNNStudioResult{Text = "An answer", Tokens = 7, Seconds = 1.25, Waveform = new[]{.1234567f}, SampleRate = 24000})};
                var history = new MNNStudioHistory();
                history.Remember(MNNStudioConversation.Capture("one", "model-a", messages, "Follow up", "draft.png", "draft.wav", "User is Alice."));
                history.Save(path);
                history.Save(path); // Exercise atomic replacement of an existing file.
                var restored = MNNStudioHistory.Load(path).Conversations[0];
                Assert.AreEqual("model-a", restored.ModelDirectory);
                Assert.AreEqual("Follow up", restored.Draft);
                Assert.AreEqual("User is Alice.", restored.ContextSummary);
                Assert.AreEqual("draft.png", restored.ImagePath);
                Assert.AreEqual("draft.wav", restored.AudioPath);
                StringAssert.DoesNotContain("\n", restored.Title);
                var turns = new List<MNNStudioMessage>(restored.RestoreMessages());
                Assert.IsTrue(turns[0].IsUser);
                Assert.IsFalse(turns[1].IsUser);
                Assert.AreEqual("你好\nExplain this image", turns[0].Text);
                Assert.AreEqual("image.png", turns[0].ImagePath);
                Assert.AreEqual(7, turns[1].Result.Tokens);
                Assert.AreEqual(1.25, turns[1].Result.Seconds);
                Assert.IsEmpty(turns[1].Result.Waveform);
                StringAssert.DoesNotContain("0.1234567", File.ReadAllText(path));
                Assert.IsFalse(File.Exists(path + ".tmp"));
                var prompt = MNNStudioPrompt.Build(turns, "Next question");
                Assert.AreEqual(3, prompt.Length);
                Assert.AreEqual(MNNChatRole.Assistant, prompt[1].Role);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void History_UpdatesInPlacePreservesManualOrderAndDoesNotEvictChats()
        {
            var history = new MNNStudioHistory();
            for (int i = 0; i < 25; ++i)
                history.Remember(MNNStudioConversation.Capture(i.ToString(), "model", new List<MNNStudioMessage>(), "Draft " + i, null, null));
            Assert.AreEqual(25, history.Conversations.Count);
            Assert.AreEqual("24", history.Conversations[0].Id);
            history.Remember(MNNStudioConversation.Capture("10", "model", new List<MNNStudioMessage>(), "Changed", null, null));
            Assert.AreEqual(25, history.Conversations.Count);
            Assert.AreEqual("24", history.Conversations[0].Id);
            Assert.AreEqual("Changed", history.Conversations.Find(item => item.Id == "10").Draft);
            Assert.AreEqual(1, history.Conversations.FindAll(item => item.Id == "10").Count);
        }

        [Test]
        public void Window_SwitchingChatsPreservesDraftAndIsolatesPromptContext()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            string root = Path.GetFullPath(Path.Combine("Library", "MNN", "StudioHistoryTests", Guid.NewGuid().ToString("N")));
            try
            {
                Set(window, "_historyPath", Path.Combine(root, "history.json"));
                Set(window, "_history", new MNNStudioHistory());
                var model = new MNNStudioModel("Text-MNN", "model-a", MNNStudioTask.Chat, MNNModelCapabilities.Text);
                Set(window, "_models", new List<MNNStudioModel>{model});
                Set(window, "_available", new List<MNNStudioModel>{model});
                var messages = (List<MNNStudioMessage>)Get(window, "_messages");
                messages.Clear();
                messages.Add(new MNNStudioMessage(true, "My name is Alice."));
                messages.Add(new MNNStudioMessage(false, "Hello Alice."));
                Set(window, "_draft", "What is my name?");
                Invoke(window, "StartNewChat");
                Assert.IsEmpty(messages);
                Assert.AreEqual("", Get(window, "_draft"));
                string first = ((MNNStudioHistory)Get(window, "_history")).Conversations[0].Id;
                messages.Add(new MNNStudioMessage(true, "A separate conversation."));
                Set(window, "_draft", "Separate draft");
                Invoke(window, "OpenConversation", first);
                Assert.AreEqual(2, messages.Count);
                Assert.AreEqual("What is my name?", Get(window, "_draft"));
                var prompt = MNNStudioPrompt.Build(messages, (string)Get(window, "_draft"));
                Assert.AreEqual(3, prompt.Length);
                StringAssert.Contains("Alice", prompt[0].Content);
                Assert.That(Array.TrueForAll(prompt, item => !item.Content.Contains("separate")));
                Assert.AreEqual(2, ((MNNStudioHistory)Get(window, "_history")).Conversations.Count);
                Set(window, "_models", new List<MNNStudioModel>());
                Invoke(window, "StartNewChat");
                Invoke(window, "OpenConversation", first);
                Assert.IsNull(typeof(MNNChatStudio).GetProperty("Selected", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window));
                Assert.AreEqual(2, messages.Count, "Missing models must not hide the saved transcript.");
                Invoke(window, "RefreshKeepingConversation");
                Assert.IsNull(typeof(MNNChatStudio).GetProperty("Selected", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window));
                Assert.AreEqual(2, messages.Count, "Refresh must not silently pick a different model or erase the transcript.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static object Get(object target, string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => typeof(MNNChatStudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string name, params object[] arguments) => typeof(MNNChatStudio).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }
}
