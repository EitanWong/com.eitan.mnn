using System;
namespace MNN.Unity
{
    public enum MNNOpenAIConversationStage { Model, Asr, Tts }
    public sealed class MNNOpenAIConversationOptions
    {
        public string Endpoint, Model, Voice;
        public string[] SpeechLanguages = Array.Empty<string>();
        public int TimeoutSeconds = 300, MaximumTokens = 8192, MaximumCharacters = 65536;
        public float Temperature = .85f;
        public bool AcceptsAudioInput, SupportsProgressivePcm;
        internal MNNOpenAIConversationOptions Snapshot()
        {
            Resolve();
            if (MaximumTokens < 1 || MaximumTokens > 8192 || MaximumCharacters < 1 || MaximumCharacters > 65536 ||
                float.IsNaN(Temperature) || float.IsInfinity(Temperature) || Temperature < 0 || Temperature > 2)
                throw new ArgumentException("OpenAI conversation limits are outside supported ranges.");
            var copy = (MNNOpenAIConversationOptions)MemberwiseClone();
            copy.SpeechLanguages = MNNRemoteProviderOptions.CopyLanguages(SpeechLanguages);
            return copy;
        }
        internal Uri Resolve() => MNNRemoteProviderOptions.Resolve(Endpoint, Model, TimeoutSeconds, "");
    }
}
