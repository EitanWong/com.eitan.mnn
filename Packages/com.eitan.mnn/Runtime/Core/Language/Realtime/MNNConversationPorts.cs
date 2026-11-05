using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Default application ports over shared input and notification ownership.</summary>
    public sealed class MNNConversationPorts : IMNNConversationContext, IMNNConversationInput
    {
        private readonly Func<MNNRealtimeSession> _session;
        private readonly Func<bool> _speakerAudible;
        private int _exitRevision = -1;
        public MNNConversationInputState InputState { get; } = new MNNConversationInputState();
        public MNNConversationMailbox Mailbox { get; }
        public MNNConversationRecording RecordingControl { get; }
        public Action<bool> CaptureGate { get; set; }
        public Func<CancellationToken, Task> PauseCaptureAsync { get; set; }
        public Func<bool> CaptureReady { get; set; } = () => true;
        public IMNNAudioCaptureSource Capture { get; }
        public MNNRealtimeSession Session => _session();
        public IMNNConversationInput Input => InputState;
        public bool CanInterruptSpeech { get; set; }
        public bool SpeakerAudible => _speakerAudible?.Invoke() ?? (Session?.Output.QueuedFrames > 0);
        public bool RequiresUtteranceSpeakerSelection => false;
        public bool AllowUnverifiedBargeIn => true;
        public TimeSpan MaximumCaptureAge { get; set; } = TimeSpan.FromMilliseconds(300);
        public long CaptureRevision => 0;
        public int RealtimeInactivitySeconds { get; set; } = 60;
        public Func<Task> FlushInputAsync { get => RecordingControl.FlushInputAsync; set => RecordingControl.FlushInputAsync = value; }
        public Func<long> LostInputFrames { get; set; }
        public bool Realtime => InputState.Realtime;
        public bool Recording => InputState.Recording;
        public int Revision => InputState.Revision;
        public int RealtimeRevision => InputState.RealtimeRevision;
        public MNNConversationPorts(IMNNAudioCaptureSource capture, Func<MNNRealtimeSession> session, Func<bool> speakerAudible = null)
        {
            Capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _session = session ?? throw new ArgumentNullException(nameof(session)); _speakerAudible = speakerAudible;
            Mailbox = new MNNConversationMailbox(response => Session?.Output.Accepts(response) == true, stageCapacity: 64);
            RecordingControl = new MNNConversationRecording(InputState)
            {
                PauseCaptureAsync = token => PauseCaptureAsync?.Invoke(token) ?? Task.CompletedTask,
                SetCaptureEnabled = enabled => SetCaptureEnabled(enabled),
                LostInputFrames = () => LostInputFrames?.Invoke() ?? Capture.DroppedCaptureBlocks
            };
        }
        public void SetCaptureEnabled(bool enabled)
        {
            if (enabled) RecordingControl.EnsureDrained();
            CaptureGate?.Invoke(enabled && (InputState.Realtime || InputState.Recording));
        }
        /// <summary>Creates command bindings with shared capture ownership. Supply provider submission and entrance delegates.</summary>
        public MNNConversationCommandBindings CreateCommandBindings() => new MNNConversationCommandBindings(InputState, RecordingControl)
        {
            SessionState = () => Session?.State ?? MNNRealtimeSessionState.Created,
            CanInterrupt = () => CanInterruptSpeech,
            SpeakerAudible = () => SpeakerAudible,
            CaptureReady = () => CaptureReady(),
            SetCaptureEnabled = SetCaptureEnabled,
            InterruptAsync = InterruptAsync
        };
        public void SetRealtime(bool enabled) => InputState.SetRealtime(enabled);
        public bool Consume(float[] frame, bool speechActive = false, long enqueuedMonotonicTicks = 0, bool discontinuity = false) =>
            InputState.Consume(frame, speechActive, enqueuedMonotonicTicks, discontinuity);
        public bool TryAcceptRealtime(int revision, Action accept) => InputState.TryAcceptRealtime(revision, accept);
        public Task InterruptAsync() => Session?.InterruptAsync() ?? Task.CompletedTask;
        public IMNNRealtimeVoiceInputProcessor CreateVoiceInputProcessor(MNNPcmFormat format, int maximumBlockFrames, IMNNRealtimeVoiceActivity activity) =>
            new MNNRealtimeVoiceInputProcessor(format, activity, maximumBlockFrames);
        public Task<float[]> PrepareVoiceAsync(float[] samples, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(samples); }
        public MNNConversationWakeHandoff TakeWakeCommand() => null;
        public void ResetIdentity() { }
        public bool RequestRealtimeExit(int revision) => TryAcceptRealtime(revision, () => Interlocked.Exchange(ref _exitRevision, revision));
        public bool TakeRealtimeExit() { int revision = Interlocked.Exchange(ref _exitRevision, -1); return revision >= 0 && TryAcceptRealtime(revision, () => { }); }
        public void PublishInputWarning(string message) => Mailbox.PublishInputWarning(message);
        public string TakeInputWarning() => Mailbox.TakeInputWarning();
        public void PublishRecognitionPreview(int revision, string text) => Mailbox.PublishRecognitionPreview(revision, text);
        public string TakeRecognitionPreview() => Mailbox.TryReadRecognitionPreview(Revision, Realtime, out var text) ? text : null;
        public bool TryPublishStage(MNNResponseId response, MNNConversationStage stage) => Mailbox.TryPublishStage(response, stage);
        public bool TryPublishUserText(string text) => text != null && Mailbox.TryPublishUserText(text);
        public bool TryPublishAssistantSnapshot(MNNResponseId response, StringBuilder text) => Mailbox.TryPublishAssistantSnapshot(response, text);
        public Task<bool> PublishAssistantSnapshotAsync(MNNResponseId response, StringBuilder text, CancellationToken token) => Mailbox.PublishAssistantSnapshotAsync(response, text, token);
        public bool TryRead(out MNNConversationNotification notification) => Mailbox.TryRead(out notification);
    }
}
