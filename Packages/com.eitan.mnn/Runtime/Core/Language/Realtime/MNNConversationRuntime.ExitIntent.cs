using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public partial class MNNConversationRuntime
    {
        protected async Task<bool> ShouldExitRealtimeAsync(string text, int revision, CancellationToken token)
        {
            if (!_config.endRealtimeOnExitIntent || revision < 0 || string.IsNullOrWhiteSpace(text)) return false;
            if (!_context.Input.TryAcceptRealtime(revision, () => { })) return false;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                // Includes waiting for the shared model; a failed judgment never closes the conversation.
                deadline.CancelAfter(TimeSpan.FromSeconds(5));
                bool acquired = false;
                try
                {
                    await _modelGate.WaitAsync(deadline.Token).ConfigureAwait(false);
                    acquired = true;
                    var verdict = new StringBuilder(128);
                    Func<string, Task> consume = delta =>
                    {
                        deadline.Token.ThrowIfCancellationRequested();
                        if (delta == null || verdict.Length + delta.Length > 512)
                            throw new InvalidOperationException("Exit intent verdict exceeded its budget.");
                        verdict.Append(delta);
                        return Task.CompletedTask;
                    };
                    var prompt = MNNConversationExitIntent.Prompt(text);
                    if (_providers.Model is IMNNConversationVoiceIntentProvider classifier)
                        await classifier.ClassifyVoiceIntentAsync(prompt, consume, deadline.Token).ConfigureAwait(false);
                    else await ((IMNNConversationTextProvider)_providers.Model).GenerateAsync(prompt, null, 16000, consume, deadline.Token).ConfigureAwait(false);
                    deadline.Token.ThrowIfCancellationRequested();
                    return MNNConversationExitIntent.IsExitVerdict(verdict.ToString()) && _context.RequestRealtimeExit(revision);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception) { return false; }
                finally { if (acquired) _modelGate.Release(); }
            }
        }
    }
}
