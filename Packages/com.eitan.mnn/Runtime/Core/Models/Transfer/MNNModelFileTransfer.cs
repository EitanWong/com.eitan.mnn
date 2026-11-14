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

namespace MNN.Unity
{
    /// <summary>Shared Editor/Player transfer engine: bounded connections, resume, pinned checkpoints and verification.</summary>
    public sealed partial class MNNModelFileTransfer : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly MNNModelTransferOptions _options;
        private readonly SemaphoreSlim _connections;
        [Serializable] private class Checkpoint { public string Identity; public int Parts; }
        private sealed class RangeUnsupportedException : IOException { }

        public MNNModelFileTransfer(HttpClient client, MNNModelTransferOptions options = null)
        {
            _httpClient = client ?? throw new ArgumentNullException(nameof(client));
            _options = (options ?? new MNNModelTransferOptions()).Snapshot();
            if (_options.MaxConnections < 1 || _options.MaxConnections > 16 || _options.RetryCount < 0)
                throw new ArgumentOutOfRangeException(nameof(options));
            _connections = new SemaphoreSlim(_options.MaxConnections);
        }

        public async Task<long> DownloadAsync(string modelScopeId, MNNModelTransferFile file, string destination, Action<long> onProgress, CancellationToken token, Func<string> urlProvider, Action<bool> sourceResult = null, int retryLimit = -1)
        {
            token.ThrowIfCancellationRequested();
            if (file == null || urlProvider == null) throw new ArgumentNullException(file == null ? nameof(file) : nameof(urlProvider));
            onProgress = onProgress ?? (_ => { });
            destination = Path.GetFullPath(destination);
            var partsDirectory = destination + ".mnn-parts";
            var identity = TransferIdentity(modelScopeId, file);
            var count = PartCount(file, retryLimit);
            try
            {
                // Size alone cannot establish that an existing file belongs to this repository revision.
                if (_options.PreservePartialDownloads && !string.IsNullOrWhiteSpace(file.Sha256)
                    && File.Exists(destination) && await IsValidAsync(destination, file, token).ConfigureAwait(false))
                {
                    var size = new FileInfo(destination).Length;
                    onProgress(size);
                    return size;
                }

                EnsureSpace(Path.GetDirectoryName(destination), modelScopeId,
                    await RequiredSpaceAsync(modelScopeId, file, destination, token, retryLimit).ConfigureAwait(false));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                var checkpointPath = Path.Combine(partsDirectory, "checkpoint.json");
                if (Directory.Exists(partsDirectory))
                {
                    var checkpoint = ReadCheckpoint(partsDirectory);
                    if (!_options.PreservePartialDownloads || checkpoint?.Identity != identity)
                        Directory.Delete(partsDirectory, true);
                    else if (checkpoint.Parts >= 1 && checkpoint.Parts <= 16)
                        count = checkpoint.Parts;
                }

                Directory.CreateDirectory(partsDirectory);
                WriteDownloadMetadata(checkpointPath, JsonUtility.ToJson(new Checkpoint { Identity = identity, Parts = count }), modelScopeId);
                if (count > 1)
                {
                    var progress = new long[count];
                    var progressLock = new object();
                    using (var group = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        var tasks = Enumerable.Range(0, count).Select(async index =>
                        {
                            try
                            {
                                await DownloadPartAsync(modelScopeId, urlProvider, Path.Combine(partsDirectory, index + ".part"), file.Size * index / count, file.Size * (index + 1) / count - 1, true, bytes =>
                                {
                                    lock (progressLock)
                                    {
                                        progress[index] = bytes;
                                        onProgress(progress.Sum());
                                    }
                                }, group.Token, sourceResult, retryLimit).ConfigureAwait(false);
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
                            if (tasks.Any(task => task.Exception?.Flatten().InnerExceptions.Any(e => e is MNNInsufficientDiskSpaceException || e is IOException io && IsDiskFull(io)) == true))
                                throw;
                            if (!tasks.Any(task => task.Exception?.Flatten().InnerExceptions.Any(e => e is RangeUnsupportedException || e is HttpRequestException || e is IOException) == true))
                                throw;
                            count = 1;
                            Directory.Delete(partsDirectory, true);
                            Directory.CreateDirectory(partsDirectory);
                            WriteDownloadMetadata(checkpointPath, JsonUtility.ToJson(new Checkpoint { Identity = identity, Parts = 1 }), modelScopeId);
                        }
                    }
                }

                if (count == 1)
                    await DownloadPartAsync(modelScopeId, urlProvider, Path.Combine(partsDirectory, "0.part"), 0, file.Size > 0 ? file.Size - 1 : (long?)null, false, onProgress, token, sourceResult, retryLimit).ConfigureAwait(false);
                var temporary = Path.Combine(partsDirectory, count == 1 ? "0.part" : "verified.tmp");
                if (count > 1)
                {
                    await Task.Run(async () =>
                    {
                        using (var target = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, true))
                            for (var index = 0; index < count; index++)
                                using (var source = File.OpenRead(Path.Combine(partsDirectory, index + ".part")))
                                    await source.CopyToAsync(target, 128 * 1024, token).ConfigureAwait(false);
                    }, token).ConfigureAwait(false);
                }
                if (!await IsValidAsync(temporary, file, token).ConfigureAwait(false))
                {
                    Directory.Delete(partsDirectory, true);
                    throw new InvalidDataException($"Size or SHA-256 verification failed for '{file.Path}'.");
                }

                token.ThrowIfCancellationRequested();
                if (File.Exists(destination))
                    File.Replace(temporary, destination, null);
                else
                    File.Move(temporary, destination);
                Directory.Delete(partsDirectory, true);
                return new FileInfo(destination).Length;
            }
            catch (IOException error) when (!(error is MNNInsufficientDiskSpaceException) && IsDiskFull(error))
            {
                throw StorageFailure(Path.GetDirectoryName(destination), modelScopeId, Math.Max(1, file.Size), error);
            }
            catch
            {
                if (!_options.PreservePartialDownloads && Directory.Exists(partsDirectory))
                    Directory.Delete(partsDirectory, true);
                throw;
            }
        }

        private async Task DownloadPartAsync(string repository, Func<string> urlProvider, string path, long start, long? end, bool requireRange, Action<long> onProgress, CancellationToken token, Action<bool> sourceResult = null, int retryLimit = -1)
        {
            for (var attempt = 0; ; attempt++)
            {
                token.ThrowIfCancellationRequested();
                await _connections.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    var length = end.HasValue ? end.Value - start + 1 : (long?)null;
                    var existing = File.Exists(path) ? new FileInfo(path).Length : 0;
                    if (length.HasValue && existing > length.Value)
                    {
                        File.Delete(path);
                        existing = 0;
                    }

                    onProgress(existing);
                    if (length.HasValue && existing == length.Value)
                        return;
                    var url = urlProvider();
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
                            if (!length.HasValue && response.Content.Headers.ContentLength.HasValue)
                                EnsureSpace(Path.GetDirectoryName(path), repository, response.Content.Headers.ContentLength.Value);
                            var buffer = new byte[128 * 1024];
                            using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var target = new FileStream(path, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, buffer.Length, true))
                            {
                                int read;
                                while ((read = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                                {
                                    if (length.HasValue && existing + read > length.Value)
                                        throw new InvalidDataException("Server returned too many bytes for a download segment.");
                                    if (!length.HasValue)
                                        EnsureSpace(Path.GetDirectoryName(path), repository, read);
                                    await target.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                                    existing += read;
                                    onProgress(existing);
                                }

                                await target.FlushAsync(token).ConfigureAwait(false);
                            }

                            if (length.HasValue && existing != length.Value)
                                throw new IOException($"Incomplete download: expected {length} bytes, received {existing} bytes.");
                            sourceResult?.Invoke(true);
                            return;
                        }
                    }
                }
                catch (Exception error) when (!(error is MNNInsufficientDiskSpaceException) && !(error is IOException io && IsDiskFull(io)) && !(error is InvalidDataException) && !(error is RangeUnsupportedException) && (error is IOException || error is HttpRequestException || error is TaskCanceledException && !token.IsCancellationRequested))
                {
                    sourceResult?.Invoke(false);
                    if (attempt >= (retryLimit < 0 ? _options.RetryCount : retryLimit))
                        throw;
                }
                finally
                {
                    _connections.Release();
                }

                await Task.Delay(500 * (attempt + 1), token).ConfigureAwait(false);
            }
        }

        private static Task<bool> IsValidAsync(string path, MNNModelTransferFile file, CancellationToken token)
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

        // The caller owns the shared HTTP client and must await all transfers before disposal.
        public void Dispose() => _connections.Dispose();
    }
}
