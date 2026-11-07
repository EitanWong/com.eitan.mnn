using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Unity
{
    public static partial class MNNPinnedModelMetadata
    {
        public sealed class ManifestEntry
        {
            public string modelId, repository, revision, proofPath, proofHash;
            public MNNConversationModelAdapter adapter;
            public long bytes;
            public bool modelScope;
        }
        public static IReadOnlyList<ManifestEntry> Describe(IEnumerable<MNNPinnedModelManifest> manifests)
        {
            var entries = new List<ManifestEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var manifest in manifests ?? Array.Empty<MNNPinnedModelManifest>())
            {
                if (manifest == null || string.IsNullOrWhiteSpace(manifest.modelId) || !seen.Add(manifest.modelId) ||
                    string.IsNullOrWhiteSpace(manifest.revision) || manifest.files == null || manifest.files.Length == 0) continue;
                if (!Enum.IsDefined(typeof(MNNConversationModelAdapter), manifest.adapter)) continue;
                bool pinned = true;
                long bytes = 0;
                string repository = null, proofPath = null, proofHash = null;
                bool modelScope = false;
                foreach (var file in manifest.files)
                {
                    if (file == null || file.bytes < 1 || file.sha256 == null || file.sha256.Length != 64 || file.sha256.Any(value => !Uri.IsHexDigit(value)))
                    { pinned = false; break; }
                    if (file.bytes > long.MaxValue - bytes) { pinned = false; break; }
                    bytes += file.bytes;
                    if (repository == null && Uri.TryCreate(file.url, UriKind.Absolute, out var uri))
                    {
                        string[] parts = uri.AbsolutePath.Trim('/').Split('/');
                        if (uri.Host == "huggingface.co" && parts.Length >= 5 && parts[2] == "resolve" && parts[3] == manifest.revision)
                            repository = parts[0] + "/" + parts[1];
                        else if ((uri.Host == "www.modelscope.cn" || uri.Host == "modelscope.cn") &&
                            parts.Length >= 6 && parts[0] == "api" && parts[2] == "models" &&
                            uri.Query.IndexOf(manifest.revision, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            repository = parts[3] + "/" + parts[4];
                            modelScope = true;
                        }
                        if (repository != null) { proofPath = file.path; proofHash = file.sha256; }
                    }
                }
                if (pinned) entries.Add(new ManifestEntry
                {
                    modelId = manifest.modelId,
                    adapter = manifest.adapter,
                    repository = repository,
                    revision = manifest.revision,
                    bytes = bytes,
                    modelScope = modelScope,
                    proofPath = proofPath,
                    proofHash = proofHash
                });
            }
            entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.modelId, b.modelId));
            return entries;
        }
    }
}
