using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Transport-independent bounded mono capture, safety gate epochs and asynchronous drain.
    /// Producers copy borrowed spans; one worker consumes the queue.</summary>
    public sealed class MNNRealtimeCapturePort : IMNNAudioCaptureSource
    {
        private readonly MNNAudioCaptureBuffer _queue;
        private readonly Func<bool> _safetyGate;
        private FormatSnapshot _format;
        private long _nextStream;
        private int _gateVersion;
        private int _drainVersion, _activePasses;
        private Producer _producer;
        private Exception _fault;

        public MNNPcmFormat CaptureFormat => Volatile.Read(ref _format)?.Format ?? default;
        public int MaximumCaptureSamples => _queue.MaximumSamples;
        public long DroppedCaptureBlocks => _queue.DroppedBlocks;
        public bool HasPendingCaptureFrames => _queue.HasPendingFrames;
        public Exception Fault => Volatile.Read(ref _fault);
        public async Task PauseCaptureAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int version = GateVersion;
            if ((version & 1) != 0)
            {
                Volatile.Write(ref _drainVersion, version);
                Accepting = false;
            }
            long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
            while (Volatile.Read(ref _activePasses) != 0)
            {
                token.ThrowIfCancellationRequested();
                if (Stopwatch.GetTimestamp() > deadline) throw new TimeoutException("Microphone callback did not finish in time.");
                await Task.Delay(1, token).ConfigureAwait(false);
            }
        }
        // One atomic version encodes both the gate and its epoch. Reopening before the audio
        // callback observes the closed gate must still invalidate queued PCM and resampler state.
        internal int GateVersion => Volatile.Read(ref _gateVersion);
        public bool Accepting
        {
            get => (GateVersion & 1) != 0;
            set
            {
                int previous;
                do
                {
                    previous = GateVersion;
                    if (((previous & 1) != 0) == value) return;
                }
                while (Interlocked.CompareExchange(ref _gateVersion, unchecked(previous + 1), previous) != previous);
            }
        }

        public MNNRealtimeCapturePort(int capacity, Func<bool> safetyGate = null, int maximumBlockSamples = 1920)
        {
            _queue = new MNNAudioCaptureBuffer(capacity, maximumBlockSamples);
            _safetyGate = safetyGate;
        }
        private void CheckSafetyGate()
        {
            // Pure managed state only. A stalled Unity Update cannot leave capture open after
            // AEC health expires while a cue keeps playing on the independent render thread.
            if (_safetyGate != null && !_safetyGate()) Accepting = false;
        }
        public bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count)
        {
            // Safety is evaluated on the producer after APM processes this block.
            // A later health expiry must not discard PCM that was already captured safely.
            while (_queue.TryRead(destination, offset, out info, out count))
            {
                int version = GateVersion;
                if ((version & 1) != 0 && info.FormatEpoch == version ||
                    (version & 1) == 0 && info.FormatEpoch == Volatile.Read(ref _drainVersion)) return true;
            }
            info = default;
            count = 0;
            return false;
        }

        internal void Fail(Exception error) => Interlocked.CompareExchange(ref _fault, error, null);

        public sealed class Producer : IDisposable
        {
            private readonly MNNRealtimeCapturePort _port;
            private readonly Action _beforeCapture;
            private readonly float[] _scratch;
            private MNNPcmFormat _format;
            private long _stream;
            private long _position;
            private long _sequence;
            private long _lastPublishedTicks;
            private int _blockFrames;
            private bool _gap = true;
            private int _gateVersion;
            private int _closed;

            internal Producer(MNNRealtimeCapturePort port, Action beforeCapture = null)
            { _port = port; _beforeCapture = beforeCapture; _scratch = new float[port.MaximumCaptureSamples]; }
            public void Initialize(MNNPcmFormat format)
            {
                format.RequireValid();
                _format = format;
                if (_format.Channels != 1)
                    throw new NotSupportedException("Downmix capture to mono before publishing.");
                _stream = Interlocked.Increment(ref _port._nextStream);
                _position = _sequence = _lastPublishedTicks = 0;
                _gap = true;
                _blockFrames = Math.Min(_scratch.Length, Math.Max(1, format.SampleRate / 100));
                Volatile.Write(ref _port._format, new FormatSnapshot(_format));
            }

            public void Submit(ReadOnlySpan<float> buffer, MNNPcmFormat format, double estimatedCaptureDelayMilliseconds = 0)
            {
                Interlocked.Increment(ref _port._activePasses);
                try
                {
                    if (Volatile.Read(ref _closed) != 0) return;
                    // This runs after APM on the transport worker, independently of Unity Update.
                    _beforeCapture?.Invoke();
                    _port.CheckSafetyGate();
                    int gateVersion = _port.GateVersion;
                    if ((gateVersion & 1) == 0 || buffer.IsEmpty)
                    {
                        _gap = true;
                        return;
                    }
                    if (_gateVersion != gateVersion)
                    {
                        _gap = true;
                        _gateVersion = gateVersion;
                    }
                    if (format.Channels != _format.Channels || format.SampleRate != _format.SampleRate)
                    {
                        _port.Fail(new InvalidOperationException("Capture format changed without pipeline reinitialization."));
                        return;
                    }
                    // The supplied device/transport age is an estimate, not a hardware sample timestamp.
                    long now = Stopwatch.GetTimestamp();
                    long age = (long)(Math.Max(0, estimatedCaptureDelayMilliseconds) * (Stopwatch.Frequency / 1000.0));
                    long first = Math.Max(0, now - age - (long)(buffer.Length * (double)Stopwatch.Frequency / _format.SampleRate));
                    for (int offset = 0; offset < buffer.Length;)
                    {
                        int count = Math.Min(_blockFrames, buffer.Length - offset);
                        buffer.Slice(offset, count).CopyTo(_scratch);
                        // The host delay estimate can fluctuate while sample sequence remains
                        // continuous. Do not manufacture a discontinuity from an older estimate.
                        long timestamp = Math.Max(_lastPublishedTicks, first + (long)(offset * (double)Stopwatch.Frequency / _format.SampleRate));
                        _lastPublishedTicks = timestamp;
                        var info = new MNNAudioFrameInfo(_stream, gateVersion, _sequence++, _position,
                            timestamp, _format, _gap);
                        _port._queue.TryWrite(_scratch, 0, count, info);
                        _gap = false;
                        _position += count;
                        offset += count;
                    }
                }
                catch (Exception error) { _port.Fail(error); }
                finally { Interlocked.Decrement(ref _port._activePasses); }
            }
            public void Dispose()
            {
                Volatile.Write(ref _closed, 1);
                if (Volatile.Read(ref _port._activePasses) != 0)
                    throw new InvalidOperationException("Detach capture callbacks and retry disposing the producer after they finish.");
                Interlocked.CompareExchange(ref _port._producer, null, this);
            }
        }

        /// <summary>Stop the old producer before creating its replacement. A port has one active producer.</summary>
        public Producer CreateProducer(Action beforeCapture = null)
        {
            var producer = new Producer(this, beforeCapture);
            if (Interlocked.CompareExchange(ref _producer, producer, null) != null)
                throw new InvalidOperationException("Dispose the previous capture producer before replacing it.");
            return producer;
        }

        private sealed class FormatSnapshot
        {
            internal readonly MNNPcmFormat Format;
            internal FormatSnapshot(MNNPcmFormat format) { Format = format; }
        }
    }
}
