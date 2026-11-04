using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity.Editor
{
    /// <summary>One window's replaceable discovery request. Workers own unpublished models;
    /// the Editor thread takes a completed snapshot without waiting for filesystem work.</summary>
    internal sealed class MNNStudioModelDiscovery : IDisposable
    {
        internal sealed class Snapshot
        {
            internal readonly List<MNNStudioModel> Models;
            internal readonly string Error;
            internal Snapshot(List<MNNStudioModel> models, string error = null)
            {
                Models = models;
                Error = error;
            }
        }

        private readonly Func<string[], CancellationToken, Snapshot> _discover;
        private CancellationTokenSource _cancellation;
        private Task<Snapshot> _pending;
        private bool _disposed;
        internal bool IsRunning => _pending != null;
        internal MNNStudioTask WorkspaceTask { get; private set; }

        internal MNNStudioModelDiscovery(Func<string[], CancellationToken, Snapshot> discover = null) =>
            _discover = discover;

        internal void Refresh(string[] roots, MNNStudioTask task = MNNStudioTask.Chat)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MNNStudioModelDiscovery));
            if (roots == null)
                throw new ArgumentNullException(nameof(roots));
            var ownedRoots = (string[])roots.Clone();
            Cancel();
            WorkspaceTask = task;
            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;
            _pending = Task.Run(() => Run(ownedRoots, task, token));
        }

        private Snapshot Run(string[] roots, MNNStudioTask task, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                return _discover != null ? _discover(roots, token) : Discover(roots, task, token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception error)
            {
                // Abandoned requests cannot leave unobserved task exceptions.
                return new Snapshot(null, "Unable to discover local models: " + error.GetBaseException().Message);
            }
        }

        internal bool TryTake(out Snapshot snapshot)
        {
            snapshot = null;
            if (_pending == null || !_pending.IsCompleted)
                return false;
            snapshot = _pending.GetAwaiter().GetResult();
            _pending = null;
            _cancellation.Dispose();
            _cancellation = null;
            return true;
        }

        internal void Cancel()
        {
            var pending = _pending;
            var cancellation = _cancellation;
            _pending = null;
            _cancellation = null;
            if (cancellation == null)
                return;
            cancellation.Cancel();
            // A provider may still be reading the token. Release its source only after
            // the worker finishes, and never wait on the Editor thread during close.
            pending.ContinueWith(_ => cancellation.Dispose(), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Cancel();
        }

        private static Snapshot Discover(string[] roots, MNNStudioTask task, CancellationToken token)
        {
            var errors = new List<string>();
            var models = MNNStudioModel.Discover(roots, errors, token);
            foreach (var model in models)
            {
                token.ThrowIfCancellationRequested();
                model.PrepareForDisplay(task);
            }
            return new Snapshot(models, errors.Count > 0 ? string.Join("\n", errors) : null);
        }
    }
}
