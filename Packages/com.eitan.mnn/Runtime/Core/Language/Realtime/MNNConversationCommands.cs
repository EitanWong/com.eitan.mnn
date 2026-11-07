using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Application ports for shared input commands. Configure on the owning UI context before use.
    /// Delegates run on that context, except TranscribeAsync which is dispatched to a worker.</summary>
    public sealed class MNNConversationCommandBindings
    {
        public MNNConversationInputState Input { get; }
        public MNNConversationRecording Recording { get; }
        public Func<MNNRealtimeSessionState> SessionState { get; set; } = () => MNNRealtimeSessionState.Created;
        public Func<bool> Active { get; set; } = () => true;
        public Func<bool> Resetting { get; set; } = () => false;
        public Func<bool> SupportsRealtime { get; set; } = () => false;
        public Func<bool> SupportsRecordedVoice { get; set; } = () => false;
        public Func<bool> SupportsTranscription { get; set; } = () => false;
        public Func<bool> CanInterrupt { get; set; } = () => true;
        public Func<bool> SpeakerAudible { get; set; } = () => false;
        public Func<bool> CaptureReady { get; set; } = () => true;
        public Func<int> CaptureTailMilliseconds { get; set; } = () => 500;
        public Func<CancellationToken> Lifetime { get; set; } = () => CancellationToken.None;
        public Action<bool> SetCaptureEnabled { get; set; } = _ => { };
        public Func<Task> InterruptAsync { get; set; } = () => Task.CompletedTask;
        public Func<CancellationToken, Task> PrepareRealtimeAsync { get; set; } = _ => Task.CompletedTask;
        public Action ActivateRealtime { get; set; }
        public Action PreparationCancelled { get; set; }
        public Action<bool> CaptureFinishing { get; set; }
        public Action<Exception> BackgroundFailure { get; set; }
        public Func<float[], CancellationToken, Task<string>> TranscribeAsync { get; set; }
        public Func<float[], CancellationToken, Task> SubmitAudioAsync { get; set; }
        public Func<string, CancellationToken, Task> SubmitTextAsync { get; set; }
        public MNNConversationCommandBindings(MNNConversationInputState input, MNNConversationRecording recording)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input)); Recording = recording ?? throw new ArgumentNullException(nameof(recording));
            if (!ReferenceEquals(input, recording.Input)) throw new ArgumentException("Recording and commands must own the same input state.", nameof(recording));
        }
    }

    /// <summary>Single UI-context owner of input mode, entrance, recording and submission ordering.
    /// Cancelling a gesture cancels its provider request while the capture tail still joins independently.
    /// Another hold can start after capture drains; provider submissions remain serialized.</summary>
    public sealed class MNNConversationCommands
    {
        private readonly MNNConversationCommandBindings _ports;
        private MNNConversationInputState Input => _ports.Input;
        private CancellationTokenSource _realtimePreparation, _voiceSubmission;
        private Task _realtimePreparationTask = Task.CompletedTask;
        private Task<bool> _voiceStartTask = Task.FromResult(false);
        private Task _voiceCaptureFinished = Task.CompletedTask, _voiceInterruption = Task.CompletedTask;
        private Task _submissionFinished = Task.CompletedTask;
        private int _inputCommand;
        private bool _voiceFinishing, _submitting;
        public bool IsPreparingRealtime { get; private set; }
        public bool IsRealtime => IsPreparingRealtime || Input.Realtime;
        public bool IsFinishingRecording => _voiceFinishing;
        public bool IsSubmitting => _submitting;
        public bool IsBusy => _submitting || _voiceFinishing || _ports.Resetting();
        public Task PreparationFinished => _realtimePreparationTask;
        public Task<bool> RecordingStarted => _voiceStartTask;
        public Task CaptureFinished => _voiceCaptureFinished;
        public Task InterruptionFinished => _voiceInterruption;
        public Task SubmissionFinished => _submissionFinished;
        private bool SessionAvailable => _ports.SessionState() == MNNRealtimeSessionState.Running || _ports.SessionState() == MNNRealtimeSessionState.Recovering;
        public MNNConversationCommands(MNNConversationCommandBindings ports) { _ports = ports ?? throw new ArgumentNullException(nameof(ports)); }

        public Task SetRealtimeAsync(bool enabled)
        {
            if (enabled && _ports.Resetting()) return Task.CompletedTask;
            if (enabled && IsRealtime) return _realtimePreparationTask;
            if (enabled && (!SessionAvailable || !_ports.SupportsRealtime())) return Task.CompletedTask;
            Exception failure = null;
            Attempt(CancelPreparation, ref failure); ++_inputCommand;
            Attempt(() => _voiceSubmission?.Cancel(), ref failure);
            Input.SetRealtime(false); Attempt(() => _ports.SetCaptureEnabled(false), ref failure);
            if (!enabled)
            {
                if (_ports.SessionState() == MNNRealtimeSessionState.Running)
                { _voiceInterruption = _ports.InterruptAsync(); _ = ObserveAsync(_voiceInterruption); }
                if (failure != null) throw failure;
                return _realtimePreparationTask;
            }
            if (failure != null) throw failure;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_ports.Lifetime());
            IsPreparingRealtime = true; _realtimePreparation = cancellation;
            var previous = _realtimePreparationTask;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _realtimePreparationTask = completion.Task;
            _ = CompletePreparationAsync(previous, cancellation, _inputCommand, completion);
            return completion.Task;
        }

        private async Task CompletePreparationAsync(Task previous, CancellationTokenSource cancellation, int command, TaskCompletionSource<bool> completion)
        {
            var token = cancellation.Token;
            Exception failure = null;
            try
            {
                try { await previous; } catch (Exception) { }
                try { await _voiceStartTask; } catch (Exception) { }
                await _voiceCaptureFinished; token.ThrowIfCancellationRequested();
                await _ports.InterruptAsync(); token.ThrowIfCancellationRequested();
                await _ports.PrepareRealtimeAsync(token); token.ThrowIfCancellationRequested();
                if (_ports.ActivateRealtime != null) _ports.ActivateRealtime(); else Input.SetRealtime(true);
                _ports.SetCaptureEnabled(true);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception error)
            {
                failure = error;
                if (command == _inputCommand && ReferenceEquals(_realtimePreparation, cancellation))
                {
                    Input.SetRealtime(false);
                    Attempt(() => _ports.SetCaptureEnabled(false), ref failure);
                    Attempt(() => _ports.PreparationCancelled?.Invoke(), ref failure);
                }
            }
            finally
            {
                if (ReferenceEquals(_realtimePreparation, cancellation)) { _realtimePreparation = null; IsPreparingRealtime = false; }
                cancellation.Dispose();
                if (failure == null) completion.TrySetResult(true); else completion.TrySetException(failure);
            }
        }

        public void CancelPreparation()
        {
            var preparation = _realtimePreparation;
            _realtimePreparation = null; IsPreparingRealtime = false;
            if (preparation == null) return;
            Exception failure = null;
            Attempt(() => _ports.PreparationCancelled?.Invoke(), ref failure);
            Attempt(preparation.Cancel, ref failure);
            if (failure != null) throw failure;
        }

        public Task<bool> BeginRecordingAsync()
        {
            if (!SessionAvailable || _ports.Resetting() || Input.Recording && !_voiceFinishing || !_ports.SupportsRecordedVoice()) return Task.FromResult(false);
            CancelPreparation(); int command = ++_inputCommand; _voiceSubmission?.Cancel();
            var previousStart = _voiceStartTask; var previousCapture = _voiceCaptureFinished;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _voiceStartTask = completion.Task;
            _ = CompleteStartAsync(previousStart, previousCapture, command, completion);
            return completion.Task;
        }

        private bool Current(int command) => command == _inputCommand && SessionAvailable && _ports.Active();
        private async Task CompleteStartAsync(Task previousStart, Task previousCapture, int command, TaskCompletionSource<bool> completion)
        {
            try { completion.TrySetResult(await StartRecordingAsync(previousStart, previousCapture, command)); }
            catch (OperationCanceledException) { completion.TrySetCanceled(); }
            catch (Exception error) { completion.TrySetException(error); }
        }
        private async Task<bool> StartRecordingAsync(Task previousStart, Task previousCapture, int command)
        {
            try
            {
                try { await previousStart; } catch (Exception) { }
                try { await previousCapture; } catch (Exception) { }
                try { await _ports.Recording.PendingDrain; } catch (Exception) { }
                try { await _realtimePreparationTask; } catch (Exception) { }
                if (!Current(command)) return false;
                bool interruptWhileRecording = _ports.CanInterrupt();
                Input.SetRealtime(false);
                if (!interruptWhileRecording)
                {
                    bool waitForSpeaker = _ports.SpeakerAudible();
                    _ports.SetCaptureEnabled(false);
                    _voiceInterruption = _ports.InterruptAsync(); await _voiceInterruption;
                    if (!Current(command)) return false;
                    if (waitForSpeaker) await Task.Delay(_ports.CaptureTailMilliseconds(), _ports.Lifetime());
                    if (!Current(command)) return false;
                }
                // Enqueue time cannot identify PCM still being processed from an earlier mode.
                // Quiesce that producer and worker before accepting any samples for this hold.
                await _ports.Recording.PrepareAsync(_ports.Lifetime());
                if (!Current(command)) return false;
                _ports.Recording.Begin(); _ports.SetCaptureEnabled(true);
                long deadline = System.Diagnostics.Stopwatch.GetTimestamp() + System.Diagnostics.Stopwatch.Frequency * 2;
                while (!_ports.CaptureReady())
                {
                    if (!Current(command)) return false;
                    if (System.Diagnostics.Stopwatch.GetTimestamp() > deadline)
                        throw new TimeoutException("Microphone capture is not ready. Check the audio device and permission.");
                    await Task.Delay(5, _ports.Lifetime());
                }
                if (interruptWhileRecording) { _voiceInterruption = _ports.InterruptAsync(); _ = ObserveAsync(_voiceInterruption); }
                return true;
            }
            catch
            {
                if (command == _inputCommand) { Input.FinishRecording(true); _ports.SetCaptureEnabled(Input.Realtime); }
                throw;
            }
        }

        public void CancelRecording()
        {
            Exception failure = null;
            Attempt(CancelPreparation, ref failure); ++_inputCommand;
            Attempt(() => _voiceSubmission?.Cancel(), ref failure);
            Input.FinishRecording(true); Attempt(() => _ports.SetCaptureEnabled(Input.Realtime), ref failure);
            if (failure != null) throw failure;
        }

        public async Task<string> FinishRecordingAsync(bool transcribeOnly)
        {
            if (_voiceFinishing) throw new InvalidOperationException("The recording is already finishing.");
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_ports.Lifetime());
            int command = ++_inputCommand; _voiceFinishing = true;
            Task previousSubmission = _submissionFinished;
            var captureFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _voiceCaptureFinished = captureFinished.Task;
            var previousCancellation = _voiceSubmission; _voiceSubmission = cancellation;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _submissionFinished = completion.Task;
            Exception setupFailure = null, primaryFailure = null;
            try
            {
                // Provider cancellation and UI hooks cannot abandon the physical microphone tail.
                Attempt(() => previousCancellation?.Cancel(), ref setupFailure);
                Attempt(() => _ports.CaptureFinishing?.Invoke(true), ref setupFailure);
                await _ports.Recording.DrainAsync(_ports.Lifetime());
                var audio = Input.FinishRecording(command != _inputCommand);
                Exception captureFailure = Combine(setupFailure, CompleteCapture(captureFinished, false));
                setupFailure = null;
                if (captureFailure != null) throw captureFailure;
                if (command != _inputCommand) throw new OperationCanceledException();
                if (audio == null) throw new InvalidOperationException("Recording too short. Hold to speak for at least a quarter second.");
                await _voiceInterruption; await previousSubmission;
                var token = cancellation.Token; token.ThrowIfCancellationRequested();
                if (command != _inputCommand) throw new OperationCanceledException();
                if (_ports.SessionState() != MNNRealtimeSessionState.Running) throw new InvalidOperationException("Wait for the conversation to become ready.");
                _submitting = true;
                if (transcribeOnly)
                {
                    if (!_ports.SupportsTranscription() || _ports.TranscribeAsync == null) throw new NotSupportedException("Configure speech recognition to convert voice to text.");
                    string draft = await Task.Run(() => _ports.TranscribeAsync(audio, token), token);
                    token.ThrowIfCancellationRequested();
                    if (command != _inputCommand) throw new OperationCanceledException();
                    return draft;
                }
                if (_ports.SubmitAudioAsync == null) throw new NotSupportedException("Configure audio submission before sending voice.");
                await _ports.SubmitAudioAsync(audio, token); return null;
            }
            catch (Exception error)
            {
                primaryFailure = Combine(setupFailure, error);
                if (ReferenceEquals(primaryFailure, error)) throw;
                throw primaryFailure;
            }
            finally
            {
                Exception cleanupFailure = captureFinished.Task.IsCompleted ? null : CompleteCapture(captureFinished, true);
                if (ReferenceEquals(_voiceSubmission, cancellation)) _voiceSubmission = null;
                cancellation.Dispose();
                try { await previousSubmission; }
                catch (Exception error) { cleanupFailure = Combine(cleanupFailure, error); }
                finally { _submitting = false; completion.TrySetResult(true); }
                if (cleanupFailure != null) throw Combine(primaryFailure, cleanupFailure);
            }
        }

        private Exception CompleteCapture(TaskCompletionSource<bool> completion, bool discard)
        {
            Exception failure = null;
            if (discard) Attempt(() => Input.FinishRecording(true), ref failure);
            Attempt(() => _ports.CaptureFinishing?.Invoke(false), ref failure);
            Attempt(() => _ports.SetCaptureEnabled(false), ref failure);
            _voiceFinishing = false; completion.TrySetResult(true); return failure;
        }

        public async Task SendTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (text.Length > MNNConversationMailbox.MaximumTextCharacters) throw new ArgumentException("Text exceeds the character limit.", nameof(text));
            if (_ports.SessionState() != MNNRealtimeSessionState.Running || IsBusy)
                throw new InvalidOperationException("Text submission requires a running session with no pending text submission.");
            if (_ports.SubmitTextAsync == null) throw new NotSupportedException("This model backend does not provide text submission.");
            if (IsPreparingRealtime) { CancelPreparation(); ++_inputCommand; }
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _submissionFinished = completion.Task; _submitting = true;
            int command = _inputCommand; var token = _ports.Lifetime();
            try
            {
                await _ports.InterruptAsync();
                if (command != _inputCommand) throw new OperationCanceledException();
                token.ThrowIfCancellationRequested(); await _ports.SubmitTextAsync(text, token);
            }
            finally { _submitting = false; completion.TrySetResult(true); }
        }

        public Exception BackgroundError { get; private set; }
        private async Task ObserveAsync(Task task)
        {
            try { await task; }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                BackgroundError = error;
                try { _ports.BackgroundFailure?.Invoke(error); }
                catch (Exception notificationError) { BackgroundError = Combine(error, notificationError); }
            }
        }
        private static void Attempt(Action action, ref Exception failure)
        {
            try { action(); }
            catch (Exception error) { failure = Combine(failure, error); }
        }
        private static Exception Combine(Exception first, Exception second)
        {
            if (first == null) return second;
            if (second == null || ReferenceEquals(first, second)) return first;
            return new AggregateException("Conversation command and cleanup failed.", first, second);
        }
    }
}
