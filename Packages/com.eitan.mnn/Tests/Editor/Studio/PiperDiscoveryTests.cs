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
    public class PiperDiscoveryTests
    {
        [Test]
        public void ModelDataIsAutomaticEvenWithMissingOrStaleAssetFolder()
        {
            string root = Path.GetFullPath("TestArtifacts~/PiperDiscovery/" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(root, "espeak-ng-data");
            try
            {
                foreach (string file in MNNPiper.RequiredDataFiles)
                {
                    string path = Path.Combine(data, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, "fixture");
                }

                Assert.AreEqual(data, MNNPiper.ResolveDataDirectory(root));
                Assert.AreEqual(data, MNNPiper.ResolveDataDirectory(root, "missing-old-location"));
                Assert.AreEqual(data, MNNPiper.ResolveDataDirectory(root, "espeak-ng-data"));
                Assert.AreEqual(data, MNNPiper.ResolveDataDirectory("/missing-model", selected: data));
                Assert.Throws<DirectoryNotFoundException>(() => MNNPiper.ResolveDataDirectory(root, selected: Path.Combine(data, "phontab")));
                Assert.AreNotEqual(MNNStudioPiperSettings.DataPreference(root), MNNStudioPiperSettings.DataPreference(root + "-other"));
                File.Delete(Path.Combine(data, "phondata"));
                Assert.Throws<FileNotFoundException>(() => MNNPiper.ResolveDataDirectory(root));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void ExecutableBesideModelIsDiscoveredAndDataDirectoryCannotBeSelectedAsTool()
        {
            string root = Path.GetFullPath("TestArtifacts~/PiperDiscovery/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "bin"));
            string tool = Path.Combine(root, "bin/espeak-ng");
            File.WriteAllText(tool, "#!/bin/sh\nexit 0\n");
            try
            {
                Assert.AreEqual(tool, MNNPiper.FindEspeak(root));
                Assert.AreEqual(tool, MNNPiper.ResolveEspeak(null, root));
                Assert.Throws<ArgumentException>(() => MNNPiper.ResolveEspeak(root, root));
                Assert.That(MNNStudioPiperSettings.PickerDirectory("", root), Is.EqualTo(root));
                Assert.That(MNNStudioPiperSettings.PickerDirectory("/missing/file", root), Is.EqualTo(root));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void DictionaryPreferenceAndEnvironmentCannotBecomeProcessTargets()
        {
            string root = Path.GetFullPath("TestArtifacts~/PiperDiscovery/" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(root, "espeak-ng-data");
            Directory.CreateDirectory(data);
            string dictionary = Path.Combine(data, "af_dict");
            File.WriteAllText(dictionary, "dictionary data");
            string tool = Path.Combine(root, "espeak-ng");
            File.WriteAllText(tool, "#!/bin/sh\nexit 0\n");
            string key = MNNStudioPiperSettings.ExecutablePreference;
            bool had = EditorPrefs.HasKey(key);
            string old = EditorPrefs.GetString(key, "");
            string env = Environment.GetEnvironmentVariable("MNN_ESPEAK_NG_PATH");
            try
            {
                Assert.False(MNNPiper.IsEspeakExecutable(dictionary));
                Assert.Throws<ArgumentException>(() => MNNPiper.ResolveEspeak(dictionary, root));
                // A corrupt model-local tool must not mask a valid environment tool.
                File.WriteAllText(tool, "not a program");
                string wrapper = Path.Combine(root, "wrapper.sh");
                File.WriteAllText(wrapper, "#!/bin/sh\nexit 0\n");
                Environment.SetEnvironmentVariable("MNN_ESPEAK_NG_PATH", wrapper);
                Assert.AreEqual(wrapper, MNNPiper.FindEspeak(root));
                File.WriteAllText(tool, "#!/bin/sh\nexit 0\n");
                EditorPrefs.SetString(key, dictionary);
                Assert.AreEqual(tool, MNNStudioPiperSettings.ResolveExecutable(root));
                Assert.False(EditorPrefs.HasKey(key), "Invalid legacy executable preferences must be removed automatically.");
                Environment.SetEnvironmentVariable("MNN_ESPEAK_NG_PATH", dictionary);
                Assert.AreNotEqual(dictionary, MNNPiper.FindEspeak());
                // Data folders remain data even when a file happens to have a script header.
                File.WriteAllText(dictionary, "#!/bin/sh\nexit 0\n");
                Assert.False(MNNPiper.IsEspeakExecutable(dictionary));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MNN_ESPEAK_NG_PATH", env);
                if (had)
                    EditorPrefs.SetString(key, old);
                else
                    EditorPrefs.DeleteKey(key);
                Directory.Delete(root, true);
            }
        }

        [UnityTest]
        public IEnumerator BrowseClickWithEmptyPreferenceOpensFolderPickerAndPreservesDraft()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(MNNChatStudio);
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            string directory = GenerationTestData.Model("piper-voices-MNN");
            string key = MNNStudioPiperSettings.DataPreference(directory);
            bool had = EditorPrefs.HasKey(key);
            string old = EditorPrefs.GetString(key, "");
            int calls = 0;
            string start = null;
            Action scheduled = null;
            string data = MNNPiper.ResolveDataDirectory(directory);
            try
            {
                EditorPrefs.DeleteKey(key);
                type.GetField("_piperPickerSchedule", flags).SetValue(window, new Action<Action>(action => scheduled = action));
                type.GetField("_available", flags).SetValue(window, new List<MNNStudioModel>{MNNStudioModel.Read(directory)});
                type.GetField("_modelIndex", flags).SetValue(window, 0);
                type.GetField("_taskIndex", flags).SetValue(window, (int)MNNStudioTask.SpeechSynthesis);
                type.GetField("_query", flags).SetValue(window, "Keep this draft.");
                type.GetField("_piperDataFolderPicker", flags).SetValue(window, new Func<string, string>(initial =>
                {
                    ++calls;
                    start = initial;
                    return data;
                }));
                window.position = new Rect(40, 40, 1000, 900);
                window.Show();
                window.Focus();
                for (int i = 0; i < 5; ++i)
                {
                    window.Repaint();
                    yield return null;
                }

                var rect = (Rect)type.GetField("_piperBrowseRect", flags).GetValue(window);
                Assert.Greater(rect.width, 0, "Browse must be visible in the Piper workspace.");
                var click = rect.center + window.rootVisualElement.worldBound.position;
                TestContext.WriteLine("Browse rect=" + rect + " window=" + window.position + " root=" + window.rootVisualElement.worldBound + " click=" + click);
                window.SendEvent(new Event{type = EventType.MouseDown, button = 0, mousePosition = click});
                window.SendEvent(new Event{type = EventType.MouseUp, button = 0, mousePosition = click});
                Assert.NotNull(scheduled, "Browse click must schedule the folder dialog outside IMGUI.");
                Assert.AreEqual(0, calls, "The native dialog must not open inside the mouse event.");
                scheduled();
                Assert.AreEqual(1, calls, "A real Browse click must reach the folder picker once.");
                Assert.AreEqual(data, start);
                Assert.AreEqual(data, EditorPrefs.GetString(key));
                Assert.AreEqual("Keep this draft.", type.GetField("_query", flags).GetValue(window));
                var rejected = Assert.Throws<TargetInvocationException>(() => type.GetMethod("ApplyPiperDataFolder", flags).Invoke(window, new object[]{Path.Combine(data, "phontab")}));
                Assert.IsInstanceOf<DirectoryNotFoundException>(rejected.InnerException);
                Assert.AreEqual(data, EditorPrefs.GetString(key), "An invalid file selection must not overwrite the data folder.");
                Assert.IsNull(type.GetField("_error", flags).GetValue(window));
                // Cancellation must leave the selected folder and draft intact.
                type.GetField("_piperDataFolderPicker", flags).SetValue(window, new Func<string, string>(_ => ""));
                type.GetMethod("BrowsePiperDataFolder", flags).Invoke(window, null);
                scheduled();
                Assert.AreEqual(data, EditorPrefs.GetString(key));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (had)
                    EditorPrefs.SetString(key, old);
                else
                    EditorPrefs.DeleteKey(key);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void StudioSynthesizesUsingAutomaticToolAndModelDataWithoutSavedPath()
        {
            string key = MNNStudioPiperSettings.ExecutablePreference;
            bool had = EditorPrefs.HasKey(key);
            string old = EditorPrefs.GetString(key, "");
            string env = Environment.GetEnvironmentVariable("MNN_ESPEAK_NG_PATH");
            try
            {
                EditorPrefs.DeleteKey(key);
                Environment.SetEnvironmentVariable("MNN_ESPEAK_NG_PATH", null);
                var model = MNNStudioModel.Read(GenerationTestData.Model("piper-voices-MNN"));
                Assert.AreEqual(Path.Combine(model.Directory, "espeak-ng-data"), model.PiperDataDirectory);
                Assert.True(File.Exists(MNNStudioPiperSettings.ResolveExecutable(model.Directory)));
                EditorPrefs.SetString(key, model.PiperDataDirectory);
                Assert.True(File.Exists(MNNStudioPiperSettings.ResolveExecutable(model.Directory)), "An old data-folder value must not block automatic tool discovery.");
                // The English-only integration fixture includes en_dict; af_dict
                // and other dictionary names are covered by the discovery regression.
                string dictionary = Path.Combine(model.PiperDataDirectory, "en_dict");
                Assert.True(File.Exists(dictionary));
                EditorPrefs.SetString(key, dictionary);
                Environment.SetEnvironmentVariable("MNN_ESPEAK_NG_PATH", dictionary);
                using (var backend = MNNStudioNativeBackend.Load(model, 1, false))
                {
                    Assert.False(EditorPrefs.HasKey(key), "A saved dictionary process target must be removed before synthesis.");
                    var result = backend.Run(new MNNStudioRequest{Prompt = "The capital of France is Paris.", Voice = "en_US-amy-low"});
                    Assert.AreEqual(16000, result.SampleRate);
                    Assert.Greater(result.Waveform.Length, 16000);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("MNN_ESPEAK_NG_PATH", env);
                if (had)
                    EditorPrefs.SetString(key, old);
                else
                    EditorPrefs.DeleteKey(key);
            }
        }
    }
}
