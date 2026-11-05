using MNN.Unity;

namespace MNN.Examples
{
    /// <summary>Minimal application composition. Device binding and UI consume package ports.</summary>
    public static class MNNConversationExample
    {
        public static MNNConversationRuntimeFactory Configure(
            MNNRealtimeSessionComponent owner, IMNNAudioCaptureSource capture,
            MNNRealtimeOutputPipeline output, MNNRealtimeCapabilities deviceCapabilities,
            string modelDirectory, string recognitionDirectory, string speechDirectory,
            out MNNConversationPorts ports)
        {
            ports = new MNNConversationPorts(capture, () => owner.Session);
            var factory = new MNNConversationRuntimeFactory(ports,
                new MNNConversationOptions(), CreateLocalRegistrations(modelDirectory, recognitionDirectory, speechDirectory));
            owner.Configure(factory.CreateFallback(output, deviceCapabilities, requireMeasuredSpeech: false));
            return factory;
        }

        public static MNNConversationRegistrations CreateLocalRegistrations(string modelDirectory, string recognitionDirectory, string speechDirectory) =>
            new MNNConversationRegistrations
            {
                Model = Local(MNNConversationModelAdapter.Language, modelDirectory),
                Asr = Local(MNNConversationModelAdapter.SpeechRecognition, recognitionDirectory),
                Tts = Local(MNNConversationModelAdapter.SpeechSynthesis, speechDirectory),
                Vad = MNNConversationStageRegistration.Energy,
                AllowBufferedSpeech = true
            };

        private static MNNConversationStageRegistration Local(MNNConversationModelAdapter task, string directory) =>
            new MNNConversationStageRegistration(MNNConversationLocalProviders.DescribeCapabilities(task),
                () => MNNConversationLocalProviders.Create(task, directory));
    }
}
