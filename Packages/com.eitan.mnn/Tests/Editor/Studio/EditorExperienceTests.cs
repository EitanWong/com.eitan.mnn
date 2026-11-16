using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using System.Reflection;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class EditorExperienceTests
    {
        [Test]
        public void IdleDownloadsDoNotWakeEditorWindows()
        {
            var tasks = MNNModelDownloadTasks.instance;
            Assert.IsEmpty(tasks.Jobs, "Use the isolated test project, without user downloads.");
            int notifications = 0;
            Action changed = () => ++notifications;
            tasks.Changed += changed;
            try
            {
                typeof(MNNModelDownloadTasks).GetField("_lastUpdate", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tasks, -1d);
                typeof(MNNModelDownloadTasks).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tasks, null);
                Assert.AreEqual(0, notifications, "An idle download service must not trigger repaints or installation scans.");
            }
            finally { tasks.Changed -= changed; }
        }

        [Test]
        public void SearchWithZeroLimitReturnsNoResults()
        {
            var history = new MNNStudioHistory();
            history.Remember(MNNStudioConversation.Capture("one", "model", new[] { new MNNStudioMessage(true, "needle") }, "", null, null));
            Assert.IsEmpty(MNNStudioSearch.Find(history, "", 0));
            Assert.IsEmpty(MNNStudioSearch.Find(history, "needle", 0));
        }
        [Test]
        public void SearchSlicesBoundWorkAndFindMatchesAcrossTextChunks()
        {
            var history = new MNNStudioHistory();
            string text = new string('x', 16382) + "BoundaryNeedle" + new string('x', 70000);
            history.Remember(MNNStudioConversation.Capture("one", "model", new[] { new MNNStudioMessage(false, text) }, "", null, null));
            var operation = new MNNStudioSearch.Operation(history, "boundaryneedle");
            Assert.False(operation.Step(1), "One work unit must not scan the entire history.");
            while (!operation.Step(1)) { }
            Assert.AreEqual(1, operation.Hits.Count);
            Assert.AreEqual(0, operation.Hits[0].MessageIndex);
            StringAssert.Contains("BoundaryNeedle", operation.Hits[0].Preview);
            Assert.Throws<ArgumentOutOfRangeException>(() => operation.Step(0));
        }

        [Test]
        public void SearchLargeHistoryYieldsWhileReturningExactLocations()
        {
            var history = new MNNStudioHistory();
            var messages = new MNNStudioMessage[20000];
            for (int i = 0; i < messages.Length; ++i)
                messages[i] = new MNNStudioMessage(i % 2 == 0, "Entry " + i + new string('x', 500));
            messages[19999] = new MNNStudioMessage(false, "A rare needle at the end");
            history.Remember(MNNStudioConversation.Capture("large", "model", messages, "", null, null));
            var operation = new MNNStudioSearch.Operation(history, "rare needle");
            int slices = 0;
            double maximum = 0;
            var total = Stopwatch.StartNew();
            bool complete;
            do
            {
                var clock = Stopwatch.StartNew();
                complete = operation.Step(32);
                maximum = Math.Max(maximum, clock.Elapsed.TotalMilliseconds);
                ++slices;
            } while (!complete);
            Assert.Greater(slices, 100);
            Assert.AreEqual(1, operation.Hits.Count);
            Assert.AreEqual(19999, operation.Hits[0].MessageIndex);
            TestContext.WriteLine("Messages=20000; slices=" + slices + "; maximum slice ms=" + maximum.ToString("F3") + "; total ms=" + total.Elapsed.TotalMilliseconds.ToString("F3"));
            Assert.Less(maximum, 50, "Bounded search work must yield to the Editor.");
        }

        [UnityTest]
        public IEnumerator OpeningLongConversationOnlyBuildsVisibleMarkdown()
        {
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var messages = (List<MNNStudioMessage>)typeof(MNNChatStudio).GetField("_messages", flags).GetValue(window);
            for (int i = 0; i < 2000; ++i)
                messages.Add(new MNNStudioMessage(false, "## Response " + i + "\nA short **formatted** response with a useful [link](https://example.com)."));
            typeof(MNNChatStudio).GetField("_autoTitle", flags).SetValue(window, false);
            try
            {
                window.position = new Rect(80, 80, 960, 700);
                window.Show();
                for (int i = 0; i < 6; ++i) { window.Repaint(); yield return null; }
                var markdown = (IDictionary)typeof(MNNChatStudio).GetField("_markdown", flags).GetValue(window);
                Assert.Greater(markdown.Count, 0);
                Assert.Less(markdown.Count, 40, "Cold offscreen rows must not parse/render thousands of Markdown messages.");
                TestContext.WriteLine("Conversation messages=2000; parsed visible Markdown=" + markdown.Count);
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); UnityEngine.Object.DestroyImmediate(window); }
        }

        [UnityTest]
        public IEnumerator LargeCatalogKeepsSelectionAndLastRowReachable()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            string cacheRoot = System.IO.Path.GetFullPath("TestArtifacts~/EditorExperienceCatalog/" + Guid.NewGuid().ToString("N"));
            var previousCache = MNNModelCatalogCache.Shared;
            var cache = new MNNModelCatalogCache(cacheRoot, provider => System.Threading.Tasks.Task.FromResult(new MNNModelRepository()));
            MNNModelManagerWindow window = null;
            try
            {
                MNNModelCatalogCache.Shared = cache;
                window = ScriptableObject.CreateInstance<MNNModelManagerWindow>();
                double deadline = EditorApplication.timeSinceStartup + 5;
                while ((bool)typeof(MNNModelManagerWindow).GetField("_isLoadingModels", flags).GetValue(window) && EditorApplication.timeSinceStartup < deadline)
                    yield return null;
                Assert.False((bool)typeof(MNNModelManagerWindow).GetField("_isLoadingModels", flags).GetValue(window));
                var repository = new MNNModelRepository();
                for (int i = 0; i < 20000; ++i)
                    repository.models.Add(new MNNModelInfo { id = "model-" + i, name = "model-" + i,
                        displayName = "Model " + i, category = "LargeLanguageModel", repositoryId = "author/model-" + i });
                typeof(MNNModelManagerWindow).GetField("_repository", flags).SetValue(window, repository);
                typeof(MNNModelManagerWindow).GetMethod("RebuildCategoryOptions", flags).Invoke(window, null);
                typeof(MNNModelManagerWindow).GetMethod("RefreshModelList", flags).Invoke(window, null);
                window.position = new Rect(80, 80, 1100, 720);
                window.Show();
                for (int frame = 0; frame < 4; ++frame) { window.Repaint(); yield return null; }
                GUIUtility.keyboardControl = 0;
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.End });
                for (int frame = 0; frame < 4; ++frame) { window.Repaint(); yield return null; }
                var selected = (MNNModelInfo)typeof(MNNModelManagerWindow).GetField("_selectedModel", flags).GetValue(window);
                Assert.AreSame(repository.models[19999], selected);
                var scroll = (Vector2)typeof(MNNModelManagerWindow).GetField("_modelListScrollPos", flags).GetValue(window);
                Assert.Greater(scroll.y, 1500000);
                var clock = Stopwatch.StartNew();
                for (int i = 0; i < 10; ++i)
                {
                    window.SendEvent(new Event { type = EventType.Layout });
                    window.SendEvent(new Event { type = EventType.Repaint });
                }
                TestContext.WriteLine("Catalog=20000; warm GUI cycle ms=" + (clock.Elapsed.TotalMilliseconds / 10).ToString("F3"));
                Assert.Less(clock.Elapsed.TotalMilliseconds / 10, 50);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (window != null) { window.Close(); UnityEngine.Object.DestroyImmediate(window); }
                MNNModelCatalogCache.Shared = previousCache;
            }
        }

    }
}
