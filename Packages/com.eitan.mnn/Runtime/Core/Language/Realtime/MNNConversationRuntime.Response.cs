using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public partial class MNNConversationRuntime
    {
        protected async Task RespondAsync(string text, float[] audio, CancellationToken requestToken, MNNConversationTelemetry.Trace trace, int realtimeRevision, bool voiceValidated, bool audioPrepared, int exitRevision)
        {
            Task speechTask = null;
            Task speechTextTask = null;
            MNNConversationSpeechTextBuffer speechText = null;
            var speechTaskJoined = false;
            var responseAbandoned = false;
            Exception responseFailure = null;
            Exception speechFailure = null;
            Exception cleanupFailure = null;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(requestToken))
            using (var production = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(_config.maximumResponseSeconds));
                var token = deadline.Token;
                try
                {
                    await Volatile.Read(ref _recovery).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (_context.Session.State != MNNRealtimeSessionState.Running) return;
                    trace.ProcessingStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                    CommitPresentedHistory();
                    if (realtimeRevision >= 0)
                    {
                        if (!_context.Input.TryAcceptRealtime(realtimeRevision, () => _response = _context.Session.BeginResponse())) return;
                    }
                    else _response = _context.Session.BeginResponse();
                    _eventReply?.Begin(_response);
                    if (_eventReply == null && !await AcceptUserInputAsync(text, audio, voiceValidated, production.Token, trace, audioPrepared, exitRevision).ConfigureAwait(false)) return;
                    var history = BuildResponseHistory();
                    if (_eventReply != null)
                    {
                        Memory.AddEvent(_eventReply.Input);
                        _eventReply.Injected();
                    }
                    if (!_config.speechOutput)
                    {
                        await ProduceTextReplyAsync(history, production.Token, trace).ConfigureAwait(false);
                        return;
                    }
                    if (_route == MNNRealtimeRoute.EndToEndSpeech)
                    {
                        _context.TryPublishStage(_response, MNNConversationStage.Generating);
                        trace.ModelStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                        await ProduceModelSpeechAsync(history, production.Token).ConfigureAwait(false);
                        return;
                    }
                    var channel = new MNNRealtimeSpeechTextChannel(_output, _response, 2, _config.speechSegmentCharacters);
                    var speechTextCleaner = new MNNSpeechTextCleaner();
                    speechText = new MNNConversationSpeechTextBuffer(_config.maximumResponseCharacters);
                    speechTextTask = Task.Run(async () =>
                    {
                        try
                        {
                            await speechText.DrainAsync(async (delta, ct) =>
                            {
                                await channel.AppendAsync(delta, ct).ConfigureAwait(false);
                            }, production.Token).ConfigureAwait(false);
                            while (channel.IsCurrent && !channel.Complete())
                                await Task.Delay(1, production.Token).ConfigureAwait(false);
                        }
                        catch (Exception textError)
                        {
                            CancelProductionAfterSpeechFailure(production, textError);
                            throw;
                        }
                    });
                    var stream = new MNNRealtimeSpeechStream(_output, _response, TimeSpan.FromSeconds(2), SynthesisMeter);
                    var tts = (IMNNConversationTtsProvider)_providers.Tts;
                    MNNSpeechLanguageException languageFailure = null;
                    speechTask = Task.Run(async () =>
                    {
                        try
                        {
                            await stream.ProduceAsync(channel, _output.Format, async (segment, publish, ct) =>
                            {
                                if (trace.SpeechStartedAt == 0)
                                {
                                    _context.TryPublishStage(_response, MNNConversationStage.Synthesizing);
                                    Interlocked.CompareExchange(ref trace.SpeechStartedAt, System.Diagnostics.Stopwatch.GetTimestamp(), 0);
                                }
                                var converter = new MNNStreamingSpeechConverter(tts.SampleRate, _output.Format, async (samples, offset, count, final) =>
                                {
                                    if (trace.FirstAudioAt == 0) Interlocked.CompareExchange(ref trace.FirstAudioAt, System.Diagnostics.Stopwatch.GetTimestamp(), 0);
                                    await publish(samples, offset, count, final).ConfigureAwait(false);
                                }, trimTrailingSilence: true);
                                double speechComputeBefore = stream.SynthesisMeter.TotalComputeMilliseconds;
                                await tts.SynthesizeAsync(segment, converter.AppendAsync, ct).ConfigureAwait(false);
                                if (_providers.SpeechTimings != null)
                                {
                                    var stages = _providers.SpeechTimings();
                                    trace.TtsTokenMs += stages.TokenGenerationMs;
                                    trace.TtsFlowMs += stages.FlowMs;
                                    trace.TtsVocoderMs += stages.VocoderMs;
                                }
                                await converter.CompleteAsync().ConfigureAwait(false);
                                trace.SpeechWorkMs += stream.SynthesisMeter.TotalComputeMilliseconds - speechComputeBefore;
                                Interlocked.Exchange(ref trace.SpeechFinishedAt, System.Diagnostics.Stopwatch.GetTimestamp());
                            }, production.Token).ConfigureAwait(false);
                        }
                        catch (MNNSpeechLanguageException error)
                        {
                            languageFailure = error;
                            // Keep draining so bounded speech backpressure cannot stop the
                            // visible text response when no local voice supports its script.
                            await DrainUnspokenTextAsync(channel, production.Token).ConfigureAwait(false);
                        }
                        catch (Exception speechError)
                        {
                            CancelProductionAfterSpeechFailure(production, speechError);
                            throw;
                        }
                    });
                    var display = new StringBuilder();
                    var publishedDisplayLength = 0;
                    var displayClock = System.Diagnostics.Stopwatch.StartNew();
                    long modelStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                    trace.ModelStartedAt = modelStarted;
                    _context.TryPublishStage(_response, MNNConversationStage.Generating);
                    await GenerateTextAsync(history, delta =>
                    {
                        production.Token.ThrowIfCancellationRequested();
                        if (delta == null || display.Length + delta.Length > _config.maximumResponseCharacters)
                            throw new InvalidOperationException("Model response exceeded the configured text budget.");
                        if (trace.FirstTokenAt == 0) Interlocked.CompareExchange(ref trace.FirstTokenAt, System.Diagnostics.Stopwatch.GetTimestamp(), 0);
                        display.Append(delta);
                        if ((publishedDisplayLength == 0 || display.Length - publishedDisplayLength >= 32 || displayClock.ElapsedMilliseconds >= 50) &&
                            _context.TryPublishAssistantSnapshot(_response, display))
                        {
                            publishedDisplayLength = display.Length;
                            displayClock.Restart();
                        }
                        speechText.Append(speechTextCleaner.Append(delta));
                        return Task.CompletedTask;
                    }, production.Token).ConfigureAwait(false);
                    trace.ModelFinishedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                    if (display.Length > publishedDisplayLength)
                        await _context.PublishAssistantSnapshotAsync(_response, display, production.Token).ConfigureAwait(false);
                    speechText.Append(speechTextCleaner.Complete());
                    speechText.Complete();
                    await speechTextTask.ConfigureAwait(false);
                    await speechTask.ConfigureAwait(false);
                    if (languageFailure != null)
                    {
                        UnityEngine.Debug.LogWarning("[MNN] " + languageFailure.Message + " Reply remains available as text.");
                        // The producer has finished. Interrupt releases the incomplete speech
                        // ledger without faulting the session or pretending text was spoken.
                        trace.Failed = true;
                        Volatile.Write(ref _recovery, _context.Session.InterruptAsync());
                    }
                }
                catch (Exception) when (requestToken.IsCancellationRequested ||
                    _response.IsValid && !_output.Accepts(_response))
                {
                    responseAbandoned = true;
                }
                catch (Exception) when (deadline.IsCancellationRequested)
                {
                    responseFailure = new TimeoutException("The response exceeded its configured duration budget.");
                }
                catch (OperationCanceledException) when (speechTask != null && production.IsCancellationRequested)
                {
                    try { await speechTask.ConfigureAwait(false); }
                    catch (Exception error) { speechFailure = error; }
                    finally { speechTaskJoined = true; }
                }
                catch (MNNRealtimeRouteUnavailableException error) when (error.Route == _route)
                {
                    responseFailure = error;
                }
                catch (Exception error) { responseFailure = error; }
                finally
                {
                    try { production.Cancel(); }
                    catch (Exception error) { cleanupFailure = error; }
                    if (speechTextTask != null)
                    {
                        try { await speechTextTask.ConfigureAwait(false); }
                        catch (OperationCanceledException) when (production.IsCancellationRequested) { }
                        catch (Exception error) { cleanupFailure = CombineFailures("Speech text forwarding failed.", cleanupFailure, error); }
                        finally { speechText.Dispose(); }
                    }
                    if (speechTask != null && !speechTaskJoined)
                    {
                        try { await speechTask.ConfigureAwait(false); }
                        catch (OperationCanceledException) when (production.IsCancellationRequested) { }
                        catch (Exception error) { speechFailure = CombineFailures("Speech production failed more than once.", speechFailure, error); }
                        finally { speechTaskJoined = true; }
                    }
                    var failure = CombineFailures("Response and speech production both failed.", responseFailure, speechFailure);
                    failure = CombineFailures("Response cleanup failed.", failure, cleanupFailure);
                    if (failure != null && (cleanupFailure != null ||
                        !responseAbandoned && !requestToken.IsCancellationRequested))
                    {
                        if (cleanupFailure == null && TryGetRouteFailure(failure, _route, out var routeFailure))
                            RequestRouteFallback(routeFailure);
                        else ReportFault(failure);
                    }
                    if (failure != null || responseAbandoned || requestToken.IsCancellationRequested) trace.Failed = true;
                    if (trace.SpeechFinishedAt == 0 && trace.FirstAudioAt > 0) trace.SpeechFinishedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                    if (trace.Failed) TurnTelemetry.Complete(trace);
                    _eventReply?.Complete(!trace.Failed && (trace.FirstTokenAt > 0 || trace.FirstAudioAt > 0));
                }
            }
        }
        protected async Task<bool> AcceptUserInputAsync(string text, float[] audio, bool voiceValidated,
            CancellationToken token, MNNConversationTelemetry.Trace trace, bool audioPrepared, int exitRevision)
        {
            if (audio != null && !audioPrepared)
            {
                audio = await _context.PrepareVoiceAsync(audio, token).ConfigureAwait(false);
                if (audio == null) { _context.Session.CompleteResponse(_response); return false; }
            }
            if (audio != null && _config.filterVoiceInput && !voiceValidated)
            {
                _context.TryPublishStage(_response, MNNConversationStage.Recognizing);
                var candidate = await ValidateVoiceAsync(audio, "", token).ConfigureAwait(false);
                if (!candidate.Accepted) { _context.Session.CompleteResponse(_response); return false; }
                text = candidate.Text;
                voiceValidated = true;
            }
            if (audio != null && _route == MNNRealtimeRoute.CascadedSpeech)
            {
                long asrStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                _context.TryPublishStage(_response, MNNConversationStage.Recognizing);
                if (!voiceValidated && string.IsNullOrWhiteSpace(text)) text = await TranscribePreparedAsync(audio, token).ConfigureAwait(false);
                trace.AsrMs = (System.Diagnostics.Stopwatch.GetTimestamp() - asrStarted) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                audio = null;
            }
            if (audio != null && _config.endRealtimeOnExitIntent && exitRevision >= 0 && string.IsNullOrWhiteSpace(text))
                text = await TranscribePreparedAsync(audio, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (audio == null && string.IsNullOrWhiteSpace(text)) { _context.Session.CompleteResponse(_response); return false; }
            _context.TryPublishUserText(text ?? "[Voice message]");
            long intentStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            bool exit;
            try { exit = await ShouldExitRealtimeAsync(text, exitRevision, token).ConfigureAwait(false); }
            finally { trace.VoiceIntentMs = (System.Diagnostics.Stopwatch.GetTimestamp() - intentStarted) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; }
            if (exit || exitRevision >= 0 && !_context.Input.TryAcceptRealtime(exitRevision, () => { }))
            {
                _context.Session.CompleteResponse(_response);
                return false;
            }
            var userMessage = audio == null ? new MNNOpenAIMessage(MNNChatRole.User, text) : new MNNOpenAIMessage(audio, 16000);
            Memory.AddUser(userMessage, text);
            return true;
        }

        protected List<MNNOpenAIMessage> BuildResponseHistory()
        {
            string instructions = _config.instructions;
            if (_config.speechOutput && _route != MNNRealtimeRoute.EndToEndSpeech)
            {
                var languages = (_providers.Tts as IMNNConversationSpeechLanguages)?.SupportedSpeechLanguages;
                instructions = MNNConversationSpeechInstructions.Append(instructions,
                    languages != null && languages.Count > 0 ? languages : _config.SpeechLanguages);
            }
            var history = new List<MNNOpenAIMessage>(Memory.Messages.Count + 2) { new MNNOpenAIMessage(MNNChatRole.System, instructions) };
            history.AddRange(Memory.Messages);
            return history;
        }

        protected async Task ProduceTextReplyAsync(IReadOnlyList<MNNOpenAIMessage> history, CancellationToken token, MNNConversationTelemetry.Trace trace)
        {
            var display = new StringBuilder();
            int publishedLength = 0;
            var displayClock = System.Diagnostics.Stopwatch.StartNew();
            trace.ModelStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            _context.TryPublishStage(_response, MNNConversationStage.Generating);
            await GenerateTextAsync(history, delta =>
            {
                token.ThrowIfCancellationRequested();
                if (delta == null || display.Length + delta.Length > _config.maximumResponseCharacters)
                    throw new InvalidOperationException("Model response exceeded the configured text budget.");
                if (trace.FirstTokenAt == 0) trace.FirstTokenAt = System.Diagnostics.Stopwatch.GetTimestamp();
                display.Append(delta);
                if ((publishedLength == 0 || display.Length - publishedLength >= 32 || displayClock.ElapsedMilliseconds >= 50) &&
                    _context.TryPublishAssistantSnapshot(_response, display))
                {
                    publishedLength = display.Length;
                    displayClock.Restart();
                }
                return Task.CompletedTask;
            }, token).ConfigureAwait(false);
            trace.ModelFinishedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            token.ThrowIfCancellationRequested();
            if (display.Length > publishedLength)
                await _context.PublishAssistantSnapshotAsync(_response, display, token).ConfigureAwait(false);
            // Text has no speech ledger/presentation receipts. Commit completed visible replies
            // directly so later turns retain context, and release the response for the next send.
            if (_context.Session.CompleteResponse(_response))
            {
                Memory.AddAssistant(display.ToString());
            }
        }

        protected async Task ProduceModelSpeechAsync(IReadOnlyList<MNNOpenAIMessage> history, CancellationToken token)
        {
            var display = new StringBuilder();
            await _modelGate.WaitAsync(token).ConfigureAwait(false);
            MNNMultimodalResult result;
            try
            {
                if (_eventReply != null) history = _eventReply.Input.CreateHistory(history);
                result = await ((IMNNConversationSpeechModelProvider)_providers.Model).GenerateSpeechAsync(history, delta =>
                {
                    if (delta == null || display.Length + delta.Length > _config.maximumResponseCharacters)
                        throw new InvalidOperationException("Model response exceeded its text budget.");
                    display.Append(delta);
                    var trace = _activeTrace;
                    if (trace != null && trace.FirstTokenAt == 0) Interlocked.CompareExchange(ref trace.FirstTokenAt, System.Diagnostics.Stopwatch.GetTimestamp(), 0);
                    _context.TryPublishAssistantSnapshot(_response, display);
                    return Task.CompletedTask;
                }, token).ConfigureAwait(false);
            }
            finally { _modelGate.Release(); }
            Memory.RememberAssistant(display.ToString());
            var activeTrace = _activeTrace;
            if (activeTrace != null)
            {
                if (activeTrace.ModelStartedAt == 0) activeTrace.ModelStartedAt = activeTrace.ProcessingStartedAt;
                activeTrace.ModelFinishedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            token.ThrowIfCancellationRequested();
            if (result.Waveform == null || result.Waveform.Length == 0)
                throw new MNNRealtimeRouteUnavailableException(_route, "Omni returned no speech waveform.");
            await _context.PublishAssistantSnapshotAsync(_response, display, token).ConfigureAwait(false);
            // The official model API returns one completed waveform, without word timestamps.
            var stream = new MNNRealtimeSpeechStream(_output, _response, TimeSpan.FromSeconds(2), SynthesisMeter);
            if (!stream.BeginSegment(0, string.IsNullOrWhiteSpace(result.Text) ? "[Voice reply]" : result.Text))
                throw new InvalidOperationException("Speech ledger capacity exhausted.");
            var converter = new MNNStreamingSpeechConverter(result.SampleRate, _output.Format, async (samples, offset, count, final) =>
            {
                var trace = _activeTrace;
                if (trace != null && trace.FirstAudioAt == 0) Interlocked.CompareExchange(ref trace.FirstAudioAt, System.Diagnostics.Stopwatch.GetTimestamp(), 0);
                while (count > 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (!stream.IsCurrent) throw new OperationCanceledException("Stale model speech.");
                    int take = Math.Min(count, _output.MaximumSamplesPerChunk);
                    if (!stream.TryPublishPcm(samples, offset, take, final && take == count))
                    { await Task.Delay(1, token).ConfigureAwait(false); continue; }
                    count -= take;
                    offset += take;
                }
            });
            await converter.AppendAsync(result.Waveform, result.Waveform.Length).ConfigureAwait(false);
            await converter.CompleteAsync().ConfigureAwait(false);
            stream.CompleteResponse();
            if (activeTrace != null) activeTrace.SpeechFinishedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        public static async Task DrainUnspokenTextAsync(MNNRealtimeSpeechTextChannel channel, CancellationToken token)
        {
            while (channel.IsCurrent)
            {
                token.ThrowIfCancellationRequested();
                while (channel.TryRead(out _)) { token.ThrowIfCancellationRequested(); }
                if (channel.IsCompleted) return;
                await channel.WaitForDataAsync(token).ConfigureAwait(false);
            }
        }

    }
}
