using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>A single ModelScope search page. NextPage is zero when the search is complete.</summary>
    public sealed class MNNModelScopePage
    {
        public IReadOnlyList<MNNRemoteModel> Models { get; internal set; }
        public IReadOnlyList<string> Warnings { get; internal set; }
        public int NextPage { get; internal set; }
    }

    /// <summary>A single Hugging Face search page and its opaque continuation URL.</summary>
    public sealed class MNNHuggingFacePage
    {
        public IReadOnlyList<MNNRemoteModel> Models { get; internal set; }
        public string NextCursor { get; internal set; }
    }

    public partial class MNNModelCatalog
    {
        // Union organization, format tag and name searches: neither tags nor names alone are complete.
        private static readonly string[] ModelScopeSearches = {"owner=MNN", "filter.library=mnn", "search=mnn"};
        private static readonly string[] HuggingFaceSearches = {"author=taobao-mnn", "filter=mnn", "search=mnn"};
        private const int ModelScopePageSize = 50;
        private readonly List<string> _discoveryWarnings = new List<string>();
        public IReadOnlyList<string> DiscoveryWarnings => _discoveryWarnings;

        /// <summary>Shared provider search and continuation policy for Editor and Player consumers.</summary>
        public async Task<MNNModelCatalogPage> FetchCatalogPageAsync(MNNModelSource source, int stream = 0,
            int page = 1, string cursor = null, string query = null, HttpClient httpClient = null,
            string modelScopeApiRoot = null, MNNHuggingFaceSourcePool mirrorPool = null, CancellationToken cancellationToken = default)
        {
            bool searching = !string.IsNullOrWhiteSpace(query);
            if (source == MNNModelSource.ModelScope)
            {
                var result = await FetchModelScopeSearchPageAsync(stream, page, query, httpClient,
                    modelScopeApiRoot, cancellationToken).ConfigureAwait(false);
                bool nextStream = !searching && result.NextPage == 0 && stream + 1 < ModelScopeSearches.Length;
                return new MNNModelCatalogPage { Models = result.Models, Warnings = result.Warnings,
                    NextStream = nextStream ? stream + 1 : stream, NextPage = nextStream ? 1 : result.NextPage,
                    HasMore = nextStream || result.NextPage > 0 };
            }
            if (source == MNNModelSource.HuggingFace)
            {
                var result = await FetchHuggingFaceSearchPageAsync(stream, cursor, query, httpClient,
                    mirrorPool, cancellationToken).ConfigureAwait(false);
                bool nextStream = !searching && result.NextCursor == null && stream + 1 < HuggingFaceSearches.Length;
                return new MNNModelCatalogPage { Models = result.Models, NextStream = nextStream ? stream + 1 : stream,
                    NextCursor = result.NextCursor, HasMore = nextStream || result.NextCursor != null };
            }
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        /// <summary>Fetches one Hugging Face page, following a validated continuation only when requested.</summary>
        public async Task<MNNHuggingFacePage> FetchHuggingFacePageAsync(string cursor = null, HttpClient httpClient = null,
            MNNHuggingFaceSourcePool mirrorPool = null, CancellationToken cancellationToken = default)
            => await FetchHuggingFaceSearchPageAsync(2, cursor, null, httpClient, mirrorPool, cancellationToken).ConfigureAwait(false);

        /// <summary>Fetch a page from an official, format-tag, name, or user search stream.</summary>
        public async Task<MNNHuggingFacePage> FetchHuggingFaceSearchPageAsync(int stream, string cursor, string search,
            HttpClient httpClient = null, MNNHuggingFaceSourcePool mirrorPool = null, CancellationToken cancellationToken = default)
        {
            if (stream < 0 || stream >= HuggingFaceSearches.Length) throw new ArgumentOutOfRangeException(nameof(stream));
            var client = httpClient ?? CreateDiscoveryClient();
            var pool = mirrorPool ?? MNNHuggingFaceSourcePool.Shared;
            try
            {
                Exception failure = null;
                foreach (var mirror in pool.GetApiProbeCandidates())
                {
                    if (cursor != null && (!Uri.TryCreate(cursor, UriKind.Absolute, out var uri) ||
                        uri.Scheme != Uri.UriSchemeHttps || uri.GetLeftPart(UriPartial.Authority) != mirror.ApiRoot ||
                        uri.AbsolutePath != "/api/models" || !string.IsNullOrEmpty(uri.UserInfo))) continue;
                    try
                    {
                        string query = string.IsNullOrWhiteSpace(search) ? HuggingFaceSearches[stream] : "search=" + Uri.EscapeDataString(search.Trim());
                        string url = cursor ?? mirror.ApiRoot + "/api/models?" + query + "&limit=50&full=true&sort=lastModified&direction=-1";
                        using (var response = await MNNModelCatalogRequests.Shared.GetAsync(client, url, cancellationToken).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();
                            var body = (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim();
                            if (!body.StartsWith("[", StringComparison.Ordinal) || !body.EndsWith("]", StringComparison.Ordinal))
                                throw new InvalidDataException("Hugging Face returned an invalid search result.");
                            var items = JsonUtility.FromJson<HuggingFaceSearchResponse>("{\"items\":" + body + "}")?.items;
                            if (items == null) throw new InvalidDataException("Hugging Face search omitted its model list.");
                            var models = new List<MNNRemoteModel>();
                            foreach (var item in items)
                            {
                                if (string.IsNullOrWhiteSpace(item?.id) || item.siblings == null)
                                    throw new InvalidDataException("Hugging Face full search omitted repository ID or file evidence.");
                                var paths = item.siblings.Where(file => file?.rfilename != null).Select(file => file.rfilename).ToArray();
                                if (!item.id.StartsWith("taobao-mnn/", StringComparison.OrdinalIgnoreCase) && !HasMnnRepositoryArtifacts(item.id, item.tags, paths)) continue;
                                string name = item.id.Substring(item.id.LastIndexOf('/') + 1);
                                string revision = MNNBertEmbeddingModel.IsRepository(item.id) ? MNNBertEmbeddingModel.Revision(item.id) : string.IsNullOrWhiteSpace(item.sha) ? "main" : item.sha;
                                var model = new MNNRemoteModel
                                {
                                    id = "hf:" + item.id, provider = MNNModelSource.HuggingFace, repositoryId = item.id,
                                    revision = revision, name = name, displayName = name, author = item.author ?? item.id.Split('/')[0], version = revision,
                                    category = Classify(item.id, name, tasks: item.pipeline_tag == null ? null : new[] {item.pipeline_tag}, tags: item.tags),
                                    family = GetFamily(name), generation = GetGeneration(name), description = "Hugging Face " + (item.pipeline_tag ?? "MNN") + " repository",
                                    tags = item.tags ?? Array.Empty<string>(), tasks = item.pipeline_tag == null ? Array.Empty<string>() : new[] {item.pipeline_tag},
                                    parameterCount = InferParameterCount(name), parameterCountInferred = InferParameterCount(name) > 0,
                                    downloadCount = item.downloads, isRepository = true, downloadUrl = mirror.FileRoot + "/" + item.id + "/resolve/" + revision
                                };
                                ApplyArtifactEvidence(model, paths);
                                models.Add(model);
                            }
                            string next = NextHuggingFacePage(response, mirror.ApiRoot);
                            if (cursor != null && next == cursor) throw new InvalidDataException("Hugging Face search repeated a pagination cursor.");
                            pool.ReportSuccess(mirror);
                            return new MNNHuggingFacePage { Models = models, NextCursor = next };
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception error) when (error is HttpRequestException || error is InvalidDataException || error is ArgumentException || error is OperationCanceledException)
                    { failure = error; pool.ReportFailure(mirror); }
                }
                throw new HttpRequestException("No Hugging Face source was available for this page.", failure);
            }
            finally { if (httpClient == null) client.Dispose(); }
        }

        /// <summary>Fetches one page of public MNN repositories without downloading model weights.</summary>
        public async Task<MNNModelScopePage> FetchModelScopePageAsync(int page, HttpClient httpClient = null,
            string apiRoot = null, CancellationToken cancellationToken = default)
            => await FetchModelScopeSearchPageAsync(2, page, null, httpClient, apiRoot, cancellationToken).ConfigureAwait(false);

        /// <summary>Fetch a page from an official, format-tag, name, or user search stream.</summary>
        public async Task<MNNModelScopePage> FetchModelScopeSearchPageAsync(int stream, int page, string search,
            HttpClient httpClient = null, string apiRoot = null, CancellationToken cancellationToken = default)
        {
            if (stream < 0 || stream >= ModelScopeSearches.Length) throw new ArgumentOutOfRangeException(nameof(stream));
            if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
            var client = httpClient ?? CreateDiscoveryClient();
            string root = (apiRoot ?? ApiRoot).TrimEnd('/');
            const int pageSize = 12;
            try
            {
                string query = string.IsNullOrWhiteSpace(search) ? ModelScopeSearches[stream] : "search=" + Uri.EscapeDataString(search.Trim());
                var result = await ReadModelScopePageAsync(query, page, client, root, cancellationToken, pageSize, "last_modified").ConfigureAwait(false);
                var warnings = new List<string>();
                using (var service = new MNNModelDownloader(client, root))
                using (var limit = new SemaphoreSlim(4))
                {
                    var found = await Task.WhenAll(result.models.Select(async item =>
                    {
                        if (string.IsNullOrWhiteSpace(item?.id)) throw new InvalidDataException("ModelScope returned a model without a repository ID.");
                        await limit.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try
                        {
                            MNNModelRepositoryFile[] files;
                            try { files = await service.GetRepositoryFilesAsync(item.id, cancellationToken).ConfigureAwait(false); }
                            catch (MNNModelDownloader.RepositoryUnavailableException error)
                            {
                                lock (warnings) warnings.Add("ModelScope: skipped unavailable repository " + error.Message);
                                return null;
                            }
                            var paths = files.Where(file => file != null && string.Equals(file.Type, "blob", StringComparison.OrdinalIgnoreCase))
                                .Select(file => file.Path).Where(path => path != null).ToArray();
                            if (!item.id.StartsWith("MNN/", StringComparison.OrdinalIgnoreCase) && !HasMnnRepositoryArtifacts(item.id, item.tags, paths)) return null;
                            string name = item.id.Substring(item.id.LastIndexOf('/') + 1);
                            var model = new MNNRemoteModel
                            {
                                id = item.id, provider = MNNModelSource.ModelScope, repositoryId = item.id, modelScopeId = item.id,
                                revision = "master", name = name, displayName = string.IsNullOrWhiteSpace(item.display_name) ? name : item.display_name,
                                category = Categorize(item), family = GetFamily(name), generation = GetGeneration(name), description = item.description,
                                author = item.id.Split('/')[0], version = item.last_modified ?? "master", fileSize = files.Sum(file => Math.Max(0, file?.Size ?? 0)),
                                parameterCount = item.@params > 0 ? item.@params : InferParameterCount(name),
                                parameterCountInferred = item.@params <= 0 && InferParameterCount(name) > 0,
                                downloadCount = item.downloads, tags = item.tags ?? Array.Empty<string>(), tasks = item.tasks ?? Array.Empty<string>(),
                                isRepository = true, downloadUrl = root + "/api/v1/models/" + item.id + "/repo/files?Revision=master&Recursive=true"
                            };
                            ApplyArtifactEvidence(model, paths);
                            return model;
                        }
                        finally { limit.Release(); }
                    })).ConfigureAwait(false);
                    return new MNNModelScopePage
                    {
                        Models = found.Where(model => model != null).ToArray(), Warnings = warnings.ToArray(),
                        NextPage = result.models.Length > 0 && (result.total_count > 0 ? (long)page * pageSize < result.total_count : result.models.Length == pageSize) ? page + 1 : 0
                    };
                }
            }
            finally { if (httpClient == null) client.Dispose(); }
        }

        /// <summary>Search public ModelScope repositories and inspect manifests without downloading weights.</summary>
        public async Task DiscoverModelScopeModelsAsync(HttpClient httpClient = null, string apiRoot = null, CancellationToken cancellationToken = default)
        {
            var client = httpClient ?? CreateDiscoveryClient();
            var root = (apiRoot ?? ApiRoot).TrimEnd('/');
            try
            {
                var warnings = new List<string>();
                var candidates = new Dictionary<string, ModelScopeModel>(StringComparer.OrdinalIgnoreCase);
                var searches = await Task.WhenAll(ModelScopeSearches.Select(query =>
                    SearchModelScopeAsync(query, client, root, cancellationToken))).ConfigureAwait(false);
                foreach (var search in searches)
                    foreach (var item in search)
                        candidates[item.id] = item;

                using (var service = new MNNModelDownloader(client, root))
                using (var limit = new SemaphoreSlim(8))
                {
                    var found = await Task.WhenAll(candidates.Values.Select(async item =>
                    {
                        await limit.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try
                        {
                            MNNModelRepositoryFile[] files;
                            try
                            {
                                files = await service.GetRepositoryFilesAsync(item.id, cancellationToken).ConfigureAwait(false);
                            }
                            catch (MNNModelDownloader.RepositoryUnavailableException error)
                            {
                                lock (warnings)
                                    warnings.Add("ModelScope: skipped unavailable repository " + error.Message);
                                return null;
                            }
                            var paths = files.Where(file => file != null && string.Equals(file.Type, "blob", StringComparison.OrdinalIgnoreCase)).Select(file => file.Path).Where(path => path != null).ToArray();
                            bool official = item.id.StartsWith("MNN/", StringComparison.OrdinalIgnoreCase);
                            if (!official && !HasMnnRepositoryArtifacts(item.id, item.tags, paths))
                                return null;
                            string name = item.id.Substring(item.id.LastIndexOf('/') + 1);
                            var model = new MNNRemoteModel
                            {
                                id = item.id, provider = MNNModelSource.ModelScope, repositoryId = item.id,
                                modelScopeId = item.id, revision = "master", name = name,
                                displayName = string.IsNullOrWhiteSpace(item.display_name) ? name : item.display_name,
                                category = Categorize(item), family = GetFamily(name), generation = GetGeneration(name),
                                description = item.description, author = item.id.Split('/')[0], version = item.last_modified ?? "master",
                                fileSize = files.Sum(file => Math.Max(0, file?.Size ?? 0)),
                                parameterCount = item.@params > 0 ? item.@params : InferParameterCount(name),
                                parameterCountInferred = item.@params <= 0 && InferParameterCount(name) > 0,
                                downloadCount = item.downloads, tags = item.tags ?? Array.Empty<string>(), tasks = item.tasks ?? Array.Empty<string>(),
                                isRepository = true, downloadUrl = root + "/api/v1/models/" + item.id + "/repo/files?Revision=master&Recursive=true"
                            };
                            ApplyArtifactEvidence(model, paths);
                            return model;
                        }
                        finally
                        {
                            limit.Release();
                        }
                    })).ConfigureAwait(false);
                    ReplaceProviderModels(MNNModelSource.ModelScope, found.Where(model => model != null));
                    _discoveryWarnings.RemoveAll(warning => warning.StartsWith("ModelScope:", StringComparison.Ordinal));
                    _discoveryWarnings.AddRange(warnings.OrderBy(warning => warning, StringComparer.Ordinal));
                }
            }
            finally
            {
                if (httpClient == null)
                    client.Dispose();
            }
        }

        private static async Task<ModelScopeModel[]> SearchModelScopeAsync(string query, HttpClient client, string root, CancellationToken cancellationToken)
        {
            var found = new Dictionary<string, ModelScopeModel>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var first = await ReadModelScopePageAsync(query, 1, client, root, cancellationToken).ConfigureAwait(false);
            MergeModelScopePage(first, found, seen);
            if (first.total_count > 0)
            {
                if (first.models.Length == 0)
                    throw new InvalidDataException("ModelScope search ended before its advertised total.");
                if (first.models.Length < ModelScopePageSize)
                {
                    await ReadSequentialModelScopePagesAsync(query, client, root, first.total_count, found, seen, cancellationToken).ConfigureAwait(false);
                    return found.Values.ToArray();
                }
                using (var limit = new SemaphoreSlim(2))
                {
                    var pages = await Task.WhenAll(Enumerable.Range(2, (first.total_count - 1) / ModelScopePageSize).Select(async page =>
                    {
                        await limit.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try { return await ReadModelScopePageAsync(query, page, client, root, cancellationToken).ConfigureAwait(false); }
                        finally { limit.Release(); }
                    })).ConfigureAwait(false);
                    foreach (var page in pages)
                    {
                        int previous = seen.Count;
                        MergeModelScopePage(page, found, seen);
                        // Download rankings can move between pages; only a wholly repeated page is invalid.
                        if (page.models.Length == 0 || seen.Count == previous)
                            throw new InvalidDataException("ModelScope search pagination repeated or ended before its advertised total.");
                    }
                }
            }
            else if (first.models.Length > 0)
                await ReadSequentialModelScopePagesAsync(query, client, root, 0, found, seen, cancellationToken).ConfigureAwait(false);
            return found.Values.ToArray();
        }

        private static async Task ReadSequentialModelScopePagesAsync(string query, HttpClient client, string root, int totalCount,
            Dictionary<string, ModelScopeModel> found, HashSet<string> seen, CancellationToken cancellationToken)
        {
            for (var page = 2; totalCount <= 0 || seen.Count < totalCount; ++page)
            {
                var result = await ReadModelScopePageAsync(query, page, client, root, cancellationToken).ConfigureAwait(false);
                int previous = seen.Count;
                MergeModelScopePage(result, found, seen);
                if (result.models.Length == 0 && totalCount <= 0)
                    return;
                if (seen.Count == previous)
                    throw new InvalidDataException("ModelScope search pagination repeated or ended before its advertised total.");
            }
        }

        private static async Task<ModelSearchData> ReadModelScopePageAsync(string query, int page, HttpClient client, string root, CancellationToken cancellationToken, int pageSize = ModelScopePageSize, string sort = "downloads")
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var response = await MNNModelCatalogRequests.Shared.GetAsync(client, $"{root}/openapi/v1/models?{query}&page_size={pageSize}&sort={sort}&page_number={page}", cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var result = JsonUtility.FromJson<ModelSearchResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                if (result == null || !result.success || result.data?.models == null)
                    throw new InvalidDataException("ModelScope returned an invalid search result.");
                return result.data;
            }
        }

        private static void MergeModelScopePage(ModelSearchData page, Dictionary<string, ModelScopeModel> found, HashSet<string> seen)
        {
            foreach (var item in page.models)
            {
                if (string.IsNullOrWhiteSpace(item?.id))
                    throw new InvalidDataException("ModelScope returned a model without a repository ID.");
                seen.Add(item.id);
                found[item.id] = item;
            }
        }

        /// <summary>Search all public HF owners. Follow Link pagination and publish only a complete refresh.</summary>
        public async Task DiscoverHuggingFaceModelsAsync(HttpClient httpClient = null, MNNHuggingFaceSourcePool mirrorPool = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var client = httpClient ?? CreateDiscoveryClient();
            var pool = mirrorPool ?? MNNHuggingFaceSourcePool.Shared;
            try
            {
                var candidates = new Dictionary<string, MNNRemoteModel>(StringComparer.OrdinalIgnoreCase);
                // Search streams keep their own ordered cursor chain; independent queries and curated profiles overlap.
                var searches = HuggingFaceSearches.Select(query => SearchHuggingFaceAsync(query, client, pool, cancellationToken));
                var featured = FeaturedHuggingFaceRepositories.Select(async id => new List<MNNRemoteModel>
                {
                    await LoadHuggingFaceModelAsync(id, client, pool, cancellationToken).ConfigureAwait(false)
                });
                var batches = await Task.WhenAll(searches.Concat(featured)).ConfigureAwait(false);
                foreach (var batch in batches)
                    foreach (var model in batch)
                        candidates[model.id] = model;
                ReplaceProviderModels(MNNModelSource.HuggingFace, candidates.Values);
            }
            finally
            {
                if (httpClient == null)
                    client.Dispose();
            }
        }

        private async Task<List<MNNRemoteModel>> SearchHuggingFaceAsync(string query, HttpClient client, MNNHuggingFaceSourcePool pool, CancellationToken cancellationToken)
        {
            Exception failure = null;
            foreach (var mirror in pool.GetApiProbeCandidates())
            {
                try
                {
                    var found = new List<MNNRemoteModel>();
                    var pages = new HashSet<string>(StringComparer.Ordinal);
                    string url = mirror.ApiRoot + "/api/models?" + query + "&limit=500&full=true";
                    while (url != null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!pages.Add(url))
                            throw new InvalidDataException("Hugging Face search repeated a pagination cursor.");
                        using (var response = await MNNModelCatalogRequests.Shared.GetAsync(client, url, cancellationToken).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();
                            var body = (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim();
                            if (!body.StartsWith("[", StringComparison.Ordinal) || !body.EndsWith("]", StringComparison.Ordinal))
                                throw new InvalidDataException("Hugging Face returned an invalid search result.");
                            var items = JsonUtility.FromJson<HuggingFaceSearchResponse>("{\"items\":" + body + "}")?.items;
                            if (items == null)
                                throw new InvalidDataException("Hugging Face search omitted its model list.");
                            foreach (var item in items)
                            {
                                if (string.IsNullOrWhiteSpace(item?.id) || item.siblings == null)
                                    throw new InvalidDataException("Hugging Face full search omitted repository ID or file evidence.");
                                var paths = item.siblings.Where(file => file?.rfilename != null).Select(file => file.rfilename).ToArray();
                                if (!item.id.StartsWith("taobao-mnn/", StringComparison.OrdinalIgnoreCase) && !HasMnnRepositoryArtifacts(item.id, item.tags, paths))
                                    continue;
                                string name = item.id.Substring(item.id.LastIndexOf('/') + 1);
                                string revision = MNNBertEmbeddingModel.IsRepository(item.id) ? MNNBertEmbeddingModel.Revision(item.id) : string.IsNullOrWhiteSpace(item.sha) ? "main" : item.sha;
                                var model = new MNNRemoteModel
                                {
                                    id = "hf:" + item.id, provider = MNNModelSource.HuggingFace, repositoryId = item.id,
                                    revision = revision, name = name, displayName = name,
                                    author = item.author ?? item.id.Split('/')[0], version = revision,
                                    category = Classify(item.id, name, tasks: item.pipeline_tag == null ? null : new[] {item.pipeline_tag}, tags: item.tags),
                                    family = GetFamily(name), generation = GetGeneration(name),
                                    description = "Hugging Face " + (item.pipeline_tag ?? "MNN") + " repository",
                                    tags = item.tags ?? Array.Empty<string>(), tasks = item.pipeline_tag == null ? Array.Empty<string>() : new[] {item.pipeline_tag},
                                    parameterCount = InferParameterCount(name), parameterCountInferred = InferParameterCount(name) > 0,
                                    downloadCount = item.downloads, isRepository = true,
                                    downloadUrl = mirror.FileRoot + "/" + item.id + "/resolve/" + revision
                                };
                                ApplyArtifactEvidence(model, paths);
                                found.Add(model);
                            }
                            url = NextHuggingFacePage(response, mirror.ApiRoot);
                        }
                    }
                    pool.ReportSuccess(mirror);
                    return found;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception error) when (error is HttpRequestException || error is InvalidDataException || error is ArgumentException || error is OperationCanceledException)
                {
                    failure = error;
                    pool.ReportFailure(mirror);
                }
            }
            throw new HttpRequestException("No complete Hugging Face search source was available for " + query + ".", failure);
        }

        private static string NextHuggingFacePage(HttpResponseMessage response, string apiRoot)
        {
            if (!response.Headers.TryGetValues("Link", out var links))
                return null;
            foreach (var header in links)
                foreach (Match match in Regex.Matches(header, "<([^>]+)>\\s*;\\s*rel=\"?next\"?", RegexOptions.IgnoreCase))
                {
                    var uri = new Uri(new Uri(apiRoot), match.Groups[1].Value);
                    if (uri.Scheme != Uri.UriSchemeHttps || uri.GetLeftPart(UriPartial.Authority) != apiRoot || uri.AbsolutePath != "/api/models" || !string.IsNullOrEmpty(uri.UserInfo))
                        throw new InvalidDataException("Hugging Face pagination left the selected API origin or model search endpoint.");
                    return uri.AbsoluteUri;
                }
            return null;
        }

        internal static bool HasMnnArtifacts(IEnumerable<string> paths) => paths.Any(path => path != null &&
            (path.EndsWith(".mnn", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mnn.weight", StringComparison.OrdinalIgnoreCase) ||
            HasMnnLabel(path, null) && IsArchive(path)));

        internal static bool HasMnnRepositoryArtifacts(string id, string[] tags, IEnumerable<string> paths) => HasMnnArtifacts(paths) ||
            !IsKnownArchiveSearchFalsePositive(id, paths) && HasMnnLabel(id, tags) && paths.Any(IsArchive);

        internal static bool IsKnownArchiveSearchFalsePositive(string id, IEnumerable<string> paths)
        {
            // Its public card defines MNN as Multi-Head Attention Network; Flickr8k_text.zip
            // is dataset text, not an Alibaba MNN model. Re-admit it if actual MNN artifacts appear.
            return string.Equals(id, "ritzz01/cnn-mnn-model", StringComparison.OrdinalIgnoreCase) && !HasMnnArtifacts(paths);
        }

        private static bool HasMnnLabel(string id, string[] tags) => Regex.IsMatch(id ?? "", @"(?<![A-Za-z])mnn(?![A-Za-z])", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(id ?? "", @"(?<![A-Za-z])MNN") ||
            (tags ?? Array.Empty<string>()).Any(tag => string.Equals(tag, "mnn", StringComparison.OrdinalIgnoreCase) || string.Equals(tag, "library:mnn", StringComparison.OrdinalIgnoreCase));

        private static bool IsArchive(string path) => path != null && new[] {".zip", ".tar.gz", ".tgz", ".7z"}.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

        private static void ApplyArtifactEvidence(MNNRemoteModel model, string[] paths)
        {
            var files = new HashSet<string>(paths, StringComparer.Ordinal);
            bool hasGraph = paths.Any(path => path.EndsWith(".mnn", StringComparison.OrdinalIgnoreCase));
            model.mnnFiles = paths.Where(path => HasMnnArtifacts(new[] {path}) || HasMnnLabel(model.repositoryId, model.tags) && IsArchive(path)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            model.category = Classify(model.repositoryId ?? model.id, model.name, model.description, model.tasks, model.tags, paths);
            // A graph is not evidence of a complete tokenizer, preprocessing or task decoder.
            model.runtimeAvailable = model.category != MNNModelCatalogCategory.SpeechRecognition.ToString() && model.category != MNNModelCatalogCategory.SpeechSynthesis.ToString() && files.Contains("llm_config.json") && (files.Contains("tokenizer.txt") || files.Contains("tokenizer.mtok")) && hasGraph ||
                files.Contains("mnn_models/tts.json") && files.Contains("mnn_models/unicode_indexer.json") ||
                new[] {"text_encoder.mnn", "unet.mnn", "vae_decoder.mnn"}.All(file => files.Contains(file) || files.Contains("general/" + file)) ||
                files.Contains("llm/meta_queries.mnn") && files.Contains("transformer.mnn") && files.Contains("vae_decoder.mnn") ||
                files.Contains("common/mnn_models/chinese_bert.mnn") || paths.Any(path => path.EndsWith("_fp16_public.mnn", StringComparison.Ordinal));
            model.supportSummary = model.mnnFiles.Length == 0 ? "Official auxiliary resource; no MNN graph or MNN archive found."
                : model.runtimeAvailable ? "Files for an existing package pipeline found. Architecture and operator compatibility are checked when loading; this repository has not been inference-validated by this catalog."
                : !hasGraph && paths.Any(IsArchive) ? "MNN-labeled archive candidate. Archive contents have not been inspected; extract and verify the model files before use."
                : "MNN artifacts found. Use the matching task pipeline or graph API; nested variants may require preparation. Repository discovery does not verify inference compatibility.";
            if (model.provider == MNNModelSource.ModelScope && MNNDocumentLayoutModel.IsRepository(model.repositoryId))
            {
                model.category = MNNModelCatalogCategory.DocumentLayout.ToString();
                model.revision = model.version = MNNDocumentLayoutModel.Revision;
                model.fileSize = MNNDocumentLayoutModel.Sizes.Sum();
                model.runtimeAvailable = true;
                model.supportSummary = "Pinned PP-DocLayoutV3 export: 25 region classes and predicted reading order. Shared Runtime and Studio page; no OCR or table-cell decoding. Each platform requires actual-weight validation.";
            }
            if (model.provider == MNNModelSource.HuggingFace && MNNBertEmbeddingModel.IsRepository(model.repositoryId))
            {
                model.category = MNNModelCatalogCategory.EmbeddingModel.ToString();
                model.revision = model.version = MNNBertEmbeddingModel.Revision(model.repositoryId);
                model.fileSize = MNNBertEmbeddingModel.Files(model.repositoryId).Sum(file => file.Bytes);
                model.runtimeAvailable = true;
                model.supportSummary = "BERT WordPiece embedding profile with pinned graph/tokenizer files. Installs only the required MNN assets and creates the managed pooling configuration.";
            }
            if (model.provider == MNNModelSource.ModelScope && MNNVerifiedTtsModel.IsRepository(model.repositoryId) ||
                model.provider == MNNModelSource.HuggingFace && MNNVerifiedTtsModel.IsHuggingFaceRepository(model.repositoryId))
            {
                bool huggingFace = model.provider == MNNModelSource.HuggingFace;
                var profile = huggingFace ? MNNVerifiedTtsModel.HuggingFaceFiles(model.repositoryId) : MNNVerifiedTtsModel.Files(model.repositoryId);
                model.category = MNNModelCatalogCategory.SpeechSynthesis.ToString();
                model.fileSize = profile.Sum(file => file.Bytes);
                if (huggingFace)
                    model.revision = model.version = MNNVerifiedTtsModel.ResolveHuggingFaceRevision(model.repositoryId, null);
                model.runtimeAvailable = true;
                model.supportSummary = MNNVerifiedTtsModel.IsIndicF5Repository(model.repositoryId)
                    ? "IndicF5 reference speech, 24 kHz mono. Pinned 7-file DiT INT8 / dynamic Vocos FP32 plan with matching official-blob vocabulary. Reference WAV plus exact transcript required; 12 seconds reference / 2048 total mel frames. Public serialized positions adapted in memory, CPU / macOS Mono ABI. Hindi content checked; Tamil strict content checks failed with near-phonetic transcription errors. Other languages remain unverified. Not realtime."
                    : MNNVerifiedTtsModel.IsPocketRepository(model.repositoryId)
                    ? "Pocket-TTS English reference speech, 24 kHz mono. Pinned 11-file plan; all five MNN stages run without ONNX Runtime. Managed SentencePiece unigram, restored encoder Shape slices and specialized priming; CPU / macOS Mono ABI. Reference WAV required, at most 50 tokens / 30 seconds. Gated predefined voices are unavailable."
                    : MNNVerifiedTtsModel.IsKokoroExpandedRepository(model.repositoryId)
                    ? "Kokoro v1.1 expanded export, Mandarin / US English lexicons and 24 kHz mono. Exposes the 103 named prefix voices; 53 appended unnamed tables and other language frontends are unavailable. Pinned graph, external weights, voice table and lexicons; CPU / macOS Mono ABI. 103 waveforms checked; six previously problematic voices still fail strict content checks. Other voice/language quality is not fully validated."
                    : MNNVerifiedTtsModel.IsBertVits2Repository(model.repositoryId)
                    ? "Bert-VITS2 Chenxi Chinese / English / mixed-text profile, 44.1 kHz mono. Pinned graph, external weights and full matched frontend assets; inference files are byte-identical to the checked official export. CPU / macOS Mono ABI."
                    : MNNVerifiedTtsModel.IsKokoroSingleRepository(model.repositoryId)
                    ? "Kokoro v1.0 single zf_xiaoxiao voice, matched by exact voice graph hash despite the empty publisher style list. Pinned graph, tokenizer and Mandarin / US English lexicons; 24 kHz mono, CPU / macOS Mono ABI."
                    : MNNVerifiedTtsModel.IsPiperLessacRepository(model.repositoryId)
                    ? "Piper Lessac high English voice, 22.05 kHz mono. Pinned graph, voice metadata and matching English eSpeak-NG data; external eSpeak-NG executable required. CPU / macOS Mono ABI. Dataset terms are preserved in MODEL_CARD."
                    : MNNVerifiedTtsModel.IsQwen3ExplicitRepository(model.repositoryId)
                    ? "Qwen3-TTS explicit KV pipeline, Chinese / English and bundled prepared demo reference voice; 24 kHz mono. Pinned 47-file plan, CPU / macOS Mono ABI. No raw-WAV voice encoder in this export. Reuses native stages and streams text embedding rows from disk."
                    : MNNVerifiedTtsModel.IsDhVaaniRepository(model.repositoryId)
                    ? "DhVaani v1.1 Hindi / English reference speech pipeline. Pinned MNN modules, character vocabulary and managed Vocos frontend/head; 24 kHz mono. Reference WAV and exact transcript required. CPU / macOS Mono ABI; write numbers as spoken words."
                    : MNNVerifiedTtsModel.IsMossRepository(model.repositoryId)
                    ? "MOSS Nano 100M, 18 builtin reference voices, SentencePiece BPE and fixed sampled-frame or cached-step autoregressive generation. Pinned stages, CPU / macOS Mono ABI, mono 48 kHz output. Plain text frontend; write numbers as spoken words. Voice and language quality checks are documented separately."
                    : MNNVerifiedTtsModel.IsSherpaVitsRepository(model.repositoryId)
                    ? "Chinese Sherpa VITS, five speakers checked with independent ASR, 16 kHz audio and speed control. Pinned graph, external weights and Bopomofo lexicon; CPU / macOS Mono ABI. No Jieba or FST normalization: write numbers as Chinese words; polyphones follow the lexicon."
                    : MNNVerifiedTtsModel.IsMmsTamilRepository(model.repositoryId)
                    ? "MMS Tamil VITS INT8 / FP16-INT8 / FP32 profiles, character tokenizer, 16 kHz audio and speed control. Pinned graphs and tokens; CPU / macOS Mono ABI. Tamil greeting checked with independent Whisper; full sentence quality remains limited."
                    : MNNVerifiedTtsModel.IsIndicVitsRepository(model.repositoryId)
                    ? "Indic VITS character frontend, 20 documented speakers and emotion controls. Installs the pinned q8 graph and tokenizer; Tamil speech was checked with independent ASR. Other speakers require language-specific quality evaluation. CPU / macOS Mono ABI."
                    : MNNVerifiedTtsModel.IsKokoro11Repository(model.repositoryId)
                        ? "Kokoro v1.1 Mandarin / US English lexicon frontend, 103 voice tables and 24 kHz audio. Pinned graph and original external weights; CPU / macOS Mono ABI. Voice content quality evaluation is in progress."
                    : model.repositoryId.IndexOf("kokoro", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "Kokoro Mandarin / US English lexicon frontend, 24 kHz speech and installed voice tables. Pinned graph, matching lexicons and licenses; CPU / macOS Mono ABI. Other language frontends are not implemented."
                        : "Supertonic 3 INT8 pipeline with all 10 voices, versioned language tags and 44.1 kHz audio. Pinned stage graphs and styles; CPU / macOS Mono ABI. All voices checked in English; 31-language quality is not fully evaluated.";
            }
            if (model.provider == MNNModelSource.HuggingFace && MNNPaddleTextModel.IsRepository(model.repositoryId))
            {
                model.category = MNNModelCatalogCategory.OCRModel.ToString();
                model.revision = model.version = MNNPaddleTextModel.Revision;
                model.fileSize = MNNPaddleTextModel.Files().Sum(file => file.Bytes);
                model.runtimeAvailable = true;
                model.supportSummary = "Pinned PP-OCRv5/v4 page OCR: twelve language/script profiles plus Chinese v4 and fp16 variants with matched detectors and dictionaries. Studio returns text regions and annotations. Auto pages and Korean lines select CPU; documented text accuracy limits apply. Orientation classification is not included.";
            }
            if (model.provider == MNNModelSource.ModelScope && string.Equals(model.repositoryId, MNNSenseVoiceModel.Repository, StringComparison.OrdinalIgnoreCase) ||
                model.provider == MNNModelSource.HuggingFace && MNNSenseVoiceModel.IsHuggingFaceRepository(model.repositoryId))
            {
                model.category = MNNModelCatalogCategory.SpeechRecognition.ToString();
                bool huggingFace = model.provider == MNNModelSource.HuggingFace;
                model.fileSize = (huggingFace ? MNNSenseVoiceModel.HuggingFaceSizes : MNNSenseVoiceModel.Sizes).Sum();
                if (huggingFace) model.revision = model.version = MNNSenseVoiceModel.HuggingFaceRevision;
                model.runtimeAvailable = true;
                model.supportSummary = "Verified SenseVoiceSmall offline greedy CTC profile. Installation checks graph, any external weights and token hashes. Studio uses CPU; Metal lost words in validation. 85 ms to 60 seconds per recording. Model supports zh/en/ja/ko/yue; only English and Chinese/English samples have been checked.";
            }
            if (MNNZipformerModel.IsRepository(model.repositoryId))
            {
                model.category = MNNModelCatalogCategory.SpeechRecognition.ToString();
                model.fileSize = MNNZipformerModel.Files(MNNZipformerModel.IsBilingual(model.repositoryId)).Sum(file => file.Bytes);
                model.runtimeAvailable = true;
                if (model.provider == MNNModelSource.HuggingFace)
                    model.revision = model.version = MNNZipformerModel.HuggingFaceRevision(model.repositoryId);
                model.supportSummary = "Zipformer v1 greedy ASR profile. Installation verifies the tested graph/token hashes and prepares a Studio profile. English and Chinese/English; acoustic recognition errors remain possible.";
            }
            if (model.provider == MNNModelSource.HuggingFace && MNNWhisperModel.IsRepository(model.repositoryId) ||
                model.provider == MNNModelSource.ModelScope && MNNWhisperModel.IsModelScopeRepository(model.repositoryId))
            {
                model.category = MNNModelCatalogCategory.SpeechRecognition.ToString();
                var profile = MNNWhisperModel.ForRepository(model.repositoryId);
                model.revision = model.version = profile.ModelScope ? "master" : profile.Revision;
                model.fileSize = profile.Sizes.Sum();
                model.runtimeAvailable = true;
                model.supportSummary = "Pinned Whisper encoder/decoder profile. Installer verifies graph/token hashes and prepares CPU greedy transcription. All five profiles support English; small also exposes verified zh decoding. tiny/base Chinese quality is not enabled. Input 25 ms to 29.5 seconds; no timestamp alignment.";
            }
            if (model.provider == MNNModelSource.HuggingFace && MNNQwen3AsrModel.IsRepository(model.repositoryId) ||
                model.provider == MNNModelSource.ModelScope && MNNQwen3AsrModel.IsModelScopeRepository(model.repositoryId))
            {
                model.category = MNNModelCatalogCategory.SpeechRecognition.ToString();
                model.revision = model.version = model.provider == MNNModelSource.ModelScope ? "master" : MNNQwen3AsrModel.Revision;
                model.fileSize = MNNQwen3AsrModel.Sizes.Sum() + (model.provider == MNNModelSource.ModelScope ? 1456 : 11357);
                model.runtimeAvailable = true;
                model.supportSummary = "Verified Qwen3-ASR 0.6B INT8 audio/LLM asset plan with per-file hashes and immutable revisions. Metal/CPU greedy English and Chinese/English transcription; Auto prefers quality-checked Metal with CPU fallback. 0.1–30 second WAV input, no timestamp alignment. Cancellation between decoder tokens; audio encoding/prefill remain synchronous.";
            }
            if (model.provider == MNNModelSource.HuggingFace && MNNParaformerModel.IsHuggingFaceRepository(model.repositoryId) ||
                model.provider == MNNModelSource.ModelScope && MNNParaformerModel.IsModelScopeRepository(model.repositoryId))
            {
                bool modelScope = model.provider == MNNModelSource.ModelScope;
                model.category = MNNModelCatalogCategory.SpeechRecognition.ToString();
                model.revision = model.version = MNNParaformerModel.ResolveRevision(null, model.repositoryId, modelScope);
                model.fileSize = MNNParaformerModel.Sizes.Sum() + (modelScope ? 664 : 780);
                model.runtimeAvailable = true;
                model.supportSummary = "Pinned Paraformer Chinese online 8404-token encoder/decoder assets. Installer verifies hashes and prepares the Chinese greedy ASR profile; CPU, 85 ms to ten minutes, no timestamps. Bilingual speech can contain errors; English quality is not validated. The omitted encoder length is supplied through the public serialized MNN schema; encoder features and CIF weights run together once per chunk.";
            }
            if (model.provider == MNNModelSource.HuggingFace && MNNSmartTurnModel.IsRepository(model.repositoryId))
            {
                model.category = MNNModelCatalogCategory.TurnDetection.ToString();
                model.revision = model.version = MNNSmartTurnModel.Revision;
                model.fileSize = MNNSmartTurnModel.Bytes + 265;
                model.runtimeAvailable = true;
                model.supportSummary = "Pinned Smart Turn v3.2 quantized graph with SHA-256 verification. Predict whether the current audio turn has ended. Normalized mono WAV/PCM at 8–96 kHz, up to ten minutes; evaluates only the last eight seconds, prepads shorter recordings and normalizes before Whisper features. Probability > 0.5 means complete; no transcript or speech intervals. CPU default; quantized probabilities can differ from float ONNX.";
            }
            if (model.provider == MNNModelSource.ModelScope && MNNVerifiedTtsModel.IsCosyVoice300MRepository(model.repositoryId))
            {
                model.revision = model.version = MNNVerifiedTtsModel.CosyVoice300MRevision;
                model.supportSummary = "CosyVoice-300M first-generation / 22050 Hz mono, built-in reference voice. Pinned FP16 full-prompt compact AR + ten-step Flow plan. Unity macOS M5 hot RTF about1.52; new lengths compile, shape caches increase memory. Not RTF<0.3; other platforms and broad speech quality remain unverified.";
            }
            if (model.provider == MNNModelSource.ModelScope && MNNDtlnAecModel.IsRepository(model.repositoryId))
            {
                int units = MNNDtlnAecModel.Units(model.repositoryId);
                model.category = MNNModelCatalogCategory.EchoCancellation.ToString();
                model.revision = model.version = MNNDtlnAecModel.Revision(units);
                model.fileSize = MNNDtlnAecModel.Sizes(units).Sum() + 1075;
                model.runtimeAvailable = files.Contains(MNNDtlnAecModel.Graph(units, 1)) && files.Contains(MNNDtlnAecModel.Graph(units, 2));
                model.supportSummary = "DTLN AEC " + units + ": two pinned FP32 graphs with SHA-256 verification. 16 kHz normalized mono microphone + synchronized playback reference; 128-sample hops, separate recurrent states, 24 ms output delay. Managed FFT and overlap-add; optional Burst FFT, CPU/High, one thread by default. Studio supports paired WAV files; no automatic reference delay alignment or live audio-device routing. GPU/NPU are not enabled for this managed profile.";
            }
            if (model.provider == MNNModelSource.HuggingFace && MNNKwsModel.IsRepository(model.repositoryId))
            {
                model.category = MNNModelCatalogCategory.KeywordSpotting.ToString();
                model.revision = model.version = MNNKwsModel.Revision;
                model.fileSize = MNNKwsModel.Sizes.Sum();
                model.runtimeAvailable = true;
                model.supportSummary = "Chinese Wenetspeech 3.3M Zipformer2 keyword spotting. Pinned MNN graphs and 197-token vocabulary; managed streaming keyword beam search, custom tone-marked pinyin keywords, confidence and timestamps. Studio supports WAV, recording and live microphone detection. CPU / macOS Mono ABI.";
            }
            ApplyPublishedSpeechProfile(model);
            if (IsPublishedSileroRepository(model))
            {
                model.category = MNNModelCatalogCategory.VoiceActivityDetection.ToString();
                model.tasks = new[] { "voice-activity-detection" };
                model.runtimeAvailable = files.Contains(MNNSileroVadConfig.FileName) && files.Contains(MNNSileroModel.OfficialGraph);
                model.supportSummary = "Official Silero v5.1 export requires its deployment profile and FP32 graph. Loading verifies the pinned profile and graph hash; file discovery does not validate inference or platform compatibility.";
            }
            if (model.provider == MNNModelSource.HuggingFace && MNNSileroModel.IsRepository(model.repositoryId))
            {
                model.category = MNNModelCatalogCategory.VoiceActivityDetection.ToString();
                model.revision = model.version = MNNSileroModel.Revision;
                model.fileSize = MNNSileroModel.Bytes + 249;
                model.runtimeAvailable = true;
                model.supportSummary = "Pinned Silero v5 quantized VAD graph with SHA-256 verification. Detect speech intervals and 32 ms probabilities from WAV/normalized PCM at 8–96 kHz, up to ten minutes. CPU default; 16 kHz model with resampling, 250 ms minimum speech / 100 ms silence / 30 ms padding. Voice activity only, no transcript or speaker identity.";
            }
            if (model.provider == MNNModelSource.ModelScope && MNNYoloV8Model.IsRepository(model.repositoryId))
            {
                model.category = MNNModelCatalogCategory.ObjectDetection.ToString();
                model.runtimeAvailable = true;
                model.supportSummary = "YOLOv8 COCO detection profile: verified graph hash, RGB letterbox preprocessing, anchor-distance box decoding and class-aware NMS. Installer generates the matching managed profile.";
            }
        }

        private void ReplaceProviderModels(MNNModelSource provider, IEnumerable<MNNRemoteModel> found)
        {
            models = models.Where(model => model != null && model.provider != provider).Concat(found)
                .ToList();
        }

        internal static string InstallFolderName(MNNRemoteModel model)
        {
            string repository = model.repositoryId ?? model.modelScopeId ?? model.id;
            string name = string.IsNullOrWhiteSpace(model.name) ? "model" : model.name;
            name = new string(name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' ? c : '_').ToArray()).Trim('.');
            if (name.Length == 0)
                name = "model";
            // Keep the original official ModelScope locations used by existing projects.
            if (model.provider == MNNModelSource.ModelScope && repository != null && repository.StartsWith("MNN/", StringComparison.OrdinalIgnoreCase))
                return name;
            using (var hash = SHA256.Create())
            {
                string digest = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(model.provider + ":" + repository))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
                return (model.provider == MNNModelSource.HuggingFace ? "hf-" : "ms-") + name.Substring(0, Math.Min(name.Length, 100)) + "-" + digest;
            }
        }

        internal static bool MatchesInstallMarker(MNNRemoteModel model, string marker)
        {
            string repository = model.repositoryId ?? model.modelScopeId ?? model.id;
            string identity = model.provider == MNNModelSource.HuggingFace ? "hf:" + repository : repository;
            return string.Equals(marker, identity, StringComparison.OrdinalIgnoreCase) ||
                model.provider == MNNModelSource.HuggingFace && marker != null && marker.StartsWith(identity + "@", StringComparison.OrdinalIgnoreCase);
        }

        [Serializable]
        private sealed class HuggingFaceSearchResponse
        {
            public HuggingFaceModel[] items;
        }
    }
}
