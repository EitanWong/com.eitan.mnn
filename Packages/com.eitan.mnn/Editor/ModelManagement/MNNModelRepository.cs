using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity.Editor
{
    [Serializable]
    public class MNNModelInfo
    {
        public string id;
        public string modelScopeId;
        public string name;
        public string displayName;
        public string category;
        public string family;
        public string generation;
        public string description;
        public string author;
        public string version;
        public string downloadUrl;
        public long fileSize;
        public long parameterCount;
        public bool parameterCountInferred;
        public long downloadCount;
        public string[] tags;
        public string[] tasks;
        public string previewImage;
        public ModelInputSpec inputSpec;
        public ModelOutputSpec outputSpec;
        public bool isRepository;
        public bool isInstalled;
        public string localPath;
    }

    [Serializable]
    public class ModelInputSpec
    {
        public string name;
        public int[] shape;
        public string dataType;
        public string format;
    }

    [Serializable]
    public class ModelOutputSpec
    {
        public string name;
        public int[] shape;
        public string dataType;
        public int classCount;
    }

    public enum MNNModelCategory
    {
        All,
        ImageClassification,
        ObjectDetection,
        SemanticSegmentation,
        PoseEstimation,
        FaceDetection,
        FaceRecognition,
        OCR,
        NLP,
        AudioProcessing,
        SuperResolution,
        StyleTransfer,
        Other,
        LargeLanguageModel,
        VisionLanguageModel,
        OmniModel,
        AudioLanguageModel,
        EmbeddingModel,
        Reranker,
        OCRModel,
        CodeModel,
        SafetyModel,
        ImageGenerationModel,
        TextGeneration,
        SpeechSynthesis,
        SpeechRecognition
    }

    [Serializable]
    public class MNNModelRepository
    {
        private const string ApiRoot = "https://www.modelscope.cn";
        public List<MNNModelInfo> models = new List<MNNModelInfo>();
        [Serializable]
        private class ModelSearchResponse
        {
            public bool success;
            public ModelSearchData data;
        }

        [Serializable]
        private class ModelSearchData
        {
            public ModelScopeModel[] models;
            public int total_count;
        }

        [Serializable]
        private class ModelScopeModel
        {
            public string id;
            public string display_name;
            public string description;
            public string[] tasks;
            public long file_size;
            public long @params;
            public long downloads;
            public string[] tags;
            public string last_modified;
            public string license;
        }

        [Serializable]
        public class ModelScopeFile
        {
            public string Path;
            public string Type;
            public long Size;
            public string Revision;
            public string Sha256;
        }

        public static MNNModelRepository GetDefaultRepository()
        {
            return new MNNModelRepository();
        }

        public Task LoadModelScopeModelsAsync() => LoadModelScopeModelsAsync(null);
        public async Task LoadModelScopeModelsAsync(HttpClient httpClient, string apiRoot = null, CancellationToken cancellationToken = default)
        {
            var found = new List<MNNModelInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const int pageSize = 50;
            var client = httpClient ?? new HttpClient{Timeout = TimeSpan.FromSeconds(45)};
            var root = (apiRoot ?? ApiRoot).TrimEnd('/');
            try
            {
                // Official collections also contain repositories tagged "other" or "safetensors".
                // The organization is the source of truth; library tags must not hide its resources.
                for (var page = 1;; page++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var url = $"{root}/openapi/v1/models?owner=MNN&page_size={pageSize}&sort=downloads&page_number={page}";
                    using (var response = await client.GetAsync(url, cancellationToken))
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        if (!response.IsSuccessStatusCode)
                            throw new HttpRequestException($"ModelScope returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
                        var result = JsonUtility.FromJson<ModelSearchResponse>(body);
                        if (result == null || !result.success || result.data == null || result.data.models == null)
                            throw new InvalidOperationException("ModelScope returned an invalid model list.");
                        var previousCount = found.Count;
                        foreach (var item in result.data.models)
                        {
                            if (item == null || string.IsNullOrWhiteSpace(item.id))
                                throw new InvalidOperationException("ModelScope returned a model without a repository ID.");
                            if (!seen.Add(item.id))
                                continue;
                            var repoName = item.id.Contains("/") ? item.id.Substring(item.id.LastIndexOf('/') + 1) : item.id;
                            found.Add(new MNNModelInfo{id = item.id, modelScopeId = item.id, name = repoName, displayName = string.IsNullOrEmpty(item.display_name) ? repoName : item.display_name, category = Categorize(item), family = GetFamily(repoName), generation = GetGeneration(repoName), description = string.IsNullOrEmpty(item.description) ? item.license ?? "MNN model from ModelScope" : item.description, author = "MNN", version = string.IsNullOrEmpty(item.last_modified) ? "master" : item.last_modified, fileSize = item.file_size, parameterCount = item.@params > 0 ? item.@params : InferParameterCount(repoName), parameterCountInferred = item.@params <= 0 && InferParameterCount(repoName) > 0, downloadCount = item.downloads, tags = item.tags ?? Array.Empty<string>(), tasks = item.tasks ?? Array.Empty<string>(), isRepository = true, downloadUrl = root + "/api/v1/models/" + item.id + "/repo/files?Revision=master&Recursive=true"});
                        }

                        if (result.data.total_count > 0 && found.Count >= result.data.total_count)
                            break;
                        if (result.data.models.Length == 0 && result.data.total_count <= 0)
                            break;
                        if (found.Count == previousCount)
                            throw new InvalidOperationException("ModelScope pagination stopped before the complete model list was retrieved.");
                    }
                }

                // Publish only a complete catalog, so a failed refresh cannot replace it with a partial list.
                models = found;
            }
            finally
            {
                if (httpClient == null)
                    client.Dispose();
            }
        }

        private static string Categorize(ModelScopeModel item) => Classify(item.id, item.display_name, item.description, item.tasks, item.tags);
        internal static string Classify(string id, string name = null, string description = null, string[] tasks = null, string[] tags = null)
        {
            var task = tasks == null ? string.Empty : string.Join(" ", tasks).ToLowerInvariant();
            var text = ((id ?? "") + " " + (name ?? "") + " " + (description ?? "") + " " + string.Join(" ", tags ?? Array.Empty<string>())).ToLowerInvariant();
            if (ContainsAny(text, "omni", "all-modal", "all modal"))
                return MNNModelCategory.OmniModel.ToString();
            if (ContainsAny(text, "reranker", "re-ranker", "rerank"))
                return MNNModelCategory.Reranker.ToString();
            if (ContainsAny(text, "embedding", "text-embedding", "bge-") || task.Contains("sentence-embedding"))
                return MNNModelCategory.EmbeddingModel.ToString();
            if (ContainsAny(text, "stable-diffusion", "sana", "flux", "image-generation", "text-to-image") || task.Contains("text-to-image"))
                return MNNModelCategory.ImageGenerationModel.ToString();
            if (ContainsAny(task, "text-to-speech", "speech-synthesis") || ContainsAny(text, "tts", "piper", "bert-vits", "supertonic"))
                return MNNModelCategory.SpeechSynthesis.ToString();
            if (ContainsAny(task, "speech-recognition", "automatic-speech-recognition") || ContainsAny(text, "asr", "whisper", "zipformer", "sensevoice"))
                return MNNModelCategory.SpeechRecognition.ToString();
            if (ContainsAny(text, "ocr", "optical-character"))
                return MNNModelCategory.OCRModel.ToString();
            if (ContainsAny(text, "audio", "speech", "tts", "asr"))
                return MNNModelCategory.AudioLanguageModel.ToString();
            if (ContainsAny(text, "vision", "-vl-", "-vl_", "vlm", "fastvlm", "smolvlm", "gui-owl", "internvl", "minicpm-v"))
                return MNNModelCategory.VisionLanguageModel.ToString();
            if (ContainsAny(text, "coder", "-code", "code-", "starcoder"))
                return MNNModelCategory.CodeModel.ToString();
            if (ContainsAny(text, "guard", "xguard", "moderation"))
                return MNNModelCategory.SafetyModel.ToString();
            if (task.Contains("object-detection"))
                return MNNModelCategory.ObjectDetection.ToString();
            if (task.Contains("segmentation"))
                return MNNModelCategory.SemanticSegmentation.ToString();
            if (task.Contains("image-classification"))
                return MNNModelCategory.ImageClassification.ToString();
            if (task.Contains("pose-estimation"))
                return MNNModelCategory.PoseEstimation.ToString();
            if (task.Contains("face-detection"))
                return MNNModelCategory.FaceDetection.ToString();
            if (task.Contains("face-recognition"))
                return MNNModelCategory.FaceRecognition.ToString();
            if (task.Contains("super-resolution"))
                return MNNModelCategory.SuperResolution.ToString();
            if (task.Contains("style-transfer"))
                return MNNModelCategory.StyleTransfer.ToString();
            if (task.Contains("audio") || task.Contains("speech"))
                return MNNModelCategory.AudioProcessing.ToString();
            if (task.Contains("text-generation"))
                return MNNModelCategory.LargeLanguageModel.ToString();
            if (task.Contains("text-classification") || task.Contains("sentence-similarity") || task.Contains("feature-extraction"))
                return MNNModelCategory.NLP.ToString();
            if (task.Contains("audio") || task.Contains("speech"))
                return MNNModelCategory.AudioProcessing.ToString();
            if (task.Contains("ocr") || task.Contains("optical-character"))
                return MNNModelCategory.OCR.ToString();
            if (text.Contains("detection") || text.Contains("yolo"))
                return MNNModelCategory.ObjectDetection.ToString();
            if (text.Contains("segment"))
                return MNNModelCategory.SemanticSegmentation.ToString();
            if (text.Contains("ocr"))
                return MNNModelCategory.OCR.ToString();
            if (text.Contains("audio") || text.Contains("speech"))
                return MNNModelCategory.AudioProcessing.ToString();
            if (text.Contains("classif") || text.Contains("mobilenet"))
                return MNNModelCategory.ImageClassification.ToString();
            if (text.Contains("nlp") || text.Contains("text"))
                return MNNModelCategory.NLP.ToString();
            return MNNModelCategory.Other.ToString();
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            return values.Any(value => text.Contains(value));
        }

        private static string GetFamily(string modelName)
        {
            var normalizedFamily = NormalizeFamily(modelName);
            if (!string.IsNullOrEmpty(normalizedFamily))
                return normalizedFamily;
            var families = new[]{"DeepSeek", "ChatGLM", "MiniCPM", "InternLM", "StableLM", "GPT-OSS", "Qwen", "Llama", "Gemma", "Baichuan", "Mistral", "StarCoder", "CodeLlama", "Yi", "Phi", "BGE", "RWKV", "MobileNet", "ResNet", "YOLO", "DeepLab", "RetinaFace", "MoveNet", "ESRGAN", "LFM2", "Hunyuan", "InternVL", "FastVLM", "OpenELM", "SmolLM", "SmolVLM", "MobileLLM", "GLM", "ERNIE", "TinyLlama", "Reader-LM", "QwQ", "Lingshu", "MiMo", "MiniMind", "Nanbeige", "VibeThinker", "WebSailor", "Reader-LM", "Supertonic"};
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

        public List<MNNModelInfo> GetModelsByCategory(MNNModelCategory category)
        {
            return category == MNNModelCategory.All ? models : models.FindAll(m => m.category == category.ToString());
        }

        public List<MNNModelInfo> SearchModels(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return models;
            query = query.Trim();
            return models.FindAll(m => Contains(m.name, query) || Contains(m.displayName, query) || Contains(m.description, query) || Contains(m.modelScopeId, query) || (m.tags != null && Array.Exists(m.tags, tag => Contains(tag, query))) || (m.tasks != null && Array.Exists(m.tasks, task => Contains(task, query))));
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
