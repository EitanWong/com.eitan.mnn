using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    public sealed partial class MNNModelFileTransfer
    {
        internal static long AddBytes(long left, long right) => left > long.MaxValue - right ? long.MaxValue : left + right;
        private static string TransferIdentity(string repository, MNNModelTransferFile file) => repository + "\n" + file.Path + "\n" +
            (string.IsNullOrWhiteSpace(file.Revision) ? "master" : file.Revision) + "\n" + file.Size + "\n" + file.Sha256;
        private int PartCount(MNNModelTransferFile file, int retryLimit) => retryLimit >= 0 ? 1 :
            file.Size >= _options.ParallelThresholdBytes && file.Size > 0 ? (int)Math.Min(file.Size, _options.MaxConnections) : 1;

        private Checkpoint ReadCheckpoint(string directory)
        {
            string path = Path.Combine(directory, "checkpoint.json");
            if (!_options.PreservePartialDownloads || !File.Exists(path)) return null;
            try { return JsonUtility.FromJson<Checkpoint>(File.ReadAllText(path)); }
            catch (ArgumentException) { return null; }
        }

        internal async Task<long> RequiredSpaceAsync(string repository, MNNModelTransferFile file, string destination, CancellationToken token, int retryLimit = -1)
        {
            token.ThrowIfCancellationRequested();
            if (_options.PreservePartialDownloads && !string.IsNullOrWhiteSpace(file.Sha256) && File.Exists(destination) &&
                await IsValidAsync(destination, file, token).ConfigureAwait(false)) return 0;
            if (file.Size <= 0) return 0; // Unknown sizes are checked against Content-Length when the response arrives.
            int count = PartCount(file, retryLimit);
            long remaining = file.Size;
            string parts = destination + ".mnn-parts";
            var checkpoint = ReadCheckpoint(parts);
            if (checkpoint?.Identity == TransferIdentity(repository, file) && checkpoint.Parts >= 1 && checkpoint.Parts <= 16)
            {
                count = checkpoint.Parts;
                for (int index = 0; index < count; ++index)
                {
                    string part = Path.Combine(parts, index + ".part");
                    long length = File.Exists(part) ? new FileInfo(part).Length : 0;
                    long expected = file.Size / count + (file.Size % count * (index + 1) / count - file.Size % count * index / count);
                    if (length <= expected) remaining -= length;
                }
            }
            // A single segment is verified and moved directly. Parallel segments coexist with the assembled file.
            return count == 1 ? remaining : AddBytes(remaining, file.Size);
        }

        private long AvailableSpace(string directory)
        {
            if (_options.AvailableSpaceBytes != null)
            {
                long available = _options.AvailableSpaceBytes(directory);
                if (available < 0) throw new IOException("Available disk space cannot be negative.");
                return available;
            }
            string path = Path.GetFullPath(directory);
            string prefix = path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            // Use the destination volume, including mounted external disks, rather than the process working directory.
            var volume = DriveInfo.GetDrives().Where(d => path == d.Name.TrimEnd(Path.DirectorySeparatorChar) ||
                prefix.StartsWith(d.Name.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(d => d.Name.Length).FirstOrDefault();
            return (volume ?? new DriveInfo(Path.GetPathRoot(path))).AvailableFreeSpace;
        }

        internal void EnsureSpace(string directory, string repository, long payloadBytes)
        {
            long required = AddBytes(payloadBytes, _options.DiskSpaceReserveBytes);
            long available = AvailableSpace(directory);
            if (required > available)
                throw new MNNInsufficientDiskSpaceException(Path.GetFullPath(directory), repository, required, available);
        }

        private static bool IsDiskFull(IOException error)
        {
            int code = error.HResult & 0xffff;
            return code == 28 || code == 39 || code == 112; // ENOSPC, ERROR_HANDLE_DISK_FULL, ERROR_DISK_FULL.
        }

        internal void WriteDownloadMetadata(string path, string contents, string repository)
        {
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, contents);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            catch (IOException error) when (IsDiskFull(error))
            {
                throw StorageFailure(Path.GetDirectoryName(path), repository, System.Text.Encoding.UTF8.GetByteCount(contents), error);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { } // Cleanup must not hide the original storage failure.
                catch (UnauthorizedAccessException) { }
            }
        }

        private MNNInsufficientDiskSpaceException StorageFailure(string directory, string repository, long bytes, IOException error)
        {
            return new MNNInsufficientDiskSpaceException(Path.GetFullPath(directory), repository,
                AddBytes(Math.Max(1, bytes), _options.DiskSpaceReserveBytes), AvailableSpace(directory), error);
        }
    }
}
