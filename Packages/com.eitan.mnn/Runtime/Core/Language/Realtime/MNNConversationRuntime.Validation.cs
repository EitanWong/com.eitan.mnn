using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public partial class MNNConversationRuntime
    {
        protected readonly SemaphoreSlim _modelGate = new SemaphoreSlim(1, 1);
        protected readonly SemaphoreSlim _asrGate = new SemaphoreSlim(1, 1);
        protected long _rejectedVoiceInputs;
        public long RejectedVoiceInputs => Interlocked.Read(ref _rejectedVoiceInputs);
        protected string _lastVoiceRejection;
        protected string _bargeInEcho;
        public string LastVoiceRejection => Volatile.Read(ref _lastVoiceRejection);

        protected readonly struct VoiceCandidate
        {
            public readonly bool Complete, Accepted;
            public readonly string Text;
            public readonly float[] Audio;
            public VoiceCandidate(bool complete, bool accepted, string text = null, float[] audio = null)
            { Complete = complete; Accepted = accepted; Text = text; Audio = audio; }
        }

        protected async Task<VoiceCandidate> EvaluateVoiceCandidateAsync(float[] audio, bool full, string echo, CancellationToken token)
        {
            audio = await _context.PrepareVoiceAsync(audio, token).ConfigureAwait(false);
            if (audio == null) return RejectVoice("No unambiguous primary speaker");
            string transcript = null;
            if (_providers.Turn is IMNNConversationTurnProvider turn && !full)
            {
                bool complete;
                if (turn is IMNNConversationTranscriptTurnProvider textTurn && textTurn.RequiresTranscript)
                {
                    if (!HasAsr) throw new InvalidOperationException("The selected turn detector requires speech recognition.");
                    transcript = await TranscribePreparedAsync(audio, token).ConfigureAwait(false);
                    complete = await textTurn.IsTurnCompleteAsync(audio, 16000, transcript, token).ConfigureAwait(false);
                }
                else complete = await turn.IsTurnCompleteAsync(audio, 16000, token).ConfigureAwait(false);
                if (!complete) return default;
            }
            if (!_config.filterVoiceInput) return new VoiceCandidate(true, true, transcript, audio);
            var candidate = await ValidateVoiceAsync(audio, echo, token, transcript).ConfigureAwait(false);
            return new VoiceCandidate(candidate.Complete, candidate.Accepted, candidate.Text, audio);
        }

        protected async Task<VoiceCandidate> ValidateVoiceAsync(float[] audio, string echo, CancellationToken token, string transcript = null)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                try { return await ValidateVoiceCoreAsync(audio, echo, deadline.Token, transcript).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception) { return RejectVoice("Voice validation failed or timed out"); }
            }
        }

        protected async Task<VoiceCandidate> ValidateVoiceCoreAsync(float[] audio, string echo, CancellationToken token, string preparedTranscript = null)
        {
            token.ThrowIfCancellationRequested();
            if (!HasAcousticEvidence(audio)) return RejectVoice("Insufficient speech energy or invalid PCM");
            if (!HasAsr) return RejectVoice("No speech recognition provider for voice validation");
            string transcript = preparedTranscript ?? await TranscribePreparedAsync(audio, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (!MNNConversationVoiceInputFilter.TryPrepare(transcript, out string text, out string reason)) return RejectVoice(reason);
            if (MNNConversationVoiceInputFilter.IsLikelyEcho(text, echo)) return RejectVoice("Matches recent speaker output");
            // Explicit brief commands are meaningful by themselves. Do not wait behind an
            // ongoing language generation to recognize stop/no/yes and their Chinese equivalents.
            if (MNNConversationVoiceInputFilter.IsCommand(text)) return new VoiceCandidate(true, true, text);
            if (!await HasMeaningAsync(text, echo, token).ConfigureAwait(false)) return RejectVoice("No clear conversational intent");
            token.ThrowIfCancellationRequested();
            return new VoiceCandidate(true, true, text);
        }

        protected VoiceCandidate RejectVoice(string reason)
        {
            Volatile.Write(ref _lastVoiceRejection, reason);
            Interlocked.Increment(ref _rejectedVoiceInputs);
            return new VoiceCandidate(true, false);
        }

        protected async Task<bool> HasMeaningAsync(string text, string echo, CancellationToken token)
        {
            // The reply and classifier share one model instance. Never run concurrent inference,
            // or cancel a reply speculatively just to find out whether a candidate was noise.
            await _modelGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var context = new StringBuilder();
                AppendContext(context, "Previous user", Memory.LastUserText);
                AppendContext(context, "Assistant", Memory.LastAssistantText);
                AppendContext(context, "Recent playback", echo);
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(5));
                    var verdict = new StringBuilder(32);
                    try
                    {
                        Func<string, Task> consume = delta =>
                        {
                            deadline.Token.ThrowIfCancellationRequested();
                            if (delta == null || verdict.Length + delta.Length > 32) deadline.Cancel();
                            else verdict.Append(delta);
                            return Task.CompletedTask;
                        };
                        var prompt = MNNConversationVoiceInputFilter.SemanticPrompt(text, context.ToString());
                        if (_providers.Model is IMNNConversationVoiceIntentProvider classifier)
                            await classifier.ClassifyVoiceIntentAsync(prompt, consume, deadline.Token).ConfigureAwait(false);
                        else
                            await ((IMNNConversationTextProvider)_providers.Model).GenerateAsync(prompt, null, 16000, consume, deadline.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested && deadline.IsCancellationRequested)
                    { return false; }
                    token.ThrowIfCancellationRequested();
                    return !deadline.IsCancellationRequested && MNNConversationVoiceInputFilter.AcceptsVerdict(verdict.ToString());
                }
            }
            finally { _modelGate.Release(); }
        }

        protected async Task GenerateTextAsync(IReadOnlyList<MNNOpenAIMessage> history, Func<string, Task> delta, CancellationToken token)
        {
            await _modelGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var text = new StringBuilder();
                Func<string, Task> publish = async value =>
                {
                    await delta(value).ConfigureAwait(false);
                    text.Append(value);
                };
                if (_providers.Model is IMNNConversationResponseProvider hybrid)
                    // This system message is built for this response by BuildResponseHistory;
                    // it is not historical conversation data or a planner instruction.
                    await hybrid.GenerateResponseAsync(history, history[0].Text, publish, token).ConfigureAwait(false);
                else await ((IMNNConversationTextProvider)_providers.Model).GenerateAsync(history, null, 16000, publish, token).ConfigureAwait(false);
                Memory.RememberAssistant(text.ToString());
            }
            finally { _modelGate.Release(); }
        }

        protected static void AppendContext(StringBuilder context, string label, string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) context.Append(label).Append(": ").Append(MNNConversationMemory.Tail(text)).Append('\n');
        }

        protected string SnapshotPlaybackText()
        {
            var response = _output.CurrentResponse;
            if (!response.IsValid || !_output.Accepts(response)) return Volatile.Read(ref _bargeInEcho) ?? "";
            if (_output.CanBeginNextResponse(response))
            {
                var trace = _activeTrace;
                if (trace == null || trace.PresentedAt == 0 ||
                    System.Diagnostics.Stopwatch.GetTimestamp() - trace.PresentedAt > System.Diagnostics.Stopwatch.Frequency * .8) return "";
            }
            var spoken = new StringBuilder();
            for (int i = 0; i < _output.SegmentCount; ++i)
            {
                MNNConversationSegment segment;
                // Manual interruption may retire the old ledger while this snapshot is read.
                try { segment = _output.GetSegment(i); }
                catch (ArgumentOutOfRangeException) { break; }
                if (segment.Response != response || segment.SubmittedFrames == 0) continue;
                if (spoken.Length + segment.Text.Length > 2048) spoken.Clear();
                if (segment.Text.Length <= 2048) spoken.Append(segment.Text).Append(' ');
            }
            return spoken.ToString();
        }

        protected static bool HasAcousticEvidence(float[] audio)
        {
            if (audio == null) return false;
            int audibleFrames = 0, clipped = 0;
            for (int offset = 0; offset < audio.Length; offset += 512)
            {
                double energy = 0, sum = 0;
                int length = Math.Min(512, audio.Length - offset);
                for (int i = offset; i < offset + length; ++i)
                {
                    float sample = audio[i];
                    if (float.IsNaN(sample) || float.IsInfinity(sample) || Math.Abs(sample) > 1) return false;
                    if (Math.Abs(sample) >= .999f) ++clipped;
                    energy += sample * (double)sample;
                    sum += sample;
                }
                // Remove DC energy; a constant device offset is not speech.
                if (energy / length - Math.Pow(sum / length, 2) >= .003 * .003) ++audibleFrames;
            }
            return audibleFrames >= 3 && clipped < audio.Length / 10;
        }
    }
}
