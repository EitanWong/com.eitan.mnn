using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using MNN.Unity.Editor.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class StudioWindowTests
    {
        [UnityTest]
        public IEnumerator Library_MouseDragReordersMovesIntoFoldersAndPersists()
        {
            string root = Path.GetFullPath(Path.Combine("Library", "MNN", "StudioWindowTests", Guid.NewGuid().ToString("N")));
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var history = new MNNStudioHistory();
            for (int i = 2; i >= 0; --i)
                history.Remember(MNNStudioConversation.Capture(i.ToString(), "missing-model", new MNNStudioMessage[0], "Chat " + i, null, null));
            var folder = history.CreateFolder("Research");
            var secondFolder = history.CreateFolder("Notes");
            Field("_history").SetValue(window, history);
            Field("_historyPath").SetValue(window, Path.Combine(root, "history.json"));
            Field("_autoTitle").SetValue(window, false);
            int starts = 0;
            Field("_startLibraryDrag").SetValue(window, (Action<string>)(_ => ++starts));
            try
            {
                window.position = new Rect(60, 60, 900, 800);
                window.Show();
                for (int i = 0; i < 4; ++i)
                    yield return null;
                var rows = (Dictionary<string, Rect>)Field("_libraryRows").GetValue(window);
                var folders = (Dictionary<string, Rect>)Field("_folderRows").GetValue(window);
                Drag(window, rows["2"], rows["0"], false);
                TestContext.WriteLine("After first drop: " + string.Join(",", history.Conversations.Select(item => item.Id)));
                CollectionAssert.AreEqual(new[]{"2", "0", "1"}, history.Conversations.Select(item => item.Id));
                for (int i = 0; i < 3; ++i)
                    yield return null;
                Drag(window, rows["2"], rows["1"], true);
                CollectionAssert.AreEqual(new[]{"0", "1", "2"}, history.Conversations.Select(item => item.Id), "Dropping on the lower half inserts after the target.");
                for (int i = 0; i < 3; ++i)
                    yield return null;
                Drag(window, rows["1"], folders[folder.Id], false);
                Assert.AreEqual(folder.Id, history.Conversations.Find(item => item.Id == "1").FolderId);
                for (int i = 0; i < 3; ++i)
                    yield return null;
                Drag(window, folders[secondFolder.Id], folders[folder.Id], false);
                Assert.AreEqual(secondFolder.Id, history.Folders[0].Id);
                Assert.AreEqual(4, starts, "Every MouseDrag must reach the native drag service boundary exactly once.");
                for (int i = 0; i < 3; ++i)
                    yield return null;
                window.SendEvent(new Event{type = EventType.MouseDown, button = 0, mousePosition = rows["0"].center});
                window.SendEvent(new Event{type = EventType.MouseUp, button = 0, mousePosition = rows["0"].center});
                window.SendEvent(new Event{type = EventType.MouseDown, button = 0, mousePosition = new Vector2(220, 250)});
                window.SendEvent(new Event{type = EventType.MouseDrag, button = 0, mousePosition = new Vector2(228, 250)});
                Assert.AreEqual(4, starts, "A later drag in the conversation must not reuse the last clicked chat as a drag origin.");
                history.Rename("0", "Model-generated title");
                history.Remember(MNNStudioConversation.Capture("0", "missing-model", new[]{new MNNStudioMessage(true, "Follow up")}, "", null, null));
                history.Save(Path.Combine(root, "history.json"));
                var restored = MNNStudioHistory.Load(Path.Combine(root, "history.json"));
                CollectionAssert.AreEqual(new[]{"0", "2", "1"}, restored.Conversations.Select(item => item.Id));
                Assert.AreEqual("Model-generated title", restored.Conversations[0].Title);
                Assert.AreEqual(folder.Id, restored.Conversations[2].FolderId);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                DragAndDrop.SetGenericData("MNNStudioConversation", null);
                DragAndDrop.PrepareStartDrag();
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static void Drag(MNNChatStudio window, Rect source, Rect destination, bool after)
        {
            window.SendEvent(new Event{type = EventType.MouseDown, button = 0, mousePosition = source.center});
            window.SendEvent(new Event{type = EventType.MouseDrag, button = 0, mousePosition = source.center + new Vector2(8, 0), delta = new Vector2(8, 0)});
            Assert.IsNotNull(DragAndDrop.GetGenericData("MNNStudioConversation"), "A real MouseDrag must create a scoped drag payload.");
            var payload = DragAndDrop.GetGenericData("MNNStudioConversation");
            TestContext.WriteLine("Drag " + payload.GetType().GetField("Id", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(payload) + " from " + source + " to " + destination);
            var drop = new Vector2(destination.center.x, destination.y + destination.height * (after ? .8f : .2f));
            window.SendEvent(new Event{type = EventType.DragUpdated, mousePosition = drop});
            Assert.AreEqual(DragAndDropVisualMode.Move, DragAndDrop.visualMode);
            window.SendEvent(new Event{type = EventType.DragPerform, mousePosition = drop});
            Assert.IsNull(DragAndDrop.GetGenericData("MNNStudioConversation"));
        }

        private sealed class VoiceBackend : IMNNStudioBackend
        {
            internal readonly ManualResetEventSlim Release = new ManualResetEventSlim();
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                Release.Wait();
                return new MNNStudioResult{Text = "Reply"};
            }

            public void Dispose()
            {
                Release.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator VoiceOrb_StatesFitCompactAndWideWindowsWithoutOpeningMicrophone()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var backend = new VoiceBackend();
            var session = new MNNStudioSession(backend);
            Field("_session").SetValue(window, session);
            Field("_voicePanel").SetValue(window, true);
            Field("_autoTitle").SetValue(window, false);
            Field("_sessionModelDirectory").SetValue(window, ".");
            Field("_available").SetValue(window, new List<MNNStudioModel>{new MNNStudioModel("Omni-MNN", ".", MNNStudioTask.Chat, MNNModelCapabilities.AudioInput | MNNModelCapabilities.SpeechOutput)});
            try
            {
                window.Show();
                foreach (var size in new[]{new Vector2(760, 540), new Vector2(1180, 800)})
                    foreach (var state in new[]{MNNStudioVoiceState.Ready, MNNStudioVoiceState.Muted, MNNStudioVoiceState.Error, MNNStudioVoiceState.Thinking})
                    {
                        window.position = new Rect(60, 60, size.x, size.y);
                        Field("_voiceMuted").SetValue(window, state == MNNStudioVoiceState.Muted);
                        Field("_error").SetValue(window, state == MNNStudioVoiceState.Error ? "A recoverable voice error" : null);
                        if (state == MNNStudioVoiceState.Thinking && !session.Busy)
                            session.Send(new MNNStudioRequest{Prompt = "Test"});
                        window.Repaint();
                        for (int i = 0; i < 3; ++i)
                            yield return null;
                        var orb = (Rect)Field("_voiceOrbRect").GetValue(window);
                        var controls = (Rect)Field("_voiceControlsRect").GetValue(window);
                        Assert.Greater(orb.width, 100);
                        Assert.AreEqual(orb.width, orb.height);
                        Assert.GreaterOrEqual(orb.yMin, 60);
                        Assert.GreaterOrEqual(controls.yMin, orb.yMax);
                        Assert.LessOrEqual(controls.yMax, size.y - 28);
                        Assert.GreaterOrEqual(orb.xMin, 208);
                        Assert.LessOrEqual(orb.xMax, size.x);
                        Assert.AreEqual(state, typeof(MNNChatStudio).GetProperty("VoiceState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window));
                        Assert.IsFalse(((MNNStudioAudio)Field("_audio").GetValue(window)).IsRecording);
                        if (session.Busy)
                        {
                            backend.Release.Set();
                            for (int i = 0; i < 30 && session.Busy; ++i)
                            {
                                MNNStudioJobs.Update();
                                yield return null;
                            }

                            Assert.IsFalse(session.Busy);
                            backend.Release.Reset();
                        }
                    }

                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                backend.Release.Set();
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
                session.Dispose();
            }
        }

        // Exercise real IMGUI Layout/Repaint passes with long text, attachments,
        // expanded settings and every task. LogAssert fails on Unity GUI exceptions.
        [UnityTest]
        public IEnumerator Layout_AllTaskViewsAtMinimumAndWideSizes_AreValid()
        {
            string root = Path.GetFullPath(Path.Combine("Library", "MNN", "StudioWindowTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "config.json"), "{}");
            File.WriteAllText(Path.Combine(root, "llm_config.json"), "{\"is_visual\":true,\"is_audio\":true}");
            string image = Path.Combine(root, "preview.png");
            var texture = new Texture2D(1024, 512, TextureFormat.RGBA32, false);
            try
            {
                File.WriteAllBytes(image, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            try
            {
                var model = new MNNStudioModel("Layout model", root, MNNStudioTask.Chat, MNNModelCapabilities.Vision | MNNModelCapabilities.AudioInput | MNNModelCapabilities.SpeechOutput);
                var messages = (List<MNNStudioMessage>)Field("_messages").GetValue(window);
                messages.Add(new MNNStudioMessage(true, "A long developer question. " + new string ('x', 1200), image: image));
                messages.Add(new MNNStudioMessage(false, "## A formatted answer\n\n中文与 Emoji 😀\n" + new string ('y', 1800) + "\n\n```csharp\nvar text = \"<b>literal text</b>\";\n" + new string ('z', 1200) + "\n```", result: new MNNStudioResult{Tokens = 256, Text = "reply", Seconds = 1.5}));
                Field("_draft").SetValue(window, new string ('d', 1200));
                Field("_imagePath").SetValue(window, image);
                Field("_audioPath").SetValue(window, Path.Combine(root, new string ('a', 140) + ".wav"));
                window.Show();
                foreach (bool sidebar in new[]{true, false})
                    foreach (var size in new[]{new Vector2(760, 540), new Vector2(1180, 800)})
                    {
                        Field("_sidebarOpen").SetValue(window, sidebar);
                        window.position = new Rect(60, 60, size.x, size.y);
                        for (int task = 0; task < MNNStudioTasks.All.Length; ++task)
                        {
                            Field("_taskIndex").SetValue(window, task);
                            Field("_available").SetValue(window, new List<MNNStudioModel>{model});
                            Field("_modelNames").SetValue(window, new[]{"Long model display name for layout validation"});
                            window.Repaint();
                            for (int frame = 0; frame < 3; ++frame)
                                yield return null;
                            window.SendEvent(new Event{type = EventType.Repaint});
                            if (task == 0)
                            {
                                var composer = (Rect)Field("_composerRect").GetValue(window);
                                var conversation = (Rect)Field("_conversationRect").GetValue(window);
                                Assert.Greater(composer.width, 100);
                                Assert.GreaterOrEqual(composer.yMin, conversation.yMax - 1, "Composer must not cover the transcript.");
                                Assert.LessOrEqual(composer.yMax, size.y - 28, "Composer must stay above the status bar.");
                                Assert.GreaterOrEqual(composer.xMin, 0);
                                Assert.LessOrEqual(composer.xMax, size.x);
                                if (sidebar)
                                    Assert.GreaterOrEqual(composer.xMin, 208, "Composer must not extend underneath the sidebar.");
                            }
                        }
                    }

                var previews = (Dictionary<string, Texture2D>)Field("_previews").GetValue(window);
                Assert.That(previews.ContainsKey(image), "Image attachments must render a preview.");
                Assert.LessOrEqual(Math.Max(previews[image].width, previews[image].height), 320, "Preview GPU memory must stay bounded.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
                Directory.Delete(root, true);
            }
        }

        [UnityTest]
        public IEnumerator SettingsPopover_FitsCompactWindowAndPreservesLabelWidths()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            float previous = EditorGUIUtility.labelWidth;
            try
            {
                window.position = new Rect(60, 60, 760, 540);
                window.Show();
                typeof(MNNChatStudio).GetMethod("OpenSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[]{new Rect(650, 20, 28, 28)});
                for (int frame = 0; frame < 5; ++frame)
                    yield return null;
                Assert.AreEqual(previous, EditorGUIUtility.labelWidth, "Settings must not leak global GUI state.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private sealed class WindowBackend : IMNNStudioBackend
        {
            internal MNNStudioRequest Request;
            internal string Reply = "Reply";
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                Request = request;
                return new MNNStudioResult{Text = Reply, Tokens = 1};
            }

            public void Dispose()
            {
            }
        }

        [UnityTest]
        public IEnumerator Naming_UpdatesOnlyTitleAndPreservesConversationOrder()
        {
            string root = Path.GetFullPath(Path.Combine("Library", "MNN", "StudioWindowTests", Guid.NewGuid().ToString("N")));
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var backend = new WindowBackend{Reply = "彩虹的形成"};
            var session = new MNNStudioSession(backend);
            var messages = (List<MNNStudioMessage>)Field("_messages").GetValue(window);
            messages.Add(new MNNStudioMessage(true, "彩虹是怎样形成的？"));
            messages.Add(new MNNStudioMessage(false, "阳光在水滴中折射。"));
            var history = new MNNStudioHistory();
            history.Remember(MNNStudioConversation.Capture("active", ".", messages, "", null, null));
            history.Remember(MNNStudioConversation.Capture("other", ".", new MNNStudioMessage[0], "Other chat", null, null));
            Field("_history").SetValue(window, history);
            Field("_historyPath").SetValue(window, Path.Combine(root, "history.json"));
            Field("_activeChatId").SetValue(window, "active");
            Field("_session").SetValue(window, session);
            Field("_autoTitle").SetValue(window, false);
            Field("_available").SetValue(window, new List<MNNStudioModel>{new MNNStudioModel("Text-MNN", ".", MNNStudioTask.Chat, MNNModelCapabilities.Text)});
            session.Completed += () => typeof(MNNChatStudio).GetMethod("OnCompleted", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
            try
            {
                typeof(MNNChatStudio).GetMethod("QueueTitle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[]{"active", false});
                for (int i = 0; i < 30 && session.Busy; ++i)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.IsFalse(session.Busy);
                Assert.IsNotNull(backend.Request);
                Assert.AreEqual(MNNChatRole.System, backend.Request.Conversation[0].Role);
                Assert.AreEqual("彩虹的形成", history.Conversations[1].Title);
                Assert.IsTrue(history.Conversations[1].Named);
                Assert.IsNull(Field("_namingChatId").GetValue(window));
                Assert.AreEqual(2, messages.Count);
                typeof(MNNChatStudio).GetMethod("RememberConversation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                var restored = MNNStudioHistory.Load(Path.Combine(root, "history.json"));
                CollectionAssert.AreEqual(new[]{"other", "active"}, restored.Conversations.Select(item => item.Id));
                Assert.AreEqual("彩虹的形成", restored.Conversations[1].Title);
                Assert.AreEqual(2, restored.Conversations[1].Messages.Count);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                session.Dispose();
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [UnityTest]
        public IEnumerator Keyboard_EnterSendsAndShiftReturnKeepsDraft()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var backend = new WindowBackend();
            var session = new MNNStudioSession(backend);
            Field("_autoTitle").SetValue(window, false);
            try
            {
                Field("_available").SetValue(window, new List<MNNStudioModel>{new MNNStudioModel("Text-MNN", ".", MNNStudioTask.Chat, MNNModelCapabilities.Text)});
                Field("_modelNames").SetValue(window, new[]{"Text"});
                Field("_session").SetValue(window, session);
                Field("_sessionModelDirectory").SetValue(window, ".");
                session.Completed += () => typeof(MNNChatStudio).GetMethod("OnCompleted", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                Field("_draft").SetValue(window, "Hello");
                Field("_focusComposer").SetValue(window, true);
                window.Show();
                window.Repaint();
                for (int frame = 0; frame < 3; ++frame)
                    yield return null;
                window.SendEvent(new Event{type = EventType.KeyDown, keyCode = KeyCode.Return, modifiers = EventModifiers.Shift});
                Assert.IsNull(backend.Request, "Shift + Return must not send the message.");
                Field("_draft").SetValue(window, "Hello");
                window.SendEvent(new Event{type = EventType.KeyDown, keyCode = KeyCode.Return});
                for (int frame = 0; frame < 10 && session.Busy; ++frame)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.IsNotNull(backend.Request, "Enter should reach the session before TextArea consumes Return.");
                Assert.AreEqual("Hello", backend.Request.Prompt);
                Assert.IsEmpty(backend.Request.History);
                Assert.AreEqual("", Field("_draft").GetValue(window));
                Assert.AreEqual(2, ((List<MNNStudioMessage>)Field("_messages").GetValue(window)).Count);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
                session.Dispose();
            }
        }

        [Test]
        public void EmptyRestoredAttachmentPaths_DoNotEnableSend()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            try
            {
                Field("_available").SetValue(window, new List<MNNStudioModel>{new MNNStudioModel("Text-MNN", ".", MNNStudioTask.Chat, MNNModelCapabilities.Text)});
                Field("_draft").SetValue(window, "");
                Field("_imagePath").SetValue(window, "");
                Field("_audioPath").SetValue(window, "");
                Assert.IsFalse((bool)typeof(MNNChatStudio).GetProperty("CanSend", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Markdown_UnclosedFencesAndLiteralTagsPreserveModelOutput()
        {
            var blocks = MNNStudioMarkdown.Parse("## Heading\n\nA paragraph\n\n```csharp\n<b>literal</b>\n~~~\n## Still code");
            Assert.AreEqual(3, blocks.Count);
            Assert.AreEqual(2, blocks[0].Heading);
            Assert.AreEqual("A paragraph", blocks[1].Text);
            Assert.IsTrue(blocks[2].Code);
            Assert.AreEqual("csharp", blocks[2].Language);
            Assert.AreEqual("<b>literal</b>\n~~~\n## Still code", blocks[2].Text);
            var closed = MNNStudioMarkdown.Parse("~~~python\nprint(1)\n~~~\n\nBack to text");
            Assert.IsTrue(closed[0].Code);
            Assert.IsFalse(closed[1].Code);
            Assert.AreEqual("Back to text", closed[1].Text);
        }

        [TestCase(true), TestCase(false)]
        public void SemanticTextColors_MeetSmallTextContrastInBothThemes(bool dark)
        {
            Assert.GreaterOrEqual(Contrast(MNNStudioUI.TextColor(dark), MNNStudioUI.SurfaceColor(dark)), 4.5);
            Assert.GreaterOrEqual(Contrast(MNNStudioUI.SecondaryColor(dark), MNNStudioUI.SurfaceColor(dark)), 4.5);
            Assert.GreaterOrEqual(Contrast(Color.white, MNNStudioUI.Accent), 4.5);
            Assert.GreaterOrEqual(Contrast(MNNStudioUI.ActionColor(dark), MNNStudioUI.ActionTextColor(dark)), 4.5);
        }

        private static double Contrast(Color first, Color second)
        {
            double a = Luminance(first), b = Luminance(second);
            return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
        }

        private static double Luminance(Color value) => .2126 * Linear(value.r) + .7152 * Linear(value.g) + .0722 * Linear(value.b);
        private static double Linear(double value) => value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        private static FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    }
}
