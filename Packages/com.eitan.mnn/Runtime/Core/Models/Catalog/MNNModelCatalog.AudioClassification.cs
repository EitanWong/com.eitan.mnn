using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    public partial class MNNModelCatalog
    {
        private static MNNModelCatalogCategory? AudioTaskCategory(HashSet<string> tasks)
        {
            if (tasks.Overlaps(new[] { "acoustic-echo-cancellation", "echo-cancellation" })) return MNNModelCatalogCategory.EchoCancellation;
            if (tasks.Overlaps(new[] { "keyword-spotting", "keyword-detection", "wake-word-detection" })) return MNNModelCatalogCategory.KeywordSpotting;
            if (tasks.Overlaps(new[] { "turn-detection", "end-of-turn-detection", "endpoint-detection", "endpointing" })) return MNNModelCatalogCategory.TurnDetection;
            if (tasks.Overlaps(new[] { "voice-activity-detection", "speech-activity-detection", "vad" })) return MNNModelCatalogCategory.VoiceActivityDetection;
            if (tasks.Overlaps(new[] { "target-speaker-extraction", "target-speech-extraction", "audio-visual-speech-extraction" })) return MNNModelCatalogCategory.TargetSpeakerExtraction;
            if (tasks.Overlaps(new[] { "speech-separation", "audio-source-separation", "source-separation" })) return MNNModelCatalogCategory.SpeechSeparation;
            if (tasks.Overlaps(new[] { "speech-enhancement", "acoustic-noise-suppression", "noise-suppression", "speech-denoising", "speech-dereverberation" })) return MNNModelCatalogCategory.SpeechEnhancement;
            if (tasks.Contains("speaker-diarization")) return MNNModelCatalogCategory.SpeakerDiarization;
            if (tasks.Overlaps(new[] { "speaker-recognition", "speaker-identification", "speaker-verification", "speaker-embedding" })) return MNNModelCatalogCategory.SpeakerRecognition;
            if (tasks.Overlaps(new[] { "audio-classification", "audio-tagging", "sound-event-detection" })) return MNNModelCatalogCategory.AudioClassification;
            return null;
        }

        private static MNNModelCatalogCategory? AudioFamilyCategory(string text)
        {
            if (HasTerm(text, "dtln[._-]aec|acoustic-echo-cancellation")) return MNNModelCatalogCategory.EchoCancellation;
            if (HasTerm(text, "kws|keyword-spotting|keyword-detection|wake-word|wakeup")) return MNNModelCatalogCategory.KeywordSpotting;
            if (HasTerm(text, "smart[._-]?turn|livekit[._-]turn|namo[._-]turn|dualturn|turnsense|voxturn|cuelight|turn-detector|turn-detection")) return MNNModelCatalogCategory.TurnDetection;
            if (HasTerm(text, "silero[._-](?:vad|v[0-9]+(?:\\.[0-9]+)*)|marblenet|fsmn[._-]vad|voice-activity-detection|vad")) return MNNModelCatalogCategory.VoiceActivityDetection;
            if (HasTerm(text, "ls-eend|fs-eend|sortformer|speaker-diarization")) return MNNModelCatalogCategory.SpeakerDiarization;
            if (HasTerm(text, "spexplus|spex-plus|seg-ygd|av-convtasnet|av-dprnn|mossformer2-tse|target-speaker-extraction")) return MNNModelCatalogCategory.TargetSpeakerExtraction;
            if (HasTerm(text, "dtln|gtcrn|fullsubnet(?:plus)?|frcrn(?:-se)?|deepfilternet[0-9]*|dccrnet|dccrn(?:-mayavoz)?|dcunet|dptnet-libri1mix|mossformergan(?:-se)?|mossformer2-se|resemble-(?:denoiser|enhance)")) return MNNModelCatalogCategory.SpeechEnhancement;
            if (HasTerm(text, "convtasnet|dprnn|dptnet|sepformer|sepreformer|sudormrf|mossformer2-ss|tiger-speech|dave-tiger|speech-separation")) return MNNModelCatalogCategory.SpeechSeparation;
            if (HasTerm(text, "vbx-resnet101|eres2netv2?|ecapa(?:-tdnn)?|camplus|titanet|wespeaker|speaker-recognition|3dspeaker")) return MNNModelCatalogCategory.SpeakerRecognition;
            return HasTerm(text, "wavlm") ? MNNModelCatalogCategory.AudioProcessing : (MNNModelCatalogCategory?)null;
        }

        private static bool AudioFamilyOverridesTask(MNNModelCatalogCategory? family, MNNModelCatalogCategory? task)
        {
            if (!family.HasValue || !IsSpecificAudioCategory(family.Value.ToString())) return false;
            if (!task.HasValue || task == MNNModelCatalogCategory.AudioProcessing || task == MNNModelCatalogCategory.AudioClassification) return true;
            // Older hub metadata used these broader audio labels for the registered families.
            if (family == MNNModelCatalogCategory.TargetSpeakerExtraction && task == MNNModelCatalogCategory.SpeechSeparation) return true;
            if (family == MNNModelCatalogCategory.TurnDetection && task == MNNModelCatalogCategory.VoiceActivityDetection) return true;
            // These hub exports sometimes retain an ASR label although they do not transcribe.
            return task == MNNModelCatalogCategory.SpeechRecognition &&
                (family == MNNModelCatalogCategory.KeywordSpotting || family == MNNModelCatalogCategory.VoiceActivityDetection || family == MNNModelCatalogCategory.TurnDetection);
        }

        public static bool IsAudioProcessingCategory(string category) =>
            Enum.TryParse(category, out MNNModelCatalogCategory value) &&
            (value == MNNModelCatalogCategory.AudioProcessing ||
                value >= MNNModelCatalogCategory.VoiceActivityDetection && value <= MNNModelCatalogCategory.AudioClassification);

        public static bool IsSpecificAudioCategory(string category) =>
            category != MNNModelCatalogCategory.AudioProcessing.ToString() &&
            (IsAudioProcessingCategory(category) || category == MNNModelCatalogCategory.KeywordSpotting.ToString() ||
                category == MNNModelCatalogCategory.EchoCancellation.ToString() || category == MNNModelCatalogCategory.SpeakerRecognition.ToString());

        private static readonly Dictionary<string, string> AudioSearchAliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "VoiceActivityDetection", "vad voice activity detection speech activity detection 语音活动检测 人声检测" },
            { "TurnDetection", "turn detection turn detector end of turn endpoint detection endpointing 轮次检测 轮次结束检测 语音端点检测" },
            { "SpeechEnhancement", "speech enhancement noise suppression denoise denoising dereverberation 语音增强 降噪 去混响" },
            { "SpeechSeparation", "speech separation source separation 语音分离 声源分离" },
            { "TargetSpeakerExtraction", "target speaker extraction target speech extraction audio visual extraction tse 目标说话人提取 目标语音提取 视听提取" },
            { "SpeakerDiarization", "speaker diarization who spoke when 说话人分离 说话人分段 说话人日志" },
            { "AudioClassification", "audio classification audio tagging sound event detection 声音分类 音频分类 声音事件检测" },
            { "SpeakerRecognition", "speaker recognition speaker verification speaker embedding 声纹识别 说话人识别 声纹提取" },
            { "KeywordSpotting", "keyword spotting wake word kws 唤醒词 关键词检测" },
            { "EchoCancellation", "acoustic echo cancellation aec 回声消除" }
        };

        internal static bool MatchesAudioCategorySearch(string category, string query) => category != null &&
            AudioSearchAliases.TryGetValue(category, out var aliases) &&
            (category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || aliases.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
