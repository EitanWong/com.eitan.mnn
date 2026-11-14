using System;
using System.Runtime.Serialization;
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
    public sealed partial class MNNModelDownloader
    {
        public async Task<long> DownloadHuggingFaceRepositoryAsync(string repositoryId, string stagingDirectory, string revision = "main", Action<long, long, string> onProgress = null, MNNHuggingFaceSourcePool mirrorPool = null, CancellationToken cancellationToken = default)
        {
            ValidateRepositoryId(repositoryId);
            cancellationToken.ThrowIfCancellationRequested();
            revision = string.IsNullOrWhiteSpace(revision) ? "main" : revision;
            if (MNNVerifiedTtsModel.IsHuggingFaceRepository(repositoryId))
                revision = MNNVerifiedTtsModel.ResolveHuggingFaceRevision(repositoryId, revision);
            if (MNNCosyVoiceModel.IsRepository(repositoryId) && (string.IsNullOrWhiteSpace(revision) || revision == "main"))
                revision = MNNCosyVoiceModel.Revision;
            if (MNNReferenceSpeechModel.IsRepository(repositoryId))
                revision = MNNReferenceSpeechModel.ResolveRevision(repositoryId, revision);
            if (MNNBertEmbeddingModel.IsRepository(repositoryId))
                revision = MNNBertEmbeddingModel.ResolveRevision(repositoryId, revision);
            if (MNNPaddleTextModel.IsRepository(repositoryId))
                revision = MNNPaddleTextModel.ResolveRevision(revision);
            if (MNNQwen3AsrModel.IsRepository(repositoryId))
                revision = MNNQwen3AsrModel.ResolveRevision(revision, repositoryId);
            if (MNNParaformerModel.IsHuggingFaceRepository(repositoryId))
                revision = MNNParaformerModel.ResolveRevision(revision, repositoryId, false);
            if (MNNKwsModel.IsRepository(repositoryId))
                revision = MNNKwsModel.ResolveRevision(revision);
            if (MNNSileroModel.IsRepository(repositoryId))
                revision = MNNSileroModel.ResolveRevision(revision);
            if (MNNSmartTurnModel.IsRepository(repositoryId))
                revision = MNNSmartTurnModel.ResolveRevision(revision);
            if (MNNWhisperModel.IsRepository(repositoryId))
                revision = MNNWhisperModel.ResolveRevision(revision, repositoryId);
            var shouldDiscoverMirrors = mirrorPool == null && _discoverHuggingFaceMirrors;
            mirrorPool = mirrorPool ?? _huggingFaceMirrorPool;
            try
            {
                if (shouldDiscoverMirrors)
                    await mirrorPool.RefreshFromWebAsync(_httpClient, MNNHuggingFaceSourcePool.DefaultDiscoveryUrls, cancellationToken).ConfigureAwait(false);
                var mirrorRetryCount = mirrorPool.GetFileCandidates().Count - 1;
                var manifestPath = Path.Combine(stagingDirectory, ".mnn-download-manifest.json");
                var whisper = MNNWhisperModel.IsRepository(repositoryId) ? MNNWhisperModel.ForRepository(repositoryId) : null;
                var files = MNNVerifiedTtsModel.IsHuggingFaceRepository(repositoryId) ? MNNVerifiedTtsModel.HuggingFaceFiles(repositoryId).Select(file => new MNNModelRepositoryFile { Path = file.Path, Type = "blob", Size = file.Bytes, Sha256 = file.Hash, Revision = file.Revision, SourceRepository = file.SourceRepository }).ToArray() : MNNQwen3AsrModel.IsRepository(repositoryId) ? Qwen3AsrDownloadPlan(false) : MNNParaformerModel.IsHuggingFaceRepository(repositoryId) ? ParaformerDownloadPlan(repositoryId, false) : MNNSmartTurnModel.IsRepository(repositoryId) ? new[] { new MNNModelRepositoryFile { Path = MNNSmartTurnModel.Graph, Type = "blob", Size = MNNSmartTurnModel.Bytes, Sha256 = MNNSmartTurnModel.Hash, Revision = MNNSmartTurnModel.Revision }, new MNNModelRepositoryFile { Path = "README.md", Type = "blob", Size = 265, Sha256 = "5c206aa47242023d4111c117439317499909ef26faf50937b9b4eece377d3bf9", Revision = MNNSmartTurnModel.Revision } } : MNNKwsModel.IsRepository(repositoryId) ? MNNKwsModel.Paths.Select((path, i) => new MNNModelRepositoryFile { Path = MNNKwsModel.Prefix + path, Type = "blob", Size = MNNKwsModel.Sizes[i], Sha256 = MNNKwsModel.Hashes[i], Revision = MNNKwsModel.Revision }).ToArray() : MNNSileroModel.IsRepository(repositoryId) ? new[] { new MNNModelRepositoryFile { Path = MNNSileroModel.Graph, Type = "blob", Size = MNNSileroModel.Bytes, Sha256 = MNNSileroModel.Hash, Revision = MNNSileroModel.Revision }, new MNNModelRepositoryFile { Path = "README.md", Type = "blob", Size = 249, Sha256 = "657c1c6958ed5ae08c28fc33e9e827314c77649646064ce8185baf1a3b65eef4", Revision = MNNSileroModel.Revision } } : MNNBertEmbeddingModel.IsRepository(repositoryId) ? MNNBertEmbeddingModel.Files(repositoryId).Select(file => new MNNModelRepositoryFile { Path = file.Path, Type = "blob", Size = file.Bytes, Sha256 = file.Sha256, Revision = revision }).ToArray() : MNNPaddleTextModel.IsRepository(repositoryId) ? MNNPaddleTextModel.Files().Select(file => new MNNModelRepositoryFile { Path = file.Path, Type = "blob", Size = file.Bytes, Sha256 = file.Sha256, Revision = revision }).ToArray() : MNNReferenceSpeechModel.IsRepository(repositoryId) ? ReferenceSpeechDownloadPlan(repositoryId, revision) : whisper != null ? whisper.Paths.Select((path, i) => new MNNModelRepositoryFile { Path = path, Type = "blob", Size = whisper.Sizes[i], Sha256 = whisper.Hashes[i], Revision = revision }).ToArray() : await GetHuggingFaceRepositoryFilesAsync(repositoryId, revision, mirrorPool, cancellationToken).ConfigureAwait(false);
                var blobs = SelectInferenceFiles(repositoryId, files).Where(file => !MNNCosyVoiceModel.IsRepository(repositoryId) || file.Path == MNNCosyVoiceModel.Archive || file.Path == "README.md" || file.Path == "release-manifest.json").ToArray();
                if (MNNCosyVoiceModel.IsRepository(repositoryId) && !blobs.Any(file => file.Path == MNNCosyVoiceModel.Archive))
                    throw new InvalidDataException("CosyVoice repository is missing its complete synthesis archive.");
                if (blobs.Length == 0)
                    throw new InvalidDataException("The Hugging Face repository contains no downloadable files.");
                var destinations = blobs.Select(file =>
                {
                    var path = Path.GetFullPath(Path.Combine(stagingDirectory, ValidateRepositoryPath(file.Path)));
                    EnsureWithinDirectory(stagingDirectory, path, file.Path);
                    return path;
                }).ToArray();
                if (destinations.Distinct(StringComparer.OrdinalIgnoreCase).Count() != destinations.Length)
                    throw new InvalidDataException("Hugging Face returned duplicate file paths.");
                await EnsureRepositorySpaceAsync(repositoryId, blobs, destinations, cancellationToken, mirrorRetryCount).ConfigureAwait(false);
                Directory.CreateDirectory(stagingDirectory);
                if (_options.PreservePartialDownloads)
                    _transfers.WriteDownloadMetadata(manifestPath, WriteMetadata(new FileListData { Files = blobs }), repositoryId);
                var candidates = mirrorPool.GetFileCandidates().ToArray();
                if (candidates.Length == 0)
                    throw new HttpRequestException("No Hugging Face file source is available.");
                var total = blobs.Sum(file => Math.Max(0L, file.Size));
                var received = new long[blobs.Length];
                var progressLock = new object();
                var tasks = blobs.Select((file, index) =>
                {
                    var sourceIndex = -1;
                    MNNHuggingFaceSource activeMirror = null;
                    Func<string> urlProvider = () =>
                    {
                        activeMirror = candidates[(Interlocked.Increment(ref sourceIndex) & int.MaxValue) % candidates.Length];
                        return activeMirror.GetFileUrl(file.SourceRepository ?? repositoryId, file.Revision ?? revision, file.Path);
                    };
                    Action<bool> sourceResult = success =>
                    {
                        if (success)
                            mirrorPool.ReportSuccess(activeMirror);
                        else
                            mirrorPool.ReportFailure(activeMirror);
                    };
                    return DownloadFileAsync(file.SourceRepository ?? repositoryId, file, destinations[index], bytes =>
                    {
                        lock (progressLock)
                        {
                            received[index] = bytes;
                            onProgress?.Invoke(received.Sum(), total, file.Path);
                        }
                    }, cancellationToken, urlProvider, sourceResult, mirrorRetryCount);
                }).ToArray();
                var lengths = await Task.WhenAll(tasks).ConfigureAwait(false);
                if (File.Exists(manifestPath))
                    File.Delete(manifestPath);
                return lengths.Sum();
            }
            catch
            {
                if (!_options.PreservePartialDownloads && Directory.Exists(stagingDirectory))
                    Directory.Delete(stagingDirectory, true);
                throw;
            }
        }

        private async Task<MNNModelRepositoryFile[]> GetHuggingFaceRepositoryFilesAsync(string repositoryId, string revision, MNNHuggingFaceSourcePool mirrorPool, CancellationToken cancellationToken)
        {
            foreach (var mirror in mirrorPool.GetApiProbeCandidates())
            {
                try
                {
                    var url = mirror.ApiRoot + "/api/models/" + Uri.EscapeDataString(repositoryId).Replace("%2F", "/") + "/revision/" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(revision) ? "main" : revision) + "?blobs=true";
                    using (var response = await MNNModelCatalogRequests.Shared.GetAsync(_httpClient, url, cancellationToken).ConfigureAwait(false))
                    {
                        var body = await MNNModelMetadata.ReadTextAsync(response.Content, cancellationToken).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                            throw new HttpRequestException($"Hugging Face file listing failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
                        var model = ReadMetadata<HuggingFaceModelResponse>(body);
                        if (model?.siblings == null)
                            throw new InvalidDataException("Hugging Face returned an invalid file listing.");
                        mirrorPool.ReportSuccess(mirror);
                        return model.siblings.Where(item => item != null && !string.IsNullOrWhiteSpace(item.rfilename)).Select(item => new MNNModelRepositoryFile { Path = item.rfilename, Type = "blob", Size = item.lfs != null && item.lfs.size > 0 ? item.lfs.size : item.size, Revision = revision, Sha256 = item.lfs?.sha256 }).ToArray();
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    mirrorPool.ReportFailure(mirror);
                }
            }

            throw new HttpRequestException("No Hugging Face metadata source was available for " + repositoryId + ".");
        }

        [Serializable, DataContract, UnityEngine.Scripting.Preserve]
        private sealed class HuggingFaceModelResponse
        {
            [DataMember, UnityEngine.Scripting.Preserve] public HuggingFaceSibling[] siblings;
        }

        [Serializable, DataContract, UnityEngine.Scripting.Preserve]
        private sealed class HuggingFaceSibling
        {
            [DataMember, UnityEngine.Scripting.Preserve] public string rfilename;
            [DataMember, UnityEngine.Scripting.Preserve] public long size;
            [DataMember, UnityEngine.Scripting.Preserve] public HuggingFaceLfs lfs;
        }

        [Serializable, DataContract, UnityEngine.Scripting.Preserve]
        private sealed class HuggingFaceLfs
        {
            [DataMember, UnityEngine.Scripting.Preserve] public string sha256;
            [DataMember, UnityEngine.Scripting.Preserve] public long size;
        }
    }
}
