using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    public static class MNNCosyVoiceAssets
    {
        public static void Install(string stagingPath, CancellationToken token)
        {
            InstallArchive(stagingPath, MNNCosyVoiceModel.ArchiveSha256,
                MNNCosyVoiceModel.RequiredFiles.Select((name, i) => new KeyValuePair<string, long>(name, MNNCosyVoiceModel.RequiredSizes[i]))
                    .ToDictionary(pair => pair.Key, pair => pair.Value), token);
        }

        /// <summary>Compatibility entry point for the official complete CosyVoice archive.
        /// Its pinned archive SHA establishes provenance; existing callers provide the exact file sizes.</summary>
        public static void InstallArchive(string stagingPath, string sha256, IDictionary<string, long> required,
            CancellationToken token = default, Action<long> onExtracted = null)
        {
            if (required == null) throw new ArgumentNullException(nameof(required));
            var files = required.Select(file => new MNNModelAssetFile { path = file.Key, bytes = file.Value }).ToArray();
            MNNModelArchiveInstaller.InstallCore(Path.Combine(stagingPath, MNNCosyVoiceModel.Archive), sha256,
                Path.Combine(stagingPath, "model"), files, token, onExtracted, 256, 2L * 1024 * 1024 * 1024, false);
        }

        public static string FileHash(string path, CancellationToken token = default) => Hash(path, token);
        public static string Hash(string path, CancellationToken token = default) => MNNModelDeliveryManifest.ComputeSha256(path, token);
    }
}
