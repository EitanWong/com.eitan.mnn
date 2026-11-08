using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Generation evidence only. Playback requires the output pipeline's presentation receipts.</summary>
    public sealed class MNNConversationEventReply
    {
        private readonly object _sync = new object();
        private MNNResponseId _response;
        private int _injected, _succeeded, _textLength, _displayedLength, _playbackStarted, _playbackCompleted, _interrupted;
        public MNNConversationEvent Input { get; }
        public MNNResponseId Response { get { lock (_sync) return _response; } }
        public bool ContextInjected => Volatile.Read(ref _injected) != 0;
        public bool Succeeded => Volatile.Read(ref _succeeded) != 0;
        public bool TextDisplayed => Completion.IsCompleted && Succeeded && Volatile.Read(ref _textLength) > 0 &&
            Volatile.Read(ref _displayedLength) == Volatile.Read(ref _textLength);
        public bool PlaybackStarted => Volatile.Read(ref _playbackStarted) != 0;
        public bool PlaybackCompleted => Volatile.Read(ref _playbackCompleted) != 0;
        public bool Interrupted => Volatile.Read(ref _interrupted) != 0;
        public Task Completion { get; internal set; }
        internal MNNConversationEventReply(MNNConversationEvent input) { Input = input; }
        internal void Begin(MNNResponseId response) { lock (_sync) _response = response; }
        internal void Injected() => Volatile.Write(ref _injected, 1);
        internal void TextGenerated(int length) => Volatile.Write(ref _textLength, length);
        internal void Complete(bool success) => Volatile.Write(ref _succeeded, success ? 1 : 0);

        /// <summary>Call after the application actually delivers a cumulative text snapshot to its presentation.</summary>
        public void AcknowledgeTextDisplayed(MNNResponseId response, int characters)
        { if (response == Response && response.IsValid) Volatile.Write(ref _displayedLength, characters); }

        /// <summary>Conservative segment-level evidence: queued or device-submitted audio alone never means heard.</summary>
        public void ObservePresentation(MNNRealtimeOutputPipeline output)
        {
            var response = Response;
            if (!response.IsValid || output == null) return;
            bool hasSegments = false;
            bool allPresented = true;
            for (int i = 0; i < output.SegmentCount; ++i)
            {
                var segment = output.GetSegment(i);
                if (segment.Response != response) continue;
                hasSegments = true;
                allPresented &= segment.IsPresented;
                if (segment.IsPresented) Volatile.Write(ref _playbackStarted, 1);
            }
            if (hasSegments && allPresented && Completion.IsCompleted && Succeeded)
                Volatile.Write(ref _playbackCompleted, 1);
            if (!output.Accepts(response) && !PlaybackCompleted) Volatile.Write(ref _interrupted, 1);
        }
    }
}
