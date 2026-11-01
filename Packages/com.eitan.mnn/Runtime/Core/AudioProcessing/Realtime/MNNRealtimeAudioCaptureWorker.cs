using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>
    /// Drains one bounded capture source on a dedicated long-lived thread and forwards borrowed PCM blocks
    /// to a single processor. Expired blocks are discarded by the consumer and the next block carries a gap.
    /// Stop the capture producer before stopping the worker; queued blocks drain under the age policy before exit.
    /// The processor must not retain the buffer or call Unity APIs.
    /// </summary>
    public sealed class MNNRealtimeAudioCaptureWorker : IDisposable
    {
        private static readonly Action<MNNRealtimeVadUpdate> s_ignoreVadUpdate = IgnoreVadUpdate;
        private readonly object _lifecycle = new object();
        private readonly TaskCompletionSource<bool> _completion =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly IMNNAudioCaptureSource _source;
        private readonly Action<float[], int, MNNAudioFrameInfo> _process;
        private readonly IMNNRealtimeVoiceInputProcessor _voiceProcessor;
        private readonly MNNRealtimeModelInputBuffer _modelInput;
        private readonly Action<MNNRealtimeVadUpdate> _vadUpdateHandler;
        private readonly Action<float[], int, MNNRealtimeVadUpdate> _vadFrameHandler;
        private readonly float[] _scratch;
        private readonly float[] _voiceScratch;
        private readonly Thread _thread;
        private readonly Func<long> _monotonicClock;
        private readonly long _maximumCaptureAgeTicks;
        private int _state;
        private int _threadStarted;
        private int _stopRequested;
        private long _processedBlocks;
        private long _emptyPolls;
        private Exception _fault;
        private long _lastStreamId;
        private long _lastFormatEpoch;
        private long _nextCaptureSequence;
        private long _nextCaptureFrame;
        private long _lastCaptureTimestamp;
        private long _expiredCaptureBlocks;
        private long _expiredCaptureFrames;
        private long _processedInputFrames;
        private bool _pendingCaptureDiscontinuity;
        private bool _pendingModelDiscontinuity;
        private TaskCompletionSource<bool> _flush;

        /// <summary>After the producer has paused, drains queued capture and flushes the
        /// resampler and partial VAD frame on their owning thread, then resets for the next stream.
        /// Completion means all final model frames have been published, not consumed.</summary>
        public Task FlushAsync()
        {
            lock (_lifecycle)
            {
                if (!IsRunning || Volatile.Read(ref _stopRequested) != 0)
                    throw new InvalidOperationException("Flush requires a running capture worker and a paused producer.");
                if (_flush == null)
                    _flush = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _flush.Task;
            }
        }

        public bool IsRunning => Volatile.Read(ref _state) == 1;
        public bool IsStopped => Volatile.Read(ref _state) == 2;
        public bool IsFaulted => Volatile.Read(ref _state) == 3;
        public Exception Fault => Volatile.Read(ref _fault);
        /// <summary>
        /// One shared task, completed after processing and the shutdown flush, or faulted with the original error.
        /// Observe this task to report failures without polling. Registered continuations are dispatched asynchronously.
        /// Completion does not join the OS thread: stop the capture producer, then call Stop before releasing
        /// resources used by the worker. Stopping before Start completes successfully without flushing.
        /// </summary>
        public Task Completion => _completion.Task;
        public long ProcessedBlocks => Interlocked.Read(ref _processedBlocks);
        public long EmptyPolls => Interlocked.Read(ref _emptyPolls);
        public long ProcessedInputFrames => Interlocked.Read(ref _processedInputFrames);
        public long DroppedCaptureBlocks => _source.DroppedCaptureBlocks;
        public long DroppedModelFrames => _modelInput?.DroppedFrames ?? 0;
        public long ExpiredModelFrames => _modelInput?.ExpiredFrames ?? 0;
        public long ExpiredCaptureBlocks => Interlocked.Read(ref _expiredCaptureBlocks);
        public long ExpiredCaptureFrames => Interlocked.Read(ref _expiredCaptureFrames);
        public TimeSpan MaximumCaptureAge { get; }

        /// <summary>
        /// Discards blocks whose first-sample timestamp is older than the budget (300 ms by default).
        /// Use Timeout.InfiniteTimeSpan only when intentionally draining historical/offline audio.
        /// </summary>
        public MNNRealtimeAudioCaptureWorker(IMNNAudioCaptureSource source,
            Action<float[], int, MNNAudioFrameInfo> process, string threadName = "MNN Audio Processing",
            TimeSpan? maximumCaptureAge = null)
            : this(source, process, null, null, null, threadName, maximumCaptureAge, null)
        {
        }

        /// <summary>Creates the standard capture → resample/VAD → fixed model-frame pipeline.</summary>
        public MNNRealtimeAudioCaptureWorker(IMNNAudioCaptureSource source,
            IMNNRealtimeVoiceInputProcessor voiceProcessor, MNNRealtimeModelInputBuffer modelInput,
            Action<MNNRealtimeVadUpdate> onVadUpdate = null, string threadName = "MNN Audio Processing",
            TimeSpan? maximumCaptureAge = null)
            : this(source, null, voiceProcessor, modelInput, onVadUpdate, threadName, maximumCaptureAge, null)
        {
        }

        internal MNNRealtimeAudioCaptureWorker(IMNNAudioCaptureSource source,
            Action<float[], int, MNNAudioFrameInfo> process, IMNNRealtimeVoiceInputProcessor voiceProcessor,
            MNNRealtimeModelInputBuffer modelInput, Action<MNNRealtimeVadUpdate> onVadUpdate, string threadName,
            TimeSpan? maximumCaptureAge, Func<long> monotonicClock)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            if ((process == null) == (voiceProcessor == null))
                throw new ArgumentException("Provide exactly one block processor or standard voice processor.");
            if (voiceProcessor != null && modelInput == null)
                throw new ArgumentNullException(nameof(modelInput));
            if (_source.MaximumCaptureSamples < 1)
                throw new ArgumentException("The capture source must expose a positive block capacity.", nameof(source));
            if (string.IsNullOrWhiteSpace(threadName))
                throw new ArgumentException("A worker thread name is required.", nameof(threadName));
            MaximumCaptureAge = maximumCaptureAge ?? TimeSpan.FromMilliseconds(300);
            if (MaximumCaptureAge != Timeout.InfiniteTimeSpan &&
                (MaximumCaptureAge <= TimeSpan.Zero ||
                 MaximumCaptureAge.TotalSeconds * Stopwatch.Frequency >= long.MaxValue))
                throw new ArgumentOutOfRangeException(nameof(maximumCaptureAge));
            _maximumCaptureAgeTicks = MaximumCaptureAge == Timeout.InfiniteTimeSpan
                ? -1 : Math.Max(1, (long)(MaximumCaptureAge.TotalSeconds * Stopwatch.Frequency));
            _monotonicClock = monotonicClock ?? new Func<long>(Stopwatch.GetTimestamp);
            _source.CaptureFormat.RequireValid();
            _process = process;
            _voiceProcessor = voiceProcessor;
            _modelInput = modelInput;
            _vadUpdateHandler = onVadUpdate ?? (voiceProcessor == null ? null : s_ignoreVadUpdate);
            _vadFrameHandler = voiceProcessor == null ? null : PublishModelFrame;
            if (voiceProcessor != null)
            {
                MNNPcmFormat captureFormat = _source.CaptureFormat;
                captureFormat.RequireValid();
                if (captureFormat.SampleRate != voiceProcessor.InputFormat.SampleRate ||
                    voiceProcessor.InputFormat.Channels != 1)
                    throw new ArgumentException("Capture rate must match the voice processor's mono input format.", nameof(source));
                if (_source.MaximumCaptureSamples % captureFormat.Channels != 0)
                    throw new ArgumentException("Capture capacity must contain complete interleaved sample frames.", nameof(source));
                if (voiceProcessor.MaximumInputBlockFrames < 1)
                    throw new ArgumentException("Voice processor input block capacity must be positive.", nameof(voiceProcessor));
                _voiceScratch = new float[_source.MaximumCaptureSamples / captureFormat.Channels];
            }
            _scratch = new float[_source.MaximumCaptureSamples];
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = threadName
            };
        }

        /// <summary>Starts the worker once. Thread creation occurs on the control thread.</summary>
        public void Start()
        {
            lock (_lifecycle)
            {
                if (_state != 0)
                    throw new InvalidOperationException("A capture worker can only be started once.");
                Volatile.Write(ref _state, 1);
                try
                {
                    _thread.Start();
                    Volatile.Write(ref _threadStarted, 1);
                }
                catch (Exception error)
                {
                    Volatile.Write(ref _fault, error);
                    Volatile.Write(ref _state, 3);
                    _completion.TrySetException(error);
                    throw;
                }
            }
        }

        /// <summary>Requests a stop and waits only for the caller-supplied bound. Call on a control thread.</summary>
        public bool Stop(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (timeout != Timeout.InfiniteTimeSpan && timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            lock (_lifecycle)
            {
                int state = Volatile.Read(ref _state);
                if (state == 0)
                {
                    Volatile.Write(ref _state, 2);
                    _completion.TrySetResult(true);
                    return true;
                }
                Volatile.Write(ref _stopRequested, 1);
            }
            if (Thread.CurrentThread == _thread)
                return false;
            if (Volatile.Read(ref _threadStarted) == 0)
                return true;
            if (!_thread.Join(timeout))
                return false;
            return true;
        }

        public void Dispose()
        {
            if (!Stop(Timeout.InfiniteTimeSpan))
                throw new InvalidOperationException("The audio processing worker did not stop.");
        }

        private void Run()
        {
            int idleSpins = 0;
            try
            {
                while (Volatile.Read(ref _stopRequested) == 0 || _source.HasPendingCaptureFrames)
                {
                    if (_source.TryReadCapture(_scratch, 0, out var info, out int count))
                    {
                        if (count < 1 || count > _scratch.Length)
                            throw new InvalidOperationException("Capture source returned a block outside its declared capacity.");
                        idleSpins = 0;
                        info.Format.RequireValid();
                        if (!_source.CaptureFormat.Equals(info.Format) || count % info.Format.Channels != 0 ||
                            info.StreamId < 1 || info.FormatEpoch < 1)
                            throw new InvalidOperationException("Capture metadata does not match the bound source format.");
                        long nextSequence = checked(info.Sequence + 1);
                        long nextFrame = checked(info.StartFrame + count / info.Format.Channels);
                        if (_maximumCaptureAgeTicks >= 0)
                        {
                            long now = _monotonicClock();
                            if (now < info.MonotonicTicks)
                                throw new InvalidOperationException("Capture timestamps must use the current process monotonic clock.");
                            if (now - info.MonotonicTicks > _maximumCaptureAgeTicks)
                            {
                                _pendingCaptureDiscontinuity = true;
                                Interlocked.Increment(ref _expiredCaptureBlocks);
                                Interlocked.Add(ref _expiredCaptureFrames, count / info.Format.Channels);
                                continue;
                            }
                        }
                        if (_pendingCaptureDiscontinuity || (_lastStreamId != 0 &&
                            (_lastStreamId != info.StreamId || _lastFormatEpoch != info.FormatEpoch ||
                             _nextCaptureSequence != info.Sequence || _nextCaptureFrame != info.StartFrame ||
                             info.MonotonicTicks < _lastCaptureTimestamp)))
                            info = info.WithGap();
                        if (_voiceProcessor != null)
                            ProcessVoiceBlock(count, info);
                        else
                            _process(_scratch, count, info);
                        _lastStreamId = info.StreamId;
                        _lastFormatEpoch = info.FormatEpoch;
                        _nextCaptureSequence = nextSequence;
                        _nextCaptureFrame = nextFrame;
                        _lastCaptureTimestamp = info.MonotonicTicks;
                        _pendingCaptureDiscontinuity = false;
                        Interlocked.Increment(ref _processedBlocks);
                        continue;
                    }

                    TaskCompletionSource<bool> flush;
                    lock (_lifecycle) { flush = _flush; }
                    if (flush != null)
                    {
                        if (_voiceProcessor != null)
                        {
                            if (_pendingCaptureDiscontinuity) _voiceProcessor.Reset();
                            _voiceProcessor.Complete(_vadUpdateHandler, _vadFrameHandler);
                            _voiceProcessor.Reset();
                            _pendingCaptureDiscontinuity = false;
                            _pendingModelDiscontinuity = true;
                        }
                        lock (_lifecycle) { if (ReferenceEquals(_flush, flush)) _flush = null; }
                        flush.TrySetResult(true);
                    }
                    Interlocked.Increment(ref _emptyPolls);
                    if (++idleSpins < 8)
                        Thread.SpinWait(8);
                    else
                    {
                        idleSpins = 0;
                        Thread.Sleep(1);
                    }
                }
                if (_voiceProcessor != null)
                {
                    // A trailing discarded block breaks the partial resampler/VAD frame too.
                    // Never flush that old partial frame as uninterrupted input at shutdown.
                    if (_pendingCaptureDiscontinuity)
                        _voiceProcessor.Reset();
                    _voiceProcessor.Complete(_vadUpdateHandler, _vadFrameHandler);
                }
                Volatile.Write(ref _state, 2);
                _completion.TrySetResult(true);
                lock (_lifecycle) { _flush?.TrySetException(new OperationCanceledException("Capture stopped before flush completed.")); _flush = null; }
            }
            catch (Exception error)
            {
                Volatile.Write(ref _fault, error);
                Volatile.Write(ref _state, 3);
                _completion.TrySetException(error);
                lock (_lifecycle) { _flush?.TrySetException(error); _flush = null; }
            }
        }

        private void ProcessVoiceBlock(int sampleCount, MNNAudioFrameInfo info)
        {
            if (info.Format.SampleRate != _voiceProcessor.InputFormat.SampleRate)
                throw new InvalidOperationException("Capture format changed without rebinding the input processor.");

            bool discontinuity = info.Discontinuity;
            _pendingModelDiscontinuity |= discontinuity;
            int channels = info.Format.Channels;
            int frames = sampleCount / channels;
            float[] mono = _scratch;
            if (channels > 1)
            {
                mono = _voiceScratch;
                for (int frame = 0; frame < frames; ++frame)
                {
                    float mixed = 0;
                    int sampleOffset = frame * channels;
                    for (int channel = 0; channel < channels; ++channel)
                        mixed += _scratch[sampleOffset + channel];
                    mono[frame] = mixed / channels;
                }
            }
            int offset = 0;
            while (offset < frames)
            {
                int count = Math.Min(_voiceProcessor.MaximumInputBlockFrames, frames - offset);
                _voiceProcessor.Process(mono, offset, count, _vadUpdateHandler,
                    discontinuity, _vadFrameHandler);
                discontinuity = false;
                offset += count;
                Interlocked.Add(ref _processedInputFrames, count);
            }
        }

        private static void IgnoreVadUpdate(MNNRealtimeVadUpdate update) { }

        private void PublishModelFrame(float[] samples, int offset, MNNRealtimeVadUpdate update)
        {
            if (_modelInput.TryWrite(samples, offset, update, _pendingModelDiscontinuity))
                _pendingModelDiscontinuity = false;
        }
    }
}
