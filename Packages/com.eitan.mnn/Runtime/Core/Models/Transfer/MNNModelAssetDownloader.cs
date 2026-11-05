using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>On-demand, bounded-memory model installation. Pinned SHA-256 is mandatory; partial files resume.</summary>
    public static class MNNModelAssetDownloader
    {
        public static string ResolvePath(string directory, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(":") || relative.Contains("\\"))
                throw new ArgumentException("Model file paths must be relative forward-slash paths.");
            foreach (string part in relative.Split('/'))
                if (part == ".." || part == "." || part.Length == 0)
                    throw new ArgumentException("Invalid model path.");
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root, StringComparison.Ordinal))
                throw new ArgumentException("Model path escapes its directory.");
            return path;
        }

        public static async Task<string> EnsureAsync(string directory, MNNModelAssetFile file, Action<long, long> progress = null, CancellationToken token = default)
        {
            if (file == null || file.bytes < 1 || file.sha256 == null || file.sha256.Length != 64)
                throw new ArgumentException("Model files require a positive exact byte count and pinned SHA-256.");
            foreach (char c in file.sha256)
                if (!Uri.IsHexDigit(c))
                    throw new ArgumentException("Invalid model SHA-256.");
            if (!Uri.TryCreate(file.url, UriKind.Absolute, out var url) || url.Scheme != "https" || !string.IsNullOrEmpty(url.UserInfo))
                throw new ArgumentException("Model downloads require HTTPS.");
            file = new MNNModelAssetFile { path = file.path, url = file.url, sha256 = file.sha256, bytes = file.bytes };
            string target = ResolvePath(directory, file.path);
            using (await MNNModelPathGate.EnterAsync(target, token).ConfigureAwait(false))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var handler = new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 })
                using (var client = new HttpClient(handler)
                { Timeout = TimeSpan.FromMinutes(30) })
                using (var transfer = new MNNModelFileTransfer(client, new MNNModelTransferOptions { PreservePartialDownloads = true }))
                {
                    await transfer.DownloadAsync(file.url, new MNNModelTransferFile { Path = file.path, Revision = file.sha256, Size = file.bytes, Sha256 = file.sha256 }, target, count => progress?.Invoke(count, file.bytes), token, () => file.url).ConfigureAwait(false);
                }

                return target;
            }
        }
    }
}
