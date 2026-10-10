using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MNN.Unity.Editor
{
    internal sealed class MNNStudioModel
    {
        internal readonly string Name, Directory, Category;
        internal readonly MNNStudioTask Task;
        internal readonly MNNModelCapabilities Capabilities;
        internal string DisplayName => Name.Replace("-MNN", string.Empty);
        internal string CapabilityLabel => Task != MNNStudioTask.Chat ? MNNStudioTasks.Get((int)Task).Name : Supports(MNNModelCapabilities.Vision | MNNModelCapabilities.AudioInput) ? "Omni" : Supports(MNNModelCapabilities.Vision) ? "Vision + text" : Supports(MNNModelCapabilities.AudioInput) ? "Audio + text" : "Text";
        internal bool Supports(MNNModelCapabilities value) => (Capabilities & value) == value;
        internal string SupertonicPrecision => new[]{"fp16", "fp32", "int8"}.FirstOrDefault(precision => new[]{"duration_predictor", "text_encoder", "vector_estimator", "vocoder"}.All(stage => File.Exists(Path.Combine(Directory, "mnn_models", precision, stage + ".mnn"))));
        internal bool IsSana
        {
            get
            {
                string path = Path.Combine(Directory, "config.json");
                try
                {
                    return File.Exists(path) && JsonUtility.FromJson<Config>(File.ReadAllText(path))?.model_name == "Sana-Distill-v2";
                }
                catch (Exception error)when (error is IOException || error is ArgumentException)
                {
                    return false;
                }
            }
        }

        internal bool IsPiper
        {
            get
            {
                try
                {
                    return File.Exists(Path.Combine(Directory, "config.json")) && JsonUtility.FromJson<Config>(File.ReadAllText(Path.Combine(Directory, "config.json")))?.model_type == "piper";
                }
                catch (Exception error)when (error is IOException || error is ArgumentException)
                {
                    return false;
                }
            }
        }

        internal string PiperDataDirectory => MNNPiper.ResolveDataDirectory(Directory, ReadTaskConfig()?.asset_folder);
        internal bool IsBertVits2 => ReadTaskConfig()?.model_type == "bertvits";
        private Config ReadTaskConfig()
        {
            try
            {
                string path = Path.Combine(Directory, "config.json");
                return File.Exists(path) ? JsonUtility.FromJson<Config>(File.ReadAllText(path)) : null;
            }
            catch (Exception error)when (error is IOException || error is ArgumentException)
            {
                return null;
            }
        }

        internal string[] TtsVoices => IsPiper ? System.IO.Directory.GetFiles(Directory, "*_fp16*.mnn").Select(Path.GetFileName).Select(n => n.Replace("_fp16_public.mnn", "").Replace("_fp16.mnn", "")).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray() : IsBertVits2 ? new[]{"Chenxi"} : new[]{"M1", "M2", "F1", "F2"}.Where(voice => File.Exists(Path.Combine(Directory, "voice_styles", voice + ".json")) || File.Exists(Path.Combine(Directory, "mnn_models", "voice_styles", voice + ".json"))).ToArray();
        internal bool HasGenerationPipeline
        {
            get
            {
                if (Task == MNNStudioTask.SpeechSynthesis)
                {
                    var metadata = ReadTaskConfig();
                    if (IsPiper)
                    {
                        if (metadata == null || metadata.sample_rate != 16000 || string.IsNullOrWhiteSpace(metadata.model_path) || !File.Exists(Path.Combine(Directory, metadata.model_path)))
                            return false;
                        try
                        {
                            MNNPiper.ResolveDataDirectory(Directory, metadata.asset_folder);
                            return true;
                        }
                        catch (Exception error)when (error is IOException || error is ArgumentException)
                        {
                            return false;
                        }
                    }

                    if (IsBertVits2)
                        return metadata != null && metadata.sample_rate == 44100 && !string.IsNullOrWhiteSpace(metadata.model_path) && File.Exists(Path.Combine(Directory, metadata.model_path)) && new[]{"common/mnn_models/chinese_bert.mnn", "common/mnn_models/chinese_bert.mnn.weight", "common/text_processing_jsons/cn_bert_token.bin", "common/text_processing_jsons/pinyin_dict.bin", "common/text_processing_jsons/phrases_dict.bin", "common/text_processing_jsons/pinyin_to_symbol_map.bin", "common/text_processing_jsons/hotwords_cn.json", "common/text_processing_jsons/default_tone_words.json"}.All(file => File.Exists(Path.Combine(Directory, metadata.asset_folder ?? ".", file)));
                    string config = Path.Combine(Directory, "config.json");
                    try
                    {
                        return File.Exists(config) && JsonUtility.FromJson<Config>(File.ReadAllText(config))?.model_type == "supertonic" && SupertonicPrecision != null && File.Exists(Path.Combine(Directory, "mnn_models", "tts.json")) && File.Exists(Path.Combine(Directory, "mnn_models", "unicode_indexer.json")) && new[]{"M1", "M2", "F1", "F2"}.Any(voice => File.Exists(Path.Combine(Directory, "voice_styles", voice + ".json")) || File.Exists(Path.Combine(Directory, "mnn_models", "voice_styles", voice + ".json")));
                    }
                    catch (Exception error)when (error is IOException || error is ArgumentException)
                    {
                        return false;
                    }
                }

                if (Task != MNNStudioTask.ImageGeneration)
                    return false;
                if (IsSana)
                    return new[]{"connector", "projector", "transformer", "vae_encoder", "vae_decoder", "llm/llm"}.All(stage => File.Exists(Path.Combine(Directory, stage + ".mnn")) && File.Exists(Path.Combine(Directory, stage + ".mnn.weight"))) && new[]{"llm/llm_config.json", "llm/tokenizer.txt", "llm/meta_queries.mnn"}.All(file => File.Exists(Path.Combine(Directory, file)));
                string root = System.IO.Directory.Exists(Path.Combine(Directory, "general")) ? Path.Combine(Directory, "general") : Directory;
                return new[]{"text_encoder", "unet", "vae_decoder"}.All(stage => File.Exists(Path.Combine(root, stage + ".mnn")) && File.Exists(Path.Combine(root, stage + ".mnn.weight"))) && File.Exists(Path.Combine(root, "vocab.json")) && File.Exists(Path.Combine(root, "merges.txt"));
            }
        }

        [Serializable]
        private sealed class Config
        {
            public bool is_visual = false, is_audio = false, has_talker = false;
            public string model_type = null, modelType = null, talker_type = null, model_name = null, model_path = null, asset_folder = null;
            public int hidden_size = 0, image_pad = 0, sample_rate = 0;
        }

        internal MNNStudioModel(string name, string directory, MNNStudioTask task, MNNModelCapabilities capabilities, string category = null)
        {
            Name = name;
            Directory = directory;
            Task = task;
            Capabilities = capabilities;
            Category = category ?? MNNModelRepository.Classify(name);
        }

        internal static MNNStudioModel Read(string directory)
        {
            if (!System.IO.Directory.Exists(directory) || !IsModelDirectory(directory))
                throw new ArgumentException("Choose a complete MNN model folder containing config.json, MNN weights or an installed repository marker.", nameof(directory));
            string name = Path.GetFileName(directory);
            string marker = Path.Combine(directory, MNNModelDownloadTasks.InstallMarker);
            string identity = File.Exists(marker) ? File.ReadAllText(marker).Trim() : name;
            string category = MNNModelRepository.Classify(identity);
            var task = MNNStudioTasks.FromCategory(category);
            string rootConfig = Path.Combine(directory, "config.json");
            var pipelineConfig = File.Exists(rootConfig) ? JsonUtility.FromJson<Config>(File.ReadAllText(rootConfig)) : null;
            if (pipelineConfig?.modelType == "zipformer")
            {
                category = MNNModelCategory.SpeechRecognition.ToString();
                task = MNNStudioTask.SpeechRecognition;
            }
            else if (pipelineConfig?.model_type == "supertonic" || pipelineConfig?.model_type == "piper" || pipelineConfig?.model_type == "bertvits")
                task = MNNStudioTask.SpeechSynthesis;
            else if (pipelineConfig?.model_name == "Sana-Distill-v2" || HasDiffusionStages(directory) || HasDiffusionStages(Path.Combine(directory, "general")))
                task = MNNStudioTask.ImageGeneration;
            var config = new Config();
            string metadata = Path.Combine(directory, "llm_config.json");
            if (File.Exists(metadata))
                config = JsonUtility.FromJson<Config>(File.ReadAllText(metadata)) ?? config;
            // Diffusion/TTS/ASR use separate pipelines even when they contain config.json.
            bool dedicated = task == MNNStudioTask.ImageGeneration || task == MNNStudioTask.SpeechSynthesis || task == MNNStudioTask.SpeechRecognition || task == MNNStudioTask.Embedding || task == MNNStudioTask.Reranker;
            if (!dedicated && (File.Exists(metadata) || category == MNNModelCategory.CodeModel.ToString() || category == MNNModelCategory.SafetyModel.ToString()))
                task = MNNStudioTask.Chat;
            // Use the same verified talker contract as the runtime, not every model named Omni.
            bool speech = config.has_talker && string.IsNullOrEmpty(config.talker_type) && string.IsNullOrEmpty(config.model_type) && config.image_pad == 151655 && config.hidden_size == 2048;
            var capabilities = (config.is_visual ? MNNModelCapabilities.Vision : 0) | (config.is_audio ? MNNModelCapabilities.AudioInput : 0) | (speech ? MNNModelCapabilities.SpeechOutput : 0);
            return new MNNStudioModel(name, Path.GetFullPath(directory), task, capabilities, category);
        }

        internal static List<MNNStudioModel> Discover(IEnumerable<string> roots, ICollection<string> errors = null)
        {
            var found = new Dictionary<string, MNNStudioModel>(StringComparer.Ordinal);
            foreach (string root in roots.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
            {
                if (!System.IO.Directory.Exists(root))
                    continue;
                try
                {
                    // Accept the chosen model folder itself as well as a parent containing models.
                    if (IsModelDirectory(root))
                    {
                        try
                        {
                            var model = Read(root);
                            found[model.Directory] = model;
                        }
                        catch (Exception error)when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
                        {
                            errors?.Add(Path.GetFileName(root) + ": " + error.Message);
                        }

                        continue;
                    }

                    foreach (string directory in System.IO.Directory.GetDirectories(root).OrderBy(value => value, StringComparer.Ordinal))
                    {
                        if (!IsModelDirectory(directory))
                            continue;
                        try
                        {
                            var model = Read(directory);
                            found[model.Directory] = model;
                        }
                        catch (Exception error)when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
                        {
                            errors?.Add(Path.GetFileName(directory) + ": " + error.Message);
                        }
                    }
                }
                catch (Exception error)when (error is IOException || error is UnauthorizedAccessException)
                {
                    errors?.Add(root + ": " + error.Message);
                }
            }

            return found.Values.OrderBy(model => model.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool IsModelDirectory(string directory) => File.Exists(Path.Combine(directory, "config.json")) || File.Exists(Path.Combine(directory, MNNModelDownloadTasks.InstallMarker)) || HasDiffusionStages(directory) || HasDiffusionStages(Path.Combine(directory, "general")) || HasDiffusionStages(Path.Combine(directory, "opencl")) || System.IO.Directory.EnumerateFiles(directory, "*.mnn", SearchOption.TopDirectoryOnly).Any();
        private static bool HasDiffusionStages(string directory) => File.Exists(Path.Combine(directory, "text_encoder.mnn")) && File.Exists(Path.Combine(directory, "unet.mnn")) && File.Exists(Path.Combine(directory, "vae_decoder.mnn"));
    }

    internal sealed class MNNStudioMessage
    {
        internal readonly bool IsUser;
        internal readonly string Text, ImagePath, AudioPath;
        internal readonly MNNStudioResult Result;
        internal MNNStudioMessage(bool user, string text, string image = null, string audio = null, MNNStudioResult result = null)
        {
            IsUser = user;
            Text = text ?? string.Empty;
            ImagePath = image;
            AudioPath = audio;
            Result = result;
        }
    }

    internal static class MNNStudioPrompt
    {
        internal const int MaximumCharacters = 16000;
        internal const int CompressionThresholdCharacters = 12000;
        internal const int RecentMessagesAfterCompression = 8;
        internal const int MaximumSummaryCharacters = 2400;
        // Keep complete recent turns, within a bounded text budget. Media is supplied
        // separately for the current turn; prior image/audio paths never become fake context.
        internal static MNNChatMessage[] Build(IReadOnlyList<MNNStudioMessage> messages, string current, string summary = null)
        {
            if (string.IsNullOrWhiteSpace(current))
                throw new ArgumentException("Enter a message.", nameof(current));
            if (current.Length > MaximumCharacters)
                throw new ArgumentException("The message is too long (16,000 characters maximum).", nameof(current));
            summary = BoundSummary(summary);
            int summaryBudget = Math.Max(0, MaximumCharacters - current.Length - 256);
            if (summary.Length > summaryBudget)
                summary = summary.Substring(summary.Length - summaryBudget);
            int count = messages?.Count ?? 0, start = count, length = current.Length + (summary?.Length ?? 0);
            while (start > 0)
            {
                int addition = messages[start - 1].Text.Length;
                if (length + addition > MaximumCharacters)
                    break;
                length += addition;
                --start;
            }

            if (start < count && !messages[start].IsUser)
                ++start;
            var result = new List<MNNChatMessage>();
            if (!string.IsNullOrWhiteSpace(summary))
                result.Add(SummaryMessage(summary));
            for (int i = start; i < count; ++i)
                AppendRole(result, messages[i].IsUser ? MNNChatRole.User : MNNChatRole.Assistant, messages[i].Text);
            AppendRole(result, MNNChatRole.User, current);
            return result.ToArray();
        }

        internal static MNNChatMessage[] WithSummary(IReadOnlyList<MNNChatMessage> conversation, string summary)
        {
            var result = new List<MNNChatMessage>{SummaryMessage(BoundSummary(summary))};
            for (int i = 0; i < conversation.Count; ++i)
                if (i != 0 || conversation[i].Role != MNNChatRole.System)
                    result.Add(conversation[i]);
            return result.ToArray();
        }

        internal static void AppendRole(List<MNNChatMessage> messages, MNNChatRole role, string content)
        {
            if (messages.Count > 0 && messages[messages.Count - 1].Role == role)
            {
                var previous = messages[messages.Count - 1];
                messages[messages.Count - 1] = new MNNChatMessage(role, previous.Content + "\n\n" + content);
            }
            else
                messages.Add(new MNNChatMessage(role, content));
        }

        internal static string CleanSummary(string value)
        {
            value = value ?? "";
            int endThink = value.LastIndexOf("</think>", StringComparison.Ordinal);
            if (endThink >= 0)
                value = value.Substring(endThink + 8);
            else if (value.Contains("<think>"))
                return "";
            return BoundSummary(value.Trim());
        }

        private static MNNChatMessage SummaryMessage(string summary) => new MNNChatMessage(MNNChatRole.System, "Use this compressed memory as background context. Follow the current user request if it conflicts.\n" + summary);
        internal static string BoundSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
                return "";
            summary = summary.Trim();
            if (summary.Length > MaximumSummaryCharacters)
                summary = summary.Substring(0, MaximumSummaryCharacters).TrimEnd();
            return summary;
        }

        internal static float Cosine(float[] first, float[] second)
        {
            if (first == null || second == null || first.Length == 0 || first.Length != second.Length)
                throw new ArgumentException("The embedding model returned incompatible vectors.");
            double dot = 0, a = 0, b = 0;
            for (int i = 0; i < first.Length; ++i)
            {
                if (float.IsNaN(first[i]) || float.IsInfinity(first[i]) || float.IsNaN(second[i]) || float.IsInfinity(second[i]))
                    throw new ArgumentException("The embedding model returned non-finite values.");
                dot += (double)first[i] * second[i];
                a += (double)first[i] * first[i];
                b += (double)second[i] * second[i];
            }

            if (a <= 0 || b <= 0)
                throw new ArgumentException("The embedding model returned a zero vector.");
            return (float)Math.Max(-1, Math.Min(1, dot / Math.Sqrt(a * b)));
        }
    }
}
