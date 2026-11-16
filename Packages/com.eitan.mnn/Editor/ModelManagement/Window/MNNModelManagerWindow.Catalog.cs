using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public partial class MNNModelManagerWindow
    {
        private async void CheckInstalledModels()
        {
            _installationCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            _installationCancellation = cancellation;
            var token = cancellation.Token;
            // Workers own snapshots. Only the editor thread publishes UI state.
            var originals = _repository.models.ToArray();
            var snapshots = originals.Select(model => model.CopyCatalogEntry()).ToArray();
            try
            {
                await Task.Run(() => ReadInstallations(snapshots, token), token);
                if (_isClosing || token.IsCancellationRequested)
                    return;
                for (int i = 0; i < originals.Length; ++i)
                {
                    originals[i].isInstalled = snapshots[i].isInstalled;
                    originals[i].localPath = snapshots[i].localPath;
                }
                RefreshModelStatus();
                Repaint();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!_isClosing && !token.IsCancellationRequested)
                    _repositoryError = "Unable to check local models: " + error.Message;
            }
            finally
            {
                if (_installationCancellation == cancellation)
                    _installationCancellation = null;
                cancellation.Dispose();
            }
        }

        private static void ReadInstallations(MNNModelInfo[] models, CancellationToken token)
        {
            var installations = MNNModelInstallIndex.Read(MODELS_FOLDER);
            foreach (var model in models)
            {
                token.ThrowIfCancellationRequested();
                var path = GetInstalledPath(model, installations);
                var registration = MNNModelRegistry.Find((MNNModelSource)model.provider, model.repositoryId);
                model.isInstalled = registration != null ? registration.IsComplete(path)
                    : model.repositoryId == MNNMiniMindO.Repository ? MNNMiniMindO.IsComplete(path) : model.isRepository ? Directory.Exists(path) && File.Exists(Path.Combine(path, INSTALL_MARKER)) && MNNModelRepository.MatchesInstallMarker(model, File.ReadAllText(Path.Combine(path, INSTALL_MARKER)).Trim()) : File.Exists(path);
                model.localPath = model.isInstalled ? path : null;
            }
        }

        private void RefreshRepository()
        {
            LoadRepository(true);
        }

        private async void LoadRepository(bool forceRefresh = false)
        {
            if (_isLoadingModels)
                return;
            _isLoadingModels = true;
            var cancellation = new CancellationTokenSource();
            _catalogCancellation = cancellation;
            _repositoryError = null;
            Repaint();
            try
            {
                var errors = new List<string>();
                var refreshed = 0;
                async Task LoadProvider(MNNModelProvider provider)
                {
                    try
                    {
                        var snapshot = await MNNModelCatalogCache.Shared.LoadAsync(provider, forceRefresh);
                        if (_isClosing || cancellation.IsCancellationRequested)
                            return;
                        _repository.models.RemoveAll(model => model.provider == provider);
                        _repository.models.AddRange(snapshot.models);
                        _repository.IncludePublishedProfiles();
                        errors.AddRange(snapshot.warnings);
                        if (snapshot.hasMore || snapshot.nextPage > 0 || snapshot.nextCursor != null) _catalogHasMore = true;
                        ++refreshed;
                        RebuildCategoryOptions();
                        RebuildFamilyOptions();
                        CheckInstalledModels();
                        RefreshModelList();
                        Repaint();
                    }
                    catch (Exception error)
                    {
                        errors.Add(provider + ": " + error.Message);
                    }
                }

                // Independent snapshots prevent one slow or failed hub from blocking the other list.
                await Task.WhenAll(LoadProvider(MNNModelProvider.ModelScope), LoadProvider(MNNModelProvider.HuggingFace));
                if (_isClosing || cancellation.IsCancellationRequested)
                    return;
                if (refreshed == 0)
                    throw new InvalidOperationException(string.Join("\n", errors));
                _repositoryError = errors.Count == 0 ? null : string.Join("\n", errors);
            }
            catch (Exception exception)
            {
                if (_isClosing || cancellation.IsCancellationRequested)
                    return;
                _repositoryError = exception.Message;
                Debug.LogError("Failed to discover MNN models: " + exception);
                MNNEditorUI.ShowErrorDialog("Failed to discover MNN models:\n" + exception.Message);
            }
            finally
            {
                if (_catalogCancellation == cancellation)
                {
                    _catalogCancellation = null;
                    _isLoadingModels = false;
                }

                cancellation.Dispose();
                if (!_isClosing)
                    Repaint();
            }
        }

        private async void LoadMoreRepository()
        {
            if (_loadMorePending || _isLoadingModels || !HasMoreVisibleRepository || _isClosing) return;
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                await LoadSearchRepositoryAsync(_searchQuery.Trim(), _searchCancellation?.Token ?? CancellationToken.None);
                return;
            }
            _loadMorePending = true;
            try
            {
                var snapshots = await Task.WhenAll(
                    MNNModelCatalogCache.Shared.LoadNextAsync(MNNModelProvider.ModelScope),
                    MNNModelCatalogCache.Shared.LoadNextAsync(MNNModelProvider.HuggingFace));
                if (_isClosing) return;
                string selectedId = _selectedModel?.id;
                foreach (var snapshot in snapshots)
                {
                    if (snapshot == null) continue;
                    _repository.models.RemoveAll(model => model.provider == snapshot.provider);
                    _repository.models.AddRange(snapshot.models);
                }
                _repository.IncludePublishedProfiles();
                _catalogHasMore = snapshots.Any(snapshot => snapshot != null && (snapshot.hasMore || snapshot.nextPage > 0 || snapshot.nextCursor != null));
                _selectedModel = _repository.models.Find(model => model.id == selectedId);
                RebuildCategoryOptions();
                RebuildFamilyOptions();
                CheckInstalledModels();
                RefreshModelList();
            }
            catch (Exception error) { if (!_isClosing) _repositoryError = error.Message; }
            finally { _loadMorePending = false; if (!_isClosing) Repaint(); }
        }

        private bool HasMoreVisibleRepository => string.IsNullOrWhiteSpace(_searchQuery) ? _catalogHasMore : _searchReady && (_searchScopePage > 0 || _searchFaceMore);

        private async void SearchOnlineRepository(string value)
        {
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _searchCancellation = null;
            _searchReady = false;
            _searchScopePage = string.IsNullOrWhiteSpace(value) ? 0 : 1;
            _searchFaceCursor = null;
            _searchFaceMore = _searchScopePage > 0;
            if (_searchScopePage == 0)
            {
                RefreshModelList();
                return;
            }
            var source = _searchCancellation = new CancellationTokenSource();
            try
            {
                await Task.Delay(150, source.Token);
                if (_isClosing) return;
                RefreshModelList();
                Repaint();
                await Task.Delay(200, source.Token);
                while (_loadMorePending) await Task.Delay(50, source.Token);
                _searchReady = true;
                await LoadSearchRepositoryAsync(value.Trim(), source.Token);
            }
            catch (OperationCanceledException) { }
        }

        private async Task LoadSearchRepositoryAsync(string query, CancellationToken token)
        {
            if (_loadMorePending || _isClosing) return;
            _loadMorePending = true;
            try
            {
                var models = new List<MNNRemoteModel>();
                var errors = new List<string>();
                int scopeNext = _searchScopePage;
                string faceNext = _searchFaceCursor;
                bool faceMore = _searchFaceMore;
                async Task Scope()
                {
                    if (scopeNext == 0) return;
                    try
                    {
                        var page = await MNNModelManager.SearchModelScopeCatalogPageAsync(0, scopeNext, query, token);
                        lock (models) { models.AddRange(page.Models); scopeNext = page.NextPage; }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception error) { lock (models) errors.Add("ModelScope: " + error.Message); }
                }
                async Task Face()
                {
                    if (!faceMore) return;
                    try
                    {
                        var page = await MNNModelManager.SearchHuggingFaceCatalogPageAsync(0, faceNext, query, token);
                        lock (models) { models.AddRange(page.Models); faceNext = page.NextCursor; faceMore = faceNext != null; }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception error) { lock (models) errors.Add("Hugging Face: " + error.Message); }
                }
                await Task.WhenAll(Scope(), Face());
                token.ThrowIfCancellationRequested();
                if (_isClosing || !string.Equals(query, _searchQuery.Trim(), StringComparison.Ordinal)) return;
                _searchScopePage = scopeNext; _searchFaceCursor = faceNext; _searchFaceMore = faceMore;
                var identities = new HashSet<string>(_repository.models.Select(model => model.provider + ":" + model.repositoryId), StringComparer.OrdinalIgnoreCase);
                foreach (var remote in models)
                {
                    var model = MNNModelRepository.FromRemote(remote);
                    if (identities.Add(model.provider + ":" + model.repositoryId))
                        _repository.models.Add(model);
                }
                _repositoryError = errors.Count == 0 ? null : string.Join("\n", errors);
                RebuildCategoryOptions(); RebuildFamilyOptions(); CheckInstalledModels(); RefreshModelList();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!_isClosing) _repositoryError = error.Message; }
            finally { _loadMorePending = false; if (!_isClosing) Repaint(); }
        }
    }
}
