using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class StudioCatalogTests
    {
        [UnityTest, Explicit("Measures a catalog click against this checkout's existing model assets; never downloads models.")]
        public IEnumerator ClickingCatalogWithInstalledModelsKeepsTheEditorResponsive()
        {
            string project = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath()), "../../../../.."));
            string models = Path.Combine(project, "Assets/StreamingAssets/MNN/Models");
            if (!Directory.Exists(models))
                Assert.Ignore("No installed development models.");
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            Set(window, "_customRoot", models);
            Set(window, "_historyPath", null);
            Set(window, "_autoTitle", false);
            Set(window, "_sidebarOpen", true);
            var tickCallback = (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                typeof(EditorApplication.CallbackFunction), window,
                typeof(MNNChatStudio).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic));
            EditorApplication.update -= tickCallback;
            try
            {
                Invoke(window, "RefreshModels");
                yield return FinishDiscovery(window, 30);
                int known = ((List<MNNStudioModel>)Get(window, "_models")).Count;
                Assert.Greater(known, 0);
                window.position = new Rect(80, 80, 900, 700);
                window.Show();
                for (int i = 0; i < 4; ++i) { window.Repaint(); yield return null; }
                var timer = Stopwatch.StartNew();
                var navigation = (Rect)Get(window, "_catalogNavigationRect");
                Assert.Greater(navigation.width, 100);
                var point = navigation.center;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });
                double clickMilliseconds = timer.Elapsed.TotalMilliseconds;
                Assert.AreEqual((int)MNNStudioTask.Other, Get(window, "_taskIndex"));
                double maximumTick = 0;
                int ticks = 0;
                var discovery = (MNNStudioModelDiscovery)Get(window, "_modelDiscovery");
                double deadline = EditorApplication.timeSinceStartup + 120;
                while (discovery.IsRunning && EditorApplication.timeSinceStartup < deadline)
                {
                    var tick = Stopwatch.StartNew();
                    Invoke(window, "Tick");
                    maximumTick = Math.Max(maximumTick, tick.Elapsed.TotalMilliseconds);
                    ++ticks;
                    window.Repaint();
                    yield return null;
                }
                TestContext.WriteLine("Installed models: " + known + "\nCatalog click + first GUI cycle ms: " + clickMilliseconds.ToString("F3") +
                    "\nMaximum catalog Tick ms: " + maximumTick.ToString("F3") + "\nBackground completion ms: " + timer.Elapsed.TotalMilliseconds.ToString("F3") +
                    "\nResponsive update iterations: " + ticks);
                Assert.False(discovery.IsRunning, "Catalog validation must eventually finish.");
                Assert.True(Catalog(window).IsComplete);
                Assert.Greater(Catalog(window).Local(MNNStudioTask.Chat), 0);
                Assert.Less(clickMilliseconds, 200);
                Assert.Less(maximumTick, 200, "Publication must not move validation back to an Editor update.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator ReturningToCompletedCatalogReusesCountsUntilExplicitRefresh()
        {
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            using (var discovery = new MNNStudioModelDiscovery((roots, token) =>
            {
                var models = new List<MNNStudioModel>
                {
                    new MNNStudioModel("chat", "missing-chat", MNNStudioTask.Chat, MNNModelCapabilities.Text)
                };
                return new MNNStudioModelDiscovery.Snapshot(models, catalog: MNNStudioTaskCatalog.Prepare(models, token));
            }))
            {
                Set(window, "_modelDiscovery", discovery);
                Set(window, "_historyPath", null);
                Set(window, "_autoTitle", false);
                try
                {
                    Invoke(window, "SelectTask", (int)MNNStudioTask.Other);
                    yield return FinishDiscovery(window);
                    var completed = Catalog(window);
                    Assert.AreEqual(1, completed.Ready(MNNStudioTask.Chat));
                    Invoke(window, "SelectTask", (int)MNNStudioTask.Chat);
                    Invoke(window, "SelectTask", (int)MNNStudioTask.Other);
                    Assert.False(discovery.IsRunning, "Navigation must not revalidate unchanged models.");
                    Assert.AreSame(completed, Catalog(window));
                    Invoke(window, "RefreshKeepingConversation");
                    Assert.AreEqual("Checking…", Status(window, MNNStudioTask.Chat));
                    yield return FinishDiscovery(window);
                    Assert.AreNotSame(completed, Catalog(window));
                    Assert.AreEqual(1, Catalog(window).Ready(MNNStudioTask.Chat));
                    LogAssert.NoUnexpectedReceived();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }
            }
        }

        [UnityTest]
        public IEnumerator CatalogPublishesReadyCountsAfterWorkerFinishesAndRefreshCanRecover()
        {
            int editor = Thread.CurrentThread.ManagedThreadId, validator = editor;
            int attempt = 0;
            var multimodal = new MNNStudioModel("omni", "missing-omni", MNNStudioTask.Chat,
                MNNModelCapabilities.Text | MNNModelCapabilities.Vision | MNNModelCapabilities.AudioInput);
            var image = Image("ready", () => { validator = Thread.CurrentThread.ManagedThreadId; return true; });
            var incomplete = Image("incomplete", () => false);
            var models = new List<MNNStudioModel> { multimodal, image, incomplete };
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var discovery = new MNNStudioModelDiscovery((roots, token) =>
            {
                if (Interlocked.Increment(ref attempt) == 2)
                    throw new IOException("Catalog drive unavailable");
                entered.Set();
                release.Wait(token);
                return new MNNStudioModelDiscovery.Snapshot(models, catalog: MNNStudioTaskCatalog.Prepare(models, token));
            }))
            {
                Set(window, "_modelDiscovery", discovery);
                Set(window, "_taskIndex", (int)MNNStudioTask.Other);
                Set(window, "_historyPath", null);
                Set(window, "_models", new List<MNNStudioModel> { image });
                try
                {
                    Invoke(window, "RefreshModels");
                    double deadline = EditorApplication.timeSinceStartup + 3;
                    while (!entered.IsSet && EditorApplication.timeSinceStartup < deadline) yield return null;
                    Assert.True(entered.IsSet);
                    Assert.AreEqual("Checking…", Status(window, MNNStudioTask.ImageGeneration));
                    Assert.AreEqual(1, Catalog(window).Local(MNNStudioTask.ImageGeneration));
                    Assert.AreEqual(editor, validator, "Counting known local models must not validate their pipelines.");
                    release.Set();
                    yield return FinishDiscovery(window);
                    Assert.AreNotEqual(editor, validator);
                    Assert.AreEqual(2, Catalog(window).Local(MNNStudioTask.ImageGeneration));
                    Assert.AreEqual(1, Catalog(window).Ready(MNNStudioTask.ImageGeneration));
                    Assert.AreEqual(1, Catalog(window).Ready(MNNStudioTask.Vision));
                    Assert.AreEqual(1, Catalog(window).Ready(MNNStudioTask.AudioUnderstanding));
                    Assert.AreEqual(MNNPlatformSupport.IsSupported ? "1 ready" : "Choose model", Status(window, MNNStudioTask.ImageGeneration));
                    Assert.AreEqual("Choose model", Status(window, MNNStudioTask.Embedding));
                    Assert.AreEqual("Model setup", Status(window, MNNStudioTask.Resources));

                    Invoke(window, "RefreshModels");
                    Assert.AreEqual("Checking…", Status(window, MNNStudioTask.ImageGeneration));
                    yield return FinishDiscovery(window);
                    Assert.AreEqual("Check failed", Status(window, MNNStudioTask.ImageGeneration));
                    Assert.AreEqual(2, Catalog(window).Local(MNNStudioTask.ImageGeneration), "Failure retains known local models.");
                    StringAssert.Contains("Catalog drive unavailable", (string)Get(window, "_error"));

                    Invoke(window, "RefreshModels");
                    yield return FinishDiscovery(window);
                    Assert.AreEqual(MNNPlatformSupport.IsSupported ? "1 ready" : "Choose model", Status(window, MNNStudioTask.ImageGeneration));
                    Assert.AreEqual(1, Catalog(window).Ready(MNNStudioTask.ImageGeneration), "Refresh must replace counts, not accumulate them.");
                    LogAssert.NoUnexpectedReceived();
                }
                finally
                {
                    release.Set();
                    UnityEngine.Object.DestroyImmediate(window);
                }
            }
        }

        [Test]
        public void PiperCatalogUsesEditorDictionaryOverrideOnceAfterWorkerPreparation()
        {
            string root = Path.GetFullPath("TestArtifacts~/StudioCatalog/" + Guid.NewGuid().ToString("N"));
            string modelRoot = Path.Combine(root, "voice");
            string data = Path.Combine(root, "external-dictionary");
            string key = MNNStudioPiperSettings.DataPreference(modelRoot);
            Directory.CreateDirectory(modelRoot);
            File.WriteAllText(Path.Combine(modelRoot, "config.json"), "{\"model_type\":\"piper\",\"sample_rate\":16000,\"model_path\":\"voice.mnn\"}");
            File.WriteAllText(Path.Combine(modelRoot, "voice.mnn"), "fixture");
            foreach (string file in MNNPiper.RequiredDataFiles)
            {
                string path = Path.Combine(data, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "fixture");
            }
            try
            {
                EditorPrefs.SetString(key, data);
                var models = new List<MNNStudioModel>
                {
                    new MNNStudioModel("piper-voices-MNN", modelRoot, MNNStudioTask.SpeechSynthesis, 0)
                };
                var catalog = Task.Run(() => MNNStudioTaskCatalog.Prepare(models, CancellationToken.None)).GetAwaiter().GetResult();
                Assert.False(catalog.IsComplete);
                Assert.AreEqual(1, catalog.Local(MNNStudioTask.SpeechSynthesis));
                Assert.AreEqual(0, catalog.Ready(MNNStudioTask.SpeechSynthesis));
                catalog.CompleteEditorSettings(models);
                Assert.True(catalog.IsComplete);
                Assert.AreEqual(1, catalog.Ready(MNNStudioTask.SpeechSynthesis));
                catalog.CompleteEditorSettings(models);
                Assert.AreEqual(1, catalog.Ready(MNNStudioTask.SpeechSynthesis));
            }
            finally
            {
                EditorPrefs.DeleteKey(key);
                Directory.Delete(root, true);
            }
        }

        [UnityTest]
        public IEnumerator ClickingTasksAndModelsDoesNotValidatePipelinesInGui()
        {
            int editor = Thread.CurrentThread.ManagedThreadId;
            int editorChecks = 0;
            var model = new MNNStudioModel("slow-image", "missing-image", MNNStudioTask.ImageGeneration, 0);
            typeof(MNNStudioModel).GetField("_snapshotHasGenerationPipeline", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(model, new Lazy<bool>(() =>
                {
                    if (Thread.CurrentThread.ManagedThreadId == editor)
                        Interlocked.Increment(ref editorChecks);
                    Thread.Sleep(350);
                    return true;
                }));
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            Set(window, "_models", new List<MNNStudioModel> { model });
            Set(window, "_historyPath", null);
            Set(window, "_autoTitle", false);
            Set(window, "_sidebarOpen", true);
            try
            {
                window.position = new Rect(80, 80, 900, 700);
                window.Show();
                for (int i = 0; i < 4; ++i) { window.Repaint(); yield return null; }
                var timer = Stopwatch.StartNew();
                var navigation = (Rect)Get(window, "_catalogNavigationRect");
                Assert.Greater(navigation.width, 100);
                var point = navigation.center;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });
                double milliseconds = timer.Elapsed.TotalMilliseconds;
                TestContext.WriteLine("Catalog click + first GUI cycle ms: " + milliseconds.ToString("F3"));
                Assert.AreEqual((int)MNNStudioTask.Other, Get(window, "_taskIndex"), "Exercise the actual sidebar click.");
                Assert.AreEqual(0, editorChecks, "Catalog paint must never start expensive pipeline validation.");
                Assert.Less(milliseconds, 200, "Opening the catalog must not wait for model validation.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static object Get(MNNChatStudio window, string field) =>
            typeof(MNNChatStudio).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);

        private static void Set(MNNChatStudio window, string field, object value) =>
            typeof(MNNChatStudio).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, value);

        private static MNNStudioModel Image(string name, Func<bool> readiness)
        {
            var model = new MNNStudioModel(name, "missing-" + name, MNNStudioTask.ImageGeneration, 0);
            typeof(MNNStudioModel).GetField("_snapshotHasGenerationPipeline", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(model, new Lazy<bool>(readiness));
            return model;
        }

        private static MNNStudioTaskCatalog Catalog(MNNChatStudio window) => (MNNStudioTaskCatalog)Get(window, "_taskCatalog");

        private static string Status(MNNChatStudio window, MNNStudioTask task) => (string)typeof(MNNChatStudio)
            .GetMethod("CatalogStatus", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(window, new object[] { MNNStudioTasks.Get((int)task) });

        private static void Invoke(MNNChatStudio window, string name, params object[] arguments) =>
            typeof(MNNChatStudio).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, arguments);

        private static IEnumerator FinishDiscovery(MNNChatStudio window, double timeout = 5)
        {
            var discovery = (MNNStudioModelDiscovery)Get(window, "_modelDiscovery");
            double deadline = EditorApplication.timeSinceStartup + timeout;
            while (discovery.IsRunning && EditorApplication.timeSinceStartup < deadline)
            {
                Invoke(window, "Tick");
                yield return null;
            }
            Assert.False(discovery.IsRunning);
        }

        private static string SourcePath([CallerFilePath] string source = null) => source;
    }
}
