using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Unity
{
    public sealed partial class MNNSpeechSynthesizer
    {
        /// <summary>Detect a known export protocol from local metadata. No weights are loaded or files created.
        /// Detection alone does not prove that all required assets are installed.</summary>
        public static MNNSpeechModelKind DetectModelKind(string directory, string name = null) => MNNTtsModelProfiles.Detect(directory, name);

        /// <summary>Read installed voice identifiers without loading a model. The returned array belongs to the caller.</summary>
        public static string[] InstalledVoices(string directory)
        {
            switch (DetectModelKind(directory))
            {
                case MNNSpeechModelKind.Unknown: return Array.Empty<string>();
                case MNNSpeechModelKind.Qwen3Explicit: return MNNQwen3ExplicitTts.InstalledVoices(directory);
                case MNNSpeechModelKind.MossNano: return MNNMossNano.InstalledVoices(directory);
                case MNNSpeechModelKind.SherpaVitsChinese: return MNNSherpaVitsChinese.InstalledVoices(directory);
                case MNNSpeechModelKind.MmsTamil: return new[] { "Tamil" };
                case MNNSpeechModelKind.IndicVits: return (string[])MNNIndicVits.SpeakerNames.Clone();
                case MNNSpeechModelKind.Kokoro: return MNNKokoro.InstalledVoices(directory);
                case MNNSpeechModelKind.CosyVoice:
                case MNNSpeechModelKind.CosyVoice300M: return new[] { "Default" };
                case MNNSpeechModelKind.Piper: return MNNPiper.InstalledVoices(directory);
                case MNNSpeechModelKind.BertVits2: return new[] { "Chenxi" };
                case MNNSpeechModelKind.Supertonic: return MNNSupertonic.InstalledVoices(directory);
                default: return new[] { "Reference audio" };
            }
        }

        /// <summary>Read the languages declared by the installed Supertonic version without loading weights.</summary>
        public static string[] InstalledSupertonicLanguages(string directory)
        {
            string version = (string)MNNModelData.Get(MNNModelData.Read(Path.Combine(directory, "mnn_models", "tts.json")), "tts_version");
            return MNNSupertonic.LanguagesForVersion(version);
        }

        private static readonly IReadOnlyDictionary<MNNSpeechModelKind, Func<string, string, bool>> AssetChecks =
            new Dictionary<MNNSpeechModelKind, Func<string, string, bool>>
        {
            { MNNSpeechModelKind.Unknown, (directory, data) => false },
            { MNNSpeechModelKind.IndicF5, (directory, data) => MNNIndicF5Tts.IsComplete(directory) },
            { MNNSpeechModelKind.Pocket, (directory, data) => MNNPocketTts.IsComplete(directory) },
            { MNNSpeechModelKind.Qwen3Explicit, (directory, data) => MNNQwen3ExplicitTts.IsComplete(directory) },
            { MNNSpeechModelKind.DhVaani, (directory, data) => MNNDhVaani.IsComplete(directory) },
            { MNNSpeechModelKind.MossNano, (directory, data) => MNNMossNano.IsComplete(directory) },
            { MNNSpeechModelKind.Kokoro, (directory, data) => MNNKokoroModel.IsComplete(directory) },
            { MNNSpeechModelKind.SherpaVitsChinese, (directory, data) => MNNSherpaVitsChinese.IsComplete(directory) },
            { MNNSpeechModelKind.MmsTamil, (directory, data) => MNNMmsTamil.IsComplete(directory) },
            { MNNSpeechModelKind.IndicVits, (directory, data) => MNNIndicVits.IsComplete(directory) },
            { MNNSpeechModelKind.IndexTts25, (directory, data) => MNNReferenceSpeechModel.IsComplete(directory, true) },
            { MNNSpeechModelKind.FireRedTts3, (directory, data) => MNNReferenceSpeechModel.IsComplete(directory, false) },
            { MNNSpeechModelKind.CosyVoice300M, (directory, data) => MNNCosyVoice300MModel.IsComplete(directory) },
            { MNNSpeechModelKind.CosyVoice, (directory, data) => MNNCosyVoiceModel.IsComplete(directory) },
            { MNNSpeechModelKind.Supertonic, (directory, data) => MNNSupertonic.IsComplete(directory) },
            { MNNSpeechModelKind.Piper, (directory, data) => ClassicAssetsComplete(directory, false, data) },
            { MNNSpeechModelKind.BertVits2, (directory, data) => ClassicAssetsComplete(directory, true, data) }
        };

        /// <summary>Check required local assets without loading weights or verifying platform support.
        /// speechDataDirectory optionally selects Piper's external eSpeak data directory.</summary>
        public static bool IsComplete(string directory, MNNSpeechModelKind kind, string speechDataDirectory = null)
        {
            if (!AssetChecks.TryGetValue(kind, out var check)) throw new ArgumentOutOfRangeException(nameof(kind));
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) && check(directory, speechDataDirectory);
        }

        private static bool ClassicAssetsComplete(string directory, bool bertVits, string data)
        {
            try
            {
                var metadata = MNNModelData.Object(MNNModelData.Read(Path.Combine(directory, "config.json")));
                string graph = MNNModelData.Get(metadata, "model_path") as string;
                int rate = MNNModelData.Integer(MNNModelData.Get(metadata, "sample_rate"));
                if (string.IsNullOrWhiteSpace(graph) || !File.Exists(Path.Combine(directory, graph))) return false;
                metadata.TryGetValue("asset_folder", out var assets);
                if (bertVits)
                    return rate == 44100 && BertAssets.All(file => File.Exists(Path.Combine(directory, assets as string ?? ".", file)));
                if (rate != 16000 && rate != 22050) return false;
                MNNPiper.ResolveDataDirectory(directory, assets as string, data);
                return true;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static readonly string[] BertAssets = { "common/mnn_models/chinese_bert.mnn", "common/mnn_models/chinese_bert.mnn.weight",
            "common/text_processing_jsons/cn_bert_token.bin", "common/text_processing_jsons/pinyin_dict.bin",
            "common/text_processing_jsons/phrases_dict.bin", "common/text_processing_jsons/pinyin_to_symbol_map.bin",
            "common/text_processing_jsons/hotwords_cn.json", "common/text_processing_jsons/default_tone_words.json" };
    }
}
