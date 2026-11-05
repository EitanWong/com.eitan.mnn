using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;

namespace MNN.Unity
{
    public sealed class MNNConversationProviderRuntime : IMNNRealtimeSessionRuntime
    {
        private readonly MNNConversationRuntimeDiagnostics _diagnostics;
        private readonly IMNNConversationProvider _provider;
        private readonly MNNConversationStageRegistration _registration;
        private readonly IMNNRealtimeSessionRuntime _inner;
        private bool _innerDisposed, _providerDisposed;
        public MNNConversationProviderRuntime(IMNNConversationProvider provider, MNNConversationStageRegistration registration, IMNNRealtimeSessionRuntime inner, MNNConversationRuntimeDiagnostics diagnostics = null)
        { _diagnostics = diagnostics; _provider = provider; _registration = registration; _inner = inner ?? throw new InvalidOperationException("Provider returned no runtime."); }
        public async Task PrepareAsync(CancellationToken token)
        {
            await (_diagnostics == null ? _registration.PrepareAsync(_provider, token) :
                _diagnostics.PrepareAsync(_registration, _provider, token)).ConfigureAwait(false);
            await _inner.PrepareAsync(token).ConfigureAwait(false);
        }
        public Task StartListeningAsync(CancellationToken token) => _inner.StartListeningAsync(token);
        public Task RequestCancellationAsync(MNNResponseId response) => _inner.RequestCancellationAsync(response);
        public Task WaitForInferenceQuiescenceAsync(MNNResponseId response) => _inner.WaitForInferenceQuiescenceAsync(response);
        public Task RestoreContextAsync(MNNResponseId response) => _inner.RestoreContextAsync(response);
        public Task StopListeningAsync() => _inner.StopListeningAsync();
        public Task WaitForQuiescenceAsync() => _inner.WaitForQuiescenceAsync();
        public async Task DisposeAsync()
        {
            List<Exception> failures = null;
            if (!_innerDisposed)
            {
                try { await _inner.DisposeAsync().ConfigureAwait(false); _innerDisposed = true; }
                catch (Exception error) { (failures ?? (failures = new List<Exception>())).Add(error); }
            }
            if (!_providerDisposed)
            {
                try { if (_diagnostics != null) _diagnostics.Dispose(_provider); else _provider.Dispose(); _providerDisposed = true; }
                catch (Exception error) { (failures ?? (failures = new List<Exception>())).Add(error); }
            }
            if (failures != null) throw new AggregateException("Realtime provider runtime cleanup failed.", failures);
        }
    }
}
