using System;
using System.IO;
using System.Linq;
using MNN.Unity;
using UnityEngine;

namespace MNN.Unity
{
    public static class MNNConversationModelFiles
    {
        // These are file contracts only. Native loading still validates graph/operator compatibility.
        [Serializable]
        private sealed class LocalConfig
        {
            public string llm_model = "llm.mnn", llm_weight = "llm.mnn.weight";
            public string model = "model.mnn", tokens = "tokens.txt", model_path = "", asset_folder = "";
        }

        public static string FindExistingDirectory(string root, MNNRemoteModel model, string target)
        {
            try
            {
                if (File.Exists(Path.Combine(target, MNNModelInstallation.ReceiptName)) || IsExistingComplete(target, model)) return target;
                if (!Directory.Exists(root)) return null;
                if (model.provider == MNNModelSource.ModelScope && model.repositoryId == MNNMiniMindO.Repository)
                {
                    string prepared = Path.Combine(root, MNNMiniMindO.InstallationName);
                    if (IsExistingComplete(prepared, model)) return prepared;
                }
                string repository = model.repositoryId ?? model.modelScopeId;
                string name = repository.Split('/').Last();
                bool defaultRevision = string.IsNullOrEmpty(model.revision) || model.revision == MNNConversationModelCatalog.DefaultRevision(model.provider);
                var matches = Directory.GetDirectories(root).Where(directory =>
                    (defaultRevision && (string.Equals(Path.GetFileName(directory), name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileName(directory), repository.Replace("/", "--"), StringComparison.OrdinalIgnoreCase))) ||
                    MarkerMatches(directory, model, false)).Where(directory =>
                    !File.Exists(Path.Combine(directory, MNNModelInstallation.ReceiptName)) && IsExistingComplete(directory, model)).ToArray();
                return matches.Length == 1 ? matches[0] : null;
            }
            catch (IOException) { }
            catch (InvalidDataException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
            return null;
        }

        private static bool MarkerMatches(string directory, MNNRemoteModel model, bool allowMissing)
        {
            string marker = Path.Combine(directory, ".mnn-modelscope-installed");
            if (!File.Exists(marker)) return allowMissing;
            string value = File.ReadAllText(marker).Trim();
            string repository = model.repositoryId ?? model.modelScopeId;
            string expected = model.provider == MNNModelSource.HuggingFace ? "hf:" + repository + "@" +
                (model.revision ?? "main") : repository;
            if (string.Equals(value, expected, StringComparison.OrdinalIgnoreCase)) return true;
            // These built-in presets also have official/registered ModelScope exports.
            // Reuse their local prepared files after checking the adapter contract below.
            string preset = MNNConversationModelCatalog.DefaultInstallationName(model);
            string mirror = preset == "Supertonic-3-INT8-MNN" ? "dr3334/Supertonic-3-INT8-MNN" :
                preset == "bert-vits2-MNN" || preset == "Hunyuan-1.8B-Instruct-MNN" || preset == "supertonic-tts-mnn"
                    ? "MNN/" + preset : null;
            return mirror != null && string.Equals(value, mirror, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasProfileGraphs(string directory, string profile)
        {
            if (profile == null || !File.Exists(Path.Combine(directory, profile))) return false;
            return MNNAudioProfileFiles.Graphs(directory, profile).All(file =>
            {
                string path = MNNModelAssetDownloader.ResolvePath(directory, file);
                return File.Exists(path) && new FileInfo(path).Length > 0;
            });
        }

        public static bool IsExistingComplete(string directory, MNNRemoteModel model)
        {
            if (!Directory.Exists(directory) || !MarkerMatches(directory, model, true) ||
                !MNNConversationModelCatalog.TryGetAdapter(model, out var adapter)) return false;
            bool Has(params string[] files) => files.All(file =>
            {
                if (string.IsNullOrEmpty(file) || Path.IsPathRooted(file)) return false;
                string relative = string.Join("/", file.Split('/').Where(part => part != "." && part.Length > 0));
                string path = MNNModelAssetDownloader.ResolvePath(directory, relative);
                return File.Exists(path) && new FileInfo(path).Length > 0;
            });
            LocalConfig Config(string name = "config.json") => Has(name)
                ? JsonUtility.FromJson<LocalConfig>(File.ReadAllText(Path.Combine(directory, name))) : new LocalConfig();
            try
            {
                switch (adapter)
                {
                    case MNNConversationModelAdapter.MiniMindO:
                        return MNNMiniMindO.IsComplete(directory);
                    case MNNConversationModelAdapter.Language:
                    case MNNConversationModelAdapter.Omni:
                        var language = Config();
                        return Has("config.json", "llm_config.json", language.llm_model, language.llm_weight) &&
                            (Has("tokenizer.txt") || Has("tokenizer.mtok")) &&
                            (adapter != MNNConversationModelAdapter.Omni || Has("audio.mnn", "audio.mnn.weight"));
                    case MNNConversationModelAdapter.SenseVoice:
                        var recognition = Config();
                        return Has(recognition.model, recognition.tokens);
                    case MNNConversationModelAdapter.Paraformer:
                        return Has("mnn_paraformer.json", "encoder.mnn", "decoder.mnn", "asr_config.json", "config.json", "tokenizer.txt");
                    case MNNConversationModelAdapter.Qwen3Asr:
                        return Has("config.json", "llm_config.json", "llm.mnn", "llm.mnn.weight", "audio.mnn",
                            "audio.mnn.weight", "embeddings_bf16.bin", "tokenizer.txt");
                    case MNNConversationModelAdapter.DtlnAec:
                        string profile = MNNConversationModelCatalog.DefaultInstallationName(model).Replace("DTLN-aec-", "dtln_aec_");
                        return Has("mnn_dtln_aec.json", profile.Replace("-MNN", "_1.mnn"), profile.Replace("-MNN", "_2.mnn"));
                    case MNNConversationModelAdapter.Silero:
                        return Has(MNNSileroVadConfig.FileName) && Has(MNNAcousticModelFiles.Vad(directory));
                    case MNNConversationModelAdapter.SpeechSynthesis:
                        return MNNSpeechSynthesizer.HasProfile(directory);
                    case MNNConversationModelAdapter.SpeechRecognition:
                        return HasProfileGraphs(directory, MNNSpeechRecognizer.ProfileFile(directory));
                    case MNNConversationModelAdapter.NoiseReduction:
                        return HasProfileGraphs(directory, MNNNoiseSuppressor.ProfileFile(directory));
                    case MNNConversationModelAdapter.Separation:
                        return HasProfileGraphs(directory, MNNSpeechSeparator.ProfileFile(directory));
                    case MNNConversationModelAdapter.SpeakerEmbedding:
                        return MNNSpeakerEncoder.ProfileFile(directory) != null && Has(MNNAcousticModelFiles.Speaker(directory));
                    case MNNConversationModelAdapter.KeywordSpotting:
                        return Has(MNNKeywordSpotterConfig.FileName) && Has(MNNAcousticModelFiles.Wake(directory));
                    case MNNConversationModelAdapter.VoiceActivity:
                        return MNNVoiceActivityDetector.HasProfile(directory) && HasProfileGraphs(directory, MNNVoiceActivityDetector.ProfileFile(directory));
                    case MNNConversationModelAdapter.AudioTurn:
                    case MNNConversationModelAdapter.TextTurn:
                    case MNNConversationModelAdapter.SmartTurn:
                        return MNNTurnDetector.ProfileFile(directory) != null && HasProfileGraphs(directory, MNNTurnDetector.ProfileFile(directory));
                    case MNNConversationModelAdapter.Supertonic:
                        string precision = MNNConversationModelCatalog.DefaultInstallationName(model) == "Supertonic-3-INT8-MNN" ||
                            (model.repositoryId ?? model.modelScopeId).IndexOf("int8", StringComparison.OrdinalIgnoreCase) >= 0 ? "int8" : "fp16";
                        return Has("mnn_models/tts.json", "mnn_models/unicode_indexer.json") &&
                            new[] { "duration_predictor", "text_encoder", "vector_estimator", "vocoder" }
                                .All(stage => Has("mnn_models/" + precision + "/" + stage + ".mnn")) &&
                            new[] { "M1", "M2", "M3", "M4", "M5", "F1", "F2", "F3", "F4", "F5" }
                                .Any(voice => Has("voice_styles/" + voice + ".json") || Has("mnn_models/voice_styles/" + voice + ".json"));
                    case MNNConversationModelAdapter.CosyVoice:
                        string prefix = Has("manifest.json") ? "" : "model/";
                        return new[] { "config-cpu-cosyvoice-ras.json", "embeddings_bf16.bin", "flow-conditioner.fp32.mnn",
                            "flow.cfg-student-2step.batch1.fp16.mnn", "flow.cfg-student-2step.batch1.fp16.mnn.weight",
                            "hift-core.fp32.mnn", "hift-f0.fp32.mnn", "llm_config.json", "llm.mnn", "llm.mnn.weight",
                            "manifest.json", "prompt-cond.bin", "prompt-speech-tokens.csv", "rand-noise.bin",
                            "source-linear-bias.bin", "source-linear-weight.bin", "spks.bin", "tokenizer.mtok" }
                            .All(file => Has(prefix + file));
                    case MNNConversationModelAdapter.Kokoro:
                        if (Has("model.mnn", "voices.bin", "tokens.txt", "lexicon-zh.txt", "lexicon-us-en.txt")) return true;
                        return Has("tts.mnn", "tokenizer.txt") && (Has("lexicon-zh.txt") || Has("lexicon-us-en.txt")) &&
                            (Has("voices.mnn", "tts_config.json") || Directory.Exists(Path.Combine(directory, "voices")) &&
                                Directory.GetFiles(Path.Combine(directory, "voices"), "*.bin").Any(file => new FileInfo(file).Length == 510 * 256 * 4));
                    case MNNConversationModelAdapter.SherpaVitsChinese:
                        return Has("model.mnn", "model.mnn.weight", "tokens.txt", "lexicon.txt", "G_multisperaker_latest.json");
                    case MNNConversationModelAdapter.Piper:
                        var piper = Config();
                        string voice = Path.GetFileNameWithoutExtension(piper.model_path);
                        return Has("config.json", piper.model_path) &&
                            (Has("assets/" + voice + ".onnx.json") || Has(voice + ".onnx.json") || Has(voice + ".mnn.json")) &&
                            new[] { piper.asset_folder + "/espeak-ng-data/", piper.asset_folder + "/", "espeak-ng-data/" }
                                .Any(folder => Has(folder + "phontab", folder + "phondata", folder + "phonindex", folder + "intonations"));
                    case MNNConversationModelAdapter.BertVits2:
                        var bert = Config();
                        return Has("config.json", bert.model_path, bert.asset_folder + "/common/mnn_models/chinese_bert.mnn") &&
                            new[] { "tokenizer.txt", "pinyin_dict.bin", "phrases_dict.bin", "word_freq.bin", "char_state.bin",
                                "prob_emit.bin", "prob_start.bin", "prob_trans.bin", "eng_dict.bin", "hotwords_cn.bin" }
                                .All(file => Has(bert.asset_folder + "/common/text_processing_jsons/" + file));
                    case MNNConversationModelAdapter.MossNano:
                        bool cached = Has("tts/tokenizer.model");
                        string tts = cached ? "tts/" : "";
                        return Has(tts + (cached ? "moss_tts_prefill_nocumsum.mnn" : "moss_tts_prefill.mnn"),
                            tts + "moss_tts_decode_step.mnn", tts + (cached ? "moss_tts_local_cached_step.mnn" : "moss_tts_local_fixed_sampled_frame.mnn"),
                            tts + (cached ? "tokenizer.model" : "tokenizer_mnn.txt"), tts + "browser_poc_manifest.json",
                            (cached ? "audio_tokenizer/" : "") + "moss_audio_tokenizer_decode_full.mnn");
                }
            }
            catch (IOException) { }
            catch (InvalidDataException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
            return false;
        }
    }
}
