using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Bounded worker-to-UI notifications. A single consumer drains on its UI thread.
    /// Adjacent snapshots of one response coalesce; user turns and response boundaries retain FIFO order.</summary>
    public sealed class MNNConversationMailbox
    {
        public const int MaximumTextCharacters = 65536;
        private readonly object _gate = new object();
        private readonly Func<MNNResponseId, bool> _accepts;
        private readonly int _transcriptCapacity, _stageCapacity;
        private readonly Queue<PendingText> _texts;
        private readonly Queue<MNNConversationNotification> _stages;
        private PendingText _last;
        private char[] _spare;
        private int _partialRevision = -1;
        private string _partial, _warning;

        private sealed class PendingText
        {
            internal MNNResponseId Response;
            internal string Text;
            internal char[] Buffer;
            internal int Length;
        }

        public MNNConversationMailbox(Func<MNNResponseId, bool> accepts, int transcriptCapacity = 64, int stageCapacity = 32)
        {
            _accepts = accepts ?? throw new ArgumentNullException(nameof(accepts));
            if (transcriptCapacity < 1 || transcriptCapacity > 1024) throw new ArgumentOutOfRangeException(nameof(transcriptCapacity));
            if (stageCapacity < 1 || stageCapacity > 1024) throw new ArgumentOutOfRangeException(nameof(stageCapacity));
            _transcriptCapacity = transcriptCapacity; _stageCapacity = stageCapacity;
            _texts = new Queue<PendingText>(transcriptCapacity);
            _stages = new Queue<MNNConversationNotification>(stageCapacity);
        }

        public void PublishInputWarning(string warning) => Interlocked.Exchange(ref _warning, warning);
        public string TakeInputWarning() => Interlocked.Exchange(ref _warning, null);
        public void PublishRecognitionPreview(int revision, string text)
        {
            if (text == null || text.Length > MaximumTextCharacters) return;
            lock (_gate) { _partialRevision = revision; _partial = text; }
        }
        public bool TryReadRecognitionPreview(int revision, bool realtime, out string text)
        {
            lock (_gate)
            {
                text = _partial;
                _partial = null;
                if (text == null) return false;
                if (!realtime || _partialRevision != revision) text = "";
                return true;
            }
        }
        public bool TryPublishStage(MNNResponseId response, MNNConversationStage stage)
        {
            if (!Current(response)) return false;
            lock (_gate)
            {
                if (_stages.Count == _stageCapacity) return false;
                _stages.Enqueue(new MNNConversationNotification(response, stage, null));
                return true;
            }
        }
        /// <summary>Dequeues one queued stage with its response identity. The caller must check the response
        /// before presenting it; use TryReadStage to discard stale stages automatically.</summary>
        public bool TryReadQueuedStage(out MNNConversationNotification notification)
        {
            lock (_gate)
            {
                if (_stages.Count == 0) { notification = default; return false; }
                notification = _stages.Dequeue(); return true;
            }
        }
        public bool TryReadStage(out MNNConversationNotification notification)
        {
            for (int i = 0; i < _stageCapacity && TryReadQueuedStage(out notification); ++i)
                if (Current(notification.Response)) return true;
            notification = default; return false;
        }
        public bool TryPublishUserText(string text) => PublishText(default, text);
        public bool TryPublishAssistantText(MNNResponseId response, string text) => Current(response) && PublishText(response, text);
        private bool PublishText(MNNResponseId response, string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (text.Length > MaximumTextCharacters) return false;
            lock (_gate)
            {
                var pending = Reserve(response);
                if (pending == null) return false;
                pending.Text = text;
                return true;
            }
        }
        public bool TryPublishAssistantSnapshot(MNNResponseId response, StringBuilder text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (!Current(response) || text.Length > MaximumTextCharacters) return false;
            lock (_gate)
            {
                var pending = Reserve(response);
                if (pending == null) return false;
                if (pending.Buffer == null || pending.Buffer.Length < text.Length)
                {
                    int capacity = Math.Min(MaximumTextCharacters, Math.Max(256, Math.Max(text.Length, (pending.Buffer?.Length ?? 0) * 2)));
                    if (_spare != null && _spare.Length >= capacity) { pending.Buffer = _spare; _spare = null; }
                    else pending.Buffer = new char[capacity];
                }
                text.CopyTo(0, pending.Buffer, 0, text.Length);
                pending.Text = null;
                pending.Length = text.Length;
                return true;
            }
        }
        public async Task<bool> PublishAssistantSnapshotAsync(MNNResponseId response, StringBuilder text, CancellationToken token)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (text.Length > MaximumTextCharacters) return false;
            while (Current(response))
            {
                token.ThrowIfCancellationRequested();
                if (TryPublishAssistantSnapshot(response, text)) return true;
                await Task.Delay(1, token).ConfigureAwait(false);
            }
            return false;
        }
        private PendingText Reserve(MNNResponseId response)
        {
            if (response.IsValid && _last != null && _last.Response == response) return _last;
            if (_texts.Count == _transcriptCapacity) return null;
            _last = new PendingText { Response = response };
            _texts.Enqueue(_last);
            return _last;
        }
        public bool TryReadTranscript(out MNNConversationNotification notification)
        {
            for (int i = 0; i < _transcriptCapacity; ++i)
            {
                PendingText pending;
                lock (_gate)
                {
                    if (_texts.Count == 0) break;
                    pending = _texts.Dequeue();
                    if (ReferenceEquals(pending, _last)) _last = null;
                }
                bool current = !pending.Response.IsValid || Current(pending.Response);
                notification = current ? new MNNConversationNotification(pending.Response, MNNConversationStage.Generating,
                    pending.Text ?? new string(pending.Buffer, 0, pending.Length)) : default;
                if (pending.Buffer != null)
                {
                    lock (_gate)
                        if (_spare == null || _spare.Length < pending.Buffer.Length) _spare = pending.Buffer;
                }
                if (current) return true;
            }
            notification = default; return false;
        }
        public bool TryRead(out MNNConversationNotification notification) => TryReadStage(out notification) || TryReadTranscript(out notification);
        public void Clear()
        {
            lock (_gate) { _texts.Clear(); _stages.Clear(); _last = null; _spare = null; _partial = null; _partialRevision = -1; }
            TakeInputWarning();
        }
        private bool Current(MNNResponseId response) => response.IsValid && _accepts(response);
    }
}
