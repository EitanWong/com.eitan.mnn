using System;

namespace MNN.Unity
{
    /// <summary>
    /// Composes response gating, bounded PCM playback and presentation-aware spoken history.
    /// One control owner calls lifecycle methods, one producer enqueues, and one device callback renders.
    /// Ledger metadata operations are internally serialized and never run on the device callback.
    /// </summary>
    public sealed class MNNRealtimeOutputPipeline
    {
        private readonly MNNRealtimePlayback _playback;
        private readonly MNNConversationLedger _ledger;
        internal event Action<MNNResponseId> ResponseInterrupted;

        public MNNPcmFormat Format => _playback.Format;
        public MNNResponseId CurrentResponse => _playback.CurrentResponse;
        public int MaximumSamplesPerChunk => _playback.MaximumSamples;
        public long QueuedFrames => _playback.QueuedFrames;
        public double QueuedAudioMilliseconds => _playback.QueuedAudioMilliseconds;
        public bool IsFaulted => _playback.IsFaulted;
        public long UnderrunFrames => _playback.UnderrunFrames;
        public long DiscardedChunks => _playback.DiscardedChunks;
        public int SegmentCount => _ledger.Count;

        public MNNRealtimeOutputPipeline(MNNPcmFormat format, int framesPerBlock,
            int blockCapacity = 6, int receiptCapacity = 256, int segmentCapacity = 256)
        {
            _playback = new MNNRealtimePlayback(format, framesPerBlock, blockCapacity, receiptCapacity);
            _ledger = new MNNConversationLedger(segmentCapacity);
        }

        /// <summary>Control owner only. Use Interrupt first when replacing a response before it naturally finishes.</summary>
        public MNNResponseId BeginResponse() => _playback.BeginResponse();

        /// <summary>Register stable spoken text before enqueueing audio. Safe from the single active response producer.</summary>
        public bool TryRegisterSegment(MNNResponseId response, long segmentId, string stableSpokenText)
        {
            if (!_playback.Accepts(response) || _playback.IsComplete(response))
                return false;
            return _ledger.TryRegister(response, segmentId, stableSpokenText);
        }

        public bool Accepts(MNNResponseId response) => _playback.Accepts(response);

        /// <summary>Marks generation EOS after its sole producer has stopped publishing; queued audio still plays.</summary>
        public bool CompleteResponse(MNNResponseId response) => _playback.CompleteResponse(response);

        public bool CanBeginNextResponse(MNNResponseId completedResponse) =>
            _playback.IsComplete(completedResponse) && !_playback.HasPendingAudio &&
            !_playback.HasPendingReceipts && _ledger.IsPresented(completedResponse);

        /// <summary>Producer copies bounded PCM. False means stale response, fault or queue backpressure.</summary>
        public bool TryEnqueue(float[] samples, int offset, int count, MNNSpeechChunkInfo chunk)
        {
            if (!_playback.Accepts(chunk.Response))
                return false;
            if (!_ledger.IsRegistered(chunk.Response, chunk.SegmentId))
                throw new InvalidOperationException("Register stable spoken text before enqueueing segment audio.");
            return _playback.TryEnqueue(samples, offset, count, chunk);
        }

        /// <summary>Device callback only. Supplies every output sample, including silence.</summary>
        public void Render(float[] destination, int offset, int count) => _playback.Render(destination, offset, count);

        /// <summary>Drain in render order; receipts become submitted history, not yet presented history.</summary>
        public int DrainReceipts(int maximumReceipts)
        {
            if (maximumReceipts < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumReceipts));
            int drained = 0;
            while (drained < maximumReceipts && _playback.TryReadReceipt(out var receipt))
            {
                _ledger.RecordSubmission(receipt);
                ++drained;
            }
            return drained;
        }

        /// <summary>The watermark is the exclusive device presentation frame.</summary>
        public void AcknowledgePresentation(long throughFrame, bool estimated) =>
            _ledger.AcknowledgePresentation(throughFrame, estimated);

        public MNNConversationSegment GetSegment(int index) => _ledger.GetSegment(index);

        internal bool HasPresentationStarted(MNNResponseId response) => _ledger.HasPresentationStarted(response);

        /// <summary>
        /// Explicit context rollover after the caller joins old producers and snapshots presented text.
        /// Late device receipts from these epochs are ignored conservatively. The presentation clock is preserved.
        /// </summary>
        public void RetireHistoryThrough(MNNResponseId response)
        {
            var current = _playback.CurrentResponse;
            if (!response.IsValid || response.Session != current.Session || response.Epoch >= current.Epoch || Accepts(response))
                throw new InvalidOperationException("Interrupt and join an old response before retiring its history.");
            _ledger.RetireThrough(response);
        }

        /// <summary>Immediately gates playback; hardware already submitted remains subject to its device tail.</summary>
        public MNNResponseId Interrupt()
        {
            MNNResponseId previous = _playback.CurrentResponse;
            _playback.Interrupt();
            if (previous.IsValid)
            {
                _ledger.Interrupt(previous);
                ResponseInterrupted?.Invoke(previous);
            }
            return previous;
        }
    }
}
