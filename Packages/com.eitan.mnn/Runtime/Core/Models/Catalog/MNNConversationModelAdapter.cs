namespace MNN.Unity
{
    /// <summary>Registered execution protocols; catalog discovery alone does not imply a runnable export.</summary>
    public enum MNNConversationModelAdapter
    {
        Language, Omni, SenseVoice, Kokoro, CosyVoice, Silero, SmartTurn, Qwen3Asr,
        Supertonic, Piper, BertVits2, SherpaVitsChinese, MossNano, DtlnAec, Paraformer,
        VoiceActivity, AudioTurn, TextTurn, NoiseReduction, Separation, SpeakerEmbedding,
        KeywordSpotting, SpeechRecognition, SpeechSynthesis
    }
}
