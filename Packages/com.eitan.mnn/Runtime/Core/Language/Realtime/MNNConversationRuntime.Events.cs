using System;
using System.Threading;

namespace MNN.Unity
{
    public partial class MNNConversationRuntime
    {
        protected MNNConversationEventReply _eventReply;

        /// <summary>Returns null when the foreground owns the slot. The caller retains and retries the event.</summary>
        public MNNConversationEventReply TryBeginEventReply(MNNConversationEvent input, CancellationToken token)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            token.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (_stopped || _context.Session.State != MNNRealtimeSessionState.Running || !_responseTask.IsCompleted ||
                    _context.Input.Recording || _context.SpeakerAudible || _utterance.HasSpeechStarted || _turnTask != null ||
                    _acceptedAudio != null || _context.Input is MNNConversationInputState state && state.Speaking) return null;
                var previous = _output.CurrentResponse;
                if (previous.IsValid && _output.Accepts(previous) && !_output.CanBeginNextResponse(previous)) return null;
                if (_providers.Model is IMNNConversationResponseProvider && !(_providers.Model is IMNNConversationEventProvider))
                    throw new NotSupportedException("This routing provider has no response-only event path.");
                var reply = new MNNConversationEventReply(input);
                reply.Completion = StartResponseCore(null, null, token, -1, false, false, reply);
                return reply;
            }
        }
    }
}
