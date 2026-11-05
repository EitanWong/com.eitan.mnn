using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Shared metadata cache. Automatic callers reuse disk/memory; only explicit refresh fetches again.</summary>
    public sealed class MNNOnlineModelCache
    {
        [Serializable]
        public class Snapshot
        {
            public int schemaVersion = 3;
            public long updatedUtcTicks;
            public MNNRemoteModel[] models = Array.Empty<MNNRemoteModel>();
            public string[] warnings = Array.Empty<string>();
            public int modelScopeNextPage;
            public string huggingFaceNextCursor;
            public int modelScopeStream, huggingFaceStream;
            public bool huggingFaceHasMore;

        }

        public class Discovery
        {
            public bool Succeeded;
            public MNNRemoteModel[] Models = Array.Empty<MNNRemoteModel>();
            public string[] Warnings = Array.Empty<string>();
            public int ModelScopeNextPage;
            public string HuggingFaceNextCursor;
            public int ModelScopeStream, HuggingFaceStream;
            public bool HuggingFaceHasMore;
        }

        private static readonly Dictionary<string, WeakReference<MNNOnlineModelCache>> Shared = new Dictionary<string, WeakReference<MNNOnlineModelCache>>(StringComparer.Ordinal);
        private readonly object _gate = new object();
        private readonly MNNJsonSettingsStore<Snapshot> _store;
        private readonly Func<CancellationToken, Task<Discovery>> _discover;
        private readonly Func<Snapshot, CancellationToken, Task<Discovery>> _discoverNext;
        private Snapshot _snapshot;
        private Task<Snapshot> _inFlight;
        private readonly Task<Snapshot> _diskReady;
        public Task Ready => _diskReady ?? Task.CompletedTask;
        private bool _attempted;
        private long _nextAutomaticRefreshTicks;
        private string _readWarning;

        public static MNNOnlineModelCache ForPath(string path)
        {
            path = Path.GetFullPath(path);
            lock (Shared)
            {
                foreach (string dead in Shared.Where(pair => !pair.Value.TryGetTarget(out _)).Select(pair => pair.Key).ToArray()) Shared.Remove(dead);
                if (Shared.TryGetValue(path, out var reference) && reference.TryGetTarget(out var cache)) return cache;
                cache = new MNNOnlineModelCache(path, null, null, true);
                Shared[path] = new WeakReference<MNNOnlineModelCache>(cache);
                return cache;
            }
        }

        /// <summary>Opens a private cache synchronously. For UI use ForPath, whose disk snapshot loads in the background; await Ready or LoadAsync.</summary>
        public MNNOnlineModelCache(string path, Func<CancellationToken, Task<Discovery>> discover = null,
            Func<Snapshot, CancellationToken, Task<Discovery>> discoverNext = null) : this(path, discover, discoverNext, false) { }

        private MNNOnlineModelCache(string path, Func<CancellationToken, Task<Discovery>> discover,
            Func<Snapshot, CancellationToken, Task<Discovery>> discoverNext, bool deferDisk)
        {
            _store = new MNNJsonSettingsStore<Snapshot>(path, Validate, 16 * 1024 * 1024);
            _discover = discover ?? DiscoverAsync;
            _discoverNext = discoverNext ?? (discover == null ? DiscoverNextAsync : null);
            if (deferDisk) _diskReady = Task.Run(ReadDisk);
            else ReadDisk();
        }

        private Snapshot ReadDisk()
        {
            Snapshot loaded = null; string warning = null;
            try
            {
                loaded = _store.Load();
                if (loaded != null) loaded.models = loaded.models.Where(model => MNNModelRepositoryIdentity.TryCreate(model, out _)).ToArray();
            }
            catch (Exception error) when (error is InvalidDataException || error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { warning = "Saved model catalog could not be read: " + error.Message; }
            lock (_gate) { _snapshot = loaded; _readWarning = warning; }
            return loaded;
        }

        private async Task<Snapshot> AfterDiskAsync(bool forceRefresh, bool more, CancellationToken token)
        {
            if (token.CanBeCanceled) await WaitAsync(_diskReady, token).ConfigureAwait(false);
            else await _diskReady.ConfigureAwait(false);
            return await (more ? LoadMoreAsync(token) : LoadAsync(forceRefresh, token)).ConfigureAwait(false);
        }

        public Snapshot Read() { lock (_gate) return Copy(_snapshot); }

        public Task<Snapshot> LoadAsync(bool forceRefresh = false, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (_diskReady != null && !_diskReady.IsCompleted) return AfterDiskAsync(forceRefresh, false, token);
            _diskReady?.GetAwaiter().GetResult();
            Task<Snapshot> task;
            lock (_gate)
            {
                if (_inFlight != null) task = _inFlight;
                else if (!forceRefresh && ((_snapshot != null && DateTime.UtcNow.Ticks - _snapshot.updatedUtcTicks < TimeSpan.FromHours(24).Ticks) || (_attempted && DateTime.UtcNow.Ticks < _nextAutomaticRefreshTicks))) task = Task.FromResult(_snapshot);
                else
                {
                    _attempted = true;
                    _nextAutomaticRefreshTicks = DateTime.UtcNow.AddHours(24).Ticks;
                    var completion = new TaskCompletionSource<Snapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                    task = _inFlight = completion.Task;
                    // The shared operation owns its lifetime. A closed panel cancels only its own wait.
                    _ = RefreshAsync(completion, false);
                }
            }
            return PublishAsync(task, token);
        }

        public async Task<Snapshot> LoadCompleteAsync(bool forceRefresh = false, CancellationToken token = default,
            Action<Snapshot> available = null)
        {
            var snapshot = await LoadAsync(forceRefresh, token);
            available?.Invoke(snapshot);
            while (HasMore(snapshot))
            {
                token.ThrowIfCancellationRequested();
                var previous = snapshot;
                snapshot = await LoadMoreAsync(token);
                available?.Invoke(snapshot);
                if (SameContinuation(previous, snapshot)) break;
            }
            return snapshot;
        }

        private static bool HasMore(Snapshot snapshot) => snapshot != null &&
            (snapshot.modelScopeNextPage > 0 || snapshot.huggingFaceHasMore || snapshot.huggingFaceNextCursor != null);

        private static bool SameContinuation(Snapshot a, Snapshot b) => a.modelScopeNextPage == b.modelScopeNextPage &&
            a.modelScopeStream == b.modelScopeStream && a.huggingFaceStream == b.huggingFaceStream &&
            a.huggingFaceNextCursor == b.huggingFaceNextCursor && a.huggingFaceHasMore == b.huggingFaceHasMore;

        public Task<Snapshot> LoadMoreAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (_diskReady != null && !_diskReady.IsCompleted) return AfterDiskAsync(false, true, token);
            _diskReady?.GetAwaiter().GetResult();
            Task<Snapshot> task;
            lock (_gate)
            {
                if (_inFlight != null) task = _inFlight;
                else if (_discoverNext == null || _snapshot == null || (_snapshot.modelScopeNextPage == 0 && !_snapshot.huggingFaceHasMore && _snapshot.huggingFaceNextCursor == null))
                    task = Task.FromResult(_snapshot);
                else
                {
                    var completion = new TaskCompletionSource<Snapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                    task = _inFlight = completion.Task;
                    _ = RefreshAsync(completion, true);
                }
            }
            return PublishAsync(task, token);
        }

        private async Task RefreshAsync(TaskCompletionSource<Snapshot> completion, bool next)
        {
            Snapshot result = null;
            Exception failure = null;
            try { result = await RefreshSnapshotAsync(next).ConfigureAwait(false); }
            catch (Exception error) { failure = error; }
            finally
            {
                lock (_gate)
                {
                    // Publish completion and release the shared operation together. A continuation
                    // may immediately request an explicit retry, including after a processing failure.
                    _inFlight = null;
                    if (failure == null)
                    {
                        _snapshot = result;
                        completion.TrySetResult(result);
                    }
                    else completion.TrySetException(failure);
                }
            }
        }

        private async Task<Snapshot> RefreshSnapshotAsync(bool next)
        {
            Snapshot previous;
            lock (_gate) previous = _snapshot;
            Discovery discovery;
            try
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
                    discovery = next ? await _discoverNext(Copy(previous), timeout.Token).ConfigureAwait(false) : await _discover(timeout.Token).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                discovery = new Discovery { Warnings = new[] { "Online model search failed: " + error.Message } };
            }

            var models = new Dictionary<MNNModelRepositoryIdentity, MNNRemoteModel>();
            if (previous != null) foreach (var model in previous.models)
                    if (MNNModelRepositoryIdentity.TryCreate(model, out var key)) models[key] = model;
            if (discovery.Succeeded) foreach (var model in discovery.Models ?? Array.Empty<MNNRemoteModel>())
                    if (MNNModelRepositoryIdentity.TryCreate(model, out var key)) models[key] = MNNRemoteModelSnapshot.Copy(model);
            var warnings = new List<string>(discovery.Warnings ?? Array.Empty<string>());
            if (!discovery.Succeeded && _readWarning != null) warnings.Add(_readWarning);
            var result = new Snapshot
            {
                updatedUtcTicks = discovery.Succeeded ? DateTime.UtcNow.Ticks : previous?.updatedUtcTicks ?? 0,
                models = models.Values.ToArray(),
                warnings = warnings.ToArray(),
                modelScopeNextPage = discovery.Succeeded ? discovery.ModelScopeNextPage : previous?.modelScopeNextPage ?? 0,
                huggingFaceNextCursor = discovery.Succeeded ? discovery.HuggingFaceNextCursor : previous?.huggingFaceNextCursor,
                modelScopeStream = discovery.Succeeded ? discovery.ModelScopeStream : previous?.modelScopeStream ?? 0,
                huggingFaceStream = discovery.Succeeded ? discovery.HuggingFaceStream : previous?.huggingFaceStream ?? 0,
                huggingFaceHasMore = discovery.Succeeded ? discovery.HuggingFaceHasMore : previous?.huggingFaceHasMore ?? false
            };
            if (discovery.Succeeded)
            {
                try { _store.Save(result); _readWarning = null; }
                catch (Exception error) when (error is InvalidDataException || error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
                {
                    warnings.Add("Model catalog is available for this session, but could not be saved: " + error.Message);
                    result.warnings = warnings.ToArray();
                }
            }
            return result;
        }

        private static async Task<Snapshot> PublishAsync(Task<Snapshot> task, CancellationToken token) =>
            Copy(await (token.CanBeCanceled ? WaitAsync(task, token) : task).ConfigureAwait(false));

        private static Snapshot Copy(Snapshot value) => value == null ? null : new Snapshot
        {
            schemaVersion = value.schemaVersion,
            updatedUtcTicks = value.updatedUtcTicks,
            models = value.models.Select(MNNRemoteModelSnapshot.Copy).ToArray(),
            warnings = (string[])value.warnings.Clone(),
            modelScopeNextPage = value.modelScopeNextPage,
            huggingFaceNextCursor = value.huggingFaceNextCursor,
            modelScopeStream = value.modelScopeStream,
            huggingFaceStream = value.huggingFaceStream,
            huggingFaceHasMore = value.huggingFaceHasMore
        };

        private static async Task<Snapshot> WaitAsync(Task<Snapshot> task, CancellationToken token)
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return await task.ConfigureAwait(false);
            }
        }

        private static async Task<Discovery> DiscoverAsync(CancellationToken token)
        {
            return await FetchPagesAsync(0, 1, 0, null, true, token).ConfigureAwait(false);
        }

        private static Task<Discovery> DiscoverNextAsync(Snapshot previous, CancellationToken token) =>
            FetchPagesAsync(previous.modelScopeStream, previous.modelScopeNextPage, previous.huggingFaceStream,
                previous.huggingFaceNextCursor, previous.huggingFaceHasMore || previous.huggingFaceNextCursor != null, token);

        private static async Task<Discovery> FetchPagesAsync(int scopeStream, int scopePage, int faceStream, string faceCursor, bool faceMore, CancellationToken token)
        {
            var models = new List<MNNRemoteModel>();
            var warnings = new List<string>();
            int nextScope = scopePage;
            string nextFace = faceCursor;
            int nextScopeStream = scopeStream, nextFaceStream = faceStream;
            bool nextFaceMore = faceMore;
            bool succeeded = false;
            async Task Scope()
            {
                if (scopePage == 0) return;
                try
                {
                    var page = await new MNNModelCatalog().FetchCatalogPageAsync(MNNModelSource.ModelScope, scopeStream, scopePage, cancellationToken: token).ConfigureAwait(false);
                    lock (models)
                    {
                        models.AddRange(page.Models); warnings.AddRange(page.Warnings); nextScope = page.NextPage;
                        nextScopeStream = page.NextStream;
                        succeeded = true;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception error) { lock (models) warnings.Add("ModelScope: " + error.Message); }
            }
            async Task Face()
            {
                if (!faceMore) return;
                try
                {
                    var page = await new MNNModelCatalog().FetchCatalogPageAsync(MNNModelSource.HuggingFace, faceStream, cursor: faceCursor, cancellationToken: token).ConfigureAwait(false);
                    lock (models)
                    {
                        models.AddRange(page.Models); nextFace = page.NextCursor;
                        nextFaceStream = page.NextStream;
                        nextFaceMore = page.HasMore;
                        warnings.AddRange(page.Warnings);
                        succeeded = true;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception error) { lock (models) warnings.Add("HuggingFace: " + error.Message); }
            }
            await Task.WhenAll(Scope(), Face()).ConfigureAwait(false);
            return new Discovery
            {
                Succeeded = succeeded,
                Models = models.ToArray(),
                Warnings = warnings.ToArray(),
                ModelScopeNextPage = nextScope,
                HuggingFaceNextCursor = nextFace,
                ModelScopeStream = nextScopeStream,
                HuggingFaceStream = nextFaceStream,
                HuggingFaceHasMore = nextFaceMore
            };
        }

        private static void Validate(Snapshot snapshot)
        {
            if (snapshot.schemaVersion != 3 || snapshot.updatedUtcTicks <= 0 || snapshot.updatedUtcTicks > DateTime.MaxValue.Ticks ||
                snapshot.models == null || snapshot.warnings == null)
                throw new InvalidDataException("Invalid model catalog cache.");
        }
    }
}
