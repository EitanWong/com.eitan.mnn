using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace MNN.Unity
{
    public static class MNNCosyVoiceAssets
    {
        internal static void Install(string stagingPath, CancellationToken token)
        {
            InstallArchive(stagingPath, MNNCosyVoiceModel.ArchiveSha256, MNNCosyVoiceModel.RequiredFiles.Select((name, i) => new KeyValuePair<string, long>(name, MNNCosyVoiceModel.RequiredSizes[i])).ToDictionary(pair => pair.Key, pair => pair.Value), token);
        }

        // Retain the verified archive until the download job publishes the entire
        // staging directory. Pausing or a process interruption can safely retry.
        public static void InstallArchive(string stagingPath, string sha256, IDictionary<string, long> required, CancellationToken token = default, Action<long> onExtracted = null)
        {
            token.ThrowIfCancellationRequested();
            var archivePath = Path.Combine(stagingPath, MNNCosyVoiceModel.Archive);
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("CosyVoice complete model archive is missing.", archivePath);
            if (!string.Equals(Hash(archivePath, token), sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("CosyVoice archive SHA-256 verification failed. Remove the archive and retry the download.");
            var extractionPath = Path.Combine(stagingPath, ".cosyvoice-extracting");
            var destination = Path.Combine(stagingPath, "model");
            RejectLink(stagingPath);
            RejectLink(extractionPath);
            RejectLink(destination);
            if (Directory.Exists(extractionPath))
                Directory.Delete(extractionPath, true);
            Directory.CreateDirectory(extractionPath);
            try
            {
                using (var archive = ZipFile.OpenRead(archivePath))
                {
                    if (archive.Entries.Count > 256 || archive.Entries.Sum(entry => entry.Length) > 2L * 1024 * 1024 * 1024)
                        throw new InvalidDataException("CosyVoice archive exceeds supported entry or size limits.");
                    var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
                    var directories = new List<string>();
                    var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in archive.Entries)
                    {
                        token.ThrowIfCancellationRequested();
                        bool directory = entry.FullName.EndsWith("/", StringComparison.Ordinal);
                        string path = ValidatePath(directory ? entry.FullName.Substring(0, entry.FullName.Length - 1) : entry.FullName);
                        int kind = (entry.ExternalAttributes >> 16) & 0xf000;
                        if (kind == 0xa000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                            throw new InvalidDataException("CosyVoice archive contains a symbolic link.");
                        if (!paths.Add(path))
                            throw new InvalidDataException("CosyVoice archive contains duplicate paths: " + path);
                        if (directory)
                        {
                            if (entry.Length != 0)
                                throw new InvalidDataException("CosyVoice directory entry contains data.");
                            directories.Add(path);
                            continue;
                        }

                        if (entries.ContainsKey(path))
                            throw new InvalidDataException("CosyVoice archive contains duplicate paths: " + path);
                        entries.Add(path, entry);
                    }

                    // Accept flat upstream archives or a single enclosing folder;
                    // never flatten arbitrary directory trees into colliding filenames.
                    string prefix = entries.Count > 0 && entries.Keys.All(path => path.Contains("/")) ? entries.Keys.First().Split('/')[0] + "/" : string.Empty;
                    if (entries.Count != required.Count || entries.Keys.Any(path => !path.StartsWith(prefix, StringComparison.Ordinal)) || directories.Any(path => prefix.Length == 0 || path != prefix.TrimEnd('/')))
                        throw new InvalidDataException("CosyVoice archive has unexpected contents.");
                    foreach (var file in required)
                    {
                        if (!entries.TryGetValue(prefix + file.Key, out var entry) || entry.Length != file.Value)
                            throw new InvalidDataException("CosyVoice archive is missing or has an invalid file: " + file.Key);
                    }

                    var buffer = new byte[1024 * 1024];
                    long extracted = 0;
                    foreach (var file in required)
                    {
                        token.ThrowIfCancellationRequested();
                        using (var input = entries[prefix + file.Key].Open())
                        using (var output = new FileStream(Path.Combine(extractionPath, file.Key), FileMode.CreateNew, FileAccess.Write))
                        {
                            long written = 0;
                            int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                token.ThrowIfCancellationRequested();
                                written = checked(written + read);
                                if (written > file.Value)
                                    throw new InvalidDataException("CosyVoice archive entry exceeds declared length.");
                                output.Write(buffer, 0, read);
                                extracted += read;
                                onExtracted?.Invoke(extracted);
                            }

                            if (written != file.Value)
                                throw new InvalidDataException("CosyVoice archive entry is truncated.");
                        }
                    }
                }

                token.ThrowIfCancellationRequested();
                // Only the unpublished staging model is replaced. The currently
                // installed model is untouched until CompleteJob's atomic rename.
                if (Directory.Exists(destination))
                    Directory.Delete(destination, true);
                Directory.Move(extractionPath, destination);
            }
            finally
            {
                if (Directory.Exists(extractionPath))
                    Directory.Delete(extractionPath, true);
            }
        }

        private static void RejectLink(string path)
        {
            if ((Directory.Exists(path) || File.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("CosyVoice installation directory cannot be a symbolic link: " + path);
        }

        private static string ValidatePath(string value)
        {
            var path = value.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Split('/').Any(part => string.IsNullOrEmpty(part) || part == "." || part == ".." || part.IndexOf(':') >= 0 || part.IndexOf('\0') >= 0 || part.TrimEnd(' ', '.') != part))
                throw new InvalidDataException("Unsafe archive path: " + value);
            return path;
        }

        public static string FileHash(string path, CancellationToken token = default) => Hash(path, token);

        internal static string Hash(string path, CancellationToken token = default)
        {
            using (var hash = SHA256.Create())
            using (var input = File.OpenRead(path))
            {
                var buffer = new byte[1024 * 1024];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    hash.TransformBlock(buffer, 0, read, buffer, 0);
                }

                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(hash.Hash).Replace("-", string.Empty);
            }
        }
    }
}
