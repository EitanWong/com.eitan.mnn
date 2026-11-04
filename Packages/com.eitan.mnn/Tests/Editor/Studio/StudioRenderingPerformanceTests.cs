using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class StudioRenderingPerformanceTests
    {
        [UnityTest]
        public IEnumerator WarmLibraryCostFollowsVisibleRowsRatherThanSavedChatCount()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var history = new MNNStudioHistory();
            Field("_historyPath").SetValue(window, null);
            Field("_history").SetValue(window, history);
            Field("_autoTitle").SetValue(window, false);
            Field("_error").SetValue(window, null);
            try
            {
                window.position = new Rect(80, 80, 900, 700);
                window.Show();
                AddChats(history, 24);
                for (int i = 0; i < 5; ++i) { window.Repaint(); yield return null; }
                double shortMilliseconds = Measure(window);
                AddChats(history, 960);
                for (int i = 0; i < 5; ++i) { window.Repaint(); yield return null; }
                double longMilliseconds = Measure(window);
                string root = Path.GetFullPath(Path.Combine("TestArtifacts~", "StudioRendering"));
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "library.txt"),
                    "24 chats ms/GUI cycle: " + shortMilliseconds.ToString("F3") +
                    "\n960 chats ms/GUI cycle: " + longMilliseconds.ToString("F3") + "\n");
                TestContext.WriteLine(File.ReadAllText(Path.Combine(root, "library.txt")));
                Assert.Less(longMilliseconds, shortMilliseconds * 6 + 3,
                    "The sidebar must not create IMGUI controls for every saved conversation.");
                Field("_sidebarScroll").SetValue(window, new Vector2(0, float.MaxValue));
                for (int i = 0; i < 4; ++i) { window.Repaint(); yield return null; }
                Assert.Greater(((Vector2)Field("_sidebarScroll").GetValue(window)).y, 20000);
                var rows = (Dictionary<string, Rect>)Field("_libraryRows").GetValue(window);
                Assert.True(rows.ContainsKey("959"), "The final saved chat must remain reachable.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator WarmTranscriptCostFollowsViewportRatherThanHistoryLength()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            // Keep this synthetic benchmark independent of the developer's saved history/models.
            Field("_historyPath").SetValue(window, null);
            Field("_history").SetValue(window, new MNNStudioHistory());
            Field("_autoTitle").SetValue(window, false);
            Field("_sidebarOpen").SetValue(window, false);
            Field("_error").SetValue(window, null);
            var messages = (List<MNNStudioMessage>)Field("_messages").GetValue(window);
            try
            {
                window.position = new Rect(80, 80, 900, 700);
                window.Show();
                AddMessages(messages, 24);
                for (int i = 0; i < 5; ++i) { window.Repaint(); yield return null; }
                double shortMilliseconds = Measure(window);
                AddMessages(messages, 480);
                for (int i = 0; i < 5; ++i) { window.Repaint(); yield return null; }
                double longMilliseconds = Measure(window);
                string root = Path.GetFullPath(Path.Combine("TestArtifacts~", "StudioRendering"));
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "transcript.txt"),
                    "Unity " + Application.unityVersion + "\n24 messages ms/GUI cycle: " + shortMilliseconds.ToString("F3") +
                    "\n480 messages ms/GUI cycle: " + longMilliseconds.ToString("F3") + "\n");
                TestContext.WriteLine(File.ReadAllText(Path.Combine(root, "transcript.txt")));
                Assert.Less(longMilliseconds, shortMilliseconds * 6 + 3,
                    "Typing/repainting a warm transcript must not lay out all 480 messages.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator TranscriptSupportsScrollSearchResizeAppendAndReset()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            Field("_historyPath").SetValue(window, null);
            Field("_autoTitle").SetValue(window, false);
            var messages = (List<MNNStudioMessage>)Field("_messages").GetValue(window);
            AddMessages(messages, 160);
            try
            {
                window.position = new Rect(80, 80, 900, 700);
                window.Show();
                foreach (float scroll in new[] { 0f, 6000f, float.MaxValue, 0f })
                {
                    Field("_scroll").SetValue(window, new Vector2(0, scroll));
                    for (int frame = 0; frame < 4; ++frame) { window.Repaint(); yield return null; }
                    var actual = (Vector2)Field("_scroll").GetValue(window);
                    Assert.GreaterOrEqual(actual.y, 0);
                    if (scroll > 0) Assert.Greater(actual.y, 1000, "The full transcript must retain its scroll extent.");
                }
                Field("_jumpMessageIndex").SetValue(window, 100);
                for (int frame = 0; frame < 4; ++frame) { window.Repaint(); yield return null; }
                Assert.AreEqual(-1, Field("_jumpMessageIndex").GetValue(window));
                Assert.Greater(((Vector2)Field("_scroll").GetValue(window)).y, 1000, "Search must reach an offscreen message.");
                window.position = new Rect(80, 80, 760, 540);
                AddMessages(messages, 162);
                Field("_scroll").SetValue(window, new Vector2(0, float.MaxValue));
                for (int frame = 0; frame < 5; ++frame) { window.Repaint(); yield return null; }
                var composer = (Rect)Field("_composerRect").GetValue(window);
                var conversation = (Rect)Field("_conversationRect").GetValue(window);
                Assert.GreaterOrEqual(composer.yMin, conversation.yMax - 1);
                typeof(MNNChatStudio).GetMethod("ResetWorkspace", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
                AddMessages(messages, 2);
                for (int frame = 0; frame < 4; ++frame) { window.Repaint(); yield return null; }
                Assert.Less(((Vector2)Field("_scroll").GetValue(window)).y, 1000, "A new chat must discard old layout extents.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void AddMessages(List<MNNStudioMessage> messages, int count)
        {
            while (messages.Count < count)
                messages.Add(new MNNStudioMessage(messages.Count % 2 == 0,
                    "Message " + messages.Count + "\n\n## Explanation\n\n" +
                    "A paragraph that wraps across the transcript. 中文排版与滚动。 " + new string('x', 180) +
                    "\n\n```csharp\nvar value = 42;\nDebug.Log(value);\n```\n\nFinal paragraph."));
        }

        private static void AddChats(MNNStudioHistory history, int count)
        {
            while (history.Conversations.Count < count)
            {
                string id = history.Conversations.Count.ToString();
                history.Conversations.Add(MNNStudioConversation.Capture(id, "missing-model",
                    Array.Empty<MNNStudioMessage>(), "Saved conversation " + id, null, null));
            }
        }

        private static double Measure(MNNChatStudio window)
        {
            var samples = new double[7];
            for (int i = 0; i < samples.Length; ++i)
            {
                var timer = Stopwatch.StartNew();
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });
                samples[i] = timer.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);
            return samples[samples.Length / 2];
        }

        private static FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    }
}
