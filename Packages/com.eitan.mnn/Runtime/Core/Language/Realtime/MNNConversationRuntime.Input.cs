using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public partial class MNNConversationRuntime
    {
        protected void OnVad(MNNRealtimeVadUpdate update)
        {
            int inputRevision = _context.Input.Revision;
            // Stop playback on confirmed near-end speech with healthy echo control.
            // ASR/semantic validation decides whether to submit the completed turn; it must
            // never wait behind the response that the user is trying to interrupt.
            if (!_context.Input.Realtime || !_context.CanInterruptSpeech || !_context.AllowUnverifiedBargeIn)
            {
                _bargeIn.Reset();
                return;
            }
            var response = _output.CurrentResponse;
            bool responding = response.IsValid && _output.Accepts(response) && !_output.CanBeginNextResponse(response);
            bool isolated = _context.CanInterruptSpeech;
            if (_bargeIn.Process(update, isolated, update.SpeechProbability, responding, 32) == MNNBargeInAction.Confirmed)
            {
                string echo = SnapshotPlaybackText();
                _context.Input.TryAcceptRealtime(inputRevision, () =>
                {
                    Volatile.Write(ref _bargeInEcho, echo);
                    Volatile.Write(ref _recovery, _context.InterruptAsync());
                }); // Playback fades and joins independently; input PCM is never reset here.
            }
        }
        protected void InputLoop()
        {
            var frame = new float[512];
            var inactivity = new MNNConversationInactivity();
            bool interruptedTurn = false;
            int recoverySilence = 0;
            MNNConversationWakeHandoff wake = null;
            try
            {
                while (!_lifetime.IsCancellationRequested)
                {
                    int revision = _context.Input.Revision;
                    if (_inputRevision != revision)
                    {
                        ResetUtterance();
                        _acceptedAudio = null;
                        Volatile.Write(ref _bargeInEcho, null);
                        _turnCancellation?.Cancel();
                        interruptedTurn = false;
                        recoverySilence = 0;
                        _inputRevision = revision;
                        wake = _context.TakeWakeCommand();
                        if (_context.Input.Realtime && wake != null)
                        {
                            interruptedTurn = wake.LostPrefix || wake.CaptureRevision != _context.CaptureRevision;
                            if (interruptedTurn)
                                _context.PublishInputWarning("Wake command audio was interrupted. Please repeat this sentence.");
                            for (int offset = 0; !interruptedTurn && !_utterance.IsFull && offset < wake.Frames.Length; offset += 512)
                            {
                                Array.Copy(wake.Frames, offset, frame, 0, 512);
                                _utterance.Append(frame, true, false);
                                _recognitionPreview?.Append(frame, true);
                            }
                            if (_utterance.IsFull)
                            {
                                interruptedTurn = true;
                                RejectFullUtterance();
                            }
                        }
                    }
                    if (inactivity.Tick(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency,
                        revision, _context.Input.Realtime, IsResponding || _context.SpeakerAudible || _utterance.HasSpeechStarted,
                        _config.realtimeInactivitySeconds))
                        _context.RequestRealtimeExit(revision);
                    if (_recognitionPreview != null && _recognitionPreview.TryRead(out string partial))
                        _context.PublishRecognitionPreview(_inputRevision, partial);
                    ObservePresentedResponse();
                    if (_input.TryRead(frame, 0, out var info))
                    {
                        if (wake != null)
                        {
                            if (wake.CaptureRevision != _context.CaptureRevision)
                            {
                                wake = null;
                                interruptedTurn = true; recoverySilence = 0;
                                ResetUtterance(); _acceptedAudio = null; _turnCancellation?.Cancel();
                                _context.PublishInputWarning("Wake command capture changed. Please repeat this sentence.");
                            }
                            else if (info.StartSample < wake.EndSample) continue;
                            else wake = null;
                        }
                        if (_context.Input.Consume(frame, info.IsSpeechActive, info.EnqueuedMonotonicTicks, info.Discontinuity)) continue;
                        if (SuppressSpeakerEcho()) continue;
                        if (info.Discontinuity) _bargeIn.Reset();
                        OnVad(info.Vad);
                        if (info.Discontinuity && _utterance.HasSpeechStarted)
                        {
                            Interlocked.Increment(ref _inputGaps);
                            // Never recognize a suffix of a turn whose beginning was lost.
                            // Wait for a new silence boundary before accepting another utterance.
                            interruptedTurn = true;
                            recoverySilence = 0;
                            ResetUtterance();
                            _acceptedAudio = null;
                            _turnCancellation?.Cancel();
                            _context.PublishInputWarning("Microphone samples were lost. Please repeat this sentence.");
                        }
                        if (interruptedTurn)
                        {
                            recoverySilence = info.IsSpeechActive ? 0 : recoverySilence + 512;
                            if (recoverySilence >= _config.silenceMilliseconds * 16)
                            {
                                interruptedTurn = false;
                                ResetUtterance();
                            }
                            continue;
                        }
                        _utterance.Append(frame, info.IsSpeechActive, info.Discontinuity);
                        if (_utterance.IsFull)
                        {
                            // A capacity boundary is not an end of speech. Never submit a
                            // truncated prefix or its continuing suffix as a complete command.
                            interruptedTurn = true;
                            recoverySilence = 0;
                            RejectFullUtterance();
                            continue;
                        }
                        _recognitionPreview?.Append(frame, info.IsSpeechActive);
                        if (_acceptedAudio != null && _acceptedRevision != _utterance.Revision) _acceptedAudio = null;
                        TryLaunchAccepted();
                        if (_turnTask != null && _turnRevision != _utterance.Revision) _turnCancellation.Cancel();
                        if (_turnTask != null && _turnTask.IsCompleted) FinishTurnCandidate();
                        if (_turnTask == null && _acceptedAudio == null)
                        {
                            if (ShouldEvaluateTurn()) BeginTurnCandidate();
                        }
                        continue;
                    }
                    if (_turnTask != null && _turnTask.IsCompleted) FinishTurnCandidate();
                    TryLaunchAccepted();
                    Interlocked.Exchange(ref _recordingDrain, null)?.TrySetResult(true);
                    _input.WaitForData(TimeSpan.FromMilliseconds(10));
                }
            }
            catch (Exception error)
            {
                lock (_sync) _inputFailure = error;
                if (!_lifetime.IsCancellationRequested) ReportFault(error);
            }
            finally
            {
                lock (_sync)
                {
                    _inputFinished = true;
                    var drain = Interlocked.Exchange(ref _recordingDrain, null);
                    if (_inputFailure != null) drain?.TrySetException(_inputFailure);
                    else drain?.TrySetCanceled();
                }
            }
        }
        protected void ResetUtterance()
        {
            _utterance.Reset();
            _recognitionPreview?.Reset();
            _context.PublishRecognitionPreview(_inputRevision, "");
        }
        protected void RejectFullUtterance()
        {
            ResetUtterance();
            _acceptedAudio = null;
            _turnCancellation?.Cancel();
            _context.PublishInputWarning("Speech reached the recording limit. Please use a shorter sentence.");
        }
        protected void BeginTurnCandidate()
        {
            _turnAudio = _utterance.Snapshot();
            _turnRevision = _utterance.Revision;
            _turnSilenceSamples = _utterance.SilenceSamples;
            _turnCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            float[] audio = _turnAudio;
            bool full = _utterance.IsFull;
            string echo = SnapshotPlaybackText();
            CancellationToken token = _turnCancellation.Token;
            _turnTask = Task.Run(() => EvaluateVoiceCandidateAsync(audio, full, echo, token), token);
        }
        protected void FinishTurnCandidate()
        {
            bool current = _turnRevision == _utterance.Revision && _utterance.IsCandidate;
            VoiceCandidate candidate = default;
            try { candidate = _turnTask.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) when (_turnCancellation.IsCancellationRequested) { }
            finally { _turnCancellation.Dispose(); _turnCancellation = null; _turnTask = null; }
            if (current && candidate.Accepted && _config.filterVoiceInput &&
                MNNConversationVoiceInputFilter.IsLikelyEcho(candidate.Text, SnapshotPlaybackText()))
                candidate = RejectVoice("Matches speaker output during validation");
            if (current && candidate.Accepted)
            {
                _acceptedAudio = candidate.Audio ?? _turnAudio;
                _acceptedText = candidate.Text;
                _acceptedRevision = _turnRevision;
                TryLaunchAccepted();
            }
            else if (current && candidate.Complete)
            {
                // A rejected recording is consumed once; do not repeatedly recognize the same noise.
                ResetUtterance();
                _acceptedAudio = null;
                _acceptedText = null;
                Volatile.Write(ref _bargeInEcho, null);
                _nextTurnSilenceSamples = 0;
            }
            else if (current)
            {
                _rejectedRevision = _turnRevision;
                _nextTurnSilenceSamples = _turnSilenceSamples + _turnRetrySilenceSamples;
            }
            _turnAudio = null;
        }
        protected bool ShouldEvaluateTurn() => _utterance.IsCandidate &&
            (_utterance.IsFull || _utterance.Revision != _rejectedRevision ||
             _utterance.SilenceSamples >= _nextTurnSilenceSamples);
        protected void TryLaunchAccepted()
        {
            if (!_context.Input.Realtime || _acceptedAudio == null || !Volatile.Read(ref _recovery).IsCompleted) return;
            // A completed producer may still have queued speech. Gate and join it before accepting
            // the next turn; never make a new producer wait for its own cancellation task.
            var previous = _output.CurrentResponse;
            if (previous.IsValid && _output.Accepts(previous))
            {
                if (!_context.CanInterruptSpeech && !_output.CanBeginNextResponse(previous))
                {
                    _acceptedAudio = null;
                    _acceptedText = null;
                    ResetUtterance();
                    return;
                }
                _context.Input.TryAcceptRealtime(_inputRevision, () => Volatile.Write(ref _recovery, _context.InterruptAsync()));
                return;
            }
            lock (_sync)
            {
                if (!_responseTask.IsCompleted || _stopped || _context.Session.State != MNNRealtimeSessionState.Running) return;
                var audio = _acceptedAudio;
                var text = _acceptedText;
                Volatile.Write(ref _bargeInEcho, null);
                _acceptedAudio = null;
                _acceptedText = null;
                ResetUtterance();
                _nextTurnSilenceSamples = 0;
                StartResponse(text, audio, _lifetime.Token, _inputRevision, voiceValidated: _config.filterVoiceInput, audioPrepared: true);
            }
        }
        // This is an automatic interpretation policy, never a device/PCM gate. Manual
        // recordings have already consumed the frame before reaching here. Once near-end
        // speech interrupts a response, keep its entire utterance even if AEC health changes.
        protected bool SuppressSpeakerEcho()
        {
            bool audible = _context.SpeakerAudible;
            bool suppress = HalfDuplex && audible && Volatile.Read(ref _bargeInEcho) == null;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (suppress)
            {
                _resumeAfter = now + System.Diagnostics.Stopwatch.Frequency / 2;
                if (Interlocked.Exchange(ref _echoSuppressed, 1) == 0)
                {
                    ResetUtterance();
                    _acceptedAudio = null;
                    _turnCancellation?.Cancel();
                    _bargeIn.Reset();
                }
                return true;
            }
            if (Volatile.Read(ref _echoSuppressed) == 0) return false;
            if (HalfDuplex && now < _resumeAfter) return true;
            _resumeAfter = 0;
            Volatile.Write(ref _echoSuppressed, 0);
            return false;
        }

        protected void ObservePresentedResponse()
        {
            var trace = _activeTrace;
            if (trace == null || Volatile.Read(ref trace.Recorded) != 0 || trace.PresentedAt != 0 || !_response.IsValid || !_output.CanBeginNextResponse(_response)) return;
            trace.PresentedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            TurnTelemetry.Complete(trace, trace.PresentedAt);
        }

    }
}
