using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNPinnedModelManifest
    {
        public MNNConversationModelAdapter adapter;
        public string modelId;
        public string revision;
        public MNNModelAssetFile archive;
        /// <summary>Optional directory containing the extracted file layout, relative to the model root.
        /// Empty installs directly in the owned model root; CosyVoice compatibility uses "model".</summary>
        public string archiveDirectory = "";
        public MNNModelAssetFile[] files = Array.Empty<MNNModelAssetFile>();

        public Task EnsureAsync(string modelsDirectory, CancellationToken token) => CaptureInstaller(modelsDirectory)(token);

        public Func<CancellationToken, Task> CaptureInstaller(string modelsDirectory,
            Action<string, long, long> progress = null, string bundledModelsDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(revision) || files == null || files.Length == 0)
                throw new InvalidOperationException("Pin a model revision and its file checksums in the manifest.");
            string directory = MNNModelAssetDownloader.ResolvePath(modelsDirectory, modelId);
            // Copy before awaiting: Unity assets belong to the main thread.
            var snapshot = new MNNModelAssetFile[files.Length];
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                if (file == null || file.bytes < 1 || file.sha256 == null || file.sha256.Length != 64)
                    throw new InvalidOperationException("Every manifest file needs a positive size and pinned SHA-256.");
                foreach (char value in file.sha256)
                    if (!Uri.IsHexDigit(value)) throw new InvalidOperationException("Manifest file SHA-256 must be hexadecimal.");
                if (!Uri.TryCreate(file.url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps ||
                    !string.IsNullOrEmpty(url.UserInfo))
                    throw new InvalidOperationException("Manifest file URLs must use HTTPS without embedded credentials.");
                string target = MNNModelAssetDownloader.ResolvePath(directory, file.path);
                if (!targets.Add(target)) throw new InvalidOperationException("Manifest contains duplicate file targets: " + file.path);
                snapshot[i] = new MNNModelAssetFile { path = file.path, url = file.url, sha256 = file.sha256, bytes = file.bytes };
            }
            string bundledDirectory = bundledModelsDirectory == null ? null :
                MNNModelAssetDownloader.ResolvePath(bundledModelsDirectory, modelId);
            var bundle = archive == null ? null : new MNNModelAssetFile { path = archive.path, url = archive.url, sha256 = archive.sha256, bytes = archive.bytes };
            string extractionDirectory = string.IsNullOrEmpty(archiveDirectory) ? directory :
                MNNModelAssetDownloader.ResolvePath(directory, archiveDirectory);
            string relativePrefix = string.IsNullOrEmpty(archiveDirectory) ? "" : archiveDirectory + "/";
            var archiveFiles = new MNNModelAssetFile[snapshot.Length];
            if (bundle != null)
            {
                if (bundle.bytes < 1 || bundle.sha256 == null || bundle.sha256.Length != 64 ||
                    !Uri.TryCreate(bundle.url, UriKind.Absolute, out var archiveUrl) || archiveUrl.Scheme != "https" || archiveUrl.UserInfo.Length != 0)
                    throw new InvalidOperationException("The archive requires HTTPS, a positive byte count and pinned SHA-256.");
                foreach (char value in bundle.sha256)
                    if (!Uri.IsHexDigit(value)) throw new InvalidOperationException("Archive SHA-256 must be hexadecimal.");
                MNNModelAssetDownloader.ResolvePath(directory, bundle.path);
                for (int i = 0; i < snapshot.Length; ++i)
                {
                    var file = snapshot[i];
                    if (!file.path.StartsWith(relativePrefix, StringComparison.Ordinal))
                        throw new InvalidOperationException("Every extracted file must be inside archiveDirectory.");
                    archiveFiles[i] = new MNNModelAssetFile { path = file.path.Substring(relativePrefix.Length), bytes = file.bytes, sha256 = file.sha256 };
                }
            }
            // Even an uncontended gate and installed archive can hash gigabytes. Dispatch all file work.
            return token => Task.Run(async () =>
            {
                using (await MNNModelPathGate.EnterAsync(directory, token).ConfigureAwait(false))
                {
                    if (bundle != null)
                    {
                        string archivePath = MNNModelAssetDownloader.ResolvePath(directory, bundle.path);
                        MNNModelArchiveInstaller.RecoverInterruptedPublication(extractionDirectory,
                            MNNModelArchiveInstaller.RetainedArchivePath(archivePath, extractionDirectory));
                        bool valid = true;
                        foreach (var file in snapshot)
                        {
                            string path = MNNModelAssetDownloader.ResolvePath(directory, file.path);
                            if (!File.Exists(path) || new FileInfo(path).Length != file.bytes ||
                                !string.Equals(MNNModelDeliveryManifest.ComputeSha256(path, token), file.sha256, StringComparison.OrdinalIgnoreCase))
                            {
                                valid = false;
                                break;
                            }
                        }
                        if (valid) return;
                        if (bundledDirectory != null)
                            await SeedBundledFileAsync(bundledDirectory, directory, bundle, token).ConfigureAwait(false);
                        await MNNModelAssetDownloader.EnsureAsync(directory, bundle,
                            (count, total) => progress?.Invoke(bundle.path, count, total), token).ConfigureAwait(false);
                        MNNModelArchiveInstaller.Install(archivePath, bundle.sha256, extractionDirectory, archiveFiles, token);
                        return;
                    }
                    foreach (var file in snapshot)
                    {
                        token.ThrowIfCancellationRequested();
                        progress?.Invoke(file.path, 0, file.bytes);
                        if (bundledDirectory != null)
                            await SeedBundledFileAsync(bundledDirectory, directory, file, token).ConfigureAwait(false);
                        await MNNModelAssetDownloader.EnsureAsync(directory, file,
                            (count, total) => progress?.Invoke(file.path, count, total), token).ConfigureAwait(false);
                    }
                }
            }, token);
        }

        public static void VerifyFiles(string directory, MNNModelAssetFile[] files, CancellationToken token)
        {
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                string path = MNNModelAssetDownloader.ResolvePath(directory, file.path);
                if (!File.Exists(path) || new FileInfo(path).Length != file.bytes ||
                    !string.Equals(MNNModelDeliveryManifest.ComputeSha256(path, token), file.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Installed model file failed size or SHA-256 verification: " + file.path);
            }
        }

        private static async Task SeedBundledFileAsync(string sourceDirectory, string directory,
            MNNModelAssetFile file, CancellationToken token)
        {
            string target = MNNModelAssetDownloader.ResolvePath(directory, file.path);
            string source = MNNModelAssetDownloader.ResolvePath(sourceDirectory, file.path);
            if (File.Exists(target) || !File.Exists(source) || new FileInfo(source).Length != file.bytes) return;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string temporary = target + ".seed-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true))
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true))
                    await input.CopyToAsync(output, 131072, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                try { File.Move(temporary, target); }
                catch (IOException) when (File.Exists(target)) { /* Another installer won; EnsureAsync verifies it. */ }
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
