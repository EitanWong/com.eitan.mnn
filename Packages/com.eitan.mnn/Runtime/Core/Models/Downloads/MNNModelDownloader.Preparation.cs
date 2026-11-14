using System;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNModelDownloader
    {
        public static void PrepareArchives(string directory, MNNModelSource source, string repository, CancellationToken token = default)
        {
            if (source == MNNModelSource.HuggingFace && MNNCosyVoiceModel.IsRepository(repository))
                MNNCosyVoiceAssets.Install(directory, token);
        }

        private static void Prepare(string directory, MNNModelSource source, string repository, CancellationToken token)
        {
            PrepareArchives(directory, source, repository, token);
            if (source == MNNModelSource.ModelScope && MNNDtlnAecModel.IsRepository(repository))
                MNNDtlnAecModel.Prepare(directory, repository);
            if (source == MNNModelSource.HuggingFace && MNNBertEmbeddingModel.IsRepository(repository))
                MNNBertEmbeddingModel.Prepare(directory, repository);
            if (source == MNNModelSource.ModelScope && MNNDocumentLayoutModel.IsRepository(repository))
                MNNDocumentLayoutModel.Verify(directory);
            if (MNNImageEmbeddingModel.IsRepository(repository))
                MNNImageEmbeddingModel.Prepare(directory);
            if (MNNImageTaskProfiles.IsRepository(repository))
                MNNImageTaskProfiles.Prepare(directory, repository);
            if (source == MNNModelSource.HuggingFace && MNNSpeakerModel.IsRepository(repository))
                MNNSpeakerModel.Prepare(directory);
            if (source == MNNModelSource.ModelScope && MNNYoloV8Model.IsRepository(repository))
                MNNYoloV8Model.Prepare(directory);
            if (source == MNNModelSource.HuggingFace && MNNPaddleTextModel.IsRepository(repository))
                MNNPaddleTextModel.Prepare(directory);
            if (source == MNNModelSource.ModelScope && string.Equals(repository, MNNSenseVoiceModel.Repository, StringComparison.OrdinalIgnoreCase) || source == MNNModelSource.HuggingFace && MNNSenseVoiceModel.IsHuggingFaceRepository(repository))
                MNNSenseVoiceModel.Prepare(directory, source == MNNModelSource.HuggingFace);
            if (MNNZipformerModel.IsRepository(repository))
                MNNZipformerModel.Prepare(directory, repository);
            if (source == MNNModelSource.HuggingFace && MNNWhisperModel.IsRepository(repository) || source == MNNModelSource.ModelScope && MNNWhisperModel.IsModelScopeRepository(repository))
                MNNWhisperModel.Prepare(directory, repository);
            if (source == MNNModelSource.HuggingFace && MNNQwen3AsrModel.IsRepository(repository) || source == MNNModelSource.ModelScope && MNNQwen3AsrModel.IsModelScopeRepository(repository))
                MNNQwen3AsrModel.Prepare(directory, repository);
            if (source == MNNModelSource.HuggingFace && MNNParaformerModel.IsHuggingFaceRepository(repository) || source == MNNModelSource.ModelScope && MNNParaformerModel.IsModelScopeRepository(repository))
                MNNParaformerModel.Prepare(directory, repository);
            if (source == MNNModelSource.HuggingFace && MNNReferenceSpeechModel.IsRepository(repository))
                MNNReferenceSpeechModel.Validate(directory, MNNReferenceSpeechModel.IsIndex(repository));
            if (source == MNNModelSource.ModelScope && MNNVerifiedTtsModel.IsRepository(repository) || source == MNNModelSource.HuggingFace && MNNVerifiedTtsModel.IsHuggingFaceRepository(repository))
                MNNVerifiedTtsModel.Prepare(directory, repository);
            if (source == MNNModelSource.ModelScope && MNNEnglishKwsModel.IsModelScopeRepository(repository))
                MNNEnglishKwsModel.Prepare(directory);
            if (source == MNNModelSource.HuggingFace && MNNKwsModel.IsRepository(repository))
                MNNKwsModel.Prepare(directory);
            if (source == MNNModelSource.HuggingFace && MNNSileroModel.IsRepository(repository))
                MNNSileroModel.Prepare(directory);
            if (source == MNNModelSource.HuggingFace && MNNSmartTurnModel.IsRepository(repository))
                MNNSmartTurnModel.Prepare(directory);
            token.ThrowIfCancellationRequested();
        }
    }
}
