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
        private string _cachedCompletionPrefix, _cachedCompletion;
        private long _completionCachedAt;
        public bool SupportsTextAssistance => _providers.Model is IMNNConversationTextProvider;

        /// <summary>Low-priority generation on the existing model gate. No chat memory, tools or speech events.</summary>
        public Task<string> AssistTextAsync(MNNConversationTextTask task, string input, CancellationToken token)
            => AssistTextAsync(task, input, null, token);

        /// <summary>Preview receives complete words on a worker thread. The returned task still owns
        /// generation and must be observed. Callers must marshal previews and discard canceled results.</summary>
        public Task<string> AssistTextAsync(MNNConversationTextTask task, string input, Action<string> preview, CancellationToken token)
        {
            var prompt = MNNConversationTextAssistance.CreatePrompt(task, input);
            lock (_sync)
            {
                if (token.IsCancellationRequested || _stopped || _joiningTextAssistance || !SupportsTextAssistance || !_responseTask.IsCompleted || !_textAssistanceTask.IsCompleted ||
                    _context.Input.Realtime || _context.Input.Recording) return Task.FromResult("");
                if (task == MNNConversationTextTask.Completion &&
                    System.Diagnostics.Stopwatch.GetTimestamp() - _completionCachedAt < 15L * System.Diagnostics.Stopwatch.Frequency)
                {
                    string cached = MNNConversationTextAssistance.RemainingCompletion(_cachedCompletionPrefix, _cachedCompletion, input);
                    if (cached.Length > 0) return Task.FromResult(cached);
                }
                _textAssistanceCancellation?.Dispose();
                _textAssistanceCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
                _textAssistanceCancellation.CancelAfter(task == MNNConversationTextTask.Completion ? 2500 : 8000);
                var source = _textAssistanceCancellation; int revision = ++_textAssistanceRevision;
                _textAssistanceTask = Task.Run(() => GenerateAssistanceAsync(task, input, prompt, source, token, revision, preview));
                return _textAssistanceTask;
            }
        }
        private static void CancelAssistanceSource(CancellationTokenSource source)
        {
            try { source?.Cancel(); } catch (ObjectDisposedException) { }
        }
        private async Task<string> GenerateAssistanceAsync(MNNConversationTextTask task, string input,
            IReadOnlyList<MNNOpenAIMessage> prompt, CancellationTokenSource deadline, CancellationToken requested, int revision, Action<string> preview)
        {
            var result = new StringBuilder(); bool entered = false; string lastPreview = "";
            try
            {
                await _modelGate.WaitAsync(deadline.Token).ConfigureAwait(false); entered = true;
                Func<string, Task> append = text =>
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    int remaining = 256 - result.Length;
                    bool full = !string.IsNullOrEmpty(text) && text.Length >= remaining;
                    if (!string.IsNullOrEmpty(text))
                    {
                        int length = Math.Min(remaining, text.Length);
                        if (full && length > 0 && char.IsHighSurrogate(text[length - 1])) --length;
                        result.Append(text, 0, length);
                    }
                    string generated = result.ToString();
                    if (preview != null && task == MNNConversationTextTask.Completion && IsAssistanceCurrent(requested, revision))
                    {
                        string partial = MNNConversationTextAssistance.PreviewCompletion(input, generated);
                        if (!string.IsNullOrWhiteSpace(partial) && partial != lastPreview) { lastPreview = partial; preview(partial); }
                    }
                    if (full || generated.IndexOf('\r') >= 0 || generated.IndexOf('\n') >= 0 ||
                        generated.IndexOf("```", StringComparison.Ordinal) >= 0 || generated.IndexOf("<think", StringComparison.OrdinalIgnoreCase) >= 0)
                        deadline.Cancel();
                    return Task.CompletedTask;
                };
                if (_providers.Model is IMNNConversationTextAssistanceProvider assistance)
                    await assistance.GenerateAssistanceAsync(task, prompt, append, deadline.Token).ConfigureAwait(false);
                else if (_providers.Model is IMNNConversationResponseProvider response)
                    await response.GenerateResponseAsync(prompt, prompt[0].Text, append, deadline.Token).ConfigureAwait(false);
                else await ((IMNNConversationTextProvider)_providers.Model).GenerateAsync(prompt, null, 16000, append, deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
            finally { if (entered) _modelGate.Release(); }
            string normalized = MNNConversationTextAssistance.Normalize(task, input, result.ToString());
            lock (_sync)
            {
                if (!IsAssistanceCurrent(requested, revision)) return "";
                if (task == MNNConversationTextTask.Completion && normalized.Length > 0)
                {
                    _cachedCompletionPrefix = input; _cachedCompletion = normalized;
                    _completionCachedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                }
            }
            return normalized;
        }
        private bool IsAssistanceCurrent(CancellationToken requested, int revision) =>
            !requested.IsCancellationRequested && !_lifetime.IsCancellationRequested && revision == Volatile.Read(ref _textAssistanceRevision);
        // Called under _sync when the conversation context changes, never across conversations.
        private void InvalidateTextAssistance()
        { ++_textAssistanceRevision; _cachedCompletionPrefix = null; _cachedCompletion = null; }
        private async Task JoinTextAssistanceAsync()
        {
            Task task; CancellationTokenSource source;
            lock (_sync)
            { _joiningTextAssistance = true; InvalidateTextAssistance(); source = _textAssistanceCancellation; task = _textAssistanceTask; }
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
