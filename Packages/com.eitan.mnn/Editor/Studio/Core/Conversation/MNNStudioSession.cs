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
        private readonly IMNNStudioBackend _backend;
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
        internal bool Busy => _pending != null;
        internal MNNStudioResult Result { get; private set; }

        internal string Error { get; private set; }

        internal event Action Completed;
        internal event Action Released;
        internal MNNStudioSession(IMNNStudioBackend backend) => _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        internal void Send(MNNStudioRequest request)
        {
            if (_closed)
                throw new ObjectDisposedException(nameof(MNNStudioSession));
            if (Busy)
                throw new InvalidOperationException("A model operation is already running.");
            if (request == null || (request.Conversation == null && string.IsNullOrWhiteSpace(request.Prompt)))
                throw new ArgumentException("Enter a message or query.");
            request = new MNNStudioRequest{Keywords = request.Keywords, KeywordThreshold = request.KeywordThreshold, KeywordScore = request.KeywordScore, RecognitionLanguage = request.RecognitionLanguage, VisionImage = request.VisionImage, LayoutScoreThreshold = request.LayoutScoreThreshold, Prompt = request.Prompt, Image = request.Image, Audio = request.Audio, PlaybackReference = request.PlaybackReference, ReferenceText = request.ReferenceText, SpeechLanguage = request.SpeechLanguage, Document = request.Document, Voice = request.Voice, EmotionId = request.EmotionId, NegativePrompt = request.NegativePrompt, Steps = request.Steps, Seed = request.Seed, Speed = request.Speed, Guidance = request.Guidance, ReferenceRgb = request.ReferenceRgb == null ? null : (byte[])request.ReferenceRgb.Clone(), TokenLimit = request.TokenLimit, Speech = request.Speech, ContextSummary = request.ContextSummary, History = request.History == null ? null : (MNNStudioMessage[])request.History.Clone(), SummarizedMessages = request.SummarizedMessages, ContextTokens = request.ContextTokens, Conversation = request.Conversation == null ? null : (MNNChatMessage[])request.Conversation.Clone()};
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
            _backend.Dispose();
            Released?.Invoke();
            Released = null;
        }
    }
}
