using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using UnityEditor;

namespace MNN.Unity.Editor
{
    internal sealed class MNNStudioRequest
    {
        internal string Prompt, Image, Audio, Document;
        internal MNNChatMessage[] Conversation;
        internal int TokenLimit = 256;
        internal bool Speech;
        internal string Voice = "M1", NegativePrompt = "";
        internal int Steps = 20, Seed = 42;
        internal float Speed = 1, Guidance = 7.5f;
        internal byte[] ReferenceRgb;
        internal string ContextSummary;
        internal MNNStudioMessage[] History;
        internal int SummarizedMessages, ContextTokens = MNNStudioContext.DefaultTokenBudget;
        internal Action<string, int> MemoryProgress;
        internal Action<bool> ContextProgress;
        internal Action<MNNGenerationUpdate> Progress;
        internal CancellationToken Cancellation;
    }

    internal sealed class MNNStudioResult
    {
        internal string Text;
        internal int Tokens, SampleRate;
        internal bool Limited;
        internal bool RepetitionStopped;
        internal bool Cancelled;
        internal double Seconds;
        internal float[] Waveform = Array.Empty<float>(), Vector;
        internal float? Score;
        internal MNNGeneratedImage GeneratedImage;
        internal string ContextSummary;
    }

    internal interface IMNNStudioBackend : IDisposable
    {
        MNNStudioResult Run(MNNStudioRequest request);
    }

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
            request = new MNNStudioRequest{Prompt = request.Prompt, Image = request.Image, Audio = request.Audio, Document = request.Document, Voice = request.Voice, NegativePrompt = request.NegativePrompt, Steps = request.Steps, Seed = request.Seed, Speed = request.Speed, Guidance = request.Guidance, ReferenceRgb = request.ReferenceRgb == null ? null : (byte[])request.ReferenceRgb.Clone(), TokenLimit = request.TokenLimit, Speech = request.Speech, ContextSummary = request.ContextSummary, History = request.History == null ? null : (MNNStudioMessage[])request.History.Clone(), SummarizedMessages = request.SummarizedMessages, ContextTokens = request.ContextTokens, Conversation = request.Conversation == null ? null : (MNNChatMessage[])request.Conversation.Clone()};
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
            var timer = System.Diagnostics.Stopwatch.StartNew();
            _pending = Task.Run(() =>
            {
                var result = _backend.Run(request);
                result.Seconds = timer.Elapsed.TotalSeconds;
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

    [InitializeOnLoad]
    internal static class MNNStudioJobs
    {
        private static readonly List<MNNStudioSession> Active = new List<MNNStudioSession>();
        static MNNStudioJobs()
        {
            EditorApplication.update += Update;
            EditorApplication.quitting += BeforeQuit;
        }

        internal static void Track(MNNStudioSession session)
        {
            if (Active.Contains(session))
                return;
            if (Active.Count == 0)
                EditorApplication.LockReloadAssemblies();
            Active.Add(session);
        }

        internal static void Update()
        {
            if (Active.Count == 0)
                return;
            try
            {
                for (int i = Active.Count - 1; i >= 0; --i)
                {
                    var session = Active[i];
                    session.Poll();
                    if (!session.Busy)
                        Active.RemoveAt(i);
                }
            }
            finally
            {
                if (Active.Count == 0)
                    EditorApplication.UnlockReloadAssemblies();
            }
        }

        private static void BeforeQuit()
        {
            foreach (var session in Active)
                session.Dispose();
        // Model handles remain owned by active sessions until process exit. Do not
        // block Unity's main thread waiting for a long native generation here.
        }
    }

    internal sealed class MNNStudioNativeBackend : IMNNStudioBackend
    {
        private MNNLlm _llm;
        private MNNEmbedding _embedding;
        private MNNReranker _reranker;
        private MNNSupertonic _tts;
        private MNNPiper _piper;
        private MNNBertVits2 _bertVits;
        private MNNStableDiffusion _diffusion;
        private MNNSana _sana;
        private readonly string _cache;
        private MNNStudioNativeBackend(string cache) => _cache = cache;
        internal static MNNStudioNativeBackend Load(MNNStudioModel model, int threads, bool thinking)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (!MNNStudioTasks.Get((int)model.Task).CanRun(model))
                throw new NotSupportedException(MNNStudioTasks.Get((int)model.Task).Name + " inference is not connected to Studio. Downloaded model files do not enable this pipeline.");
            if (!MNNPlatformSupport.IsSupported)
                throw new PlatformNotSupportedException(MNNPlatformSupport.UnsupportedReason);
            var backend = new MNNStudioNativeBackend(Path.GetFullPath(Path.Combine("Library", "MNN", "Studio", Guid.NewGuid().ToString("N"))));
            try
            {
                switch (model.Task)
                {
                    case MNNStudioTask.Chat:
                        backend._llm = MNNLlm.LoadForChat(model.Directory, threads, thinking, backend._cache);
                        break;
                    case MNNStudioTask.Embedding:
                        backend._embedding = MNNEmbedding.Load(model.Directory, threads, backend._cache);
                        break;
                    case MNNStudioTask.Reranker:
                        backend._reranker = MNNReranker.Load(model.Directory, threads, backend._cache);
                        break;
                    case MNNStudioTask.SpeechSynthesis:
                        if (model.IsPiper)
                            backend._piper = MNNPiper.Load(model.Directory, threads, MNNStudioPiperSettings.ResolveExecutable(model.Directory), MNNStudioPiperSettings.ResolveDataDirectory(model));
                        else if (model.IsBertVits2)
                            backend._bertVits = MNNBertVits2.Load(model.Directory, threads);
                        else
                            backend._tts = MNNSupertonic.Load(model.Directory, threads, model.SupertonicPrecision);
                        break;
                    case MNNStudioTask.ImageGeneration:
                        if (model.IsSana)
                            backend._sana = MNNSana.Load(model.Directory, threads);
                        else
                            backend._diffusion = MNNStableDiffusion.Load(model.Directory, threads);
                        break;
                }

                return backend;
            }
            catch
            {
                backend.Dispose();
                throw;
            }
        }

        public MNNStudioResult Run(MNNStudioRequest request)
        {
            if (_piper != null || _bertVits != null)
            {
                var audio = _piper != null ? _piper.Synthesize(request.Prompt, request.Voice, request.Cancellation) : _bertVits.Synthesize(request.Prompt, request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_tts != null)
            {
                var audio = _tts.Synthesize(request.Prompt, request.Voice, request.Steps, request.Speed, request.Seed, request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_diffusion != null)
            {
                var image = _diffusion.Generate(request.Prompt, request.NegativePrompt, request.Steps, request.Guidance, request.Seed, request.Cancellation, percent => request.Progress?.Invoke(new MNNGenerationUpdate("Generating image: " + percent + "%", 0)));
                return new MNNStudioResult{Text = image.Prompt, GeneratedImage = image};
            }

            if (_sana != null)
            {
                var image = _sana.Edit(request.Prompt, request.ReferenceRgb, request.Steps, request.Seed, request.Cancellation, percent => request.Progress?.Invoke(new MNNGenerationUpdate("Editing image: " + percent + "%", 0)), request.Guidance);
                return new MNNStudioResult{Text = image.Prompt, GeneratedImage = image};
            }

            if (_llm != null)
            {
                if (request.Conversation != null || request.History != null)
                {
                    string summary = request.ContextSummary;
                    var conversationInput = request.Conversation;
                    int mediaReserve = (request.Image != null ? 2048 : 0) + (request.Audio != null ? 2048 : 0);
                    if (request.History != null)
                    {
                        var prepared = MNNStudioContext.Prepare(request.History, request.Prompt, summary, request.SummarizedMessages, request.ContextTokens, request.TokenLimit, mediaReserve, _llm.CountTokens, prompt => _llm.GenerateConversation(prompt, MNNStudioContext.SummaryOutputTokens, cancellationToken: request.Cancellation).Text, request.MemoryProgress, request.Cancellation, () => request.ContextProgress?.Invoke(true));
                        request.ContextProgress?.Invoke(false);
                        conversationInput = prepared.Conversation;
                        summary = prepared.Summary;
                    }
                    else if (MNNStudioContext.Estimate(conversationInput, _llm.CountTokens) > request.ContextTokens - request.TokenLimit - mediaReserve - 512)
                        throw new ArgumentException("The conversation exceeds the configured context budget.");
                    using (var stop = CancellationTokenSource.CreateLinkedTokenSource(request.Cancellation))
                    {
                        bool repeated = false;
                        string cleanText = null;
                        var conversation = _llm.GenerateConversationStreaming(conversationInput, update =>
                        {
                            if (MNNStudioRepetition.TryTrim(update.Text, out var trimmed))
                            {
                                repeated = true;
                                cleanText = trimmed;
                                stop.Cancel();
                            }

                            request.Progress?.Invoke(repeated ? new MNNGenerationUpdate(cleanText, update.GeneratedTokens) : update);
                        }, request.TokenLimit, request.Image, request.Audio, request.Speech, cancellationToken: stop.Token);
                        return new MNNStudioResult{Text = repeated ? cleanText : conversation.Text, Tokens = conversation.GeneratedTokens, Limited = conversation.ReachedTokenLimit, RepetitionStopped = repeated, Waveform = conversation.Waveform, SampleRate = conversation.SampleRate, Cancelled = request.Cancellation.IsCancellationRequested || (conversation.Cancelled && !repeated), ContextSummary = summary};
                    }
                }

                if (request.Image != null || request.Audio != null || request.Speech)
                {
                    var result = _llm.GenerateMultimodal(request.Prompt, request.Image, request.Audio, request.TokenLimit, request.Speech);
                    return new MNNStudioResult{Text = result.Text, Tokens = result.GeneratedTokens, Limited = result.ReachedTokenLimit, Waveform = result.Waveform, SampleRate = result.SampleRate};
                }

                var text = _llm.Generate(request.Prompt, request.TokenLimit);
                return new MNNStudioResult{Text = text.Text, Tokens = text.GeneratedTokens, Limited = text.ReachedTokenLimit};
            }

            if (_embedding != null)
            {
                var query = _embedding.Encode(request.Prompt);
                var document = _embedding.Encode(request.Document);
                return new MNNStudioResult{Vector = query, Score = MNNStudioPrompt.Cosine(query, document)};
            }

            if (_reranker != null)
                return new MNNStudioResult{Score = _reranker.Score(request.Prompt, request.Document)};
            throw new ObjectDisposedException(nameof(MNNStudioNativeBackend));
        }

        public void Dispose()
        {
            _llm?.Dispose();
            _llm = null;
            _embedding?.Dispose();
            _embedding = null;
            _reranker?.Dispose();
            _reranker = null;
            _tts?.Dispose();
            _tts = null;
            _piper?.Dispose();
            _piper = null;
            _bertVits?.Dispose();
            _bertVits = null;
            _diffusion?.Dispose();
            _diffusion = null;
            _sana?.Dispose();
            _sana = null;
            try
            {
                if (Directory.Exists(_cache))
                    Directory.Delete(_cache, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
