using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity.Editor
{
    /// <summary>Editor compatibility adapter; all network transfers run in the runtime API.</summary>
    public sealed partial class MNNModelDownloadService : IDisposable
    {
        private readonly MNNModelDownloader _downloader;

        public MNNModelDownloadService(HttpClient httpClient = null, string apiRoot = null, MNNModelDownloadOptions options = null, MNNHuggingFaceMirrorPool mirrorPool = null)
        {
            _downloader = new MNNModelDownloader(httpClient, apiRoot, options ?? new MNNModelDownloadOptions(), mirrorPool?.RuntimePool);
        }

        public Task<long> DownloadRepositoryAsync(string modelScopeId, string stagingDirectory, Action<long, long, string> onProgress = null, CancellationToken cancellationToken = default)
            => _downloader.DownloadRepositoryAsync(modelScopeId, stagingDirectory, onProgress, cancellationToken);

        public Task<long> DownloadHuggingFaceRepositoryAsync(string repositoryId, string stagingDirectory, string revision = "main", Action<long, long, string> onProgress = null, MNNHuggingFaceMirrorPool mirrorPool = null, CancellationToken cancellationToken = default)
            => _downloader.DownloadHuggingFaceRepositoryAsync(repositoryId, stagingDirectory, revision, onProgress, mirrorPool?.RuntimePool, cancellationToken);

        public async Task<MNNModelRepository.ModelScopeFile[]> GetRepositoryFilesAsync(string modelScopeId, CancellationToken cancellationToken = default)
            => (await _downloader.GetRepositoryFilesAsync(modelScopeId, cancellationToken).ConfigureAwait(false)).Select(ToEditorFile).ToArray();

        public Task<long> DownloadRepositoryFileAsync(string modelScopeId, MNNModelRepository.ModelScopeFile file, string destination, Action<long, long, string> onProgress = null, CancellationToken cancellationToken = default)
            => _downloader.DownloadRepositoryFileAsync(modelScopeId, file, destination, onProgress, cancellationToken);

        internal Task<string> DownloadAsync(MNNModelProvider provider, string repository, string directory, string revision,
            Action<long, long, string> onProgress, CancellationToken token)
            => _downloader.DownloadAsync((MNNModelSource)provider, repository, directory, revision, onProgress, token);

        internal Task<long> DownloadDocumentLayoutProfileAsync(string repository, string directory,
            Action<long, long, string> onProgress, CancellationToken cancellationToken)
            => _downloader.DownloadDocumentLayoutProfileAsync(repository, directory, onProgress, cancellationToken);

        internal Task<long> DownloadImageEmbeddingProfileAsync(string repository, string directory,
            Action<long, long, string> onProgress, CancellationToken cancellationToken)
            => _downloader.DownloadImageEmbeddingProfileAsync(repository, directory, onProgress, cancellationToken);

        internal Task<long> DownloadImageProfileAsync(string repository, string directory,
            Action<long, long, string> onProgress, CancellationToken cancellationToken)
            => _downloader.DownloadImageProfileAsync(repository, directory, onProgress, cancellationToken);

        internal static MNNModelRepository.ModelScopeFile[] DtlnAecDownloadPlan(string repository)
            => MNNModelDownloader.DtlnAecDownloadPlan(repository).Select(ToEditorFile).ToArray();

        internal static MNNModelRepository.ModelScopeFile[] ReferenceSpeechDownloadPlan(string repositoryId, string revision)
            => MNNModelDownloader.ReferenceSpeechDownloadPlan(repositoryId, revision).Select(ToEditorFile).ToArray();

        private static MNNModelRepository.ModelScopeFile ToEditorFile(MNNModelRepositoryFile file)
            => new MNNModelRepository.ModelScopeFile {Path = file.Path, Type = file.Type, Size = file.Size,
                Revision = file.Revision, Sha256 = file.Sha256, SourceRepository = file.SourceRepository};

        public void Dispose() => _downloader.Dispose();
    }
}
