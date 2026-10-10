using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MNN.Unity.Editor;
using MNN.Unity.Editor.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class StudioSearchTests
    {
        [Test]
        public void Search_FindsTitlesAndBothRolesWithStableMessageLocations()
        {
            var history = new MNNStudioHistory();
            var messages = new[]{new MNNStudioMessage(true, "Explain a rainbow"), new MNNStudioMessage(false, "彩虹来自水滴的折射。"), new MNNStudioMessage(true, new string ('a', 300) + "water droplets" + new string ('b', 300))};
            history.Remember(MNNStudioConversation.Capture("one", "model", messages, "", null, null));
            history.Rename("one", "Science discussion");
            var title = MNNStudioSearch.Find(history, "SCIENCE");
            Assert.AreEqual(1, title.Count);
            Assert.AreEqual(-1, title[0].MessageIndex);
            var answer = MNNStudioSearch.Find(history, "彩虹");
            Assert.AreEqual(1, answer.Count);
            Assert.AreEqual(1, answer[0].MessageIndex);
            Assert.IsFalse(answer[0].IsUser);
            var prompt = MNNStudioSearch.Find(history, "rainbow");
            Assert.AreEqual(0, prompt[0].MessageIndex);
            Assert.IsTrue(prompt[0].IsUser);
            var snippet = MNNStudioSearch.Find(history, "water droplets")[0];
            StringAssert.Contains("water droplets", snippet.Preview);
            Assert.Less(snippet.Preview.Length, 160);
            Assert.IsEmpty(MNNStudioSearch.Find(history, "missing"));
            Assert.AreEqual(1, MNNStudioSearch.Find(history, "  ").Count);
            Assert.AreEqual(1, MNNStudioSearch.Find(history, "a", 1).Count);
        }

        [UnityTest]
        public IEnumerator SearchPopup_IsCenteredAndReturnOpensSelectedMessage()
        {
            var parent = new Rect(60, 60, 760, 540);
            var history = new MNNStudioHistory();
            history.Remember(MNNStudioConversation.Capture("one", "model", new[]{new MNNStudioMessage(true, "Question"), new MNNStudioMessage(false, "Needle in an answer")}, "", null, null));
            MNNStudioSearchHit chosen = null;
            var popup = MNNStudioSearchWindow.Open(parent, history, hit => chosen = hit);
            try
            {
                typeof(MNNStudioSearchWindow).GetField("_hits", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(popup, MNNStudioSearch.Find(history, "Needle"));
                for (int i = 0; i < 3; ++i)
                    yield return null;
                Assert.Less(Vector2.Distance(parent.center, popup.position.center), 2);
                Assert.Less(popup.position.width, parent.width);
                Assert.Less(popup.position.height, parent.height);
                popup.SendEvent(new Event{type = EventType.KeyDown, keyCode = KeyCode.Return});
                Assert.IsNotNull(chosen);
                Assert.AreEqual("one", chosen.ChatId);
                Assert.AreEqual(1, chosen.MessageIndex);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (popup != null)
                    popup.Close();
            }
        }

        [UnityTest]
        public IEnumerator SearchHit_RestoresSessionAndScrollsToMatchingMessage()
        {
            string root = Path.GetFullPath(Path.Combine("Library", "MNN", "StudioWindowTests", Guid.NewGuid().ToString("N")));
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var history = new MNNStudioHistory();
            var messages = new List<MNNStudioMessage>();
            for (int i = 0; i < 30; ++i)
                messages.Add(new MNNStudioMessage(i % 2 == 0, "Message " + i + ": " + new string ('x', 150)));
            history.Remember(MNNStudioConversation.Capture("target", "missing-model", messages, "", null, null));
            Set(window, "_history", history);
            Set(window, "_historyPath", Path.Combine(root, "history.json"));
            Set(window, "_autoTitle", false);
            try
            {
                window.position = new Rect(60, 60, 760, 540);
                window.Show();
                typeof(MNNChatStudio).GetMethod("JumpToSearchHit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[]{new MNNStudioSearchHit{ChatId = "target", MessageIndex = 24}});
                for (int i = 0; i < 5; ++i)
                    yield return null;
                Assert.AreEqual("target", Get(window, "_activeChatId"));
                Assert.AreEqual(24, Get(window, "_highlightMessageIndex"));
                Assert.AreEqual(-1, Get(window, "_jumpMessageIndex"));
                Assert.IsFalse((bool)Get(window, "_followOutput"));
                Assert.Greater(((Vector2)Get(window, "_scroll")).y, 800, "A late match must scroll into view rather than just open the chat.");
                Assert.AreEqual(30, ((List<MNNStudioMessage>)Get(window, "_messages")).Count);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static object Get(object target, string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => typeof(MNNChatStudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
