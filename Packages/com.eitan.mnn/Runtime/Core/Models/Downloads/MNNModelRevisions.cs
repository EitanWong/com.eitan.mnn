using System;

namespace MNN.Unity
{
    public static class MNNModelRevisions
    {
        /// <summary>Resolves a supported repository's pinned revision; rejects incompatible revisions.
        /// Unknown repositories retain the requested revision or their provider's default branch.</summary>
        public static string Resolve(MNNModelSource source, string repositoryId, string revision = null)
        {
            if (!Enum.IsDefined(typeof(MNNModelSource), source)) throw new ArgumentOutOfRangeException(nameof(source));
            if (string.IsNullOrWhiteSpace(repositoryId)) throw new ArgumentException("Supply a repository ID.", nameof(repositoryId));
            if (string.IsNullOrWhiteSpace(revision)) revision = source == MNNModelSource.ModelScope ? "master" : "main";
            if (source == MNNModelSource.HuggingFace && MNNCosyVoiceModel.IsRepository(repositoryId) && revision == "main")
                revision = MNNCosyVoiceModel.Revision;
            if (source == MNNModelSource.HuggingFace && MNNVerifiedTtsModel.IsHuggingFaceRepository(repositoryId))
                revision = MNNVerifiedTtsModel.ResolveHuggingFaceRevision(repositoryId, revision);
            if (source == MNNModelSource.HuggingFace && MNNBertEmbeddingModel.IsRepository(repositoryId))
                revision = MNNBertEmbeddingModel.ResolveRevision(repositoryId, revision);
            if (source == MNNModelSource.HuggingFace && MNNPaddleTextModel.IsRepository(repositoryId))
                revision = MNNPaddleTextModel.ResolveRevision(revision);
            if (source == MNNModelSource.HuggingFace && MNNReferenceSpeechModel.IsRepository(repositoryId))
                revision = MNNReferenceSpeechModel.ResolveRevision(repositoryId, revision);
            if (source == MNNModelSource.HuggingFace && MNNWhisperModel.IsRepository(repositoryId) || source == MNNModelSource.ModelScope && MNNWhisperModel.IsModelScopeRepository(repositoryId))
                revision = MNNWhisperModel.ResolveRevision(revision, repositoryId);
            if (source == MNNModelSource.HuggingFace && MNNQwen3AsrModel.IsRepository(repositoryId) || source == MNNModelSource.ModelScope && MNNQwen3AsrModel.IsModelScopeRepository(repositoryId))
                revision = MNNQwen3AsrModel.ResolveRevision(revision, repositoryId);
            if (source == MNNModelSource.HuggingFace && MNNParaformerModel.IsHuggingFaceRepository(repositoryId) || source == MNNModelSource.ModelScope && MNNParaformerModel.IsModelScopeRepository(repositoryId))
                revision = MNNParaformerModel.ResolveRevision(revision, repositoryId, source == MNNModelSource.ModelScope);
            if (source == MNNModelSource.HuggingFace && MNNKwsModel.IsRepository(repositoryId))
                revision = MNNKwsModel.ResolveRevision(revision);
            if (source == MNNModelSource.HuggingFace && MNNSileroModel.IsRepository(repositoryId))
                revision = MNNSileroModel.ResolveRevision(revision);
            if (source == MNNModelSource.HuggingFace && MNNSmartTurnModel.IsRepository(repositoryId))
                revision = MNNSmartTurnModel.ResolveRevision(revision);
            return revision;
        }
    }
}
