using System;
using System.Diagnostics;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>
    /// Fixed-capacity SPSC transport from the audio-processing worker to a single model owner.
    /// Input is always 512 mono samples at 16 kHz; overflow drops a frame and marks the next accepted frame discontinuous.
    /// The consumer discards frames queued longer than the age budget and marks the next delivered frame with a gap.
    /// </summary>
    public sealed class MNNRealtimeModelInputBuffer : IDisposable
    {
        public const int SampleRate = 16000;
        public const int FrameSamples = 512;
        public const int MaximumCapacity = 64;

        private readonly float[][] _frames;
        private readonly MNNRealtimeModelInputInfo[] _metadata;
        private readonly AutoResetEvent _dataAvailable;
        private readonly Func<long> _monotonicClock;
        private readonly long _maximumQueueAgeTicks;
        private int _read;
        private int _write;
        private bool _gap;
        private long _nextSequence;
        private long _droppedFrames;
        private long _expiredFrames;
        private bool _consumerGap;
        private int _disposed;

        public int Capacity => _frames.Length - 1;
        public long DroppedFrames => Interlocked.Read(ref _droppedFrames);
        public long ExpiredFrames => Interlocked.Read(ref _expiredFrames);
        public TimeSpan MaximumQueueAge { get; }
        public bool HasPendingFrames => Volatile.Read(ref _read) != Volatile.Read(ref _write);

        /// <summary>
        /// Limits queue residence to 300 ms by default, measured from enqueue to the start of a read.
        /// This does not measure capture age or model execution time. Use Timeout.InfiniteTimeSpan for offline replay.
        /// </summary>
        public MNNRealtimeModelInputBuffer(int capacity, TimeSpan? maximumQueueAge = null)
            : this(capacity, maximumQueueAge, null)
        {
        }

        internal MNNRealtimeModelInputBuffer(int capacity, TimeSpan? maximumQueueAge, Func<long> monotonicClock)
        {
            if (capacity < 1 || capacity > MaximumCapacity)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            MaximumQueueAge = maximumQueueAge ?? TimeSpan.FromMilliseconds(300);
            if (MaximumQueueAge != Timeout.InfiniteTimeSpan &&
                (MaximumQueueAge <= TimeSpan.Zero ||
                 MaximumQueueAge.TotalSeconds * Stopwatch.Frequency >= long.MaxValue))
                throw new ArgumentOutOfRangeException(nameof(maximumQueueAge));
            _maximumQueueAgeTicks = MaximumQueueAge == Timeout.InfiniteTimeSpan
                ? -1 : Math.Max(1, (long)(MaximumQueueAge.TotalSeconds * Stopwatch.Frequency));
            _monotonicClock = monotonicClock ?? new Func<long>(Stopwatch.GetTimestamp);
            _frames = new float[capacity + 1][];
            _metadata = new MNNRealtimeModelInputInfo[capacity + 1];
            for (int i = 0; i < _frames.Length; ++i)
                _frames[i] = new float[FrameSamples];
            _dataAvailable = new AutoResetEvent(false);
        }

        /// <summary>Processing worker only. The supplied frame is copied; the processor retains its scratch array.</summary>
        public bool TryWrite(float[] frame, int offset, MNNRealtimeVadUpdate vad, bool discontinuity = false)
        {
            ThrowIfDisposed();
            ValidateFrame(frame, offset);
            ValidateVad(vad);
            int next = Next(_write);
            if (next == Volatile.Read(ref _read))
            {
                _gap = true;
                Interlocked.Increment(ref _droppedFrames);
                return false;
            }

            bool hasGap = _gap || discontinuity;
            long sequence = _nextSequence;
            long nextSequence = checked(sequence + 1);
            // Mode ownership still needs an enqueue boundary when expiration is disabled.
            long timestamp = ReadClock();
            Array.Copy(frame, offset, _frames[_write], 0, FrameSamples);
            _metadata[_write] = new MNNRealtimeModelInputInfo(vad, sequence, hasGap, timestamp);
            _nextSequence = nextSequence;
            Volatile.Write(ref _write, next);
            _gap = false;
            _dataAvailable.Set();
            return true;
        }

        /// <summary>
        /// Model owner only. Discards expired frames from the current write snapshot before copying one fresh frame.
        /// A too-small destination leaves all frames queued. False may mean that all snapshot frames expired;
        /// call WaitForData or inspect cancellation before retrying. Gaps require model reset/recovery before feeding PCM.
        /// </summary>
        public bool TryRead(float[] destination, int offset, out MNNRealtimeModelInputInfo info)
        {
            ThrowIfDisposed();
            ValidateRange(destination, offset, FrameSamples);
            int end = Volatile.Read(ref _write);
            while (_read != end)
            {
                int read = _read;
                info = _metadata[read];
                if (_maximumQueueAgeTicks >= 0)
                {
                    long now = ReadClock();
                    if (now < info.EnqueuedMonotonicTicks)
                        throw new InvalidOperationException("The model input queue clock moved backwards.");
                    if (now - info.EnqueuedMonotonicTicks > _maximumQueueAgeTicks)
                    {
                        _consumerGap = true;
                        _metadata[read] = default;
                        Interlocked.Increment(ref _expiredFrames);
                        Volatile.Write(ref _read, Next(read));
                        continue;
                    }
                }
                Array.Copy(_frames[read], 0, destination, offset, FrameSamples);
                if (_consumerGap)
                    info = info.WithGap();
                _metadata[read] = default;
                Volatile.Write(ref _read, Next(read));
                _consumerGap = false;
                return true;
            }
            info = default;
            return false;
        }

        /// <summary>
        /// Waits for the single model consumer to receive available input. Writes coalesce into one wake signal;
        /// always drain with TryRead until it returns false. Availability does not imply freshness;
        /// only TryRead evaluates age. Stop producers and consumers before disposing.
        /// </summary>
        public bool WaitForData(TimeSpan timeout)
        {
            ThrowIfDisposed();
            if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (timeout != Timeout.InfiniteTimeSpan && timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (HasPendingFrames)
                return true;

            _dataAvailable.WaitOne(timeout);
            ThrowIfDisposed();
            return HasPendingFrames;
        }

        /// <summary>Wakes the model owner so it can observe session cancellation or shutdown.</summary>
        public void WakeWaiter()
        {
            ThrowIfDisposed();
            _dataAvailable.Set();
        }

        /// <summary>Call after the producer and consumer have both stopped.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _dataAvailable.Dispose();
        }

        private void ValidateFrame(float[] frame, int offset)
        {
            ValidateRange(frame, offset, FrameSamples);
            for (int i = offset; i < offset + FrameSamples; ++i)
            {
                float sample = frame[i];
                if (float.IsNaN(sample) || float.IsInfinity(sample) || sample < -1f || sample > 1f)
                    throw new ArgumentException("Model input must be finite normalized PCM.", nameof(frame));
            }
        }

        private static void ValidateVad(MNNRealtimeVadUpdate vad)
        {
            if (vad.StartSample < 0 || vad.StartSample % FrameSamples != 0 ||
                float.IsNaN(vad.SpeechProbability) || float.IsInfinity(vad.SpeechProbability) ||
                vad.SpeechProbability < 0 || vad.SpeechProbability > 1)
                throw new ArgumentOutOfRangeException(nameof(vad));
        }

        private static void ValidateRange(float[] samples, int offset, int count)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            if (offset < 0 || count < 0 || offset > samples.Length - (long)count)
                throw new ArgumentOutOfRangeException(nameof(offset));
        }

        private int Next(int value) => value + 1 == _frames.Length ? 0 : value + 1;

        private long ReadClock()
        {
            long timestamp = _monotonicClock();
            if (timestamp < 0)
                throw new InvalidOperationException("The model input queue requires nonnegative Stopwatch timestamps.");
            return timestamp;
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(MNNRealtimeModelInputBuffer));
        }
    }
}
