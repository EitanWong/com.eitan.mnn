using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Runtime catalog discovery and search for public MNN model repositories.</summary>
    [Serializable]
    public partial class MNNModelCatalog
    {
        private const string ApiRoot = "https://www.modelscope.cn";
        internal List<MNNRemoteModel> models = new List<MNNRemoteModel>();
        public IReadOnlyList<MNNRemoteModel> Models => models;
        public static readonly string[] FeaturedHuggingFaceRepositories = { MNNKwsModel.Repository, "VicenTrent/Cosy-Voice-MNN", MNNReferenceSpeechModel.IndexRepository, MNNReferenceSpeechModel.FireRedRepository };
        [Serializable, DataContract, UnityEngine.Scripting.Preserve]
        private class ModelSearchResponse
        {
            [DataMember, UnityEngine.Scripting.Preserve] public bool success;
            [DataMember, UnityEngine.Scripting.Preserve] public ModelSearchData data;
        }

        [Serializable, DataContract, UnityEngine.Scripting.Preserve]
        private class ModelSearchData
        {
            [DataMember, UnityEngine.Scripting.Preserve] public ModelScopeModel[] models;
            [DataMember, UnityEngine.Scripting.Preserve] public int total_count;
        }

        [Serializable, DataContract, UnityEngine.Scripting.Preserve]
        private class ModelScopeModel
        {
            [DataMember, UnityEngine.Scripting.Preserve] public string id;
            [DataMember, UnityEngine.Scripting.Preserve] public string display_name;
            [DataMember, UnityEngine.Scripting.Preserve] public string description;
            [DataMember, UnityEngine.Scripting.Preserve] public string[] tasks;
            [DataMember, UnityEngine.Scripting.Preserve] public long file_size;
            [DataMember, UnityEngine.Scripting.Preserve] public long @params;
            [DataMember, UnityEngine.Scripting.Preserve] public long downloads;
            [DataMember, UnityEngine.Scripting.Preserve] public string[] tags;
            [DataMember, UnityEngine.Scripting.Preserve] public string last_modified;
            [DataMember, UnityEngine.Scripting.Preserve] public string license;
        }

        public static MNNModelCatalog CreateDefault()
        {
            var repository = new MNNModelCatalog();
            repository.IncludeDtlnAecProfiles();
            repository.IncludeCosyVoice300MProfile();
            repository.IncludePublishedSpeechProfiles();
            return repository;
        }

        // Keep handler options compatible with Unity's Android MonoWebRequestHandler;
        // repository discovery already limits concurrency with a semaphore.
        private static HttpClient CreateDiscoveryClient() => new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
        { Timeout = TimeSpan.FromSeconds(45) };
        public async Task LoadModelScopeModelsAsync(HttpClient httpClient = null, string apiRoot = null, CancellationToken cancellationToken = default)
        {
            var client = httpClient ?? CreateDiscoveryClient();
            var root = (apiRoot ?? ApiRoot).TrimEnd('/');
            try
            {
                // Official collections also contain repositories tagged "other" or "safetensors".
                // The organization is the source of truth; library tags must not hide its resources.
                ModelScopeModel[] items;
                try
                {
                    items = await SearchModelScopeAsync("owner=MNN", client, root, cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidDataException error)
                {
                    throw new InvalidOperationException(error.Message, error);
                }

                var found = new List<MNNRemoteModel>(items.Length);
                foreach (var item in items)
                {
                    var repoName = item.id.Contains("/") ? item.id.Substring(item.id.LastIndexOf('/') + 1) : item.id;
                    found.Add(new MNNRemoteModel { id = item.id, provider = MNNModelSource.ModelScope, repositoryId = item.id, revision = "master", modelScopeId = item.id, name = repoName, displayName = string.IsNullOrEmpty(item.display_name) ? repoName : item.display_name, category = Categorize(item), family = GetFamily(repoName), generation = GetGeneration(repoName), description = string.IsNullOrEmpty(item.description) ? item.license ?? "MNN model from ModelScope" : item.description, author = "MNN", version = string.IsNullOrEmpty(item.last_modified) ? "master" : item.last_modified, fileSize = item.file_size, parameterCount = item.@params > 0 ? item.@params : InferParameterCount(repoName), parameterCountInferred = item.@params <= 0 && InferParameterCount(repoName) > 0, downloadCount = item.downloads, tags = item.tags ?? Array.Empty<string>(), tasks = item.tasks ?? Array.Empty<string>(), isRepository = true, downloadUrl = root + "/api/v1/models/" + item.id + "/repo/files?Revision=master&Recursive=true" });
                }

                // Publish only a complete catalog, so a failed refresh cannot replace it with a partial list.
                ReplaceProviderModels(MNNModelSource.ModelScope, found);
            }
            finally
            {
                if (httpClient == null)
                    client.Dispose();
            }
        }

        public async Task LoadHuggingFaceModelsAsync(IEnumerable<string> repositoryIds = null, HttpClient httpClient = null, MNNHuggingFaceSourcePool mirrorPool = null, CancellationToken cancellationToken = default)
        {
            if (repositoryIds == null)
            {
                await DiscoverHuggingFaceModelsAsync(httpClient, mirrorPool, cancellationToken);
                return;
            }

            var shouldDiscoverMirrors = httpClient == null && mirrorPool == null;
            var client = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
            var pool = mirrorPool ?? MNNHuggingFaceSourcePool.Shared;
            var found = new List<MNNRemoteModel>();
            try
            {
                if (shouldDiscoverMirrors)
                    await pool.RefreshFromWebAsync(client, MNNHuggingFaceSourcePool.DefaultDiscoveryUrls, cancellationToken).ConfigureAwait(false);
                foreach (var repositoryId in repositoryIds ?? FeaturedHuggingFaceRepositories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(repositoryId))
                        continue;
                    var model = await LoadHuggingFaceModelAsync(repositoryId.Trim(), client, pool, cancellationToken).ConfigureAwait(false);
                    if (model != null)
                        found.Add(model);
                }

                var merged = models.Where(model => model != null).ToList();
                foreach (var model in found)
                {
                    merged.RemoveAll(existing => string.Equals(existing.id, model.id, StringComparison.OrdinalIgnoreCase));
                    merged.Add(model);
                }

                models = merged;
            }
            finally
            {
                if (httpClient == null)
                    client.Dispose();
            }
        }

        public async Task<MNNRemoteModel> LoadHuggingFaceModelAsync(string repositoryId, HttpClient httpClient, MNNHuggingFaceSourcePool mirrorPool, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var mirror in mirrorPool.GetApiProbeCandidates())
            {
                var url = mirror.ApiRoot + "/api/models/" + Uri.EscapeDataString(repositoryId).Replace("%2F", "/") + (MNNCosyVoiceModel.IsRepository(repositoryId) ? "/revision/" + MNNCosyVoiceModel.Revision : MNNReferenceSpeechModel.IsRepository(repositoryId) ? "/revision/" + MNNReferenceSpeechModel.Revision(repositoryId) : "") + "?blobs=true";
                try
                {
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(12));
                        using (var response = await MNNModelCatalogRequests.Shared.GetAsync(httpClient, url, timeout.Token).ConfigureAwait(false))
                        {
                            var body = await MNNModelMetadata.ReadTextAsync(response.Content, timeout.Token).ConfigureAwait(false);
                            if (!response.IsSuccessStatusCode)
                                throw new HttpRequestException($"Hugging Face metadata failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
                            var item = MNNModelMetadata.ReadJson<HuggingFaceModel>(body);
                            if (item == null || string.IsNullOrWhiteSpace(item.id))
                                throw new InvalidDataException("Hugging Face returned invalid model metadata.");
                            mirrorPool.ReportSuccess(mirror);
                            var name = item.id.Substring(item.id.LastIndexOf('/') + 1);
                            var revision = MNNBertEmbeddingModel.IsRepository(item.id) ? MNNBertEmbeddingModel.Revision(item.id) : MNNCosyVoiceModel.IsRepository(item.id) ? MNNCosyVoiceModel.Revision : MNNReferenceSpeechModel.IsRepository(item.id) ? MNNReferenceSpeechModel.Revision(item.id) : string.IsNullOrWhiteSpace(item.sha) ? "main" : item.sha;
                            var tags = item.tags ?? Array.Empty<string>();
                            var description = string.IsNullOrWhiteSpace(item.pipeline_tag) ? "Hugging Face model" : "Hugging Face " + item.pipeline_tag + " model";
                            var model = new MNNRemoteModel { id = "hf:" + item.id, provider = MNNModelSource.HuggingFace, repositoryId = item.id, revision = revision, name = name, displayName = name, category = Classify(item.id, name, description, item.pipeline_tag == null ? null : new[] { item.pipeline_tag }, tags), family = GetFamily(name), generation = GetGeneration(name), description = description + (MNNCosyVoiceModel.IsRepository(item.id) ? ". CosyVoice3 default reference voice. Complete fp16 archive is automatically verified and extracted." : MNNReferenceSpeechModel.IsRepository(item.id) ? ". Chinese/English reference-audio synthesis. Includes pinned tokenizer dependencies; supply a 0.5–15 second WAV." : string.Empty), author = item.author ?? "Hugging Face", version = revision, fileSize = MNNReferenceSpeechModel.IsRepository(item.id) ? MNNReferenceSpeechModel.Files(MNNReferenceSpeechModel.IsIndex(item.id)).Sum(file => file.Bytes) : item.siblings == null ? 0 : item.siblings.Where(sibling => sibling != null && (!MNNCosyVoiceModel.IsRepository(item.id) || sibling.rfilename == MNNCosyVoiceModel.Archive || sibling.rfilename == "README.md" || sibling.rfilename == "release-manifest.json")).Sum(sibling => Math.Max(0L, sibling.lfs?.size ?? sibling.size)), downloadCount = item.downloads, tags = tags, tasks = item.pipeline_tag == null ? Array.Empty<string>() : new[] { item.pipeline_tag }, isRepository = true, runtimeAvailable = MNNCosyVoiceModel.IsRepository(item.id) || name.IndexOf("cosy", StringComparison.OrdinalIgnoreCase) < 0, downloadUrl = mirror.FileRoot + "/" + item.id + "/resolve/" + revision };
                            if (item.siblings != null)
                                ApplyArtifactEvidence(model, item.siblings.Where(file => file?.rfilename != null).Select(file => file.rfilename).ToArray());
                            if (MNNCosyVoiceModel.IsRepository(item.id) || MNNReferenceSpeechModel.IsRepository(item.id))
                            {
                                model.runtimeAvailable = true;
                                model.supportSummary = "Pinned profile for an existing speech pipeline; the installer verifies required files. This catalog refresh does not run inference.";
                            }

                            return model;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    mirrorPool.ReportFailure(mirror);
                }
            }

            throw new HttpRequestException("No Hugging Face metadata source was available for " + repositoryId + ".");
        }

        [Serializable, DataContract, UnityEngine.Scripting.Preserve]
        private sealed class HuggingFaceModel
        {
            [DataMember, UnityEngine.Scripting.Preserve] public string id;
            [DataMember, UnityEngine.Scripting.Preserve] public string modelId;
            [DataMember, UnityEngine.Scripting.Preserve] public string author;
            [DataMember, UnityEngine.Scripting.Preserve] public string sha;
            [DataMember, UnityEngine.Scripting.Preserve] public string pipeline_tag;
            [DataMember, UnityEngine.Scripting.Preserve] public long downloads;
            [DataMember, UnityEngine.Scripting.Preserve] public string[] tags;
            [DataMember, UnityEngine.Scripting.Preserve] public HuggingFaceSibling[] siblings;
        }

        [Serializable, DataContract, UnityEngine.Scripting.Preserve]
        private sealed class HuggingFaceSibling
        {
            [DataMember, UnityEngine.Scripting.Preserve] public string rfilename;
            [DataMember, UnityEngine.Scripting.Preserve] public long size;
            [DataMember, UnityEngine.Scripting.Preserve] public HuggingFaceLfs lfs;
        }

        [Serializable, DataContract, UnityEngine.Scripting.Preserve]
        private sealed class HuggingFaceLfs
        {
            [DataMember, UnityEngine.Scripting.Preserve] public string sha256;
            [DataMember, UnityEngine.Scripting.Preserve] public long size;
        }

        private static string Categorize(ModelScopeModel item) => Classify(item.id, item.display_name, item.description, item.tasks, item.tags);
        private static bool ContainsAny(string text, params string[] values)
        {
            return values.Any(value => text.Contains(value));
        }

        private static string GetFamily(string modelName)
        {
            var normalizedFamily = NormalizeFamily(modelName);
            if (!string.IsNullOrEmpty(normalizedFamily))
                return normalizedFamily;
            var families = new[] { "DeepSeek", "ChatGLM", "MiniCPM", "InternLM", "StableLM", "GPT-OSS", "Qwen", "Llama", "Gemma", "Baichuan", "Mistral", "StarCoder", "CodeLlama", "Yi", "Phi", "BGE", "RWKV", "MobileNet", "ResNet", "YOLO", "DeepLab", "RetinaFace", "MoveNet", "ESRGAN", "LFM2", "Hunyuan", "InternVL", "FastVLM", "OpenELM", "SmolLM", "SmolVLM", "MobileLLM", "GLM", "ERNIE", "TinyLlama", "Reader-LM", "QwQ", "Lingshu", "MiMo", "MiniMind", "Nanbeige", "VibeThinker", "WebSailor", "Reader-LM", "Supertonic" };
            foreach (var family in families)
            {
                if (modelName.StartsWith(family, StringComparison.OrdinalIgnoreCase))
                    return family;
            }

            return string.Empty;
        }

        private static string GetGeneration(string modelName)
        {
            if (modelName.StartsWith("Meta-Llama", StringComparison.OrdinalIgnoreCase))
                modelName = modelName.Substring("Meta-".Length);
            var family = GetFamily(modelName);
            var suffix = modelName.Substring(Math.Min(family.Length, modelName.Length)).TrimStart('-', '_', ' ');
            if (string.IsNullOrEmpty(suffix))
                return string.Empty;
            var match = Regex.Match(suffix, @"^(?:v\d+[a-z]?|r\d+(?:\.\d+)*|\d+(?:\.\d+)*)(?=[-_ ]|$)", RegexOptions.IgnoreCase);
            return match.Success ? match.Value.ToUpperInvariant() : string.Empty;
        }

        private static string NormalizeFamily(string modelName)
        {
            if (modelName.StartsWith("Meta-Llama", StringComparison.OrdinalIgnoreCase))
                return "Llama";
            if (modelName.StartsWith("QwQ", StringComparison.OrdinalIgnoreCase))
                return "Qwen";
            if (modelName.StartsWith("Qwen", StringComparison.OrdinalIgnoreCase))
                return "Qwen";
            if (modelName.StartsWith("LFM2", StringComparison.OrdinalIgnoreCase))
                return "LFM2";
            return null;
        }

        private static long InferParameterCount(string modelName)
        {
            var match = Regex.Match(modelName ?? string.Empty, @"(?<![A-Za-z0-9])(\d+(?:\.\d+)?)([BM])(?=[^A-Za-z]|$)", RegexOptions.IgnoreCase);
            if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
                return 0;
            return (long)(size * (string.Equals(match.Groups[2].Value, "B", StringComparison.OrdinalIgnoreCase) ? 1_000_000_000d : 1_000_000d));
        }

        public List<MNNRemoteModel> GetModelsByCategory(MNNModelCatalogCategory category)
        {
            return category == MNNModelCatalogCategory.All ? models : models.FindAll(m => m.category == category.ToString());
        }

        public List<MNNRemoteModel> SearchModels(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return models;
            return models.FindAll(model => MatchesSearch(model, query));
        }

        /// <summary>Shared local search semantics for catalog consumers, including audio task aliases.</summary>
        public static bool MatchesSearch(MNNRemoteModel model, string query) => model != null &&
            MatchesSearch(query, model.name, model.displayName, model.description, model.modelScopeId,
                model.repositoryId, model.tags, model.tasks, model.category);

        internal static bool MatchesSearch(string query, string name, string displayName, string description,
            string modelScopeId, string repositoryId, string[] tags, string[] tasks, string category)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            query = query.Trim();
            return Contains(name, query) || Contains(displayName, query) || Contains(description, query) ||
                Contains(modelScopeId, query) || Contains(repositoryId, query) ||
                tags != null && Array.Exists(tags, tag => Contains(tag, query)) ||
                tasks != null && Array.Exists(tasks, task => Contains(task, query)) || MatchesAudioCategorySearch(category, query);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
