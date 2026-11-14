using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Interop;

namespace MNN.Unity
{
    public sealed partial class MNNLlm
    {
        /// <summary>Generates independent text with complete UTF-8 deltas. WebGL uses
        /// Unity frame continuations between native calls; other platforms use a worker.
        /// Cancellation cannot interrupt one native call. WebGL model loading remains synchronous.</summary>
        public Task<MNNLlmResult> GenerateStreamingAsync(string prompt, Action<MNNGenerationDelta> onDelta,
            int maxNewTokens = 128, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentNullException(nameof(prompt));
            if (maxNewTokens < 1 || maxNewTokens > 8192) throw new ArgumentOutOfRangeException(nameof(maxNewTokens));
#if UNITY_WEBGL && !UNITY_EDITOR
            return GenerateTextCooperatively(prompt, onDelta, maxNewTokens, cancellationToken);
#else
            return Task.Run(() =>
            {
                lock (_gate)
                {
                    ThrowIfDisposed();
                    _cooperativeGeneration = true;
                    try
                    {
                        using (MNNInterop.BeginNativeCall())
                            return MNNInterop.GenerateTextStreaming(_handle, prompt, maxNewTokens, onDelta, cancellationToken);
                    }
                    finally { _cooperativeGeneration = false; }
                }
            }, cancellationToken);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private Task<MNNMultimodalResult> GenerateConversationCooperatively(IReadOnlyList<MNNChatMessage> messages,
            int limit, string image, string audio, bool speech, Action<MNNGenerationUpdate> progress,
            Action<MNNGenerationDelta> delta, CancellationToken cancellation)
        {
            if (messages == null || messages.Count == 0) throw new ArgumentException("A conversation is required.", nameof(messages));
            if (limit < 1 || limit > 8192) throw new ArgumentOutOfRangeException(nameof(limit));
            if (image != null || audio != null || speech)
                throw new PlatformNotSupportedException("WebGL cooperative generation currently supports text only.");
            var snapshot = new MNNChatMessage[messages.Count];
            for (int i = 0; i < snapshot.Length; ++i)
            {
                snapshot[i] = messages[i] ?? throw new ArgumentException("Messages cannot contain null.", nameof(messages));
                if (snapshot[i].Role == MNNChatRole.System && i != 0) throw new ArgumentException("The system message must be first.", nameof(messages));
            }
            if (snapshot[snapshot.Length - 1].Role != MNNChatRole.User || string.IsNullOrWhiteSpace(snapshot[snapshot.Length - 1].Content))
                throw new ArgumentException("A conversation must end with a nonempty user message.", nameof(messages));
            Action<MNNGenerationDelta> callback = delta;
            if (progress != null)
            {
                var text = new System.Text.StringBuilder();
                callback = value => { text.Append(value.Text); progress(new MNNGenerationUpdate(text.ToString(), value.GeneratedTokens)); };
            }
            return GenerateCooperatively(null, snapshot, callback, limit, cancellation);
        }

        private async Task<MNNLlmResult> GenerateTextCooperatively(string prompt, Action<MNNGenerationDelta> progress,
            int limit, CancellationToken cancellation)
        {
            var result = await GenerateCooperatively(prompt, null, progress, limit, cancellation);
            return new MNNLlmResult(result.Text, result.GeneratedTokens, result.ReachedTokenLimit, result.PromptTokens,
                prefillUs: result.PrefillMicroseconds, decodeUs: result.DecodeMicroseconds, sampleUs: result.SampleMicroseconds);
        }

        private async Task<MNNMultimodalResult> GenerateCooperatively(string prompt, IReadOnlyList<MNNChatMessage> messages,
            Action<MNNGenerationDelta> progress, int limit, CancellationToken cancellation)
        {
            if (SynchronizationContext.Current == null)
                throw new InvalidOperationException("WebGL asynchronous inference requires the Unity main-thread synchronization context.");
            lock (_gate)
            {
                ThrowIfDisposed();
                cancellation.ThrowIfCancellationRequested();
                _cooperativeGeneration = true;
            }
            MNNMultimodalResult result = null;
            try
            {
                await Task.Yield();
                using (var steps = MNNInterop.GenerateCooperatively(_handle, prompt, messages, limit, value => result = value, cancellation).GetEnumerator())
                {
                    while (true)
                    {
                        bool next;
                        using (MNNInterop.BeginNativeCall()) next = steps.MoveNext();
                        if (!next) break;
                        if (!string.IsNullOrEmpty(steps.Current.Text)) progress?.Invoke(steps.Current);
                        // UnitySynchronizationContext resumes next Update, allowing
                        // rendering/input between calls; no thread pool on WebGL.
                        await Task.Yield();
                    }
                }
                return result;
            }
            catch (OperationCanceledException) when (messages != null && result != null)
            {
                // Conversation APIs preserve their existing partial-result contract.
                return result;
            }
            finally
            {
                lock (_gate) _cooperativeGeneration = false;
            }
        }
#endif
    }
}
