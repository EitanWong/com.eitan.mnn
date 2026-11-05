using System;
namespace MNN.Unity
{
    /// <summary>Per-provider configuration. Create snapshots these values; no Unity object or secret ownership.</summary>
    public sealed class MNNConversationLocalModelOptions
    {
        public int Threads = 4, RecognitionThreads = 1, MaximumReplyTokens = 8192, MaximumAudioTokens = 256;
        public float Temperature = .85f;
        public bool DeterministicDecisions, SpeechOutput = true, Thinking;
        internal bool TextOnly, UseDeviceLanguageDefaults;
        public MNNBackendType Backend = MNNBackendType.Auto;
        public string Voice, Language, Precision = "fp16", ReferenceWavePath, ReferenceText;
        public int SynthesisSteps = 4, SynthesisSeed = 42;
        public float SynthesisSpeed = 1;
        public string ScratchDirectory, CacheRoot, BuildIdentity, DeviceIdentity, CacheDirectory;
        internal MNNConversationLocalModelOptions Snapshot()
        {
            if (Threads < 1 || Threads > 64 || RecognitionThreads < 1 || RecognitionThreads > 64 ||
                MaximumReplyTokens < 1 || MaximumReplyTokens > 8192 || MaximumAudioTokens < 1 || MaximumAudioTokens > 2048 ||
                float.IsNaN(Temperature) || float.IsInfinity(Temperature) || Temperature < 0 || Temperature > 2 ||
                SynthesisSteps < 0 || SynthesisSteps > 30 || float.IsNaN(SynthesisSpeed) || float.IsInfinity(SynthesisSpeed) || SynthesisSpeed <= 0 || SynthesisSpeed > 10 || !Enum.IsDefined(typeof(MNNBackendType), Backend))
                throw new ArgumentException("Local provider configuration is outside supported bounded ranges.");
            return (MNNConversationLocalModelOptions)MemberwiseClone();
        }
    }
}
