using System;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;

namespace MNN.Unity
{
    public static partial class MNNConversationLocalProviders
    {
        private abstract class Provider : IMNNConversationProvider, IMNNConversationProviderDiagnostics
        {
            protected MNNBackendType Backend = MNNBackendType.Auto;
            public MNNRealtimeCapabilities Capabilities { get; }
            protected readonly string Directory;
            private string _runtimeBackend = "Not loaded";
            public string RuntimeBackend => Volatile.Read(ref _runtimeBackend);
            protected virtual string ReadBackend() => "Not reported by the managed model API";
            protected void PublishBackend()
            {
                // Called after native work, on its owner thread. UI only reads the cached string.
                try { Volatile.Write(ref _runtimeBackend, ReadBackend()); }
                catch (Exception) { Volatile.Write(ref _runtimeBackend, "Backend unavailable after operation"); }
            }
            protected Provider(MNNRealtimeCapabilities caps, SynchronizationContext unity, string directory)
            { Capabilities = caps; Directory = directory; }
            private readonly object _loadingGate = new object();
            private bool _prepared, _disposed;
            public Task PrepareAsync(CancellationToken token)
            {
                // Serialize acquisition/disposal, including concurrent first preparation.
                // A cancelled waiter must never dispose another caller's prepared model.
                return Task.Run(() =>
                {
                    lock (_loadingGate)
                    {
                        token.ThrowIfCancellationRequested();
                        if (_disposed) throw new ObjectDisposedException(GetType().Name);
                        if (_prepared) return;
                        try
                        {
                            Load();
                            token.ThrowIfCancellationRequested();
                            PublishBackend();
                            _prepared = true;
                        }
                        catch (Exception loadFailure)
                        {
                            try { ReleaseModels(); }
                            catch (Exception cleanupFailure) { throw new AggregateException(loadFailure, cleanupFailure); }
                            throw;
                        }
                    }
                }, token);
            }
            protected abstract void Load();
            protected abstract void ReleaseModels();
            public void Dispose()
            {
                lock (_loadingGate)
                {
                    if (_disposed) return;
                    ReleaseModels();
                    _disposed = true;
                    _prepared = false;
                }
            }
        }
    }
}
