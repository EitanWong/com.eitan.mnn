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
        internal string OcrPageRoot, OcrRecognizerProfile = ".";
        internal MNNStudioModel(string name, string directory, MNNStudioTask task, MNNModelCapabilities capabilities, string category = null)
        {
            Name = name;
            Directory = directory;
            Task = task;
            AudioSubtask = task == MNNStudioTask.AudioProcessing ? AudioSubtaskForDirectory(directory) : task;
            Capabilities = capabilities;
            Category = category ?? MNNModelRepository.Classify(name);
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
                MNNMossFormer2SeConfig.FileName, MNNMossFormerGanConfig.FileName)) return MNNStudioTask.SpeechEnhancement;
            if (Has(MNNConvTasNetConfig.FileName, MNNDprnnConfig.FileName, MNNDptnetConfig.FileName, MNNSepFormerConfig.FileName,
                MNNSepReformerConfig.FileName, MNNSudoRmRfConfig.FileName, MNNTigerConfig.FileName, MNNSpExPlusConfig.FileName,
                MNNAvConvTasNetConfig.FileName, MNNAvDprnnConfig.FileName, MNNAvMossFormer2TseConfig.FileName,
                MNNSegConfig.FileName, "mnn_mossformer2_ss.json")) return MNNStudioTask.SpeechSeparation;
            return MNNStudioTask.AudioProcessing;
        }

        internal static MNNStudioTask AudioSubtaskForIdentity(string identity, string[] tasks)
        {
            string value = (identity ?? "") + " " + string.Join(" ", tasks ?? Array.Empty<string>());
            bool Has(params string[] terms) => terms.Any(term => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            if (Has("silero", "fsmn-vad", "fsmn_vad", "marblenet", "voice-activity-detection")) return MNNStudioTask.VoiceActivityDetection;
            if (Has("smart-turn", "smart_turn", "dualturn", "turnsense", "voxturn", "livekit-turn", "namo-turn", "cuelight")) return MNNStudioTask.TurnDetection;
            if (Has("fs-eend", "ls-eend", "sortformer", "speaker-diarization")) return MNNStudioTask.SpeakerDiarization;
            if (Has("frcrn", "deepfilternet", "dccrn", "dcunet", "denoiser", "enhancer", "gtcrn", "fullsubnet", "mossformer2-se", "mossformergan", "dtln", "speech-enhancement")) return MNNStudioTask.SpeechEnhancement;
            if (Has("convtasnet", "dprnn", "dptnet", "sepformer", "sepreformer", "sudormrf", "spex", "tiger", "seg-ygd", "mossformer2-ss", "mossformer2-tse", "speech-separation")) return MNNStudioTask.SpeechSeparation;
            return MNNStudioTask.AudioProcessing;
        }
    }
}
