using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed partial class MNNModelDownloader
    {
        /// <summary>Download a catalog entry and prepare its supported inference profile. Returns the absolute model directory.</summary>
        /// <remarks>Progress callbacks can run on worker threads. Use a dedicated writable directory for each model.</remarks>
        public Task<string> DownloadAsync(MNNRemoteModel model, string directory,
            Action<long, long, string> onProgress = null, CancellationToken cancellationToken = default)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            return DownloadAsync(model.provider, model.repositoryId ?? model.modelScopeId, directory, model.revision, onProgress, cancellationToken);
        }

        /// <summary>Download from ModelScope or Hugging Face, then run the same preparation used by Model Manager.</summary>
        /// <remarks>Known profiles retain their pinned file revisions and hashes. Generic repositories use the requested revision.</remarks>
        public async Task<string> DownloadAsync(MNNModelSource source, string repositoryId, string directory,
            string revision = null, Action<long, long, string> onProgress = null, CancellationToken cancellationToken = default)
        {
            if (source != MNNModelSource.ModelScope && source != MNNModelSource.HuggingFace)
                throw new ArgumentOutOfRangeException(nameof(source));
            ValidateRepositoryId(repositoryId);
            revision = MNNModelRevisions.Resolve(source, repositoryId, revision);
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A dedicated writable model directory is required.", nameof(directory));
            string target = Path.GetFullPath(directory);
            cancellationToken.ThrowIfCancellationRequested();
            if (source == MNNModelSource.ModelScope && MNNDocumentLayoutModel.IsRepository(repositoryId))
                await DownloadDocumentLayoutProfileAsync(repositoryId, target, onProgress, cancellationToken).ConfigureAwait(false);
            else if (MNNImageEmbeddingModel.IsRepository(repositoryId))
                await DownloadImageEmbeddingProfileAsync(repositoryId, target, onProgress, cancellationToken).ConfigureAwait(false);
            else if (MNNImageTaskProfiles.IsRepository(repositoryId))
                await DownloadImageProfileAsync(repositoryId, target, onProgress, cancellationToken).ConfigureAwait(false);
            else if (source == MNNModelSource.HuggingFace)
                await DownloadHuggingFaceRepositoryAsync(repositoryId, target, revision ?? "main", onProgress, cancellationToken: cancellationToken).ConfigureAwait(false);
            else
                await DownloadRepositoryAsync(repositoryId, target, onProgress, cancellationToken, revision ?? "master").ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Prepare(target, source, repositoryId, cancellationToken);
            return target;
        }

        private static void ValidateRepositoryId(string repositoryId)
        {
            var parts = (repositoryId ?? string.Empty).Split('/');
            if (parts.Length != 2)
                throw new ArgumentException("Repository ID must have the form owner/name.", nameof(repositoryId));
            foreach (string part in parts)
            {
                if (part.Length == 0 || part == "." || part == "..")
                    throw new ArgumentException("Invalid model repository ID.", nameof(repositoryId));
                foreach (char character in part)
                    if (!char.IsLetterOrDigit(character) && character != '-' && character != '_' && character != '.')
                        throw new ArgumentException("Invalid model repository ID.", nameof(repositoryId));
            }
        }
    }
}
