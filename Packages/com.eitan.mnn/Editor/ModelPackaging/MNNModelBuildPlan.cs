using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>Pure planning, shared by the settings preview, build hook, and tests.</summary>
    internal sealed class MNNModelBuildPlan
    {
        internal string[] IncludedFiles;
        internal long IncludedBytes;

        internal static MNNModelBuildPlan Create(string modelsRoot, MNNModelPackagingMode mode, IEnumerable<string> selections)
        {
            if (!Enum.IsDefined(typeof(MNNModelPackagingMode), mode)) throw new InvalidDataException("Unknown MNN packaging mode.");
            if (Directory.Exists(modelsRoot) && (File.GetAttributes(modelsRoot) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("MNN Models root must not be a symbolic link.");
            var paths = (selections ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            if (mode == MNNModelPackagingMode.Selected)
                foreach (var path in paths)
                {
                    var full = MNNModelAssetDownloader.ResolvePath(modelsRoot, path);
                    if (!IsPackagedPath(path) || !File.Exists(full) && !Directory.Exists(full))
                        throw new FileNotFoundException("Selected MNN file or directory is missing or excluded: " + path);
                }
            var files = mode == MNNModelPackagingMode.None || !Directory.Exists(modelsRoot)
                ? Array.Empty<string>() : EnumerateFiles(modelsRoot).Where(path => mode == MNNModelPackagingMode.All ||
                    paths.Any(selected => path == selected || path.StartsWith(selected + "/", StringComparison.Ordinal))).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            return new MNNModelBuildPlan
            {
                IncludedFiles = files,
                IncludedBytes = files.Sum(path => new FileInfo(Path.Combine(modelsRoot, path)).Length)
            };
        }

        internal static IEnumerable<string> EnumerateFiles(string root)
        {
            // Never traverse symlinks/junctions, even when they point back inside the model tree.
            foreach (var entry in Directory.EnumerateFileSystemEntries(root).OrderBy(path => path, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(entry);
                if (!IsPackagedPath(name)) continue;
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("MNN model packaging does not follow symbolic links: " + entry);
                if (Directory.Exists(entry))
                    foreach (var child in EnumerateFiles(entry)) yield return name + "/" + child;
                else yield return name;
            }
        }

        private static bool IsPackagedPath(string path) => path.Split('/').All(part =>
            !string.IsNullOrEmpty(part) && !part.StartsWith(".", StringComparison.Ordinal) && !part.EndsWith("~", StringComparison.Ordinal) &&
            !part.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && !part.EndsWith(".part", StringComparison.OrdinalIgnoreCase) &&
            !part.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) && !part.EndsWith(".mnn-parts", StringComparison.OrdinalIgnoreCase));

        internal MNNModelDeliveryManifest CreateManifest(string root, MNNProjectSettings settings)
        {
            if (settings.SchemaVersion != 1) throw new InvalidDataException("Unsupported MNN project settings schema.");
            var manifest = new MNNModelDeliveryManifest
            {
                allowDownloads = settings.allowDownloads,
                bundledStorage = settings.bundledStorage,
                requestTimeout = settings.requestTimeout
            };
            var files = new Dictionary<string, MNNModelDeliveryFile>(StringComparer.Ordinal);
            if (settings.remoteManifest != null)
            {
                var remote = ReadRemoteManifest(settings.remoteManifest.text);
                foreach (var file in remote.files) files.Add(file.path, file);
            }
            try
            {
                for (var i = 0; i < IncludedFiles.Length; i++)
                {
                    var path = IncludedFiles[i];
                    if (UnityEditor.EditorUtility.DisplayCancelableProgressBar("MNN model packaging", path, (float)i / IncludedFiles.Length))
                        throw new OperationCanceledException("MNN model packaging cancelled.");
                    var full = Path.Combine(root, path);
                    var hash = MNNModelDeliveryManifest.ComputeSha256(full);
                    var bytes = new FileInfo(full).Length;
                    if (files.TryGetValue(path, out var remote) && (remote.bytes != bytes || !string.Equals(remote.sha256, hash, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("Bundled model differs from the pinned remote manifest: " + path);
                    files[path] = new MNNModelDeliveryFile { path = path, bytes = bytes, sha256 = hash, bundled = true, url = remote?.url };
                }
            }
            finally { UnityEditor.EditorUtility.ClearProgressBar(); }
            manifest.files = files.Values.OrderBy(file => file.path, StringComparer.Ordinal).ToArray();
            manifest.Validate();
            return manifest;
        }

        internal static MNNModelDeliveryManifest ReadRemoteManifest(string json)
        {
            var remote = JsonUtility.FromJson<MNNModelDeliveryManifest>(json);
            if (remote == null) throw new InvalidDataException("Empty MNN remote manifest.");
            // Only schemaVersion and files belong to the remote catalog; runtime policy belongs to this project.
            remote.requestTimeout = 300;
            remote.bundledStorage = MNNBundledModelStorage.Automatic;
            foreach (var file in remote.files ?? Array.Empty<MNNModelDeliveryFile>())
            {
                if (file == null) throw new InvalidDataException("Null remote model entry.");
                file.bundled = false;
            }
            remote.Validate();
            return remote;
        }
    }
}
