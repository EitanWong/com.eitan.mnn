using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed partial class MNNModelDownloader
    {
        public async Task<long> DownloadDocumentLayoutProfileAsync(string repository, string directory,
            Action<long, long, string> onProgress, CancellationToken cancellationToken)
        {
            if (!MNNDocumentLayoutModel.IsRepository(repository)) throw new ArgumentException("Unsupported layout repository.", nameof(repository));
            Directory.CreateDirectory(directory);
            long total = MNNDocumentLayoutModel.Sizes.Sum(), received = 0;
            for (int i = 0; i < MNNDocumentLayoutModel.Files.Length; ++i)
            {
                var file = new MNNModelRepositoryFile {Path = MNNDocumentLayoutModel.Files[i], Size = MNNDocumentLayoutModel.Sizes[i],
                    Sha256 = MNNDocumentLayoutModel.Hashes[i], Revision = MNNDocumentLayoutModel.Revision};
                received += await DownloadFileAsync(repository, file, Path.Combine(directory, file.Path),
                    bytes => onProgress?.Invoke(received + bytes, total, file.Path), cancellationToken,
                    () => "https://www.modelscope.cn/api/v1/models/" + repository + "/repo?Revision=" + MNNDocumentLayoutModel.Revision + "&FilePath=" + file.Path).ConfigureAwait(false);
            }
            return received;
        }

        public async Task<long> DownloadImageEmbeddingProfileAsync(string repository, string directory,
            Action<long, long, string> onProgress, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(directory);
            long total = MNNImageEmbeddingModel.Sizes.Sum(), received = 0;
            for (int i = 0; i < MNNImageEmbeddingModel.Files.Length; ++i)
            {
                var file = new MNNModelRepositoryFile {Path = MNNImageEmbeddingModel.Files[i], Size = MNNImageEmbeddingModel.Sizes[i],
                    Sha256 = MNNImageEmbeddingModel.Hashes[i], Revision = MNNImageEmbeddingModel.Revision};
                received += await DownloadFileAsync(repository, file, Path.Combine(directory, file.Path),
                    bytes => onProgress?.Invoke(received + bytes, total, file.Path), cancellationToken,
                    () => "https://www.modelscope.cn/api/v1/models/" + repository + "/repo?Revision=" + MNNImageEmbeddingModel.Revision + "&FilePath=" + file.Path).ConfigureAwait(false);
            }
            return received;
        }

        // Exact task assets only: do not download unrelated precision variants,
        // PyTorch checkpoints or ONNX weights from a publisher's collection.
        public async Task<long> DownloadImageProfileAsync(string repository, string directory,
            Action<long, long, string> onProgress, CancellationToken cancellationToken)
        {
            var source = MNNImageTaskProfiles.All.Single(item => string.Equals(item.Repository, repository, StringComparison.OrdinalIgnoreCase));
            if (source.Url == null) throw new NotSupportedException("This profile requires preparation with the official MNN converter. Use Tools~/Models/prepare_image_profile.py, then select its local folder in Studio.");
            Directory.CreateDirectory(directory);
            var graph = new MNNModelRepositoryFile {Path = source.Graph, Size = source.Bytes, Sha256 = source.Hash, Revision = source.Hash};
            long total = source.Bytes + (source.Profile == MNNImageTaskProfile.MobileNetV2 ? 21550 : 0);
            long received = await DownloadFileAsync(repository, graph, Path.Combine(directory, source.Graph),
                bytes => onProgress?.Invoke(bytes, total, source.Graph), cancellationToken, () => source.Url).ConfigureAwait(false);
            if (source.Profile == MNNImageTaskProfile.MobileNetV2)
            {
                var labels = new MNNModelRepositoryFile {Path = "labels.txt", Size = 21550,
                    Sha256 = "07994f2a44b48305675f84979bfc22f4f58d3e99da2db514142917882250a926", Revision = "07994f2a44b48305"};
                received += await DownloadFileAsync(repository, labels, Path.Combine(directory, labels.Path),
                    bytes => onProgress?.Invoke(source.Bytes + bytes, total, labels.Path), cancellationToken,
                    () => "https://raw.githubusercontent.com/wangzhaode/mnn-mobilenet/main/android/app/resource/names.txt").ConfigureAwait(false);
            }
            return received;
        }
    }
}
