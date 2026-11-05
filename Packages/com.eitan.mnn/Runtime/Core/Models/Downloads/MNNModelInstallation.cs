using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>One writer per installation, atomic SHA-256 receipts and retryable interrupted downloads.
    /// Synchronous checks hash model files; use EnsureAsync or call checks on a background worker.
    /// A local adoption records integrity from adoption time; it is not proof of remote provenance.</summary>
    public static class MNNModelInstallation
    {
        public enum Phase { Waiting, Checking, Connecting, Preparing, Complete, Reused, Cancelled, Failed }
        public const string ReceiptName = ".mnn-install.json";
        private const string LegacyReceiptName = ".siriai-install.json";
        [Serializable] private sealed class InstalledFile { public string path, sha256; public long bytes; }
        [Serializable]
        private sealed class Receipt
        {
            public int schemaVersion = 2;
            public MNNRemoteModel model;
            public bool adoptedLocal;
            public InstalledFile[] files;
        }

        public static string DirectoryFor(string root, MNNRemoteModel model, string installationName = null)
        {
            var identity = MNNModelRepositoryIdentity.FromModel(model);
            return MNNModelAssetDownloader.ResolvePath(root, installationName ?? "download-" + MNNSecretStore.AliasHash(identity.ToString()));
        }
        public static bool HasReceipt(string directory) => File.Exists(Path.Combine(directory, ReceiptName)) || File.Exists(Path.Combine(directory, LegacyReceiptName));

        public static bool IsInstalled(string directory, MNNRemoteModel model, CancellationToken token = default)
        {
            var identity = MNNModelRepositoryIdentity.FromModel(model);
            try
            {
                var receipt = ReadReceipt(directory);
                if (receipt?.schemaVersion != 2 || receipt.model == null || receipt.files == null || receipt.files.Length == 0 ||
                    !MNNModelRepositoryIdentity.TryCreate(receipt.model, out var recorded) || !identity.Equals(recorded)) return false;
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in receipt.files)
                {
                    token.ThrowIfCancellationRequested();
                    if (file == null || file.bytes < 1 || file.sha256?.Length != 64 || !paths.Add(file.path)) return false;
                    string path = MNNModelAssetDownloader.ResolvePath(directory, file.path);
                    if (!File.Exists(path) || new FileInfo(path).Length != file.bytes ||
                        !string.Equals(MNNModelDeliveryManifest.ComputeSha256(path, token), file.sha256, StringComparison.OrdinalIgnoreCase)) return false;
                }
                return true;
            }
            catch (Exception error) when (FileError(error)) { return false; }
        }

        /// <summary>Cheap file-presence hint for UI only. This skips SHA-256; EnsureAsync always verifies before reuse.</summary>
        public static bool HasRecordedFiles(string directory, MNNRemoteModel model)
        {
            var identity = MNNModelRepositoryIdentity.FromModel(model);
            try
            {
                var receipt = ReadReceipt(directory);
                return receipt?.schemaVersion == 2 && MNNModelRepositoryIdentity.TryCreate(receipt.model, out var recorded) && identity.Equals(recorded) &&
                    receipt.files?.Length > 0 && receipt.files.All(file => file != null && File.Exists(MNNModelAssetDownloader.ResolvePath(directory, file.path)) &&
                        new FileInfo(MNNModelAssetDownloader.ResolvePath(directory, file.path)).Length == file.bytes);
            }
            catch (Exception error) when (FileError(error)) { return false; }
        }

        /// <summary>Reads catalog metadata from receipts without hashing weights. Presence is not an integrity result.</summary>
        public static IReadOnlyList<MNNRemoteModel> ReadRecordedModels(string root)
        {
            var result = new List<MNNRemoteModel>();
            if (!Directory.Exists(root)) return result;
            foreach (string directory in RecordedDirectories(root))
            {
                try
                {
                    var receipt = ReadReceipt(directory);
                    if (MNNModelRepositoryIdentity.TryCreate(receipt?.model, out _)) result.Add(MNNRemoteModelSnapshot.Copy(receipt.model));
                }
                catch (Exception error) when (FileError(error)) { }
            }
            return result;
        }

        public static IReadOnlyList<MNNRemoteModel> ReadInstalled(string root, CancellationToken token = default)
        {
            var models = new List<MNNRemoteModel>();
            if (!Directory.Exists(root)) return models;
            foreach (string directory in RecordedDirectories(root))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var receipt = ReadReceipt(directory);
                    if (receipt?.model != null && IsInstalled(directory, receipt.model, token)) models.Add(MNNRemoteModelSnapshot.Copy(receipt.model));
                }
                catch (Exception error) when (FileError(error)) { }
            }
            return models;
        }

        public static async Task<string> EnsureAsync(string directory, MNNRemoteModel model,
            Action<long, long, string> progress = null, CancellationToken token = default,
            MNNModelDownloader downloader = null, Action<Phase, string> state = null,
            Func<CancellationToken, Task> download = null, Func<bool> canAdoptLocal = null)
        {
            var snapshot = MNNRemoteModelSnapshot.Copy(model);
            MNNModelRepositoryIdentity.FromModel(snapshot);
            directory = Path.GetFullPath(directory);
            state?.Invoke(Phase.Waiting, directory);
            try
            {
                using (await MNNModelPathGate.EnterAsync(directory, token).ConfigureAwait(false))
                {
                    state?.Invoke(Phase.Checking, directory);
                    if (await Task.Run(() => IsInstalled(directory, snapshot, token), token).ConfigureAwait(false))
                    { state?.Invoke(Phase.Reused, directory); return directory; }
                    // Never bless altered files using a structural check when a receipt already exists.
                    bool adopted = !HasReceipt(directory) && canAdoptLocal != null &&
                        await Task.Run(canAdoptLocal, token).ConfigureAwait(false);
                    if (!adopted)
                    {
                        state?.Invoke(Phase.Connecting, directory);
                        if (download != null) await download(token).ConfigureAwait(false);
                        else
                        {
                            bool owns = downloader == null;
                            if (owns) downloader = new MNNModelDownloader();
                            try { await downloader.DownloadAsync(snapshot, directory, progress, token).ConfigureAwait(false); }
                            finally { if (owns) downloader.Dispose(); }
                        }
                    }
                    token.ThrowIfCancellationRequested();
                    state?.Invoke(Phase.Preparing, directory);
                    await Task.Run(() => WriteReceipt(directory, snapshot, adopted, token), token).ConfigureAwait(false);
                    state?.Invoke(adopted ? Phase.Reused : Phase.Complete, directory);
                    return directory;
                }
            }
            catch (OperationCanceledException) { state?.Invoke(Phase.Cancelled, directory); throw; }
            catch { state?.Invoke(Phase.Failed, directory); throw; }
        }

        private static void WriteReceipt(string directory, MNNRemoteModel model, bool adopted, CancellationToken token)
        {
            var files = new List<InstalledFile>();
            foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                string name = Path.GetFileName(path);
                if (name == ReceiptName || name == LegacyReceiptName || name.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith(".mnn-download-", StringComparison.Ordinal) || name.Contains(".seed-")) continue;
                long bytes = new FileInfo(path).Length;
                if (bytes < 1) continue;
                string relative = path.Substring(directory.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/');
                MNNModelAssetDownloader.ResolvePath(directory, relative);
                files.Add(new InstalledFile { path = relative, bytes = bytes, sha256 = MNNModelDeliveryManifest.ComputeSha256(path, token) });
                if (files.Count > 8192) throw new InvalidDataException("Model installation has too many files.");
            }
            if (files.Count == 0) throw new InvalidDataException("The repository produced no installed model files.");
            var receipt = new Receipt { model = model, adoptedLocal = adopted, files = files.ToArray() };
            Store(directory).Save(receipt);
        }
        private static MNNJsonSettingsStore<Receipt> Store(string directory) => new MNNJsonSettingsStore<Receipt>(
            Path.Combine(directory, ReceiptName), receipt => { if (receipt.schemaVersion != 2) throw new InvalidDataException("Unsupported model receipt."); }, 4 * 1024 * 1024);
        private static Receipt ReadReceipt(string directory) => Store(directory).Load();
        private static string[] RecordedDirectories(string root)
        {
            try { return Directory.GetDirectories(root); }
            catch (Exception error) when (FileError(error)) { return Array.Empty<string>(); }
        }
        private static bool FileError(Exception error) => error is IOException || error is UnauthorizedAccessException ||
            error is ArgumentException || error is NotSupportedException || error is System.Security.SecurityException;
    }
}
