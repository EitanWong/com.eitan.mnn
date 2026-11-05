using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Owns the manual-recording capture boundary. Physical tail drains before the gate closes;
    /// the processing flush and lost-frame check complete before another recording may begin.</summary>
    public sealed class MNNConversationRecording
    {
        private readonly MNNConversationInputState _input;
        private Task _drain = Task.CompletedTask;
        private long _lossBaseline;
        public Func<CancellationToken, Task> PauseCaptureAsync { get; set; }
        public Func<Task> FlushInputAsync { get; set; }
        public Func<long> LostInputFrames { get; set; }
        public Action<bool> SetCaptureEnabled { get; set; }
        internal MNNConversationInputState Input => _input;
        public Task PendingDrain => Volatile.Read(ref _drain);
        public MNNConversationRecording(MNNConversationInputState input) { _input = input ?? throw new ArgumentNullException(nameof(input)); }
        public void EnsureDrained()
        {
            if (!PendingDrain.IsCompleted)
                throw new InvalidOperationException("The previous microphone recording is still draining. Wait before recording again.");
        }
        public void Begin()
        {
            EnsureDrained(); _lossBaseline = LostInputFrames?.Invoke() ?? 0; _input.BeginRecording();
        }
        public Task DrainAsync(CancellationToken token)
        {
            EnsureDrained();
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _drain, completion.Task);
            _ = CompleteDrainAsync(completion, token);
            return completion.Task;
        }
        private async Task CompleteDrainAsync(TaskCompletionSource<bool> completion, CancellationToken token)
        {
            try
            {
                try { if (PauseCaptureAsync != null) await PauseCaptureAsync(token); }
                finally { SetCaptureEnabled?.Invoke(false); }
                var flush = FlushInputAsync?.Invoke();
                if (flush != null)
                {
                    if (await Task.WhenAny(flush, Task.Delay(1000, token)) != flush)
                    {
                        // Keep ownership until the actual worker has quiesced, even after timeout.
                        _ = ObserveLateDrainAsync(flush);
                        token.ThrowIfCancellationRequested();
                        throw new TimeoutException("Microphone processing did not finish in time. Please record again.");
                    }
                    await flush;
                }
                token.ThrowIfCancellationRequested();
                if ((LostInputFrames?.Invoke() ?? 0) != _lossBaseline)
                    throw new InvalidOperationException("Recording contains missing microphone samples. Please record again.");
                completion.TrySetResult(true);
            }
            catch (OperationCanceledException) { completion.TrySetCanceled(); }
            catch (Exception error) { completion.TrySetException(error); }
        }
        private async Task ObserveLateDrainAsync(Task flush)
        {
            // A timed-out caller may leave, but PendingDrain remains the physical processing owner.
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _drain, pending.Task);
            try { await flush; pending.TrySetResult(true); }
            catch (Exception error) { pending.TrySetException(error); }
        }
    }
}
