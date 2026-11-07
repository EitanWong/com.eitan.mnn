using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>Retains failed owners until cleanup succeeds. Successful owners are removed before any error is reported.</summary>
    public sealed class MNNConversationProviderCleanup : IDisposable
    {
        private readonly object _gate = new object();
        private readonly List<IMNNConversationProvider> _pending;
        private readonly MNNConversationRuntimeDiagnostics _diagnostics;

        public int PendingProviderCount
        {
            get { lock (_gate) return _pending.Count; }
        }

        internal MNNConversationProviderCleanup(IEnumerable<IMNNConversationProvider> providers,
            MNNConversationRuntimeDiagnostics diagnostics)
        {
            _diagnostics = diagnostics;
            var seen = new HashSet<IMNNConversationProvider>(MNNConversationProviders.Identity);
            _pending = new List<IMNNConversationProvider>();
            foreach (var provider in providers)
            {
                if (provider != null && seen.Add(provider)) _pending.Add(provider);
            }
        }

        /// <summary>Retries only failed providers. May be called again after a failure or successful completion.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                List<Exception> failures = null;
                for (int i = _pending.Count - 1; i >= 0; --i)
                {
                    var provider = _pending[i];
                    try
                    {
                        if (_diagnostics != null) _diagnostics.Dispose(provider);
                        else provider.Dispose();
                        _pending.RemoveAt(i);
                    }
                    catch (Exception error)
                    {
                        (failures ?? (failures = new List<Exception>())).Add(error);
                    }
                }
                if (failures != null) throw new AggregateException("Provider cleanup failed; failed owners remain available for retry.", failures);
            }
        }
    }
}
