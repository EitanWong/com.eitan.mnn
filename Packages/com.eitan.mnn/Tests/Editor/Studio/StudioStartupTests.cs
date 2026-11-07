using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class StudioStartupTests
    {
        [UnityTest, Explicit("Measures opening against this development checkout's installed model assets.")]
        public IEnumerator OpeningWithInstalledModelsDoesNotBlockTheEditor()
        {
            string project = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath()), "../../../../.."));
            string models = Path.Combine(project, "Assets/StreamingAssets/MNN/Models");
            if (!Directory.Exists(models))
                Assert.Ignore("No existing development models; this test never downloads weights.");
            string key = "MNN.Studio.ModelRoot";
            bool hadPreference = EditorPrefs.HasKey(key);
            string previous = EditorPrefs.GetString(key, "");
            MNNChatStudio window = null;
            try
            {
                EditorPrefs.SetString(key, models);
                var clock = Stopwatch.StartNew();
                window = ScriptableObject.CreateInstance<MNNChatStudio>();
                // Drive the actual Tick explicitly so its completion/publication cost
                // cannot be hidden in an automatic update before the stopwatch starts.
                var tickCallback = (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                    typeof(EditorApplication.CallbackFunction), window,
                    typeof(MNNChatStudio).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic));
                EditorApplication.update -= tickCallback;
                window.position = new Rect(80, 80, 900, 700);
                window.Show();
                double openMilliseconds = clock.Elapsed.TotalMilliseconds;
                window.Repaint();
                yield return null;
                double firstFrameMilliseconds = clock.Elapsed.TotalMilliseconds;
                double maximumTickMilliseconds = 0;
                int ticks = 0;
                double deadline = EditorApplication.timeSinceStartup + 30;
                while (Discovering(window) && EditorApplication.timeSinceStartup < deadline)
                {
                    var tick = Stopwatch.StartNew();
                    Invoke(window, "Tick");
                    maximumTickMilliseconds = Math.Max(maximumTickMilliseconds, tick.Elapsed.TotalMilliseconds);
                    ++ticks;
                    window.Repaint();
                    yield return null;
                }
                Assert.False(Discovering(window), "Real installed model discovery must finish.");
                int modelCount = ((List<MNNStudioModel>)Field("_models").GetValue(window)).Count;
                Assert.Greater(modelCount, 0, "Fast opening must still discover installed models.");
                string output = Path.GetFullPath(Path.Combine("TestArtifacts~", "StudioStartup"));
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "opening.txt"),
                    "Open ms: " + openMilliseconds.ToString("F3") + "\nFirst frame ms: " + firstFrameMilliseconds.ToString("F3") +
                    "\nMaximum startup Tick ms: " + maximumTickMilliseconds.ToString("F3") +
                    "\nCompleted after ms: " + clock.Elapsed.TotalMilliseconds.ToString("F3") +
                    "\nResponsive update iterations: " + ticks + "\nModels: " + modelCount + "\nError: " + Field("_error").GetValue(window) + "\n");
                TestContext.WriteLine(File.ReadAllText(Path.Combine(output, "opening.txt")));
                Assert.Less(openMilliseconds, 200, "Opening must not synchronously scan and validate all installed models.");
                Assert.Less(maximumTickMilliseconds, 200, "Publishing completed discovery must not move the stall to a later Editor update.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                    UnityEngine.Object.DestroyImmediate(window);
                }
                if (hadPreference) EditorPrefs.SetString(key, previous);
                else EditorPrefs.DeleteKey(key);
            }
        }

        [UnityTest]
        public IEnumerator BackgroundRefreshPreservesTheLatestSelectionAndDraft()
        {
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                int editorThread = Thread.CurrentThread.ManagedThreadId, workerThread = editorThread;
                var discovery = new MNNStudioModelDiscovery((roots, token) =>
                {
                    workerThread = Thread.CurrentThread.ManagedThreadId;
                    entered.Set();
                    release.Wait(token);
                    return new MNNStudioModelDiscovery.Snapshot(new List<MNNStudioModel>
                        { Model("first"), Model("second") });
                });
                Field("_modelDiscovery").SetValue(window, discovery);
                Field("_historyPath").SetValue(window, null);
                Field("_autoTitle").SetValue(window, false);
                Field("_models").SetValue(window, new List<MNNStudioModel> { Model("first"), Model("second") });
                Invoke(window, "FilterModels");
                Field("_draft").SetValue(window, "Keep my draft");
                try
                {
                    window.position = new Rect(80, 80, 900, 700);
                    window.Show();
                    Invoke(window, "RefreshKeepingConversation");
                    double deadline = EditorApplication.timeSinceStartup + 3;
                    while (!entered.IsSet && EditorApplication.timeSinceStartup < deadline) yield return null;
                    Assert.True(entered.IsSet);
                    Assert.AreNotEqual(editorThread, workerThread);
                    Field("_modelIndex").SetValue(window, 1);
                    for (int frame = 0; frame < 3; ++frame) { window.Repaint(); yield return null; }
                    Assert.True(Discovering(window));
                    Assert.Greater(((Rect)Field("_composerRect").GetValue(window)).width, 100);
                    release.Set();
                    yield return FinishDiscovery(window);
                    Assert.AreEqual("second", Selected(window).Directory, "Publication must preserve choices made during scanning.");
                    Assert.AreEqual("Keep my draft", Field("_draft").GetValue(window));
                    Assert.IsFalse((bool)typeof(MNNChatStudio).GetProperty("Busy", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window));
                    LogAssert.NoUnexpectedReceived();
                }
                finally
                {
                    release.Set();
                    window.Close();
                    UnityEngine.Object.DestroyImmediate(window);
                    discovery.Dispose();
                }
            }
        }

        [UnityTest]
        public IEnumerator MissingModelIsReportedOnlyWhenRefreshCompletesAndFailureKeepsKnownModels()
        {
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            Field("_historyPath").SetValue(window, null);
            Field("_autoTitle").SetValue(window, false);
            Field("_models").SetValue(window, new List<MNNStudioModel> { Model("previous") });
            Invoke(window, "FilterModels");
            Field("_draft").SetValue(window, "Unsent text");
            var failure = new MNNStudioModelDiscovery((roots, token) => throw new IOException("Unavailable drive"));
            Field("_modelDiscovery").SetValue(window, failure);
            try
            {
                Invoke(window, "RefreshKeepingConversation");
                yield return FinishDiscovery(window);
                Assert.AreEqual("previous", Selected(window).Directory);
                StringAssert.Contains("Unavailable drive", (string)Field("_error").GetValue(window));
                failure.Dispose();
                var empty = new MNNStudioModelDiscovery((roots, token) =>
                    new MNNStudioModelDiscovery.Snapshot(new List<MNNStudioModel>()));
                Field("_modelDiscovery").SetValue(window, empty);
                Invoke(window, "RefreshKeepingConversation");
                Assert.AreEqual("previous", Selected(window).Directory, "Keep current choices until a replacement is ready.");
                yield return FinishDiscovery(window);
                Assert.IsNull(Selected(window));
                StringAssert.Contains("unavailable", (string)Field("_error").GetValue(window));
                Assert.AreEqual("Unsent text", Field("_draft").GetValue(window));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static IEnumerator FinishDiscovery(MNNChatStudio window)
        {
            double deadline = EditorApplication.timeSinceStartup + 5;
            while (Discovering(window) && EditorApplication.timeSinceStartup < deadline)
            {
                Invoke(window, "Tick");
                yield return null;
            }
            Assert.False(Discovering(window));
        }

        private static MNNStudioModel Model(string directory) =>
            new MNNStudioModel(directory, directory, MNNStudioTask.Chat, MNNModelCapabilities.Text);

        private static bool Discovering(MNNChatStudio window) =>
            ((MNNStudioModelDiscovery)Field("_modelDiscovery").GetValue(window)).IsRunning;

        private static MNNStudioModel Selected(MNNChatStudio window) =>
            (MNNStudioModel)typeof(MNNChatStudio).GetProperty("Selected", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);

        private static FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        private static void Invoke(MNNChatStudio window, string name) =>
            typeof(MNNChatStudio).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);

        private static string SourcePath([CallerFilePath] string source = null) => source;
    }
}
