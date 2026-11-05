using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed partial class MNNRealtimeDeviceSession
    {
        /// <summary>Control-thread update. Invalid/inaudible routes close before speaker presentation can be acknowledged.</summary>
        public void Tick()
        {
            if (!IsReady) return;
            try
            {
                if (_clock.Seconds >= _nextSupervision)
                { _nextSupervision = _clock.Seconds + .02; _binding.Supervise(); }
                ApplyCaptureGate();
                var state = _binding.ReadState();
                string failure = GetRouteFailure(state);
                if (failure != null || Fault != null) throw Fault ?? new InvalidOperationException("Audio route: " + failure);
                _output.DrainReceipts(256);
                double tail = state.PresentationTailMilliseconds;
                if (double.IsNaN(tail) || double.IsInfinity(tail) || tail < 0 || tail > 60000)
                    throw new InvalidOperationException("Device supplied an invalid presentation delay.");
                if (_options.IncludeMeasuredEchoDelayInPresentation && CanInterruptSpeech) tail = Math.Max(tail, EchoDelayMilliseconds ?? 0);
                _presentationTailMilliseconds = tail; _binding.SetPresentationTail(tail);
                long through = state.RenderedFrames - (long)(tail * _output.Format.SampleRate / 1000);
                _presentedThrough = Math.Max(_presentedThrough, through);
                _output.AcknowledgePresentation(_presentedThrough, true);
            }
            catch (Exception error) { RecordFault(error); Close(); }
        }

        public string GetRouteFailure() => GetRouteFailure(_binding.ReadState());
        private string GetRouteFailure(MNNRealtimeDeviceSnapshot state)
        {
            if (!state.Active || !state.InputActive) return "microphone component was disabled or removed.";
            if (!state.Capturing && !state.CaptureStarting) return "microphone recording stopped.";
            if (!state.OutputActive || !state.OutputRunning) return "speaker output component stopped.";
            if (!state.RouteMatches) return "playback device or source ownership changed.";
            if (!state.Audible || state.Volume <= 0) return "speaker output is muted or has zero volume.";
            if (_output != null && !state.OutputFormat.Equals(_output.Format)) return "output format changed. Restart audio for the new route.";
            if (_output?.IsFaulted == true) return "output PCM pipeline faulted.";
            if (state.OutputPaused || state.Playing) _restartDeadline = null;
            else
            {
                if (!state.CanRestartPlayback) return "speaker output stopped unexpectedly.";
                if (!_restartDeadline.HasValue)
                { _binding.StartPlayback(); _restartDeadline = _clock.Seconds + _options.OutputWarmupSeconds; }
                else if (_clock.Seconds >= _restartDeadline.Value) return "speaker output stopped unexpectedly after restart.";
            }
            return null;
        }

        public bool TryReadPlaybackFeatures(float[] waveform, out MNNAudioFeatures features)
        {
            features = default;
            if (!IsReady || _output == null || !_output.Accepts(_output.CurrentResponse)) return false;
            var state = _binding.ReadState();
            if (!state.Audible || state.Volume <= 0 || _binding.Waveform == null ||
                !_binding.Waveform.TryRead(waveform, _output.CurrentResponse, out _, out features)) return false;
            features = features.WithGain(state.Volume);
            for (int i = 0; i < waveform.Length; ++i) waveform[i] *= state.Volume;
            return true;
        }

        public void RecordFault(Exception error)
        { if (error != null) Interlocked.CompareExchange(ref _fault, error, null); }

        /// <summary>Gate callbacks and stop both physical endpoints immediately. Model work joins asynchronously.
        /// Every owner is attempted; failed owners remain available for a later CloseAsync retry.</summary>
        public void Close()
        {
            Volatile.Write(ref _state, Closed); Volatile.Write(ref _captureRequested, 0); BackendEchoHealth.Reset();
            if (!_closeTask.IsCompleted) return;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _closeTask = completion.Task;
            List<Exception> errors = null;
            Attempt(() => _openingCancellation?.Cancel(), ref errors);
            Attempt(() => _binding.SetCaptureEnabled(false), ref errors);
            Attempt(() => _output?.Interrupt(), ref errors);
            if (_reference != null) Attempt(() => { _reference.Dispose(); _reference = null; }, ref errors);
            if (_inputOwned) Attempt(() => { _binding.StopCapture(); _inputOwned = false; }, ref errors);
            if (_outputOwned) Attempt(() => { _binding.StopPlayback(); _outputOwned = false; }, ref errors);
            _ = CompleteCloseAsync(errors, completion);
        }
        public async Task CloseAsync()
        {
            Task opening = _openingFinished?.Task;
            bool pendingOpen = opening != null && !opening.IsCompleted;
            Close();
            if (pendingOpen) { await opening; Close(); }
            await _closeTask;
        }
        private async Task FinishCloseAsync(List<Exception> errors)
        {
            if (_echo != null && !_echoReleased)
            {
                try { await _echo.StopAsync(); _echoReleased = true; }
                catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
            }
            if (errors == null) return;
            var failure = new AggregateException("Audio shutdown failed; failed device/model owners are retained for retry.", errors);
            RecordFault(failure); throw failure;
        }
        private async Task CompleteCloseAsync(List<Exception> errors, TaskCompletionSource<bool> completion)
        {
            try { await FinishCloseAsync(errors); completion.TrySetResult(true); }
            catch (Exception error) { completion.TrySetException(error); }
        }
        private static void Attempt(Action action, ref List<Exception> errors)
        {
            try { action(); }
            catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
        }
        public void ResetForRetry()
        {
            if (!IsClosed || !_closeTask.IsCompleted || _closeTask.IsFaulted || _closeTask.IsCanceled ||
                _openingFinished != null && !_openingFinished.Task.IsCompleted ||
                _inputOwned || _outputOwned || _echo != null && !_echoReleased || _reference != null)
                throw new InvalidOperationException("Await successful CloseAsync before resetting audio resources.");
            _binding.ResetForRetry();
            _openingFinished = null;
            _output = null; _echo = null; _echoReleased = false; _fault = null; _presentedThrough = 0; _restartDeadline = null; _presentationTailMilliseconds = 0; _nextSupervision = 0;
            BackendEchoHealth.Reset(); Volatile.Write(ref _state, Fresh);
        }
    }
}
