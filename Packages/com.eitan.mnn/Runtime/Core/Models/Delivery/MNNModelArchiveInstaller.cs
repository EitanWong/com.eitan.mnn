using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Verifies an exact ZIP model layout in a private directory before publishing it.
    /// The destination is wholly owned by this installation. Callers serialize operations for that directory.
    /// An archive inside the destination is retained across publication. No archive metadata is trusted as a filesystem path.</summary>
    public static class MNNModelArchiveInstaller
    {
        public const int MaximumEntries = 8192;
        public const long MaximumExpandedBytes = 32L * 1024 * 1024 * 1024;

        /// <summary>Installs exact relative paths. A single enclosing ZIP folder may be removed when it yields the exact declared layout.
        /// File SHA-256 values are required. Cancellation is observed during verification and extraction, before the short publication step.</summary>
        public static void Install(string archivePath, string archiveSha256, string destination,
            IReadOnlyList<MNNModelAssetFile> requiredFiles, CancellationToken token = default, Action<long> onExtracted = null)
        {
            InstallCore(archivePath, archiveSha256, destination, requiredFiles, token, onExtracted,
                MaximumEntries, MaximumExpandedBytes, true);
        }

        internal static void InstallCore(string archivePath, string archiveSha256, string destination,
            IReadOnlyList<MNNModelAssetFile> requiredFiles, CancellationToken token, Action<long> onExtracted,
            int maximumEntries, long maximumExpandedBytes, bool requireFileHashes)
        {
            token.ThrowIfCancellationRequested();
            archivePath = Path.GetFullPath(archivePath);
            destination = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
            if (Path.GetDirectoryName(destination) == null) throw new ArgumentException("Choose a model directory below the filesystem root.");
            RejectLinkAncestors(destination);
            RejectLinkAncestors(archivePath);
            ValidateHash(archiveSha256);
            var files = CaptureFiles(destination, requiredFiles, maximumEntries, maximumExpandedBytes, requireFileHashes);
            string retainedArchive = RetainedArchivePath(archivePath, destination);
            if (retainedArchive != null && files.Keys.Any(path => path.Equals(retainedArchive, StringComparison.OrdinalIgnoreCase) || retainedArchive.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("The archive cannot also be an extracted model file.");
            RecoverInterruptedPublication(destination, retainedArchive);
            if (!File.Exists(archivePath)) throw new FileNotFoundException("The complete model archive is missing.", archivePath);
            if (!string.Equals(MNNModelDeliveryManifest.ComputeSha256(archivePath, token), archiveSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Model archive SHA-256 verification failed.");
            string temporary = ExtractionDirectory(destination);
            Directory.CreateDirectory(temporary);
            try
            {
                using (var archive = ZipFile.OpenRead(archivePath))
                {
                    var entries = ReadEntries(archive, maximumEntries, maximumExpandedBytes, token);
                    string prefix = SelectPrefix(entries, files);
                    ValidateDirectories(archive, prefix, files);
                    long extracted = 0;
                    var buffer = new byte[128 * 1024];
                    foreach (var file in files)
                    {
                        token.ThrowIfCancellationRequested();
                        var entry = entries[prefix + file.Key];
                        string path = MNNModelAssetDownloader.ResolvePath(temporary, file.Key);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        Extract(entry, path, file.Value, buffer, token, count => { extracted += count; onExtracted?.Invoke(extracted); });
                    }
                }
                token.ThrowIfCancellationRequested();
                Publish(destination, temporary, retainedArchive);
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
            }
        }

        private static Dictionary<string, MNNModelAssetFile> CaptureFiles(string destination,
            IReadOnlyList<MNNModelAssetFile> required, int maximumEntries, long maximumBytes, bool hashes)
        {
            if (required == null || required.Count == 0 || required.Count > maximumEntries)
                throw new InvalidDataException("Declare a bounded nonempty archive file list.");
            var files = new Dictionary<string, MNNModelAssetFile>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var file in required)
            {
                if (file == null || file.bytes < 0 || hashes && file.bytes == 0)
                    throw new InvalidDataException(hashes ? "Archive file sizes must be positive." : "Archive file sizes cannot be negative.");
                string path = ValidatePath(file.path);
                MNNModelAssetDownloader.ResolvePath(destination, path);
                if (files.ContainsKey(path)) throw new InvalidDataException("Duplicate archive file target: " + path);
                if (file.bytes > maximumBytes - total) throw new InvalidDataException("Model archive exceeds its expanded byte budget.");
                total += file.bytes;
                if (hashes || file.sha256 != null) ValidateHash(file.sha256);
                files.Add(path, new MNNModelAssetFile { path = path, bytes = file.bytes, sha256 = file.sha256 });
            }
            return files;
        }

        private static Dictionary<string, ZipArchiveEntry> ReadEntries(ZipArchive archive, int maximumEntries,
            long maximumBytes, CancellationToken token)
        {
            if (archive.Entries.Count > maximumEntries) throw new InvalidDataException("Model archive has too many entries.");
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long expanded = 0;
            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();
                bool directory = entry.FullName.EndsWith("/", StringComparison.Ordinal);
                string path = ValidatePath(directory ? entry.FullName.Substring(0, entry.FullName.Length - 1) : entry.FullName);
                int kind = (entry.ExternalAttributes >> 16) & 0xf000;
                if (kind == 0xa000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Model archives cannot contain symbolic links.");
                if (!paths.Add(path)) throw new InvalidDataException("Model archive contains duplicate paths: " + path);
                if (directory)
                {
                    if (entry.Length != 0) throw new InvalidDataException("An archive directory entry contains data.");
                    continue;
                }
                if (entry.Length < 0 || entry.Length > maximumBytes - expanded) throw new InvalidDataException("Model archive exceeds its expanded byte budget.");
                expanded += entry.Length;
                entries.Add(path, entry);
            }
            return entries;
        }

        private static string SelectPrefix(Dictionary<string, ZipArchiveEntry> entries, Dictionary<string, MNNModelAssetFile> required)
        {
            bool Matches(string prefix) => entries.Count == required.Count && required.All(file =>
                entries.TryGetValue(prefix + file.Key, out var entry) && entry.Length == file.Value.bytes);
            if (Matches("")) return "";
            if (entries.Count > 0)
            {
                string first = entries.Keys.First();
                int slash = first.IndexOf('/');
                if (slash > 0)
                {
                    string prefix = first.Substring(0, slash + 1);
                    if (entries.Keys.All(path => path.StartsWith(prefix, StringComparison.Ordinal)) && Matches(prefix)) return prefix;
                }
            }
            throw new InvalidDataException("Model archive does not contain exactly the declared relative files and sizes.");
        }

        private static void ValidateDirectories(ZipArchive archive, string prefix, Dictionary<string, MNNModelAssetFile> required)
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in required.Keys)
            {
                string path = prefix + file;
                for (int slash = path.IndexOf('/'); slash >= 0; slash = path.IndexOf('/', slash + 1))
                    allowed.Add(path.Substring(0, slash + 1));
            }
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal) && !allowed.Contains(entry.FullName))
                    throw new InvalidDataException("Model archive contains an unexpected directory: " + entry.FullName);
            }
        }

        private static void Extract(ZipArchiveEntry entry, string path, MNNModelAssetFile file, byte[] buffer,
            CancellationToken token, Action<long> extracted)
        {
            long written = 0;
            using (var source = entry.Open())
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var hash = file.sha256 == null ? null : SHA256.Create())
            {
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (read > file.bytes - written) throw new InvalidDataException("Archive file exceeds its declared length.");
                    output.Write(buffer, 0, read);
                    hash?.TransformBlock(buffer, 0, read, buffer, 0);
                    written += read;
                    extracted?.Invoke(read);
                }
                token.ThrowIfCancellationRequested();
                if (written != file.bytes) throw new InvalidDataException("Model archive file is truncated.");
                if (hash != null)
                {
                    hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    if (!string.Equals(BitConverter.ToString(hash.Hash).Replace("-", ""), file.sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Extracted model file failed SHA-256 verification: " + file.path);
                }
            }
        }

        internal static void RecoverInterruptedPublication(string destination, string retainedArchive)
        {
            RejectLinkAncestors(destination);
            string previous = PreviousDirectory(destination);
            string temporary = ExtractionDirectory(destination);
            RejectLinkAncestors(previous);
            RejectLinkAncestors(temporary);
            if (Directory.Exists(previous))
            {
                if (!Directory.Exists(destination))
                {
                    RestoreRetainedArchive(temporary, previous, retainedArchive);
                    Directory.Move(previous, destination);
                }
                else Directory.Delete(previous, true);
            }
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }

        private static void Publish(string destination, string temporary, string retainedArchive)
        {
            string previous = PreviousDirectory(destination);
            bool movedPrevious = false;
            try
            {
                if (Directory.Exists(destination))
                {
                    Directory.Move(destination, previous);
                    movedPrevious = true;
                    RestoreRetainedArchive(previous, temporary, retainedArchive);
                }
                Directory.Move(temporary, destination);
            }
            catch
            {
                if (movedPrevious && !Directory.Exists(destination))
                {
                    RestoreRetainedArchive(temporary, previous, retainedArchive);
                    Directory.Move(previous, destination);
                }
                throw;
            }
            if (Directory.Exists(previous)) Directory.Delete(previous, true);
        }

        private static void RestoreRetainedArchive(string source, string target, string relative)
        {
            if (relative == null) return;
            string from = MNNModelAssetDownloader.ResolvePath(source, relative);
            if (!File.Exists(from)) return;
            string to = MNNModelAssetDownloader.ResolvePath(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Move(from, to);
        }

        internal static string RetainedArchivePath(string archive, string destination)
        {
            string prefix = destination.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return archive.StartsWith(prefix, StringComparison.Ordinal) ? archive.Substring(prefix.Length).Replace('\\', '/') : null;
        }

        private static string ExtractionDirectory(string destination) => destination + ".mnn-extracting";
        private static string PreviousDirectory(string destination) => destination + ".mnn-previous";

        private static void ValidateHash(string hash)
        {
            if (hash == null || hash.Length != 64 || hash.Any(value => !Uri.IsHexDigit(value)))
                throw new InvalidDataException("Pin a hexadecimal SHA-256 checksum for each archive and file.");
        }

        private static string ValidatePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Contains("\\") || Path.IsPathRooted(value) ||
                value.Split('/').Any(part => string.IsNullOrEmpty(part) || part == "." || part == ".." ||
                    part.IndexOf(':') >= 0 || part.Any(char.IsControl) || part.TrimEnd(' ', '.') != part))
                throw new InvalidDataException("Unsafe model archive path.");
            return value;
        }

        private static void RejectLinkAncestors(string path)
        {
            for (string at = path; at != null; at = Path.GetDirectoryName(at))
            {
                if ((Directory.Exists(at) || File.Exists(at)) && (File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Model archive directories cannot be symbolic links.");
            }
        }
    }
}
