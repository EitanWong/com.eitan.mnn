using System;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.TestTools;
using System.IO;

[assembly: TestPlayerBuildModifier(typeof(MNNValidationPlayerLauncher))]
// Let the Python runner launch its own macOS batch Player, without focus or
// window-server dependencies. This file exists only in the isolated project.
public sealed class MNNValidationPlayerLauncher : ITestPlayerBuildModifier
{
    public BuildPlayerOptions ModifyOptions(BuildPlayerOptions options)
    {
        if (Environment.GetEnvironmentVariable("MNN_VALIDATION_PLAYER_READY") != null)
            options.options &= ~BuildOptions.AutoRunPlayer;
        return options;
    }

    [PostProcessBuild(999)]
    public static void Ready(BuildTarget target, string path)
    {
        string marker = Environment.GetEnvironmentVariable("MNN_VALIDATION_PLAYER_READY");
        if (target == BuildTarget.StandaloneOSX && marker != null)
            File.WriteAllText(marker, Path.GetFullPath(path));
    }
}

// Copied into the isolated validation project's Assets/Editor by the runner.
public static class MNNPlatformValidationSetup
{
    public static void Configure()
    {
        string value = Environment.GetEnvironmentVariable("MNN_VALIDATION_MAC_ARCH");
        int architecture;
        switch (value)
        {
            case "x64":
                architecture = 0;
                break;
            case "arm64":
                architecture = 1;
                break;
            case "universal":
                architecture = 2;
                break;
            default:
                throw new ArgumentException("Expected x64, arm64 or universal architecture.");
        }

        // Unity 2021 uses the platform module's architecture setting for macOS.
        EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX), "Architecture", value == "universal" ? "x64ARM64" : value);
        PlayerSettings.SetArchitecture(BuildTargetGroup.Standalone, architecture);
        AssetDatabase.SaveAssets();
        UnityEngine.Debug.Log("MNN validation macOS architecture: " + value);
    }
}
