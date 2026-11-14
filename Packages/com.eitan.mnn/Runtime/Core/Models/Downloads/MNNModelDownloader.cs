using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Streaming transfers with bounded concurrency, version-pinned checkpoints and verified Range responses.</summary>
    public sealed partial class MNNModelDownloader : IDisposable
    {
        private const string DefaultApiRoot = "https://www.modelscope.cn";
        private readonly HttpClient _httpClient;
        private readonly string _apiRoot;
        private readonly bool _ownsClient;
        private readonly MNNModelTransferOptions _options;
        private readonly MNNModelFileTransfer _transfers;
        private readonly MNNHuggingFaceSourcePool _huggingFaceMirrorPool;
        private readonly bool _discoverHuggingFaceMirrors;
        [DataContract, UnityEngine.Scripting.Preserve]
        private class FileListResponse
        {
            [DataMember, UnityEngine.Scripting.Preserve] public int Code;
            [DataMember, UnityEngine.Scripting.Preserve] public FileListData Data;
            [DataMember, UnityEngine.Scripting.Preserve] public string Message;
        }

        [DataContract, UnityEngine.Scripting.Preserve]
        private class FileListData
        {
            [DataMember, UnityEngine.Scripting.Preserve] public MNNModelRepositoryFile[] Files;
        }

        internal sealed class RepositoryUnavailableException : HttpRequestException
        {
            internal RepositoryUnavailableException(string repository, HttpStatusCode status) : base(repository + ": HTTP " + (int)status + " " + status)
            {
            }
        }

        public MNNModelDownloader(HttpClient httpClient = null, string apiRoot = null, MNNModelTransferOptions options = null, MNNHuggingFaceSourcePool mirrorPool = null)
        {
            _options = (options ?? new MNNModelTransferOptions { PreservePartialDownloads = true }).Snapshot();
            if (_options.MaxConnections < 1 || _options.MaxConnections > 16)
                throw new ArgumentOutOfRangeException(nameof(options), "MaxConnections must be between 1 and 16.");
            // Unity's MonoWebRequestHandler throws for MaxConnectionsPerServer on Android.
            // MNNModelFileTransfer bounds active requests with its own semaphore.
            _httpClient = httpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, AutomaticDecompression = DecompressionMethods.None })
            { Timeout = TimeSpan.FromHours(2) };
            if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MNN-Unity-ModelManager/1.0");
            _ownsClient = httpClient == null;
            _transfers = new MNNModelFileTransfer(_httpClient, _options);
            _apiRoot = (apiRoot ?? DefaultApiRoot).TrimEnd('/');
            _huggingFaceMirrorPool = mirrorPool ?? MNNHuggingFaceSourcePool.Shared;
            _discoverHuggingFaceMirrors = mirrorPool == null;
        }

        public async Task<long> DownloadRepositoryAsync(string modelScopeId, string stagingDirectory, Action<long, long, string> onProgress = null, CancellationToken cancellationToken = default, string revision = "master")
        {
            ValidateRepositoryId(modelScopeId);
            cancellationToken.ThrowIfCancellationRequested();
            if (MNNEnglishKwsModel.IsModelScopeRepository(modelScopeId)) revision = MNNEnglishKwsModel.ResolveRevision(revision);
            if (MNNPocketModel.IsRepository(modelScopeId)) revision = MNNPocketModel.ResolveRevision(revision);
            revision = MNNVerifiedTtsModel.IsCosyVoice300MRepository(modelScopeId) ? MNNVerifiedTtsModel.ResolveCosyVoice300MRevision(revision)
                : string.IsNullOrWhiteSpace(revision) ? "master" : revision;
            string identity = revision == "master" ? modelScopeId : modelScopeId + "@" + revision;
            try
            {
                var manifestPath = Path.Combine(stagingDirectory, ".mnn-download-manifest.json");
                var identityPath = Path.Combine(stagingDirectory, ".mnn-download-repository");
                MNNModelRepositoryFile[] files;
                // Verified profiles use their immutable plan even when resuming an older mutable manifest.
                if (MNNPocketModel.IsRepository(modelScopeId))
                    files = MNNPocketModel.Files();
                else if (MNNEnglishKwsModel.IsModelScopeRepository(modelScopeId))
                    files = MNNEnglishKwsModel.Files();
                else if (MNNDtlnAecModel.IsRepository(modelScopeId))
                    files = DtlnAecDownloadPlan(modelScopeId);
                else if (MNNVerifiedTtsModel.IsRepository(modelScopeId))
                    files = MNNVerifiedTtsModel.Files(modelScopeId).Select(file => new MNNModelRepositoryFile { Path = file.Path, Type = "blob", Size = file.Bytes, Sha256 = file.Hash, Revision = file.Revision }).ToArray();
                else if (MNNQwen3AsrModel.IsModelScopeRepository(modelScopeId))
                    files = Qwen3AsrDownloadPlan(true);
                else if (MNNParaformerModel.IsModelScopeRepository(modelScopeId))
                    files = ParaformerDownloadPlan(modelScopeId, true);
                else if (MNNWhisperModel.IsModelScopeRepository(modelScopeId))
                {
                    var profile = MNNWhisperModel.ForRepository(modelScopeId);
                    files = profile.Paths.Select((path, i) => new MNNModelRepositoryFile { Path = path, Type = "blob", Size = profile.Sizes[i], Sha256 = profile.Hashes[i], Revision = profile.Revision }).ToArray();
                }
                else if (_options.PreservePartialDownloads && File.Exists(manifestPath) && File.Exists(identityPath) && File.ReadAllText(identityPath) == identity)
                    files = ReadResumeFiles(manifestPath) ?? await GetRepositoryFilesAsync(modelScopeId, cancellationToken, revision).ConfigureAwait(false);
                else
                    files = await GetRepositoryFilesAsync(modelScopeId, cancellationToken, revision).ConfigureAwait(false);
                var blobs = SelectInferenceFiles(modelScopeId, files);
                if (blobs.Length == 0)
                    throw new InvalidDataException("The ModelScope repository contains no downloadable files.");
                foreach (var file in blobs)
                    if (string.IsNullOrWhiteSpace(file.Revision))
                        file.Revision = revision;
                // Validate the entire listing before any files are written.
                var destinations = blobs.Select(file =>
                {
                    var path = Path.GetFullPath(Path.Combine(stagingDirectory, ValidateRepositoryPath(file.Path)));
                    EnsureWithinDirectory(stagingDirectory, path, file.Path);
                    return path;
                }).ToArray();
                if (destinations.Distinct(StringComparer.OrdinalIgnoreCase).Count() != destinations.Length)
                    throw new InvalidDataException("ModelScope returned duplicate file paths.");
                await EnsureRepositorySpaceAsync(modelScopeId, blobs, destinations, cancellationToken).ConfigureAwait(false);
                Directory.CreateDirectory(stagingDirectory);
                if (_options.PreservePartialDownloads)
                {
                    _transfers.WriteDownloadMetadata(manifestPath, WriteMetadata(new FileListData { Files = blobs }), modelScopeId);
                    _transfers.WriteDownloadMetadata(identityPath, identity, modelScopeId);
                }

                var total = blobs.Sum(file => Math.Max(0L, file.Size));
                var received = new long[blobs.Length];
                var progressLock = new object();
                var tasks = blobs.Select((file, index) => DownloadFileAsync(modelScopeId, file, destinations[index], bytes =>
                {
                    lock (progressLock)
                    {
                        received[index] = bytes;
                        onProgress?.Invoke(received.Sum(), total, file.Path);
                    }
                }, cancellationToken, () => $"{_apiRoot}/api/v1/models/{modelScopeId}/repo?Revision={Uri.EscapeDataString(string.IsNullOrWhiteSpace(file.Revision) ? revision : file.Revision)}&FilePath={Uri.EscapeDataString(file.Path)}")).ToArray();
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

        private static MNNModelRepositoryFile[] ReadResumeFiles(string path)
        {
            try { return ReadMetadata<FileListData>(File.ReadAllText(path))?.Files; }
            catch (InvalidDataException) { return null; } // A full disk can leave an older truncated checkpoint.
        }

        private async Task EnsureRepositorySpaceAsync(string repository, MNNModelRepositoryFile[] files, string[] destinations, CancellationToken token, int retryLimit = -1)
        {
            long required = 0;
            for (int index = 0; index < files.Length; ++index)
            {
                var file = files[index];
                var transfer = new MNNModelTransferFile { Path = file.Path, Revision = file.Revision, Size = file.Size, Sha256 = file.Sha256 };
                long bytes = await _transfers.RequiredSpaceAsync(file.SourceRepository ?? repository, transfer, destinations[index], token, retryLimit).ConfigureAwait(false);
                required = MNNModelFileTransfer.AddBytes(required, bytes);
            }
            _transfers.EnsureSpace(Path.GetDirectoryName(destinations[0]), repository, required);
        }

        public async Task<MNNModelRepositoryFile[]> GetRepositoryFilesAsync(string modelScopeId, CancellationToken cancellationToken = default, string revision = "master")
        {
            ValidateRepositoryId(modelScopeId);
            using (var response = await MNNModelCatalogRequests.Shared.GetAsync(_httpClient, $"{_apiRoot}/api/v1/models/{modelScopeId}/repo/files?Revision={Uri.EscapeDataString(string.IsNullOrWhiteSpace(revision) ? "master" : revision)}&Recursive=true", cancellationToken).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Gone)
                    throw new RepositoryUnavailableException(modelScopeId, response.StatusCode);
                var body = response.Content == null ? string.Empty : await MNNModelMetadata.ReadTextAsync(response.Content, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"ModelScope file listing failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
                cancellationToken.ThrowIfCancellationRequested();
                var result = ReadMetadata<FileListResponse>(body);
                if (result == null || result.Code != 200 || result.Data?.Files == null)
                    throw new InvalidDataException("ModelScope returned an invalid file listing: " + (result?.Message ?? body));
                return result.Data.Files;
            }
        }

        // Metadata and resume manifests are processed by HTTP workers, including during editor shutdown.
        // Keep their codec independent of Unity's native type manager.
        private static T ReadMetadata<T>(string json)
        {
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            }
            catch (SerializationException error) { throw new InvalidDataException("Invalid model repository metadata.", error); }
        }

        private static string WriteMetadata<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public Task<long> DownloadRepositoryFileAsync(string modelScopeId, MNNModelRepositoryFile file, string destination, Action<long, long, string> onProgress = null, CancellationToken cancellationToken = default)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            ValidateRepositoryPath(file.Path);
            return DownloadFileAsync(modelScopeId, file, destination, bytes => onProgress?.Invoke(bytes, Math.Max(0L, file.Size), file.Path), cancellationToken, () => $"{_apiRoot}/api/v1/models/{modelScopeId}/repo?Revision={Uri.EscapeDataString(string.IsNullOrWhiteSpace(file.Revision) ? "master" : file.Revision)}&FilePath={Uri.EscapeDataString(file.Path)}");
        }

        private Task<long> DownloadFileAsync(string modelScopeId, MNNModelRepositoryFile file, string destination, Action<long> onProgress, CancellationToken token, Func<string> urlProvider, Action<bool> sourceResult = null, int retryLimit = -1) => _transfers.DownloadAsync(modelScopeId, new MNNModelTransferFile { Path = file.Path, Revision = file.Revision, Size = file.Size, Sha256 = file.Sha256 }, destination, onProgress, token, urlProvider, sourceResult, retryLimit);
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
            _transfers.Dispose();
        }
    }
}
