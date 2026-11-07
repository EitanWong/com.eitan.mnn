using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed class MNNConversationProviderMetadata { public string Model, Location, Directory; }
    /// <summary>Tracks acquired instances, including temporary transcription and provider-owned sessions.</summary>
    public sealed class MNNConversationRuntimeDiagnostics
    {
        public sealed class Entry
        {
            public string Stage, Model, Location, Directory, Implementation, State, Backend, Contract, Limitation;
            internal IMNNConversationProvider Provider;
            internal Entry Snapshot()
            {
                var copy = (Entry)MemberwiseClone();
                copy.Provider = null;
                copy.Backend = ReadBackend(Provider, Backend);
                return copy;
            }
        }

        private static string ReadBackend(IMNNConversationProvider provider, string fallback)
        {
            try { return provider is IMNNConversationProviderDiagnostics diagnostics ? diagnostics.RuntimeBackend : fallback; }
            catch (Exception) { return "Unavailable · provider diagnostics failed"; }
        }

        private readonly object _gate = new object();
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Func<string, MNNConversationProviderMetadata> _metadata;
        public MNNConversationRuntimeDiagnostics(Func<string, MNNConversationProviderMetadata> metadata = null) { _metadata = metadata; }
        public Entry[] Read() { lock (_gate) return _entries.Select(entry => entry.Snapshot()).ToArray(); }

        public IMNNConversationProvider Acquire(string stage, MNNConversationStageRegistration registration)
        {
            var endpoint = _metadata?.Invoke(stage) ?? new MNNConversationProviderMetadata();
            var provider = registration?.Create?.Invoke();
            if (provider == null) return null;
            var caps = registration.Capabilities;
            lock (_gate)
            {
                // Keep current instances plus the latest released instance for each stage.
                _entries.RemoveAll(entry => entry.Stage == stage && entry.Provider == null);
                _entries.Add(new Entry
                {
                    Stage = stage,
                    Model = endpoint.Model,
                    Location = caps?.Execution.ToString() ?? endpoint.Location,
                    Directory = endpoint.Directory,
                    Implementation = provider.GetType().FullName,
                    State = "Created · not prepared",
                    Provider = provider,
                    Contract = caps?.Contract,
                    Limitation = caps?.Limitation,
                    Backend = caps?.Execution == MNN.Unity.MNNRealtimeExecution.RemoteApi
                        ? "Remote API · server hardware is not reported" : "Not reported by provider"
                });
            }
            return provider;
        }

        public async Task PrepareAsync(MNNConversationStageRegistration registration, IMNNConversationProvider provider, CancellationToken token)
        {
            if (provider == null) return;
            SetState(provider, "Preparing · downloading / loading");
            try
            {
                await registration.PrepareAsync(provider, token).ConfigureAwait(false);
                SetState(provider, "Ready · prepared instance");
            }
            catch (Exception error)
            {
                SetState(provider, error is OperationCanceledException ? "Preparation cancelled" : "Preparation failed · " + error.GetType().Name);
                throw;
            }
        }

        private void SetState(IMNNConversationProvider provider, string state)
        {
            lock (_gate) foreach (var entry in _entries) if (ReferenceEquals(entry.Provider, provider)) entry.State = state;
        }

        public void Dispose(IMNNConversationProvider provider)
        {
            if (provider == null) return;
            try { provider.Dispose(); }
            catch { SetState(provider, "Release failed · resource state unknown"); throw; }
            lock (_gate)
            {
                foreach (var entry in _entries)
                {
                    if (!ReferenceEquals(entry.Provider, provider)) continue;
                    entry.Backend = ReadBackend(provider, entry.Backend);
                    entry.Provider = null;
                    entry.State = "Released · not loaded (last state: " + entry.State + ")";
                }
            }
        }
    }
}
