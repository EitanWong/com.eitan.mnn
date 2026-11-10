using System;
using System.Collections.Generic;
using System.IO;

namespace MNN.Unity
{
    /// <summary>Inspects installed task assets without loading weights, calling native APIs or downloading files.</summary>
    public static class MNNModelSupport
    {
        private static readonly IReadOnlyDictionary<MNNModelKind, Func<string, bool>> Checks =
            new Dictionary<MNNModelKind, Func<string, bool>>
        {
            { MNNModelKind.AvConvTasNet, MNNAvConvTasNetModel.IsComplete },
            { MNNModelKind.AvDprnn, MNNAvDprnnModel.IsComplete },
            { MNNModelKind.AvMossFormer2Tse, MNNAvMossFormer2TseModel.IsComplete },
            { MNNModelKind.ConvTasNet, MNNConvTasNetModel.IsComplete },
            { MNNModelKind.CosyVoice300M, MNNCosyVoice300MModel.IsComplete },
            { MNNModelKind.CosyVoice, MNNCosyVoiceModel.IsComplete },
            { MNNModelKind.CueLight, MNNCueLightModel.IsComplete },
            { MNNModelKind.Dccrn, MNNDccrnModel.IsComplete },
            { MNNModelKind.Dccrnet, MNNDccrnetModel.IsComplete },
            { MNNModelKind.Dcunet, MNNDcunetModel.IsComplete },
            { MNNModelKind.DeepFilterNet3, MNNDeepFilterNet3Model.IsComplete },
            { MNNModelKind.DocumentLayout, MNNDocumentLayoutModel.IsComplete },
            { MNNModelKind.Dprnn, MNNDprnnModel.IsComplete },
            { MNNModelKind.Dptnet, MNNDptnetModel.IsComplete },
            { MNNModelKind.DtlnAec, MNNDtlnAecModel.IsComplete },
            { MNNModelKind.Dtln, MNNDtlnModel.IsComplete },
            { MNNModelKind.DualTurnEndpointing, MNNDualTurnEndpointingModel.IsComplete },
            { MNNModelKind.FrcrnSe, MNNFrcrnSeModel.IsComplete },
            { MNNModelKind.FsEend, MNNFsEendModel.IsComplete },
            { MNNModelKind.FsmnVad, MNNFsmnVadModel.IsComplete },
            { MNNModelKind.FullSubNet, MNNFullSubNetModel.IsComplete },
            { MNNModelKind.Gtcrn, MNNGtcrnModel.IsComplete },
            { MNNModelKind.ImageEmbedding, MNNImageEmbeddingModel.IsComplete },
            { MNNModelKind.Kokoro, MNNKokoroModel.IsComplete },
            { MNNModelKind.Kws, MNNKwsModel.IsComplete },
            { MNNModelKind.LiveKitTurnDetector, MNNLiveKitTurnDetectorModel.IsComplete },
            { MNNModelKind.LsEend, MNNLsEendModel.IsComplete },
            { MNNModelKind.MarbleNetVad, MNNMarbleNetVadModel.IsComplete },
            { MNNModelKind.MossFormer2, MNNMossFormer2Model.IsComplete },
            { MNNModelKind.MossFormer2Se, MNNMossFormer2SeModel.IsComplete },
            { MNNModelKind.MossFormerGan, MNNMossFormerGanModel.IsComplete },
            { MNNModelKind.NamoTurnDetector, MNNNamoTurnDetectorModel.IsComplete },
            { MNNModelKind.Paraformer, MNNParaformerModel.IsComplete },
            { MNNModelKind.Qwen3Asr, MNNQwen3AsrModel.IsComplete },
            { MNNModelKind.ResembleDenoiser, MNNResembleDenoiserModel.IsComplete },
            { MNNModelKind.ResembleEnhancer, MNNResembleEnhancerModel.IsComplete },
            { MNNModelKind.Seg, MNNSegModel.IsComplete },
            { MNNModelKind.SenseVoice, MNNSenseVoiceModel.IsComplete },
            { MNNModelKind.SepFormer, MNNSepFormerModel.IsComplete },
            { MNNModelKind.SepReformer, MNNSepReformerModel.IsComplete },
            { MNNModelKind.Silero, MNNSileroModel.IsComplete },
            { MNNModelKind.SmartTurn, MNNSmartTurnModel.IsComplete },
            { MNNModelKind.Sortformer, MNNSortformerModel.IsComplete },
            { MNNModelKind.SpExPlus, MNNSpExPlusModel.IsComplete },
            { MNNModelKind.Speaker, MNNSpeakerModel.IsComplete },
            { MNNModelKind.SudoRmRf, MNNSudoRmRfModel.IsComplete },
            { MNNModelKind.TextDecision, MNNTextDecisionModel.IsComplete },
            { MNNModelKind.Tiger, MNNTigerModel.IsComplete },
            { MNNModelKind.TurnSense, MNNTurnSenseModel.IsComplete },
            { MNNModelKind.Vbx, MNNVbxModel.IsComplete },
            { MNNModelKind.VoxTurn, MNNVoxTurnModel.IsComplete },
            { MNNModelKind.WeSpeaker, MNNWeSpeakerModel.IsComplete },
            { MNNModelKind.Whisper, MNNWhisperModel.IsComplete },
            { MNNModelKind.Zipformer, MNNZipformerModel.IsComplete },
        };

        /// <summary>Checks the export's local layout and recorded sizes. This does not establish SHA integrity,
        /// platform compatibility, inference accuracy or device availability. It creates no files.</summary>
        public static bool IsComplete(MNNModelKind kind, string directory)
        {
            if (!Checks.TryGetValue(kind, out var check)) throw new ArgumentOutOfRangeException(nameof(kind));
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) && check(directory);
        }
    }
}
