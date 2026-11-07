using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Registered conversation export protocols and identity-indexed metadata. Product stages and UI stay outside this service.</summary>
    public sealed partial class MNNConversationModelCatalog
    {
        [Serializable]
        public sealed class Entry
        {
            public string modelId, repository, revision;
            public MNNConversationModelAdapter adapter;
            public bool online, modelScope, catalogOnly;
            public long bytes;
            public MNNRemoteModel remote;
        }
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Dictionary<MNNModelRepositoryIdentity, Entry> _index = new Dictionary<MNNModelRepositoryIdentity, Entry>();
        private readonly object _sync = new object();
        public IReadOnlyList<Entry> Entries { get { lock (_sync) return _entries.Select(Copy).ToArray(); } }
        private static Entry Copy(Entry entry) => entry == null ? null : new Entry
        {
            modelId = entry.modelId,
            repository = entry.repository,
            revision = entry.revision,
            adapter = entry.adapter,
            online = entry.online,
            modelScope = entry.modelScope,
            catalogOnly = entry.catalogOnly,
            bytes = entry.bytes,
            remote = MNNRemoteModelSnapshot.Copy(entry.remote)
        };
        public Task<int> RefreshAsync(CancellationToken token, HttpClient client = null) => DiscoverAsync(token, client);
        public Entry AddCatalogModel(MNNRemoteModel model)
        {
            var snapshot = MNNRemoteModelSnapshot.Copy(model);
            lock (_sync) return Copy(AddCatalogModelCore(snapshot));
        }
        private Entry AddCatalogModelCore(MNNRemoteModel model)
        {
            if (!MNNModelRepositoryIdentity.TryCreate(model, out var key)) return null;
            if (!_index.TryGetValue(key, out var entry))
            {
                entry = new Entry
                {
                    modelId = model.provider + ":" + (model.repositoryId ?? model.modelScopeId) + "@" + key.Revision,
                    repository = model.repositoryId ?? model.modelScopeId,
                    revision = model.revision,
                    modelScope = model.provider == MNNModelSource.ModelScope
                };
                AddEntry(entry, key);
            }
            entry.catalogOnly = !TryGetAdapter(model, out entry.adapter);
            entry.remote = MNNRemoteModelSnapshot.Copy(model);
            entry.bytes = model.fileSize;
            return entry;
        }
        private void AddEntry(Entry entry) => AddEntry(entry, MNNModelRepositoryIdentity.FromModel(entry.remote));
        private void AddEntry(Entry entry, MNNModelRepositoryIdentity key)
        {
            _entries.Add(entry);
            // Known aliases may share a repository. Keep the first installed alias stable.
            if (!_index.ContainsKey(key)) _index.Add(key, entry);
        }
        public static bool IsCatalogModel(MNNRemoteModel model) => MNNModelRepositoryIdentity.TryCreate(model, out _);
    }
}
