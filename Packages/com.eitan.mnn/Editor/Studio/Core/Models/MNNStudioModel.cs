using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioModel
    {
        internal readonly string Name, Directory, Category;
        internal readonly MNNStudioTask Task;
        internal readonly MNNStudioTask AudioSubtask;
        internal readonly MNNModelCapabilities Capabilities;
        internal readonly bool SupportsWhisperChinese;
        internal readonly bool IsAvMossFormer2Tse;
        internal readonly bool RequiresVisualFrames;
        internal readonly bool IsAvDprnn;
        internal readonly bool IsAvConvTasNet;
        internal readonly bool IsSpExPlus;
        internal readonly bool IsSeg;
        internal bool RequiresExactVisualFrames => IsAvDprnn || IsAvConvTasNet;
        private readonly bool _hasAvMossFormer2Pipeline;
        internal string DisplayName => string.Equals(Name, "CosyVoice-300M-MNN", StringComparison.OrdinalIgnoreCase) ? Name : Name.Replace("-MNN", string.Empty);
        internal string CapabilityLabel => Task != MNNStudioTask.Chat ? MNNStudioTasks.Get((int)Task).Name : Supports(MNNModelCapabilities.Vision | MNNModelCapabilities.AudioInput) ? "Omni" : Supports(MNNModelCapabilities.Vision) ? "Vision + text" : Supports(MNNModelCapabilities.AudioInput) ? "Audio + text" : "Text";
        internal bool Supports(MNNModelCapabilities value) => (Capabilities & value) == value;
        private MNNTtsModelProfile? _ttsProfile;
        private string[] _speechLanguages;
        private string[] _ttsVoices;
        private readonly Config _taskConfig;
        internal string OcrPageRoot, OcrRecognizerProfile = ".";
        internal MNNStudioModel(string name, string directory, MNNStudioTask task, MNNModelCapabilities capabilities, string category = null)
        {
            Name = name;
            Directory = directory;
            Task = task;
            AudioSubtask = task == MNNStudioTask.AudioProcessing ? AudioSubtaskForDirectory(directory) : task;
            Capabilities = capabilities;
            Category = category ?? MNNModelRepository.Classify(name);
            _taskConfig = ReadTaskConfigFromDisk();
            _snapshotIsDualTurn = new Lazy<bool>(() => File.Exists(Path.Combine(Directory, MNNDualTurnEndpointingConfig.FileName)));
            _snapshotIsTurnSense = new Lazy<bool>(() => File.Exists(Path.Combine(Directory, MNNTurnSenseConfig.FileName)));
            _snapshotIsVoxTurn = new Lazy<bool>(() => File.Exists(Path.Combine(Directory, MNNVoxTurnModel.ConfigFile)));
            _snapshotHasEchoCancellationPipeline = new Lazy<bool>(() => Task == MNNStudioTask.EchoCancellation && MNNDtlnAecModel.IsComplete(Directory));
            _snapshotIsKeywordSpotter = new Lazy<bool>(() => File.Exists(Path.Combine(Directory, MNNKeywordSpotterConfig.FileName)));
            _snapshotHasKeywordSpottingPipeline = new Lazy<bool>(() => Task == MNNStudioTask.KeywordSpotting && MNNKwsModel.IsComplete(Directory));
            _snapshotHasAudioProcessingPipeline = new Lazy<bool>(() => Task == MNNStudioTask.AudioProcessing && (MNNDualTurnEndpointingModel.IsComplete(Directory) || MNNTurnSenseModel.IsComplete(Directory) || MNNFsEendModel.IsComplete(Directory) || MNNCueLightModel.IsComplete(Directory) || MNNLsEendModel.IsComplete(Directory) || MNNVoxTurnModel.IsComplete(Directory) || MNNDccrnModel.IsComplete(Directory) || MNNTigerModel.IsComplete(Directory) || MNNResembleEnhancerModel.IsComplete(Directory) || MNNResembleDenoiserModel.IsComplete(Directory) || MNNSegModel.IsComplete(Directory) || MNNSpExPlusModel.IsComplete(Directory) || MNNAvConvTasNetModel.IsComplete(Directory) || MNNAvDprnnModel.IsComplete(Directory) || MNNSepReformerModel.IsComplete(Directory) || MNNDcunetModel.IsComplete(Directory) || MNNDccrnetModel.IsComplete(Directory) || MNNSudoRmRfModel.IsComplete(Directory) || MNNNamoTurnDetectorModel.IsComplete(Directory) || MNNSileroModel.IsComplete(Directory) || MNNSmartTurnModel.IsComplete(Directory) || MNNFsmnVadModel.IsComplete(Directory) || MNNFrcrnSeModel.IsComplete(Directory) || MNNMarbleNetVadModel.IsComplete(Directory) || MNNMossFormer2Model.IsComplete(Directory) || MNNLiveKitTurnDetectorModel.IsComplete(Directory) || MNNDeepFilterNet3Model.IsComplete(Directory) || MNNDtlnModel.IsComplete(Directory) || MNNGtcrnModel.IsComplete(Directory) || MNNFullSubNetModel.IsComplete(Directory) || MNNDptnetModel.IsComplete(Directory) || MNNSortformerModel.IsComplete(Directory) || MNNDprnnModel.IsComplete(Directory) || MNNConvTasNetModel.IsComplete(Directory) || MNNSepFormerModel.IsComplete(Directory) || MNNMossFormer2SeModel.IsComplete(Directory) || MNNMossFormerGanModel.IsComplete(Directory) || _hasAvMossFormer2Pipeline));
            _snapshotHasDocumentLayoutPipeline = new Lazy<bool>(() => Task == MNNStudioTask.DocumentLayout && MNNDocumentLayoutModel.IsComplete(Directory));
            _snapshotHasTextDecisionPipeline = new Lazy<bool>(() => Task == MNNStudioTask.NLP && MNNTextDecisionModel.IsComplete(Directory));
            _snapshotHasImageEmbeddingPipeline = new Lazy<bool>(() => Task == MNNStudioTask.ImageEmbedding && MNNImageEmbeddingModel.IsComplete(Directory));
            _snapshotHasSpeakerRecognitionPipeline = new Lazy<bool>(() => Task == MNNStudioTask.SpeakerRecognition && (MNNVbxModel.IsComplete(Directory) || MNNSpeakerModel.IsComplete(Directory) || MNNWeSpeakerModel.IsComplete(Directory)));
            _snapshotHasSpeechRecognitionPipeline = new Lazy<bool>(() => Task == MNNStudioTask.SpeechRecognition && (MNNZipformerModel.IsComplete(Directory) || MNNSenseVoiceModel.IsComplete(Directory) || MNNWhisperModel.IsComplete(Directory) || MNNQwen3AsrModel.IsComplete(Directory) || MNNParaformerModel.IsComplete(Directory)));
            _snapshotIsYoloV8 = new Lazy<bool>(() => File.Exists(Path.Combine(Directory, MNNYoloV8Config.FileName)));
            _snapshotIsImageTask = new Lazy<bool>(() => File.Exists(Path.Combine(Directory, MNNImageTaskConfig.FileName)));
            _snapshotIsPaddleText = new Lazy<bool>(() => File.Exists(Path.Combine(Directory, MNNPaddleTextConfig.FileName)));
            _snapshotIsPaddlePage = new Lazy<bool>(() => File.Exists(Path.Combine(OcrPageRoot ?? Directory, MNNPaddleOcrConfig.FileName)));
            _snapshotHasVisionPipeline = new Lazy<bool>(CheckHasVisionPipeline);
            _snapshotHasGenerationPipeline = new Lazy<bool>(CheckHasGenerationPipeline);
            _snapshotIsCosyVoice = new Lazy<bool>(() => !IsCosyVoice300M && (File.Exists(Path.Combine(MNNCosyVoiceModel.ResolveDirectory(Directory), "config-cpu-cosyvoice-ras.json")) || string.Equals(Name, "Cosy-Voice-MNN", StringComparison.OrdinalIgnoreCase) || Category == MNNModelCategory.SpeechSynthesis.ToString() && Name.IndexOf("cosyvoice", StringComparison.OrdinalIgnoreCase) >= 0));
            _snapshotIsIndexTts = new Lazy<bool>(() => File.Exists(Path.Combine(Directory, "gpt_prefill_fp16.mnn")) || Name.Equals("IndexTTS-2.5-mnn", StringComparison.OrdinalIgnoreCase));
            _snapshotIsFireRedTts = new Lazy<bool>(() => File.Exists(Path.Combine(Directory, "redae_encoder.mnn")) || Name.Equals("fireredtts3-mnn", StringComparison.OrdinalIgnoreCase));
            _snapshotSupertonicPrecision = new Lazy<string>(ReadSupertonicPrecision);
            IsAvMossFormer2Tse = File.Exists(Path.Combine(directory, MNNAvMossFormer2TseConfig.FileName));
            IsAvDprnn = File.Exists(Path.Combine(directory, MNNAvDprnnConfig.FileName));
            IsSeg = File.Exists(Path.Combine(directory, MNNSegConfig.FileName));
            IsSpExPlus = File.Exists(Path.Combine(directory, MNNSpExPlusConfig.FileName));
            IsAvConvTasNet = File.Exists(Path.Combine(directory, MNNAvConvTasNetConfig.FileName));
            RequiresVisualFrames = IsAvMossFormer2Tse || RequiresExactVisualFrames;
            // Discovery is a snapshot; Refresh creates a new model. Loading verifies again.
            _hasAvMossFormer2Pipeline = task == MNNStudioTask.AudioProcessing && IsAvMossFormer2Tse && MNNAvMossFormer2TseModel.IsComplete(directory);
            try
            {
                if (File.Exists(Path.Combine(directory, MNNWhisperConfig.FileName)))
                    SupportsWhisperChinese = MNNWhisperModel.ForConfig(MNNWhisperConfig.Read(directory)).ChineseAvailable;
            }
            catch (Exception error)when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                SupportsWhisperChinese = false;
            }
        }

        private static MNNStudioTask AudioSubtaskForDirectory(string directory)
        {
            bool Has(params string[] names) => names.Any(name => File.Exists(Path.Combine(directory, name)));
            if (Has(MNNSileroVadConfig.FileName, MNNMarbleNetVadConfig.FileName, "mnn_fsmn_vad.json")) return MNNStudioTask.VoiceActivityDetection;
            if (Has(MNNSmartTurnConfig.FileName, MNNDualTurnEndpointingConfig.FileName, MNNTurnSenseConfig.FileName,
                MNNVoxTurnModel.ConfigFile, MNNNamoTurnDetectorConfig.FileName, MNNCueLightConfig.FileName, "mnn_livekit_turn_detector.json")) return MNNStudioTask.TurnDetection;
            if (Has(MNNFsEendConfig.FileName, MNNLsEendConfig.FileName, MNNSortformerConfig.FileName)) return MNNStudioTask.SpeakerDiarization;
            if (Has("mnn_frcrn_se.json", "mnn_deepfilternet3.json", MNNDccrnConfig.FileName, MNNDccrnetConfig.FileName,
                MNNDcunetConfig.FileName, MNNResembleEnhancerConfig.FileName, MNNResembleDenoiserConfig.FileName,
                MNNDtlnConfig.FileName, MNNGtcrnConfig.FileName, MNNFullSubNetConfig.FileName,
                MNNMossFormer2SeConfig.FileName, MNNMossFormerGanConfig.FileName, MNNDptnetConfig.FileName)) return MNNStudioTask.SpeechEnhancement;
            if (Has(MNNConvTasNetConfig.FileName, MNNDprnnConfig.FileName, MNNSepFormerConfig.FileName,
                MNNSepReformerConfig.FileName, MNNSudoRmRfConfig.FileName, MNNTigerConfig.FileName, MNNSpExPlusConfig.FileName,
                MNNAvConvTasNetConfig.FileName, MNNAvDprnnConfig.FileName, MNNAvMossFormer2TseConfig.FileName,
                MNNSegConfig.FileName, "mnn_mossformer2_ss.json")) return MNNStudioTask.SpeechSeparation;
            return MNNStudioTask.AudioProcessing;
        }

        internal static MNNStudioTask AudioSubtaskForIdentity(string identity, string[] tasks)
        {
            return AudioSubtaskForCategory(MNNModelRepository.Classify(identity, tasks: tasks));
        }

        internal static MNNStudioTask AudioSubtaskForCategory(string category, string identity = null, string[] tasks = null)
        {
            if (category == MNNModelCategory.AudioProcessing.ToString() && (identity != null || tasks != null))
                category = MNNModelRepository.Classify(identity, tasks: tasks);
            switch (category)
            {
                case "VoiceActivityDetection": return MNNStudioTask.VoiceActivityDetection;
                case "TurnDetection": return MNNStudioTask.TurnDetection;
                case "SpeechEnhancement": return MNNStudioTask.SpeechEnhancement;
                case "SpeechSeparation":
                case "TargetSpeakerExtraction": return MNNStudioTask.SpeechSeparation;
                case "SpeakerDiarization": return MNNStudioTask.SpeakerDiarization;
                default: return MNNStudioTask.AudioProcessing;
            }
        }
    }
}
