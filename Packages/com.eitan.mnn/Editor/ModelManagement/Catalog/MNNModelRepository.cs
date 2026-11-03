using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>Editor compatibility adapter for the runtime model catalog.</summary>
    [Serializable]
    public partial class MNNModelRepository
    {
        public List<MNNModelInfo> models = new List<MNNModelInfo>();
        internal int NextModelScopePage;
        internal string NextHuggingFaceCursor;
        internal int NextSearchStream;
        internal bool HasMorePages;
        public static readonly string[] FeaturedHuggingFaceRepositories = MNNModelCatalog.FeaturedHuggingFaceRepositories;
        private readonly List<string> _discoveryWarnings = new List<string>();
        public IReadOnlyList<string> DiscoveryWarnings => _discoveryWarnings;
        internal void AddDiscoveryWarnings(IEnumerable<string> warnings) => _discoveryWarnings.AddRange(warnings);

        [Serializable]
        public class ModelScopeFile : MNNModelRepositoryFile { }

        public static MNNModelRepository GetDefaultRepository()
        {
            var repository = new MNNModelRepository();
            repository.IncludePublishedProfiles();
            return repository;
        }

        public Task LoadModelScopeModelsAsync() => LoadModelScopeModelsAsync(null);

        public Task LoadModelScopeModelsAsync(HttpClient httpClient, string apiRoot = null, CancellationToken cancellationToken = default)
            => RefreshAsync(MNNModelProvider.ModelScope, catalog => catalog.LoadModelScopeModelsAsync(httpClient, apiRoot, cancellationToken));

        public Task DiscoverModelScopeModelsAsync(HttpClient httpClient = null, string apiRoot = null, CancellationToken cancellationToken = default)
            => RefreshAsync(MNNModelProvider.ModelScope, catalog => catalog.DiscoverModelScopeModelsAsync(httpClient, apiRoot, cancellationToken));

        public Task LoadHuggingFaceModelsAsync(IEnumerable<string> repositoryIds = null, HttpClient httpClient = null, MNNHuggingFaceMirrorPool mirrorPool = null, CancellationToken cancellationToken = default)
            => RefreshAsync(MNNModelProvider.HuggingFace, catalog => catalog.LoadHuggingFaceModelsAsync(repositoryIds, httpClient, mirrorPool?.RuntimePool, cancellationToken));

        public Task DiscoverHuggingFaceModelsAsync(HttpClient httpClient = null, MNNHuggingFaceMirrorPool mirrorPool = null, CancellationToken cancellationToken = default)
            => RefreshAsync(MNNModelProvider.HuggingFace, catalog => catalog.DiscoverHuggingFaceModelsAsync(httpClient, mirrorPool?.RuntimePool, cancellationToken));

        public async Task<MNNModelInfo> LoadHuggingFaceModelAsync(string repositoryId, HttpClient httpClient, MNNHuggingFaceMirrorPool mirrorPool, CancellationToken cancellationToken = default)
            => FromRemote(await new MNNModelCatalog().LoadHuggingFaceModelAsync(repositoryId, httpClient, mirrorPool?.RuntimePool, cancellationToken));

        private async Task RefreshAsync(MNNModelProvider provider, Func<MNNModelCatalog, Task> refresh)
        {
            var catalog = new MNNModelCatalog();
            await refresh(catalog);
            // Merge only this provider, retaining concurrent refreshes and editor installation state elsewhere.
            models = models.Where(model => model != null && model.provider != provider)
                .Concat(catalog.models.Select(FromRemote)).ToList();
            string prefix = provider == MNNModelProvider.ModelScope ? "ModelScope:" : "HuggingFace:";
            _discoveryWarnings.RemoveAll(warning => warning.StartsWith(prefix, StringComparison.Ordinal));
            _discoveryWarnings.AddRange(catalog.DiscoveryWarnings);
        }

        internal void IncludePublishedProfiles()
        {
            IncludeDtlnAecProfiles();
            IncludeProfiles(catalog => catalog.IncludeCosyVoice300MProfile(), MNNVerifiedTtsModel.IsCosyVoice300MRepository);
            IncludeProfiles(catalog => catalog.IncludePublishedSpeechProfiles(), MNNModelCatalog.IsPublishedSpeechRepository);
        }

        internal void IncludeDtlnAecProfiles() => IncludeProfiles(catalog => catalog.IncludeDtlnAecProfiles(), MNNDtlnAecModel.IsRepository);

        private void IncludeProfiles(Action<MNNModelCatalog> include, Func<string, bool> matches)
        {
            var catalog = new MNNModelCatalog
            {
                models = models.Where(item => item.provider == MNNModelProvider.ModelScope && matches(item.repositoryId))
                    .Select(ToRemote).ToList()
            };
            include(catalog);
            foreach (var model in catalog.models)
            {
                var existing = models.FirstOrDefault(item => item.provider == (MNNModelProvider)model.provider && string.Equals(item.repositoryId, model.repositoryId, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                    models.Add(FromRemote(model));
                else
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(model), existing);
            }
        }

        public List<MNNModelInfo> GetModelsByCategory(MNNModelCategory category)
            => category == MNNModelCategory.All ? models : models.FindAll(model => model.category == category.ToString());

        public List<MNNModelInfo> SearchModels(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return models;
            return models.FindAll(model => MNNModelCatalog.MatchesSearch(query, model.name, model.displayName,
                model.description, model.modelScopeId, model.repositoryId, model.tags, model.tasks, model.category));
        }

        // JSON field names and enum values remain compatible with existing editor caches.
        internal static MNNRemoteModel ToRemote(MNNModelInfo model) => JsonUtility.FromJson<MNNRemoteModel>(JsonUtility.ToJson(model));
        internal static MNNModelInfo FromRemote(MNNRemoteModel model) => JsonUtility.FromJson<MNNModelInfo>(JsonUtility.ToJson(model));
        private static MNNRemoteModel Identity(MNNModelInfo model) => new MNNRemoteModel
        {
            provider = (MNNModelSource)model.provider, repositoryId = model.repositoryId,
            modelScopeId = model.modelScopeId, id = model.id, name = model.name
        };
        internal static string InstallFolderName(MNNModelInfo model) => MNNModelCatalog.InstallFolderName(Identity(model));
        internal static bool MatchesInstallMarker(MNNModelInfo model, string marker) => MNNModelCatalog.MatchesInstallMarker(Identity(model), marker);
        internal static string Classify(string id, string name = null, string description = null, string[] tasks = null, string[] tags = null, string[] paths = null)
            => MNNModelCatalog.Classify(id, name, description, tasks, tags, paths);
        internal static bool HasMnnArtifacts(IEnumerable<string> paths) => MNNModelCatalog.HasMnnArtifacts(paths);
        internal static bool HasMnnRepositoryArtifacts(string id, string[] tags, IEnumerable<string> paths) => MNNModelCatalog.HasMnnRepositoryArtifacts(id, tags, paths);
        internal static bool IsKnownArchiveSearchFalsePositive(string id, IEnumerable<string> paths) => MNNModelCatalog.IsKnownArchiveSearchFalsePositive(id, paths);
    }
}
