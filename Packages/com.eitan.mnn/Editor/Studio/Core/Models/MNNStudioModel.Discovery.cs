using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioModel
    {
        internal static MNNStudioModel Read(string directory)
        {
            if (!System.IO.Directory.Exists(directory) || !IsModelDirectory(directory))
                throw new ArgumentException("Choose a complete MNN model folder containing config.json, MNN weights or an installed repository marker.", nameof(directory));
            string name = Path.GetFileName(directory);
            if (MNNCosyVoice300MModel.HasLayout(directory))
                return new MNNStudioModel(name, Path.GetFullPath(directory), MNNStudioTask.SpeechSynthesis, (MNNModelCapabilities)0, MNNModelCategory.SpeechSynthesis.ToString());
            if (File.Exists(Path.Combine(directory, MNNDtlnAecModel.ConfigName)))
                return new MNNStudioModel(name, Path.GetFullPath(directory), MNNStudioTask.EchoCancellation, MNNModelCapabilities.AudioInput, MNNModelCategory.EchoCancellation.ToString());
            string marker = Path.Combine(directory, MNNModelDownloadTasks.InstallMarker);
            string identity = File.Exists(marker) ? File.ReadAllText(marker).Trim() : name;
            string category = MNNModelRepository.Classify(identity);
            var task = MNNStudioTasks.FromCategory(category);
            if (File.Exists(Path.Combine(directory, MNNTextDecisionModel.Files[0])) && File.Exists(Path.Combine(directory, "tokenizer.json")) && File.Exists(Path.Combine(directory, "manifest.json")))
                return new MNNStudioModel(name, Path.GetFullPath(directory), MNNStudioTask.NLP, (MNNModelCapabilities)0, MNNModelCategory.NLP.ToString());
            if (File.Exists(Path.Combine(directory, MNNDocumentLayoutModel.Metadata)) && File.Exists(Path.Combine(directory, MNNDocumentLayoutModel.Graph)))
                return new MNNStudioModel(name, Path.GetFullPath(directory), MNNStudioTask.DocumentLayout, MNNModelCapabilities.Vision, MNNModelCategory.DocumentLayout.ToString());
            if (File.Exists(Path.Combine(directory, MNNImageEmbeddingModel.Marker)))
                return new MNNStudioModel(name, Path.GetFullPath(directory), MNNStudioTask.ImageEmbedding, (MNNModelCapabilities)0, MNNModelCategory.ImageEmbedding.ToString());
            if (File.Exists(Path.Combine(directory, MNNVbxConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSpeakerModelConfig.FileName)) || File.Exists(Path.Combine(directory, MNNWeSpeakerModelConfig.FileName)))
                return new MNNStudioModel(name, Path.GetFullPath(directory), MNNStudioTask.SpeakerRecognition, (MNNModelCapabilities)0, MNNModelCategory.SpeakerRecognition.ToString());
            if (File.Exists(Path.Combine(directory, MNNImageTaskConfig.FileName)))
                return new MNNStudioModel(name, Path.GetFullPath(directory), ImageTaskFor(MNNImageTaskConfig.Read(directory).profile), MNNModelCapabilities.Vision, category);
            if (File.Exists(Path.Combine(directory, MNNZipformerConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSenseVoiceConfig.FileName)) || File.Exists(Path.Combine(directory, MNNWhisperConfig.FileName)) || File.Exists(Path.Combine(directory, MNNQwen3AsrConfig.FileName)) || File.Exists(Path.Combine(directory, MNNParaformerConfig.FileName)))
                return new MNNStudioModel(name, directory, MNNStudioTask.SpeechRecognition, (MNNModelCapabilities)0, MNNModelCategory.SpeechRecognition.ToString());
            if (File.Exists(Path.Combine(directory, MNNKeywordSpotterConfig.FileName)))
                return new MNNStudioModel(name, directory, MNNStudioTask.KeywordSpotting, (MNNModelCapabilities)0, MNNModelCategory.KeywordSpotting.ToString());
            if (File.Exists(Path.Combine(directory, MNNDualTurnEndpointingConfig.FileName)))
                return new MNNStudioModel(name, directory, MNNStudioTask.AudioProcessing, MNNModelCapabilities.AudioInput, MNNModelCategory.AudioProcessing.ToString());
            if (File.Exists(Path.Combine(directory, MNNTurnSenseConfig.FileName)))
                return new MNNStudioModel(name, directory, MNNStudioTask.AudioProcessing, (MNNModelCapabilities)0, MNNModelCategory.AudioProcessing.ToString());
            if (File.Exists(Path.Combine(directory, MNNFsEendConfig.FileName)))
                return new MNNStudioModel(name, directory, MNNStudioTask.AudioProcessing, (MNNModelCapabilities)0, MNNModelCategory.AudioProcessing.ToString());
            if (File.Exists(Path.Combine(directory, MNNCueLightConfig.FileName)))
                return new MNNStudioModel(name, directory, MNNStudioTask.AudioProcessing, (MNNModelCapabilities)0, MNNModelCategory.AudioProcessing.ToString());
            if (File.Exists(Path.Combine(directory, MNNLsEendConfig.FileName)))
                return new MNNStudioModel(name, directory, MNNStudioTask.AudioProcessing, (MNNModelCapabilities)0, MNNModelCategory.AudioProcessing.ToString());
            if (File.Exists(Path.Combine(directory, MNNVoxTurnModel.ConfigFile)) || File.Exists(Path.Combine(directory, MNNDccrnConfig.FileName)) || File.Exists(Path.Combine(directory, MNNTigerConfig.FileName)) || File.Exists(Path.Combine(directory, MNNResembleEnhancerConfig.FileName)) || File.Exists(Path.Combine(directory, MNNResembleDenoiserConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSegConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSpExPlusConfig.FileName)) || File.Exists(Path.Combine(directory, MNNAvConvTasNetConfig.FileName)) || File.Exists(Path.Combine(directory, MNNAvDprnnConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSepReformerConfig.FileName)) || File.Exists(Path.Combine(directory, MNNDcunetConfig.FileName)) || File.Exists(Path.Combine(directory, MNNDccrnetConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSudoRmRfConfig.FileName)) || File.Exists(Path.Combine(directory, MNNNamoTurnDetectorConfig.FileName)) || File.Exists(Path.Combine(directory, MNNDptnetConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSortformerConfig.FileName)) || File.Exists(Path.Combine(directory, MNNDprnnConfig.FileName)) || File.Exists(Path.Combine(directory, MNNConvTasNetConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSepFormerConfig.FileName)) || File.Exists(Path.Combine(directory, MNNMossFormer2SeConfig.FileName)) || File.Exists(Path.Combine(directory, MNNMossFormerGanConfig.FileName)) || File.Exists(Path.Combine(directory, MNNFullSubNetConfig.FileName)) || File.Exists(Path.Combine(directory, MNNGtcrnConfig.FileName)) || File.Exists(Path.Combine(directory, MNNDtlnConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSmartTurnConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSileroVadConfig.FileName)) ||
                File.Exists(Path.Combine(directory, "mnn_fsmn_vad.json")) || File.Exists(Path.Combine(directory, "mnn_frcrn_se.json")) ||
                File.Exists(Path.Combine(directory, MNNMarbleNetVadConfig.FileName)) || File.Exists(Path.Combine(directory, "mnn_deepfilternet3.json")) ||
                File.Exists(Path.Combine(directory, "mnn_livekit_turn_detector.json")) || File.Exists(Path.Combine(directory, "mnn_mossformer2_ss.json")) ||
                File.Exists(Path.Combine(directory, MNNAvMossFormer2TseConfig.FileName)))
                return new MNNStudioModel(name, directory, MNNStudioTask.AudioProcessing, (MNNModelCapabilities)0, MNNModelCategory.AudioProcessing.ToString());
            string rootConfig = Path.Combine(directory, "config.json");
            var pipelineConfig = File.Exists(rootConfig) ? JsonUtility.FromJson<Config>(File.ReadAllText(rootConfig)) : null;
            if (File.Exists(Path.Combine(directory, MNNPaddleTextConfig.FileName)))
            {
                category = MNNModelCategory.OCRModel.ToString();
                task = MNNStudioTask.OCR;
            }
            else if (File.Exists(Path.Combine(directory, MNNYoloV8Config.FileName)))
            {
                category = MNNModelCategory.ObjectDetection.ToString();
                task = MNNStudioTask.ObjectDetection;
            }
            else if (MNNBertEmbeddingConfig.IsConfigured(directory))
            {
                category = MNNModelCategory.EmbeddingModel.ToString();
                task = MNNStudioTask.Embedding;
            }
            else if (MNNIndicF5Tts.IsComplete(directory) || MNNPocketTts.IsComplete(directory) || MNNQwen3ExplicitTts.HasLayout(directory) || MNNDhVaani.IsComplete(directory) || MNNMossNano.IsComplete(directory) || MNNSherpaVitsChinese.IsComplete(directory) || MNNMmsTamil.IsComplete(directory) || MNNSupertonic.IsComplete(directory) || MNNKokoroModel.IsComplete(directory) || MNNIndicVits.IsComplete(directory) || File.Exists(Path.Combine(directory, "gpt_prefill_fp16.mnn")) || File.Exists(Path.Combine(directory, "redae_encoder.mnn")) || File.Exists(Path.Combine(MNNCosyVoiceModel.ResolveDirectory(directory), "config-cpu-cosyvoice-ras.json")))
            {
                category = MNNModelCategory.SpeechSynthesis.ToString();
                task = MNNStudioTask.SpeechSynthesis;
            }
            else if (pipelineConfig?.modelType == "zipformer" || File.Exists(Path.Combine(directory, "asr_config.json")) && File.Exists(Path.Combine(directory, "encoder.mnn")) && File.Exists(Path.Combine(directory, "decoder.mnn")))
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
            if (config.model_type == "qwen3_asr")
            {
                category = MNNModelCategory.SpeechRecognition.ToString();
                task = MNNStudioTask.SpeechRecognition;
            }

            // Diffusion/TTS/ASR use separate pipelines even when they contain config.json.
            bool dedicated = task == MNNStudioTask.ImageGeneration || task == MNNStudioTask.SpeechSynthesis || task == MNNStudioTask.SpeechRecognition || task == MNNStudioTask.Embedding || task == MNNStudioTask.Reranker || task == MNNStudioTask.ObjectDetection || task == MNNStudioTask.OCR || task == MNNStudioTask.AudioProcessing || task == MNNStudioTask.KeywordSpotting;
            if (!dedicated && (File.Exists(metadata) || category == MNNModelCategory.CodeModel.ToString() || category == MNNModelCategory.SafetyModel.ToString()))
                task = MNNStudioTask.Chat;
            // Match runtime capabilities: talker weights do not imply a supported waveform ABI.
            var capabilities = (config.is_visual ? MNNModelCapabilities.Vision : 0) | (config.is_audio ? MNNModelCapabilities.AudioInput : 0);
            return new MNNStudioModel(name, Path.GetFullPath(directory), task, capabilities, category);
        }

        internal static List<MNNStudioModel> Discover(IEnumerable<string> roots, ICollection<string> errors = null, CancellationToken cancellation = default)
        {
            var found = new Dictionary<string, MNNStudioModel>(StringComparer.Ordinal);
            foreach (string root in roots.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
            {
                cancellation.ThrowIfCancellationRequested();
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
                            AddLanguageVariants(model, found, errors, cancellation);
                        }
                        catch (Exception error)when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
                        {
                            errors?.Add(Path.GetFileName(root) + ": " + error.Message);
                        }

                        continue;
                    }

                    foreach (string directory in System.IO.Directory.GetDirectories(root).OrderBy(value => value, StringComparer.Ordinal))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (!IsModelDirectory(directory))
                            continue;
                        try
                        {
                            var model = Read(directory);
                            found[model.Directory] = model;
                            AddLanguageVariants(model, found, errors, cancellation);
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

        private static void AddLanguageVariants(MNNStudioModel repository, IDictionary<string, MNNStudioModel> found, ICollection<string> errors, CancellationToken cancellation)
        {
            if (repository.IsPaddleText)
            {
                string variants = Path.Combine(repository.Directory, "recognizers");
                if (!System.IO.Directory.Exists(variants))
                    return;
                foreach (string metadata in System.IO.Directory.EnumerateFiles(variants, MNNPaddleTextConfig.FileName, SearchOption.AllDirectories))
                {
                    cancellation.ThrowIfCancellationRequested();
                    string directory = Path.GetDirectoryName(metadata);
                    try
                    {
                        var config = MNNPaddleTextConfig.Read(directory);
                        config.Validate(directory);
                        var model = Read(directory);
                        found[model.Directory] = new MNNStudioModel(repository.Name + "/" + Path.GetFileName(directory), model.Directory, MNNStudioTask.OCR, MNNModelCapabilities.Vision, MNNModelCategory.OCRModel.ToString())
                        {OcrPageRoot = repository.IsPaddlePage ? repository.Directory : null, OcrRecognizerProfile = directory.Substring(repository.Directory.Length).TrimStart(Path.DirectorySeparatorChar)};
                    }
                    catch (Exception error)when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
                    {
                        errors?.Add(directory + ": " + error.Message);
                    }
                }

                return;
            }

            if (repository.TtsProfile == MNNTtsModelProfile.Supertonic)
            {
                foreach (string variant in new[]{"v2", "v3"})
                {
                    cancellation.ThrowIfCancellationRequested();
                    string directory = Path.Combine(repository.Directory, variant);
                    if (MNNSupertonic.IsComplete(directory))
                        found[directory] = new MNNStudioModel(repository.Name + "/" + variant, directory, MNNStudioTask.SpeechSynthesis, (MNNModelCapabilities)0, MNNModelCategory.SpeechSynthesis.ToString());
                }

                return;
            }

            // Multi-precision community repositories can contain several independent language models.
            // Do not expose the private language stages of diffusion, TTS or ASR as chat models.
            if (repository.Task == MNNStudioTask.ImageGeneration || repository.Task == MNNStudioTask.SpeechSynthesis || repository.Task == MNNStudioTask.SpeechRecognition || File.Exists(Path.Combine(repository.Directory, "llm_config.json")))
                return;
            foreach (string metadata in System.IO.Directory.EnumerateFiles(repository.Directory, "llm_config.json", SearchOption.AllDirectories))
            {
                cancellation.ThrowIfCancellationRequested();
                string directory = Path.GetDirectoryName(metadata);
                if (!File.Exists(Path.Combine(directory, "config.json")) || !System.IO.Directory.EnumerateFiles(directory, "*.mnn").Any())
                    continue;
                try
                {
                    var config = JsonUtility.FromJson<Config>(File.ReadAllText(Path.Combine(directory, "config.json")));
                    string tokenizer = string.IsNullOrEmpty(config?.tokenizer_file) ? "tokenizer.txt" : config.tokenizer_file;
                    if (!File.Exists(Path.Combine(directory, tokenizer)))
                        continue;
                    var model = Read(directory);
                    var task = repository.Task == MNNStudioTask.Embedding || repository.Task == MNNStudioTask.Reranker ? repository.Task : model.Task;
                    found[model.Directory] = new MNNStudioModel(repository.Name + "/" + directory.Substring(repository.Directory.Length + 1).Replace('\\', '/'), model.Directory, task, model.Capabilities, repository.Category);
                }
                catch (Exception error)when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
                {
                    errors?.Add(directory + ": " + error.Message);
                }
            }
        }

        private static bool IsModelDirectory(string directory)
        {
            // Common install/config markers establish the same discovery eligibility
            // without probing every specialized model protocol first.
            return File.Exists(Path.Combine(directory, MNNModelDownloadTasks.InstallMarker)) ||
                File.Exists(Path.Combine(directory, "config.json")) ||
                HasSpecializedModelLayout(directory);
        }

        private static bool HasSpecializedModelLayout(string directory) => MNNCosyVoice300MModel.HasLayout(directory) || MNNIndicF5Tts.IsComplete(directory) || MNNPocketTts.IsComplete(directory) || MNNQwen3ExplicitTts.HasLayout(directory) || MNNDhVaani.IsComplete(directory) || MNNMossNano.IsComplete(directory) || MNNSherpaVitsChinese.IsComplete(directory) || MNNMmsTamil.IsComplete(directory) || MNNSupertonic.IsComplete(directory) || MNNKokoroModel.IsComplete(directory) || File.Exists(Path.Combine(directory, MNNKeywordSpotterConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSmartTurnConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSileroVadConfig.FileName)) || File.Exists(Path.Combine(directory, "mnn_fsmn_vad.json")) || File.Exists(Path.Combine(directory, "mnn_frcrn_se.json")) || File.Exists(Path.Combine(directory, MNNMarbleNetVadConfig.FileName)) || File.Exists(Path.Combine(directory, "mnn_livekit_turn_detector.json")) || File.Exists(Path.Combine(directory, "mnn_mossformer2_ss.json")) || File.Exists(Path.Combine(directory, MNNWeSpeakerModelConfig.FileName)) || File.Exists(Path.Combine(directory, MNNParaformerConfig.FileName)) || File.Exists(Path.Combine(directory, MNNQwen3AsrConfig.FileName)) || File.Exists(Path.Combine(directory, MNNSenseVoiceConfig.FileName)) || File.Exists(Path.Combine(directory, MNNZipformerConfig.FileName)) || File.Exists(Path.Combine(directory, MNNWhisperConfig.FileName)) || File.Exists(Path.Combine(MNNCosyVoiceModel.ResolveDirectory(directory), "config-cpu-cosyvoice-ras.json")) || HasDiffusionStages(directory) || HasDiffusionStages(Path.Combine(directory, "general")) || HasDiffusionStages(Path.Combine(directory, "opencl")) || System.IO.Directory.EnumerateFiles(directory, "*.mnn", SearchOption.TopDirectoryOnly).Any();
        private static bool HasDiffusionStages(string directory) => File.Exists(Path.Combine(directory, "text_encoder.mnn")) && File.Exists(Path.Combine(directory, "unet.mnn")) && File.Exists(Path.Combine(directory, "vae_decoder.mnn"));
    }
}
