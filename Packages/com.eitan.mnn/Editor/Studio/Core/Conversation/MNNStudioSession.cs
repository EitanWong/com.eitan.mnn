using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using UnityEditor;

namespace MNN.Unity.Editor
{
    /// <summary>Owns one model. Only the worker runs inference; results are consumed on the
    /// editor thread. Closing a window defers disposal until the active call has finished.</summary>
    internal sealed class MNNStudioSession : IDisposable
    {
        private IMNNStudioBackend _backend;
        private readonly Func<IMNNStudioBackend> _factory;
        private Task _release;
        private bool _released;
        private readonly TaskCompletionSource<bool> _disposal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Disposal => _disposal.Task;
        private Task<MNNStudioResult> _pending;
        private bool _closed, _discard;
        private CancellationTokenSource _cancellation;
        private readonly object _progressGate = new object ();
        private MNNGenerationUpdate _latest;
        private string _latestMemory;
        private int _latestCovered;
        private bool _compressing;
        internal bool CompressingContext { get; private set; }

        internal string MemorySummary { get; private set; }

        internal int MemoryCovered { get; private set; }

        internal MNNGenerationUpdate Progress { get; private set; }

        internal event Action Updated;
        internal bool Busy => _pending != null || _release != null;
        internal MNNStudioResult Result { get; private set; }

        internal string Error { get; private set; }

        internal event Action Completed;
        internal event Action Released;
        internal MNNStudioSession(IMNNStudioBackend backend) => _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        internal MNNStudioSession(Func<IMNNStudioBackend> factory) => _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        internal void Send(MNNStudioRequest request)
        {
            if (_closed)
                throw new ObjectDisposedException(nameof(MNNStudioSession));
            if (Busy)
                throw new InvalidOperationException("A model operation is already running.");
            if (request == null || (request.Conversation == null && string.IsNullOrWhiteSpace(request.Prompt)))
                throw new ArgumentException("Enter a message or query.");
            request = request.Snapshot();
            Result = null;
            Error = null;
            Progress = null;
            _discard = false;
            lock (_progressGate)
            {
                _latest = null;
                _latestMemory = null;
                _compressing = false;
            }

            CompressingContext = false;
            MemorySummary = request.ContextSummary;
            MemoryCovered = request.SummarizedMessages;
            _cancellation = new CancellationTokenSource();
            request.Cancellation = _cancellation.Token;
            request.ContextProgress = value =>
            {
                lock (_progressGate)
                    _compressing = value;
            };
            request.MemoryProgress = (summary, covered) =>
            {
                lock (_progressGate)
                {
                    _latestMemory = summary;
                    _latestCovered = covered;
                }
            };
            request.Progress = update =>
            {
                lock (_progressGate)
                    _latest = update;
            };
            _pending = Task.Run(() =>
            {
                request.Cancellation.ThrowIfCancellationRequested();
                if (_backend == null) _backend = _factory() ?? throw new InvalidOperationException("Model factory returned no backend.");
                request.Cancellation.ThrowIfCancellationRequested();
                // Exclude worker queueing and model loading; include preprocessing,
                // inference, GPU readback and PCM construction, before editor playback.
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var result = _backend.Run(request);
                result.Seconds = timer.Elapsed.TotalSeconds;
                result.AudioSeconds = result.Waveform != null && result.SampleRate > 0 ? (double)result.Waveform.Length / result.SampleRate : 0;
                return result;
            });
            MNNStudioJobs.Track(this);
        }

        // No native interruption API is implemented. Discarding suppresses delivery only.
        internal void DiscardReply()
        {
            if (Busy)
                _discard = true;
        }

        internal void StopGeneration()
        {
            if (Busy)
                _cancellation?.Cancel();
        }

        internal void FlushMemoryCheckpoint()
        {
            lock (_progressGate)
            {
                if (_latestMemory == null)
                    return;
                MemorySummary = _latestMemory;
                MemoryCovered = _latestCovered;
                _latestMemory = null;
            }
        }

        internal void Poll()
        {
            if (_release != null && _release.IsCompleted)
            {
                if (_release.IsFaulted)
                {
                    Error = _release.Exception.GetBaseException().Message;
                    _disposal.TrySetException(_release.Exception.InnerExceptions);
                }
                _release = null;
                NotifyReleased();
            }
            PublishProgress();
            if (_pending == null || !_pending.IsCompleted)
                return;
            var task = _pending;
            // The final snapshot can arrive between the first drain and task completion.
            PublishProgress();
            _pending = null;
            _cancellation?.Dispose();
            _cancellation = null;
            if (task.IsCanceled || (task.IsFaulted && task.Exception.GetBaseException() is OperationCanceledException))
                Result = !_discard && !_closed ? new MNNStudioResult{Text = Progress?.Text ?? "", Tokens = Progress?.GeneratedTokens ?? 0, Cancelled = true} : null;
            else if (task.IsFaulted)
                Error = task.Exception.GetBaseException().Message;
            else if (!_discard && !_closed)
                Result = task.Result;
            if (_closed)
                Release();
            else
                Completed?.Invoke();
        }

        private void PublishProgress()
        {
            MNNGenerationUpdate latest;
            lock (_progressGate)
            {
                latest = _latest;
                _latest = null;
                CompressingContext = _compressing;
            }

            FlushMemoryCheckpoint();
            if (latest != null && !_closed && !_discard)
            {
                Progress = latest;
                Updated?.Invoke();
            }
        }

        public void Dispose()
        {
            if (_closed)
                return;
            _closed = true;
            Completed = null;
            Updated = null;
            StopGeneration();
            if (!Busy)
                Release();
        }

        private void Release()
        {
            if (_released) return;
            _released = true;
            if (_factory != null)
            {
                _release = Task.Run(() => _backend?.Dispose());
                MNNStudioJobs.Track(this);
            }
            else
            {
                _backend?.Dispose();
                NotifyReleased();
            }
        }

        private void NotifyReleased()
        {
            _disposal.TrySetResult(true);
            Released?.Invoke();
            Released = null;
        }
    }
}
