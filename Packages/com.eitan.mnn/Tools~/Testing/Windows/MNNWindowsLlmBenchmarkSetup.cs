using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class MNNWindowsLlmBenchmarkSetup
{
    public static void Build()
    {
        string backend = Environment.GetEnvironmentVariable("MNN_WINDOWS_BENCHMARK_BACKEND");
        string output = Environment.GetEnvironmentVariable("MNN_WINDOWS_BENCHMARK_PLAYER");
        if ((backend != "Mono" && backend != "IL2CPP") || string.IsNullOrEmpty(output))
            throw new ArgumentException("Provide Mono/IL2CPP backend and output executable path.");
        // A failed scripted build can exit before Unity's recovery update runs.
        typeof(MNN.Unity.Editor.MNNModelBuildProcessor).GetMethod("Recover",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,
            backend == "IL2CPP" ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
        PlayerSettings.runInBackground = true;
        PlayerSettings.companyName = "MNNValidation";
        PlayerSettings.productName = "WindowsLlmBenchmark";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        const string scenePath = "Assets/MNNWindowsBenchmark.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scenePath },
            locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Benchmark Player build failed.");
        File.WriteAllText(output + ".ready", backend);
    }
}
