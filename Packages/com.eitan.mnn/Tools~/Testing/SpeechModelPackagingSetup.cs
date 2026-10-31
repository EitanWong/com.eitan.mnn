using System;
using System.Collections.Generic;
using System.IO;
using MNN.Unity.Editor;
using UnityEditor;
using UnityEngine;

// The test runner copies this tooling into the isolated project's Editor folder.
// The frozen profile snapshot is also the build selection; do not duplicate names.
public static class SpeechModelPackagingSetup
{
    [Serializable] private sealed class Snapshot { public Profile[] models = null; }
    [Serializable] private sealed class Profile { public string model = ""; }

    [InitializeOnLoadMethod]
    private static void Configure()
    {
        if (Environment.GetEnvironmentVariable("MNN_VALIDATION_SPEECH_PACKAGING") != "1") return;
        string path = Environment.GetEnvironmentVariable("MNN_VALIDATION_SPEECH_PROFILE_SNAPSHOT");
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Set the frozen speech profile snapshot before building.");
        var snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(path));
        if (snapshot == null || snapshot.models == null || snapshot.models.Length == 0)
            throw new InvalidDataException("The frozen speech profile snapshot is empty.");
        var included = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in snapshot.models)
        {
            string name = profile?.model;
            if (string.IsNullOrWhiteSpace(name) || !name.EndsWith("-MNN", StringComparison.Ordinal) ||
                name.IndexOfAny(new[] { '/', '\\' }) >= 0 || !unique.Add(name))
                throw new InvalidDataException("Invalid or duplicate frozen speech profile: " + name);
            if (!Directory.Exists(Path.Combine(Application.streamingAssetsPath, "MNN", "Models", name)))
                throw new DirectoryNotFoundException("Missing staged speech profile: " + name);
            included.Add(name);
        }
        var settings = MNNProjectSettings.instance;
        settings.packagingMode = MNNModelPackagingMode.Selected;
        settings.includedPaths = included;
        settings.SaveSettings();
        Debug.Log("[MNN validation] Selected " + included.Count + " speech profiles from " + path);
    }
}
