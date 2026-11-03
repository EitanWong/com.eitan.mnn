using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity.Editor
{
    // Cache only complete provider snapshots. Installation state belongs to the current project scan.
    internal sealed class MNNModelCatalogCache
    {
        internal const int SchemaVersion = 29; // Specific audio task categories; migrate old cached entries offline.
        internal static MNNModelCatalogCache Shared { get; set; } = new MNNModelCatalogCache(
            Path.GetFullPath("Library/MNN/ModelCatalog"), DiscoverAsync, paged: true);

        [Serializable]
        internal sealed class Snapshot
        {
            public int schema;
            public MNNModelProvider provider;
            public long savedUtcTicks;
            public MNNModelInfo[] models;
            public string[] warnings;
            public int nextPage;
            public string nextCursor;
            public int nextStream;
            public bool hasMore;
            [NonSerialized] internal bool RequiresRefresh;
        }

        private readonly object _gate = new object();
        private readonly string _directory;
        private readonly Func<MNNModelProvider, Task<MNNModelRepository>> _discover;
        private readonly Func<DateTime> _utcNow;
        private readonly TimeSpan _lifetime;
        private readonly bool _paged;
        private readonly Dictionary<MNNModelProvider, Snapshot> _memory = new Dictionary<MNNModelProvider, Snapshot>();
        private readonly Dictionary<MNNModelProvider, Task<Snapshot>> _inflight = new Dictionary<MNNModelProvider, Task<Snapshot>>();

        internal MNNModelCatalogCache(string directory, Func<MNNModelProvider, Task<MNNModelRepository>> discover,
            Func<DateTime> utcNow = null, TimeSpan? lifetime = null, bool paged = false)
        {
            _directory = Path.GetFullPath(directory);
            _discover = discover ?? throw new ArgumentNullException(nameof(discover));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _lifetime = lifetime ?? TimeSpan.FromHours(24);
            _paged = paged;
        }

        internal bool IsFresh(Snapshot snapshot)
        {
            long age = _utcNow().Ticks - snapshot.savedUtcTicks;
            return !snapshot.RequiresRefresh && age >= 0 && age < _lifetime.Ticks;
        }

        // Also returns expired snapshots so the window can display them while updating in the background.
        internal bool TryRead(MNNModelProvider provider, out Snapshot snapshot)
        {
            lock (_gate)
            {
                if (!_memory.TryGetValue(provider, out var stored))
                {
                    // Versions 3 onward share this envelope. Older metadata is display-only
                    // until a complete current refresh succeeds; a version bump must not blank a hub.
                    for (int version = SchemaVersion; version >= 3; --version)
                    {
                        try
                        {
                            string path = Path.Combine(_directory, provider + "-v" + version + ".json");
                            if (!File.Exists(path)) continue;
                            stored = JsonUtility.FromJson<Snapshot>(File.ReadAllText(path));
                            if (stored == null || stored.schema != version)
                                throw new InvalidDataException("Mismatched catalog cache version.");
                            stored.RequiresRefresh = version != SchemaVersion;
                            stored.schema = SchemaVersion;
                            Validate(stored, provider);
                            if (stored.RequiresRefresh)
                                foreach (var model in stored.models)
                                {
                                    string category = MNNModelRepository.Classify(model.repositoryId, model.name, tasks: model.tasks, tags: model.tags, paths: model.mnnFiles);
                                    if (MNNModelCatalog.IsSpecificAudioCategory(category)) model.category = category;
                                }
                            _memory[provider] = stored;
                            break;
                        }
                        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                            error is ArgumentException || error is InvalidDataException)
                        {
                            stored = null; // Try an older compatible snapshot before falling back to discovery.
                        }
                    }
                }
                snapshot = stored == null ? null : Copy(stored);
                return snapshot != null;
            }
        }

        internal Task<Snapshot> LoadAsync(MNNModelProvider provider, bool forceRefresh = false)
        {
            lock (_gate)
            {
                // A reopened manager must subscribe to an already-running forced refresh,
                // even if the old snapshot is still within its TTL. TryRead keeps it visible.
                if (_inflight.TryGetValue(provider, out var running))
                    return CopyWhenCompleteAsync(running);
                if (!forceRefresh && TryRead(provider, out var cached) && IsFresh(cached))
                    return Task.FromResult(cached);
                if (!_inflight.TryGetValue(provider, out var task))
                {
                    var completion = new TaskCompletionSource<Snapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                    task = completion.Task;
                    _inflight[provider] = task;
                    // Start outside the caller's Unity synchronization context; readers never block on network I/O.
                    _ = Task.Run(() => RefreshAsync(provider, completion));
                }
                return CopyWhenCompleteAsync(task);
            }
        }

        internal Task<Snapshot> LoadNextAsync(MNNModelProvider provider)
        {
            lock (_gate)
            {
                if (_inflight.TryGetValue(provider, out var running)) return CopyWhenCompleteAsync(running);
                Snapshot previous = null;
                if (!_paged || !TryRead(provider, out previous) ||
                    (!previous.hasMore && (provider == MNNModelProvider.ModelScope ? previous.nextPage == 0 : previous.nextCursor == null)))
                    return Task.FromResult(previous);
                var completion = new TaskCompletionSource<Snapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                _inflight[provider] = completion.Task;
                _ = Task.Run(() => RefreshAsync(provider, completion, previous, true));
                return CopyWhenCompleteAsync(completion.Task);
            }
        }

        private async Task RefreshAsync(MNNModelProvider provider, TaskCompletionSource<Snapshot> completion, Snapshot previous = null, bool next = false)
        {
            try
            {
                if (_paged && previous == null) TryRead(provider, out previous);
                var repository = next ? await DiscoverPageAsync(provider, previous.nextStream, previous.nextPage, previous.nextCursor).ConfigureAwait(false) : await _discover(provider).ConfigureAwait(false);
                var models = _paged && previous != null ? previous.models.ToDictionary(model => model.id, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, MNNModelInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var model in repository.models) models[model.id] = model;
                var snapshot = new Snapshot
                {
                    schema = SchemaVersion, provider = provider, savedUtcTicks = _utcNow().Ticks,
                    models = models.Values.ToArray(), warnings = repository.DiscoveryWarnings.ToArray(),
                    nextPage = repository.NextModelScopePage, nextCursor = repository.NextHuggingFaceCursor,
                    nextStream = repository.NextSearchStream, hasMore = repository.HasMorePages
                };
                Validate(snapshot, provider);
                snapshot = Copy(snapshot);
                lock (_gate)
                {
                    try
                    {
                        Save(snapshot);
                    }
                    catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                    {
                        snapshot.warnings = snapshot.warnings.Concat(new[] {provider + ": could not save catalog cache: " + error.Message}).ToArray();
                    }
                    _memory[provider] = snapshot;
                    _inflight.Remove(provider);
                    completion.SetResult(snapshot);
                }
            }
            catch (Exception error)
            {
                lock (_gate)
                {
                    _inflight.Remove(provider);
                    completion.SetException(error);
                }
            }
        }

        private void Save(Snapshot snapshot)
        {
            Directory.CreateDirectory(_directory);
            string target = CachePath(snapshot.provider), temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(snapshot));
                if (File.Exists(target))
                    File.Replace(temporary, target, null);
                else
                    File.Move(temporary, target);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private string CachePath(MNNModelProvider provider) => Path.Combine(_directory, provider + "-v" + SchemaVersion + ".json");

        private static void Validate(Snapshot snapshot, MNNModelProvider provider)
        {
            if (snapshot == null || snapshot.schema != SchemaVersion || snapshot.provider != provider ||
                snapshot.savedUtcTicks <= 0 || snapshot.savedUtcTicks > DateTime.MaxValue.Ticks || snapshot.models == null || snapshot.warnings == null)
                throw new InvalidDataException("Invalid model catalog cache header.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var model in snapshot.models)
                if (model == null || model.provider != provider || string.IsNullOrWhiteSpace(model.id) ||
                    string.IsNullOrWhiteSpace(model.repositoryId) || !seen.Add(model.id))
                    throw new InvalidDataException("Invalid or duplicate model catalog cache entry.");
        }

        private static Snapshot Copy(Snapshot source)
        {
            // Strings are immutable. Copy mutable arrays and specs once instead of serializing and parsing
            // the entire catalog on every memory hit; installation state remains private to each reader.
            return new Snapshot
            {
                schema = source.schema, provider = source.provider, savedUtcTicks = source.savedUtcTicks, RequiresRefresh = source.RequiresRefresh,
                nextPage = source.nextPage, nextCursor = source.nextCursor,
                nextStream = source.nextStream, hasMore = source.hasMore,
                warnings = (string[])source.warnings.Clone(),
                models = source.models.Select(model => model.CopyCatalogEntry()).ToArray()
            };
        }

        private static async Task<Snapshot> CopyWhenCompleteAsync(Task<Snapshot> task) => Copy(await task.ConfigureAwait(false));

        private static async Task<MNNModelRepository> DiscoverAsync(MNNModelProvider provider)
        {
            return await DiscoverPageAsync(provider, 0, 1, null).ConfigureAwait(false);
        }

        private static async Task<MNNModelRepository> DiscoverPageAsync(MNNModelProvider provider, int stream, int pageNumber, string cursor)
        {
            var repository = new MNNModelRepository();
            if (provider == MNNModelProvider.ModelScope)
            {
                var page = await MNNModelManager.SearchModelScopeCatalogPageAsync(stream, pageNumber).ConfigureAwait(false);
                repository.models.AddRange(page.Models.Select(MNNModelRepository.FromRemote));
                repository.AddDiscoveryWarnings(page.Warnings);
                repository.NextModelScopePage = page.NextPage;
                repository.NextSearchStream = stream;
                if (page.NextPage == 0 && stream < 2) { repository.NextSearchStream = stream + 1; repository.NextModelScopePage = 1; }
                repository.HasMorePages = repository.NextModelScopePage > 0;
            }
            else
            {
                var page = await MNNModelManager.SearchHuggingFaceCatalogPageAsync(stream, cursor).ConfigureAwait(false);
                repository.models.AddRange(page.Models.Select(MNNModelRepository.FromRemote));
                repository.NextHuggingFaceCursor = page.NextCursor;
                repository.NextSearchStream = page.NextCursor == null && stream < 2 ? stream + 1 : stream;
                repository.HasMorePages = page.NextCursor != null || stream < 2;
            }
            return repository;
        }
    }
}
