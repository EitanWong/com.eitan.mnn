using System;
using System.Collections.Generic;
using System.IO;

namespace MNN.Unity.Editor
{
    // Build once per local refresh. Repository identity survives folder renames;
    // names alone must never merge installations belonging to different authors.
    internal sealed class MNNModelInstallIndex
    {
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static MNNModelInstallIndex Read(string root)
        {
            var index = new MNNModelInstallIndex();
            if (!Directory.Exists(root)) return index;
            foreach (string directory in Directory.GetDirectories(root))
            {
                try
                {
                    string marker = Path.Combine(directory, MNNModelDownloadTasks.InstallMarker);
                    if (!File.Exists(marker)) continue;
                    string identity = File.ReadAllText(marker).Trim();
                    if (identity.StartsWith("hf:", StringComparison.OrdinalIgnoreCase))
                    {
                        int revision = identity.IndexOf('@');
                        if (revision >= 0) identity = identity.Substring(0, revision);
                    }
                    if (string.IsNullOrWhiteSpace(identity)) continue;
                    // Do not select an arbitrary directory when two installations
                    // have the same identity. The canonical path still takes priority.
                    if (index._paths.ContainsKey(identity)) index._paths[identity] = null;
                    else index._paths.Add(identity, directory);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return index;
        }

        internal string Find(MNNModelInfo model)
        {
            string repository = model.repositoryId ?? model.modelScopeId ?? model.id;
            if (string.IsNullOrWhiteSpace(repository)) return null;
            string identity = model.provider == MNNModelProvider.HuggingFace ? "hf:" + repository : repository;
            return _paths.TryGetValue(identity, out string directory) ? directory : null;
        }
    }
}
