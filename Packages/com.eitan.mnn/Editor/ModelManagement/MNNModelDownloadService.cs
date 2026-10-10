using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity.Editor
{
    public sealed class MNNModelDownloadOptions
    {
        public int MaxConnections = 4;
        public long ParallelThresholdBytes = 8 * 1024 * 1024;
        public int RetryCount = 2;
        public bool PreservePartialDownloads;
    }

    /// <summary>Streaming transfers with bounded concurrency, version-pinned checkpoints and verified Range responses.</summary>
    public sealed class MNNModelDownloadService : IDisposable
    {
        private const string DefaultApiRoot = "https://www.modelscope.cn";
        private readonly HttpClient _httpClient;
        private readonly string _apiRoot;
        private readonly bool _ownsClient;
        private readonly MNNModelDownloadOptions _options;
        private readonly SemaphoreSlim _connections;
        [Serializable]
        private class FileListResponse
        {
            public int Code;
            public FileListData Data;
            public string Message;
        }

        [Serializable]
        private class FileListData
        {
            public MNNModelRepository.ModelScopeFile[] Files;
        }

        [Serializable]
        private class Checkpoint
        {
            public string Identity;
            public int Parts;
        }

        private sealed class RangeUnsupportedException : IOException
        {
        }

        public MNNModelDownloadService(HttpClient httpClient = null, string apiRoot = null, MNNModelDownloadOptions options = null)
        {
            _options = options ?? new MNNModelDownloadOptions();
            if (_options.MaxConnections < 1 || _options.MaxConnections > 16)
                throw new ArgumentOutOfRangeException(nameof(options), "MaxConnections must be between 1 and 16.");
            _connections = new SemaphoreSlim(_options.MaxConnections);
            _httpClient = httpClient ?? new HttpClient(new HttpClientHandler{AllowAutoRedirect = true, MaxConnectionsPerServer = _options.MaxConnections, AutomaticDecompression = DecompressionMethods.None})
            {Timeout = TimeSpan.FromHours(2)};
            if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MNN-Unity-ModelManager/1.0");
            _ownsClient = httpClient == null;
            _apiRoot = (apiRoot ?? DefaultApiRoot).TrimEnd('/');
        }

        public async Task<long> DownloadRepositoryAsync(string modelScopeId, string stagingDirectory, Action<long, long, string> onProgress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var manifestPath = Path.Combine(stagingDirectory, ".mnn-download-manifest.json");
                var identityPath = Path.Combine(stagingDirectory, ".mnn-download-repository");
                MNNModelRepository.ModelScopeFile[] files;
                if (_options.PreservePartialDownloads && File.Exists(manifestPath) && File.Exists(identityPath) && File.ReadAllText(identityPath) == modelScopeId)
                    files = JsonUtility.FromJson<FileListData>(File.ReadAllText(manifestPath)).Files;
                else
                    files = await GetRepositoryFilesAsync(modelScopeId, cancellationToken).ConfigureAwait(false);
                var blobs = files.Where(file => file != null && string.Equals(file.Type, "blob", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (blobs.Length == 0)
                    throw new InvalidDataException("The ModelScope repository contains no downloadable files.");
                // Validate the entire listing before any files are written.
                var destinations = blobs.Select(file =>
                {
                    var path = Path.GetFullPath(Path.Combine(stagingDirectory, ValidateRepositoryPath(file.Path)));
                    EnsureWithinDirectory(stagingDirectory, path, file.Path);
                    return path;
                }).ToArray();
                if (destinations.Distinct(StringComparer.OrdinalIgnoreCase).Count() != destinations.Length)
                    throw new InvalidDataException("ModelScope returned duplicate file paths.");
                Directory.CreateDirectory(stagingDirectory);
                if (_options.PreservePartialDownloads)
                {
                    File.WriteAllText(manifestPath, JsonUtility.ToJson(new FileListData{Files = blobs}));
                    File.WriteAllText(identityPath, modelScopeId);
                }

                var total = blobs.Sum(file => Math.Max(0L, file.Size));
                var received = new long[blobs.Length];
                var progressLock = new object ();
                var tasks = blobs.Select((file, index) => DownloadFileAsync(modelScopeId, file, destinations[index], bytes =>
                {
                    lock (progressLock)
                    {
                        received[index] = bytes;
                        onProgress?.Invoke(received.Sum(), total, file.Path);
                    }
                }, cancellationToken)).ToArray();
                var lengths = await Task.WhenAll(tasks).ConfigureAwait(false);
                if (File.Exists(manifestPath))
                    File.Delete(manifestPath);
                if (File.Exists(identityPath))
                    File.Delete(identityPath);
                return lengths.Sum();
            }
            catch
            {
                if (!_options.PreservePartialDownloads && Directory.Exists(stagingDirectory))
                {
                    try
                    {
                        Directory.Delete(stagingDirectory, true);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                throw;
            }
        }

        public async Task<MNNModelRepository.ModelScopeFile[]> GetRepositoryFilesAsync(string modelScopeId, CancellationToken cancellationToken = default)
        {
            using (var response = await _httpClient.GetAsync($"{_apiRoot}/api/v1/models/{modelScopeId}/repo/files?Revision=master&Recursive=true", cancellationToken).ConfigureAwait(false))
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"ModelScope file listing failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
                var result = JsonUtility.FromJson<FileListResponse>(body);
                if (result == null || result.Code != 200 || result.Data?.Files == null)
                    throw new InvalidDataException("ModelScope returned an invalid file listing: " + (result?.Message ?? body));
                return result.Data.Files;
            }
        }

        public Task<long> DownloadRepositoryFileAsync(string modelScopeId, MNNModelRepository.ModelScopeFile file, string destination, Action<long, long, string> onProgress = null, CancellationToken cancellationToken = default)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            ValidateRepositoryPath(file.Path);
            return DownloadFileAsync(modelScopeId, file, destination, bytes => onProgress?.Invoke(bytes, Math.Max(0L, file.Size), file.Path), cancellationToken);
        }

        private async Task<long> DownloadFileAsync(string modelScopeId, MNNModelRepository.ModelScopeFile file, string destination, Action<long> onProgress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            destination = Path.GetFullPath(destination);
            var partsDirectory = destination + ".mnn-parts";
            var revision = string.IsNullOrWhiteSpace(file.Revision) ? "master" : file.Revision;
            var identity = modelScopeId + "\n" + file.Path + "\n" + revision + "\n" + file.Size + "\n" + file.Sha256;
            var count = file.Size >= _options.ParallelThresholdBytes && file.Size > 0 ? (int)Math.Min(file.Size, _options.MaxConnections) : 1;
            try
            {
                if (_options.PreservePartialDownloads && File.Exists(destination) && await IsValidAsync(destination, file, token).ConfigureAwait(false))
                {
                    var size = new FileInfo(destination).Length;
                    onProgress(size);
                    return size;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                var checkpointPath = Path.Combine(partsDirectory, "checkpoint.json");
                if (Directory.Exists(partsDirectory))
                {
                    var checkpoint = File.Exists(checkpointPath) ? JsonUtility.FromJson<Checkpoint>(File.ReadAllText(checkpointPath)) : null;
                    if (!_options.PreservePartialDownloads || checkpoint?.Identity != identity)
                        Directory.Delete(partsDirectory, true);
                    else if (checkpoint.Parts >= 1 && checkpoint.Parts <= 16)
                        count = checkpoint.Parts;
                }

                Directory.CreateDirectory(partsDirectory);
                File.WriteAllText(checkpointPath, JsonUtility.ToJson(new Checkpoint{Identity = identity, Parts = count}));
                var url = $"{_apiRoot}/api/v1/models/{modelScopeId}/repo?Revision={Uri.EscapeDataString(revision)}&FilePath={Uri.EscapeDataString(file.Path)}";
                if (count > 1)
                {
                    var progress = new long[count];
                    var progressLock = new object ();
                    using (var group = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        var tasks = Enumerable.Range(0, count).Select(async index =>
                        {
                            try
                            {
                                await DownloadPartAsync(url, Path.Combine(partsDirectory, index + ".part"), file.Size * index / count, file.Size * (index + 1) / count - 1, true, bytes =>
                                {
                                    lock (progressLock)
                                    {
                                        progress[index] = bytes;
                                        onProgress(progress.Sum());
                                    }
                                }, group.Token).ConfigureAwait(false);
                            }
                            catch
                            {
                                // Cancellation happens before this task completes, while the source is still alive.
                                group.Cancel();
                                throw;
                            }
                        }).ToArray();
                        try
                        {
                            await Task.WhenAll(tasks).ConfigureAwait(false);
                        }
                        catch
                        {
                            token.ThrowIfCancellationRequested();
                            if (!tasks.Any(task => task.Exception?.Flatten().InnerExceptions.Any(e => e is RangeUnsupportedException) == true))
                                throw;
                            count = 1;
                            Directory.Delete(partsDirectory, true);
                            Directory.CreateDirectory(partsDirectory);
                            File.WriteAllText(checkpointPath, JsonUtility.ToJson(new Checkpoint{Identity = identity, Parts = 1}));
                        }
                    }
                }

                if (count == 1)
                    await DownloadPartAsync(url, Path.Combine(partsDirectory, "0.part"), 0, file.Size > 0 ? file.Size - 1 : (long? )null, false, onProgress, token).ConfigureAwait(false);
                var temporary = Path.Combine(partsDirectory, "verified.tmp");
                await Task.Run(async () =>
                {
                    using (var target = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, true))
                        for (var index = 0; index < count; index++)
                            using (var source = File.OpenRead(Path.Combine(partsDirectory, index + ".part")))
                                await source.CopyToAsync(target, 128 * 1024, token).ConfigureAwait(false);
                }, token).ConfigureAwait(false);
                if (!await IsValidAsync(temporary, file, token).ConfigureAwait(false))
                {
                    Directory.Delete(partsDirectory, true);
                    throw new InvalidDataException($"Size or SHA-256 verification failed for '{file.Path}'.");
                }

                token.ThrowIfCancellationRequested();
                if (File.Exists(destination))
                    File.Delete(destination);
                File.Move(temporary, destination);
                Directory.Delete(partsDirectory, true);
                return new FileInfo(destination).Length;
            }
            catch
            {
                if (!_options.PreservePartialDownloads && Directory.Exists(partsDirectory))
                    Directory.Delete(partsDirectory, true);
                throw;
            }
        }

        private async Task DownloadPartAsync(string url, string path, long start, long? end, bool requireRange, Action<long> onProgress, CancellationToken token)
        {
            for (var attempt = 0;; attempt++)
            {
                token.ThrowIfCancellationRequested();
                await _connections.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    var length = end.HasValue ? end.Value - start + 1 : (long? )null;
                    var existing = File.Exists(path) ? new FileInfo(path).Length : 0;
                    if (length.HasValue && existing > length.Value)
                    {
                        File.Delete(path);
                        existing = 0;
                    }

                    onProgress(existing);
                    if (length.HasValue && existing == length.Value)
                        return;
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        var ranged = requireRange || existing > 0;
                        if (ranged)
                            request.Headers.Range = new RangeHeaderValue(start + existing, end);
                        using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                        {
                            if (response.StatusCode == HttpStatusCode.OK && ranged)
                            {
                                if (requireRange)
                                    throw new RangeUnsupportedException();
                                existing = 0; // Server ignored Range: overwrite, never append a full response to a partial file.
                                onProgress(0);
                            }
                            else if (ranged)
                            {
                                var range = response.Content.Headers.ContentRange;
                                if (response.StatusCode != HttpStatusCode.PartialContent || range?.From != start + existing || (end.HasValue && range.To != end.Value) || range.Unit != "bytes")
                                    throw new InvalidDataException("Server returned an invalid Content-Range for " + url.Split('?')[0]);
                            }

                            if (!response.IsSuccessStatusCode)
                                throw new HttpRequestException($"ModelScope download failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
                            var buffer = new byte[128 * 1024];
                            using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var target = new FileStream(path, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, buffer.Length, true))
                            {
                                int read;
                                while ((read = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                                {
                                    if (length.HasValue && existing + read > length.Value)
                                        throw new InvalidDataException("Server returned too many bytes for a download segment.");
                                    await target.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                                    existing += read;
                                    onProgress(existing);
                                }

                                await target.FlushAsync(token).ConfigureAwait(false);
                            }

                            if (length.HasValue && existing != length.Value)
                                throw new IOException($"Incomplete download: expected {length} bytes, received {existing} bytes.");
                            return;
                        }
                    }
                }
                catch (Exception error)when (!(error is OperationCanceledException) && !(error is InvalidDataException) && !(error is RangeUnsupportedException) && (error is IOException || error is HttpRequestException) && attempt < _options.RetryCount)
                {
                // The next attempt reads the saved segment length and requests the remaining bytes.
                }
                finally
                {
                    _connections.Release();
                }

                await Task.Delay(500 * (attempt + 1), token).ConfigureAwait(false);
            }
        }

        private static Task<bool> IsValidAsync(string path, MNNModelRepository.ModelScopeFile file, CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                if (file.Size > 0 && new FileInfo(path).Length != file.Size)
                    return false;
                if (string.IsNullOrWhiteSpace(file.Sha256))
                    return true;
                using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                using (var stream = File.OpenRead(path))
                {
                    var buffer = new byte[128 * 1024];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        hash.AppendData(buffer, 0, read);
                    }

                    return string.Equals(BitConverter.ToString(hash.GetHashAndReset()).Replace("-", ""), file.Sha256, StringComparison.OrdinalIgnoreCase);
                }
            }, token);
        }

        private static string ValidateRepositoryPath(string path)
        {
            var normalized = (path ?? "").Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Split('/').Any(part => string.IsNullOrWhiteSpace(part) || part == "." || part == ".." || part.Contains(":")) || normalized.IndexOf('\0') >= 0 || normalized.Contains(".mnn-parts") || normalized.StartsWith(".mnn-download", StringComparison.Ordinal))
                throw new InvalidDataException("ModelScope returned an unsafe repository path: " + path);
            return normalized.Replace('/', Path.DirectorySeparatorChar);
        }

        private static void EnsureWithinDirectory(string root, string destination, string sourcePath)
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(fullRoot, StringComparison.Ordinal))
                throw new InvalidDataException("ModelScope returned a path outside the download folder: " + sourcePath);
        }

        public void Dispose()
        {
            if (_ownsClient)
                _httpClient.Dispose();
            _connections.Dispose();
        }
    }
}
