namespace MNN.Unity
{
    /// <summary>Curated repository identifiers used by the package. Query the catalog for their metadata and availability.</summary>
    public static class MNNModelSources
    {
        public static bool IsCosyVoice(string repository) => MNNCosyVoiceModel.IsRepository(repository);
        public static string DocumentLayout => MNNDocumentLayoutModel.Repository;
        public static string ImageEmbedding => MNNImageEmbeddingModel.Repository;
        public static string Kws => MNNKwsModel.Repository;
        public static string ParaformerDuplicate => MNNParaformerModel.DuplicateRepository;
        public static string Paraformer => MNNParaformerModel.Repository;
        public static string Qwen3AsrModelScope => MNNQwen3AsrModel.ModelScopeRepository;
        public static string Qwen3Asr => MNNQwen3AsrModel.Repository;
        public static string ReferenceSpeechFireRed => MNNReferenceSpeechModel.FireRedRepository;
        public static string ReferenceSpeechIndex => MNNReferenceSpeechModel.IndexRepository;
        public static bool IsReferenceSpeech(string repository) => MNNReferenceSpeechModel.IsRepository(repository);
        public static string SenseVoiceHuggingFace => MNNSenseVoiceModel.HuggingFaceRepository;
        public static string SenseVoice => MNNSenseVoiceModel.Repository;
        public static string Silero => MNNSileroModel.Repository;
        public static string SmartTurn => MNNSmartTurnModel.Repository;
        public static string Speaker => MNNSpeakerModel.Repository;
        public static string TextDecision => MNNTextDecisionModel.Repository;
        public static bool IsCosyVoice300M(string repository) => MNNVerifiedTtsModel.IsCosyVoice300MRepository(repository);
        public static bool IsHuggingFaceSpeech(string repository) => MNNVerifiedTtsModel.IsHuggingFaceRepository(repository);
        public static string Qwen3Explicit => MNNVerifiedTtsModel.Qwen3ExplicitRepository;
        public static string WeSpeaker => MNNWeSpeakerModel.Repository;
        public static string Whisper => MNNWhisperModel.Repository;
        public static string YoloV8 => MNNYoloV8Model.Repository;
        public static string DtlnAec(int units) => MNNDtlnAecModel.Repository(units);
    }
}
