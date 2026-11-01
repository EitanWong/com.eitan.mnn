using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor
{
    public enum MNNModelPackagingMode
    {
        None,
        Selected,
        All
    }

    /// <summary>Source-controlled Editor configuration. Player builds receive a separate versioned snapshot.</summary>
    [FilePath("ProjectSettings/MNNSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class MNNProjectSettings : ScriptableSingleton<MNNProjectSettings>
    {
        [SerializeField] private int schemaVersion = 1;
        public MNNModelPackagingMode packagingMode = MNNModelPackagingMode.None;
        public List<string> includedPaths = new List<string>();
        public bool allowDownloads = true;
        public MNNBundledModelStorage bundledStorage = MNNBundledModelStorage.Automatic;
        public int requestTimeout = 300;
        public TextAsset remoteManifest;

        public int SchemaVersion => schemaVersion;
        public void SaveSettings() => Save(true);
    }
}
