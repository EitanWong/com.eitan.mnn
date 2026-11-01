using System;

namespace MNN.Unity
{
    /// <summary>
    /// Bounded low-frequency metadata ledger. Registration, receipts, interruption and snapshots may come from
    /// separate producer/control workers; none are called from the real-time audio callback. Commits complete
    /// segments only, using a device presentation watermark. Generated/queued text is not spoken history.
    /// </summary>
    public sealed class MNNConversationLedger
    {
        private readonly object _gate = new object();
        private sealed class Segment
        {
            internal MNNResponseId Response;
            internal long Id;
            internal string Text;
            internal int NextChunk;
            internal int NextSourceFrame;
            internal long SubmittedFrames;
            internal long EndFrame;
            internal bool Complete;
            internal bool Interrupted;
            internal bool EstimatedPresentation;
        }

        private readonly Segment[] _segments;
        private int _count;
        private MNNResponseId _retiredThrough;
        private long _presentedThrough;
        private bool _presentationEstimated;
        public int Count
        {
            get { lock (_gate) return _count; }
        }
        public long PresentedThroughFrame
        {
            get { lock (_gate) return _presentedThrough; }
        }

        public MNNConversationLedger(int segmentCapacity = 256)
        {
            if (segmentCapacity < 1 || segmentCapacity > 65536)
                throw new ArgumentOutOfRangeException(nameof(segmentCapacity));
            _segments = new Segment[segmentCapacity];
        }

        /// <summary>Control thread. Capacity exhaustion requires explicit context rollover, never silent eviction.</summary>
        public bool TryRegister(MNNResponseId response, long segmentId, string spokenText)
        {
            if (!response.IsValid || segmentId < 0)
                throw new ArgumentOutOfRangeException(nameof(response));
            if (string.IsNullOrWhiteSpace(spokenText))
                throw new ArgumentException("Register stable spoken text only.", nameof(spokenText));
            if (spokenText.Length > 65536)
                throw new ArgumentOutOfRangeException(nameof(spokenText));
            lock (_gate)
            {
                if (IsRetiredUnsafe(response)) throw new InvalidOperationException("Response history has been retired.");
                if (FindUnsafe(response, segmentId) != null)
                    throw new InvalidOperationException("Segment identity is already registered.");
                int count = _count;
                if (count == _segments.Length)
                    return false;
                _segments[count] = new Segment { Response = response, Id = segmentId, Text = spokenText };
                _count = count + 1;
                return true;
            }
        }

        internal bool IsRegistered(MNNResponseId response, long segmentId)
        {
            lock (_gate) return FindUnsafe(response, segmentId) != null;
        }

        /// <summary>Receipts must be drained in render order, including after interruption.</summary>
        public void RecordSubmission(MNNPlaybackReceipt receipt)
        {
            lock (_gate)
            {
                // A joined old producer may still have a final device receipt in flight. Retired
                // epochs deliberately stop contributing to history; never resurrect their metadata.
                if (IsRetiredUnsafe(receipt.Chunk.Response)) return;
                var segment = FindUnsafe(receipt.Chunk.Response, receipt.Chunk.SegmentId);
                if (segment == null)
                    throw new InvalidOperationException("Register a segment before publishing its audio.");
                if (segment.Complete || receipt.FrameCount < 1 || receipt.Chunk.ChunkSequence != segment.NextChunk ||
                    receipt.SourceStartFrame != segment.NextSourceFrame || receipt.OutputStartFrame < segment.EndFrame)
                    throw new InvalidOperationException("Missing, duplicate or out-of-order playback receipt.");
                segment.NextSourceFrame += receipt.FrameCount;
                segment.SubmittedFrames += receipt.FrameCount;
                segment.EndFrame = receipt.OutputEndFrame;
                if (receipt.EndsChunk)
                {
                    ++segment.NextChunk;
                    segment.NextSourceFrame = 0;
                }
                if (receipt.EndsSegment)
                {
                    segment.Complete = true;
                    if (segment.EndFrame <= _presentedThrough)
                        segment.EstimatedPresentation = _presentationEstimated;
                }
            }
        }

        /// <summary>Device output frame clock, exclusive. Never use generated length or Unity frame time.</summary>
        public void AcknowledgePresentation(long throughFrame, bool estimated)
        {
            lock (_gate)
            {
                if (throughFrame < _presentedThrough)
                    throw new ArgumentOutOfRangeException(nameof(throughFrame), "Presentation clock must be monotonic.");
                _presentedThrough = throughFrame;
                _presentationEstimated = estimated;
                for (int i = 0; i < _count; ++i)
                {
                    if (_segments[i].Complete && _segments[i].EndFrame <= throughFrame)
                        _segments[i].EstimatedPresentation = estimated;
                }
            }
        }

        public void Interrupt(MNNResponseId response)
        {
            lock (_gate)
            {
                for (int i = 0; i < _count; ++i)
                {
                    if (_segments[i].Response == response)
                        _segments[i].Interrupted = true;
                }
            }
        }

        internal bool IsPresented(MNNResponseId response)
        {
            lock (_gate)
            {
                for (int i = 0; i < _count; ++i)
                {
                    var segment = _segments[i];
                    if (segment.Response != response)
                        continue;
                    if (!segment.Complete || segment.EndFrame > _presentedThrough)
                        return false;
                }
                return true;
            }
        }

        public MNNConversationSegment GetSegment(int index)
        {
            lock (_gate)
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                var segment = _segments[index];
                bool presented = segment.Complete && segment.EndFrame <= _presentedThrough;
                return new MNNConversationSegment(segment.Response, segment.Id, segment.Text, segment.SubmittedFrames,
                    presented, segment.Interrupted, presented && segment.EstimatedPresentation);
            }
        }

        /// <summary>After snapshotting committed text and joining producers, retire old epochs to bound metadata.</summary>
        internal void RetireThrough(MNNResponseId response)
        {
            lock (_gate)
            {
                if (_retiredThrough.IsValid && (_retiredThrough.Session != response.Session || _retiredThrough.Epoch > response.Epoch))
                    throw new ArgumentException("Retirement must advance within one session.");
                _retiredThrough = response;
                int write = 0;
                for (int read = 0; read < _count; ++read)
                    if (!IsRetiredUnsafe(_segments[read].Response)) _segments[write++] = _segments[read];
                Array.Clear(_segments, write, _count - write);
                _count = write;
            }
        }
        private bool IsRetiredUnsafe(MNNResponseId response) => _retiredThrough.IsValid &&
            response.Session == _retiredThrough.Session && response.Epoch <= _retiredThrough.Epoch;

        private Segment FindUnsafe(MNNResponseId response, long id)
        {
            for (int i = 0; i < _count; ++i)
            {
                if (_segments[i].Response == response && _segments[i].Id == id)
                    return _segments[i];
            }
            return null;
        }
    }
}
