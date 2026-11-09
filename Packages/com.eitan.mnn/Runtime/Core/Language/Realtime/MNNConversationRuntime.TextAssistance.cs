using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public partial class MNNConversationRuntime
    {
        private int _textAssistanceRevision;
        private bool _joiningTextAssistance;
        private Task<string> _textAssistanceTask = Task.FromResult("");
        private CancellationTokenSource _textAssistanceCancellation;
        public bool SupportsTextAssistance => _providers.Model is IMNNConversationTextProvider;

        /// <summary>Low-priority generation on the existing model gate. No chat memory, tools or speech events.</summary>
        public Task<string> AssistTextAsync(MNNConversationTextTask task, string input, CancellationToken token)
        {
            var prompt = MNNConversationTextAssistance.CreatePrompt(task, input);
            lock (_sync)
            {
                if (_stopped || _joiningTextAssistance || !SupportsTextAssistance || !_responseTask.IsCompleted || !_textAssistanceTask.IsCompleted ||
                    _context.Input.Realtime || _context.Input.Recording) return Task.FromResult("");
                _textAssistanceCancellation?.Dispose();
                _textAssistanceCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
                _textAssistanceCancellation.CancelAfter(task == MNNConversationTextTask.Completion ? 2500 : 8000);
                var source = _textAssistanceCancellation; int revision = ++_textAssistanceRevision;
                _textAssistanceTask = Task.Run(() => GenerateAssistanceAsync(task, input, prompt, source, token, revision));
                return _textAssistanceTask;
            }
        }
        private static void CancelAssistanceSource(CancellationTokenSource source)
        {
            try { source?.Cancel(); } catch (ObjectDisposedException) { }
        }
        private async Task<string> GenerateAssistanceAsync(MNNConversationTextTask task, string input,
            IReadOnlyList<MNNOpenAIMessage> prompt, CancellationTokenSource deadline, CancellationToken requested, int revision)
        {
            var result = new StringBuilder(); bool entered = false;
            try
            {
                await _modelGate.WaitAsync(deadline.Token).ConfigureAwait(false); entered = true;
                Func<string, Task> append = text =>
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    int remaining = 256 - result.Length;
                    if (!string.IsNullOrEmpty(text)) result.Append(text, 0, Math.Min(remaining, text.Length));
                    if (result.Length >= 256) deadline.Cancel();
                    return Task.CompletedTask;
                };
                if (_providers.Model is IMNNConversationResponseProvider response)
                    await response.GenerateResponseAsync(prompt, prompt[0].Text, append, deadline.Token).ConfigureAwait(false);
                else await ((IMNNConversationTextProvider)_providers.Model).GenerateAsync(prompt, null, 16000, append, deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
            finally { if (entered) _modelGate.Release(); }
            if (requested.IsCancellationRequested || _lifetime.IsCancellationRequested || revision != Volatile.Read(ref _textAssistanceRevision)) return "";
            return MNNConversationTextAssistance.Normalize(task, input, result.ToString());
        }
        private async Task JoinTextAssistanceAsync()
        {
            Task task; CancellationTokenSource source;
            lock (_sync)
            { _joiningTextAssistance = true; ++_textAssistanceRevision; source = _textAssistanceCancellation; task = _textAssistanceTask; }
            try
            {
                CancelAssistanceSource(source);
                try { await task.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                // The request owner observes optional inference faults; joining still releases resources.
                catch (Exception) { }
            }
            finally { lock (_sync) _joiningTextAssistance = false; }
        }
    }
}
