using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Default application ports. Poll notifications on the UI thread; capture/model work stays on workers.</summary>
    public sealed class MNNConversationPorts : IMNNConversationContext, IMNNConversationInput
    {
        private readonly Func<MNNRealtimeSession> _session;
        private readonly Func<bool> _speakerAudible;
        private readonly object _inputGate = new object(), _textGate = new object();
        private readonly ConcurrentQueue<MNNConversationNotification> _events = new ConcurrentQueue<MNNConversationNotification>();
        private int _eventCount, _revision;
        private bool _realtime;
        private long _started;
        private readonly Queue<PendingText> _transcripts = new Queue<PendingText>(64);
        private PendingText _lastTranscript;
        private char[] _spareText;
        private string _warning, _partial;
        private int _partialRevision;
        private int _exitRevision = -1;
        public IMNNAudioCaptureSource Capture { get; }
        public MNNRealtimeSession Session => _session();
        public IMNNConversationInput Input => this;
        public bool CanInterruptSpeech { get; set; }
        public bool SpeakerAudible => _speakerAudible?.Invoke() ?? (Session?.Output.QueuedFrames > 0);
        public bool RequiresUtteranceSpeakerSelection => false;
        public bool AllowUnverifiedBargeIn => true;
        public TimeSpan MaximumCaptureAge { get; set; } = TimeSpan.FromMilliseconds(300);
        public long CaptureRevision => 0;
        public int RealtimeInactivitySeconds { get; set; } = 60;
        public Func<Task> FlushInputAsync { get; set; }
        public Func<long> LostInputFrames { get; set; }
        public bool Realtime { get { lock (_inputGate) return _realtime; } }
        public bool Recording => false;
        public int Revision { get { lock (_inputGate) return _revision; } }
        public int RealtimeRevision { get { lock (_inputGate) return _realtime ? _revision : -1; } }
        public MNNConversationPorts(IMNNAudioCaptureSource capture, Func<MNNRealtimeSession> session, Func<bool> speakerAudible = null)
        { Capture = capture ?? throw new ArgumentNullException(nameof(capture)); _session = session ?? throw new ArgumentNullException(nameof(session)); _speakerAudible = speakerAudible; }
        public void SetRealtime(bool enabled)
        { lock (_inputGate) { _realtime = enabled; _started = Stopwatch.GetTimestamp(); ++_revision; } }
        public bool Consume(float[] frame, bool speechActive = false, long enqueuedMonotonicTicks = 0, bool discontinuity = false)
        { lock (_inputGate) return !_realtime || enqueuedMonotonicTicks > 0 && enqueuedMonotonicTicks < _started; }
        public bool TryAcceptRealtime(int revision, Action accept)
        { lock (_inputGate) { if (!_realtime || revision != _revision) return false; accept(); return true; } }
        public Task InterruptAsync() => Session?.InterruptAsync() ?? Task.CompletedTask;
        public IMNNRealtimeVoiceInputProcessor CreateVoiceInputProcessor(MNNPcmFormat format, int maximumBlockFrames, IMNNRealtimeVoiceActivity activity) =>
            new MNNRealtimeVoiceInputProcessor(format, activity, maximumBlockFrames);
        public Task<float[]> PrepareVoiceAsync(float[] samples, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(samples); }
        public MNNConversationWakeHandoff TakeWakeCommand() => null;
        public void ResetIdentity() { }
        public bool RequestRealtimeExit(int revision) => TryAcceptRealtime(revision, () => Interlocked.Exchange(ref _exitRevision, revision));
        public bool TakeRealtimeExit() { int revision = Interlocked.Exchange(ref _exitRevision, -1); return revision >= 0 && TryAcceptRealtime(revision, () => { }); }
        public void PublishInputWarning(string message) => Interlocked.Exchange(ref _warning, message);
        public string TakeInputWarning() => Interlocked.Exchange(ref _warning, null);
        public void PublishRecognitionPreview(int revision, string text)
        {
            if (text == null || text.Length > 65536) return;
            lock (_textGate) { _partialRevision = revision; _partial = text; }
        }
        public string TakeRecognitionPreview()
        {
            int revision = RealtimeRevision;
            lock (_textGate) { var text = _partialRevision == revision ? _partial : ""; _partial = null; return text; }
        }
        public bool TryPublishStage(MNNResponseId response, MNNConversationStage stage) =>
            Current(response) && Enqueue(new MNNConversationNotification(response, stage, null));
        public bool TryPublishUserText(string text)
        {
            if (text == null || text.Length > 65536) return false;
            lock (_textGate)
            {
                var pending = ReserveText(default);
                if (pending == null) return false;
                pending.Text = text;
                return true;
            }
        }
        public bool TryPublishAssistantSnapshot(MNNResponseId response, StringBuilder text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (!Current(response) || text.Length > 65536) return false;
            lock (_textGate)
            {
                var pending = ReserveText(response);
                if (pending == null) return false;
                if (pending.Buffer == null || pending.Buffer.Length < text.Length)
                {
                    int capacity = Math.Min(65536, Math.Max(256, Math.Max(text.Length, (pending.Buffer?.Length ?? 0) * 2)));
                    if (_spareText != null && _spareText.Length >= capacity)
                    { pending.Buffer = _spareText; _spareText = null; }
                    else pending.Buffer = new char[capacity];
                }
                text.CopyTo(0, pending.Buffer, 0, text.Length);
                pending.Length = text.Length;
            }
            return true;
        }
        public async Task<bool> PublishAssistantSnapshotAsync(MNNResponseId response, StringBuilder text, CancellationToken token)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (text.Length > 65536) return false;
            while (Current(response))
            {
                token.ThrowIfCancellationRequested();
                if (TryPublishAssistantSnapshot(response, text)) return true;
                await Task.Delay(1, token).ConfigureAwait(false);
            }
            return false;
        }
        private PendingText ReserveText(MNNResponseId response)
        {
            if (response.IsValid && _lastTranscript != null && _lastTranscript.Response == response) return _lastTranscript;
            if (_transcripts.Count >= 64) return null;
            _lastTranscript = new PendingText(response);
            _transcripts.Enqueue(_lastTranscript);
            return _lastTranscript;
        }
        public bool TryRead(out MNNConversationNotification notification)
        {
            for (int i = 0; i < 64 && _events.TryDequeue(out notification); ++i)
            {
                Interlocked.Decrement(ref _eventCount);
                if (Current(notification.Response)) return true;
            }
            for (int i = 0; i < 64; ++i)
            {
                PendingText pending;
                lock (_textGate)
                {
                    if (_transcripts.Count == 0) break;
                    pending = _transcripts.Dequeue();
                    if (ReferenceEquals(pending, _lastTranscript)) _lastTranscript = null;
                }
                bool current = !pending.Response.IsValid || Current(pending.Response);
                notification = current ? new MNNConversationNotification(pending.Response,
                    MNNConversationStage.Generating, pending.Text ?? new string(pending.Buffer, 0, pending.Length)) : default;
                if (pending.Buffer != null)
                {
                    lock (_textGate)
                        if (_spareText == null || _spareText.Length < pending.Buffer.Length) _spareText = pending.Buffer;
                }
                if (current) return true;
            }
            notification = default;
            return false;
        }
        private sealed class PendingText
        {
            internal readonly MNNResponseId Response;
            internal string Text;
            internal char[] Buffer;
            internal int Length;
            internal PendingText(MNNResponseId response) { Response = response; }
        }
        private bool Current(MNNResponseId response) => response.IsValid && Session != null && Session.Output.Accepts(response);
        private bool Enqueue(MNNConversationNotification value)
        {
            if (Interlocked.Increment(ref _eventCount) > 64) { Interlocked.Decrement(ref _eventCount); return false; }
            _events.Enqueue(value); return true;
        }
    }
}
