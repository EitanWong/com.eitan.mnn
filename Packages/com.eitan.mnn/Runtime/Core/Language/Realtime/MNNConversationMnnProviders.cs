using System;
namespace MNN.Unity
{
    /// <summary>Convenience entry points over the complete local task factory. Loading/inference execute off the UI thread.</summary>
    public static class MNNConversationMnnProviders
    {
        public static IMNNConversationTextProvider Language(string directory, int threads = 4, int maximumTokens = 256, bool thinking = false, string cacheDirectory = null) =>
            (IMNNConversationTextProvider)MNNConversationLocalProviders.Create(MNNConversationModelAdapter.Language, directory,
                new MNNConversationLocalModelOptions { Threads = threads, MaximumReplyTokens = maximumTokens, Thinking = thinking, CacheDirectory = cacheDirectory, TextOnly = true, UseDeviceLanguageDefaults = true });
        public static IMNNConversationAsrProvider Recognition(string directory, int threads = 1, string language = null) =>
            (IMNNConversationAsrProvider)MNNConversationLocalProviders.Create(MNNConversationModelAdapter.SpeechRecognition, directory,
                new MNNConversationLocalModelOptions { RecognitionThreads = threads, Language = language });
        public static IMNNConversationTtsProvider Speech(string directory, MNNSpeechSynthesisOptions options = null, int threads = 4)
        {
            var settings = options ?? new MNNSpeechSynthesisOptions();
            return (IMNNConversationTtsProvider)MNNConversationLocalProviders.Create(MNNConversationModelAdapter.SpeechSynthesis, directory,
                new MNNConversationLocalModelOptions
                {
                    Threads = threads,
                    Voice = settings.Voice,
                    Language = settings.Language,
                    Precision = settings.Precision,
                    ReferenceWavePath = settings.ReferenceWavePath,
                    ReferenceText = settings.ReferenceText,
                    SynthesisSteps = settings.Steps,
                    SynthesisSeed = settings.Seed,
                    SynthesisSpeed = settings.Speed
                });
        }
        public static MNNRealtimeCapabilities EnergyCapabilities { get; } = new MNNRealtimeCapabilities(
            MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.VoiceActivityDetection | MNNRealtimeFeature.BoundedScheduling,
            "MNN managed energy VAD v1", "Fixed 512-sample capture worker frames.", execution: MNNRealtimeExecution.Managed);
    }
}
