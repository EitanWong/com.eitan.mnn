using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    internal static class LlmPerformanceData
    {
        internal static string Model(string name)
        {
            string root = Environment.GetEnvironmentVariable("MNN_LLM_CACHE_TEST_ROOT") ??
                Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var files = activity.Call<AndroidJavaObject>("getFilesDir"))
                root = Path.Combine(files.Call<string>("getAbsolutePath"), "mobile-llm", "models");
#elif UNITY_IOS && !UNITY_EDITOR
            root = Path.Combine(Application.persistentDataPath, "mobile-llm", "models");
#endif
            if (string.IsNullOrEmpty(root))
                Assert.Ignore("Set MNN_TEST_MODEL_ROOT to existing LLM directories, or stage mobile-llm/models.");
            string directory = Path.Combine(root, name);
            Assert.IsTrue(File.Exists(Path.Combine(directory, "config.json")), directory);
            return directory;
        }
    }
}
