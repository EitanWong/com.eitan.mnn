using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    // Optional ASR belongs to one registration, and is acquired only when first used.
    // Its mutable inference state has a single owner even when recordings overlap.
    public sealed class MNNConversationTranscriptionService
    {
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly MNNConversationStageRegistration _registration;
        private readonly MNNConversationRuntimeDiagnostics _diagnostics;
        private IMNNConversationProvider _provider;
        private bool _prepared, _released, _unusable;

        public MNNConversationTranscriptionService(MNNConversationStageRegistration registration, MNNConversationRuntimeDiagnostics diagnostics = null)
        { _registration = registration; _diagnostics = diagnostics ?? new MNNConversationRuntimeDiagnostics(); }

        public async Task<string> TranscribeAsync(float[] audio, CancellationToken token, int sampleRate = 16000)
        {
            await _gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if (_released) throw new ObjectDisposedException(nameof(MNNConversationTranscriptionService));
                if (_unusable) throw new InvalidOperationException("Optional ASR cleanup failed. Stop this registration before retrying.");
                if (_registration?.Create == null) throw new NotSupportedException("Configure a speech recognition provider to convert voice to text.");
                if (!_prepared) await PrepareAsync(token).ConfigureAwait(false);
                return await ((IMNNConversationAsrProvider)_provider).TranscribeAsync(audio, sampleRate, token).ConfigureAwait(false);
            }
            finally { _gate.Release(); }
        }

        private async Task PrepareAsync(CancellationToken token)
        {
            _provider = _diagnostics.Acquire("transcription", _registration);
            try
            {
                if (!(_provider is IMNNConversationAsrProvider)) throw new InvalidOperationException("The transcription stage must implement IMNNConversationAsrProvider.");
                await _diagnostics.PrepareAsync(_registration, _provider, token).ConfigureAwait(false);
                _prepared = true;
            }
            catch (Exception preparationFailure)
            {
                try { _diagnostics.Dispose(_provider); _provider = null; }
                catch (Exception cleanupFailure)
                {
                    _unusable = true;
                    throw new AggregateException(preparationFailure, cleanupFailure);
                }
                throw;
            }
        }

        public async Task ReleaseAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                _released = true;
                _diagnostics.Dispose(_provider);
                _provider = null;
                _prepared = false;
            }
            finally { _gate.Release(); }
        }
    }
}
