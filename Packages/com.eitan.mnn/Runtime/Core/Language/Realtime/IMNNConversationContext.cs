using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNConversationContext
    {
        IMNNAudioCaptureSource Capture { get; }
        MNNRealtimeSession Session { get; }
        IMNNConversationInput Input { get; }
        bool CanInterruptSpeech { get; }
        bool SpeakerAudible { get; }
        bool RequiresUtteranceSpeakerSelection { get; }
        bool AllowUnverifiedBargeIn { get; }
        TimeSpan MaximumCaptureAge { get; }
        long CaptureRevision { get; }
        int RealtimeInactivitySeconds { set; }
        Func<Task> FlushInputAsync { get; set; }
        Func<long> LostInputFrames { get; set; }
        Task InterruptAsync();
        IMNNRealtimeVoiceInputProcessor CreateVoiceInputProcessor(MNNPcmFormat format, int maximumBlockFrames, IMNNRealtimeVoiceActivity activity);
        Task<float[]> PrepareVoiceAsync(float[] samples, CancellationToken token);
        MNNConversationWakeHandoff TakeWakeCommand();
        void ResetIdentity();
        bool RequestRealtimeExit(int revision);
        void PublishInputWarning(string message);
        void PublishRecognitionPreview(int revision, string text);
        bool TryPublishStage(MNNResponseId response, MNNConversationStage stage);
        bool TryPublishUserText(string text);
        bool TryPublishAssistantSnapshot(MNNResponseId response, StringBuilder text);
        Task<bool> PublishAssistantSnapshotAsync(MNNResponseId response, StringBuilder text, CancellationToken token);
    }
}
