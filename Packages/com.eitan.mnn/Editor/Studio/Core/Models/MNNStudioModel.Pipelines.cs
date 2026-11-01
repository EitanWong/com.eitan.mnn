using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioModel
    {
        internal bool IsDualTurn => File.Exists(Path.Combine(Directory, MNNDualTurnEndpointingConfig.FileName));
        internal bool IsTurnSense => File.Exists(Path.Combine(Directory, MNNTurnSenseConfig.FileName));
        internal bool IsVoxTurn => File.Exists(Path.Combine(Directory, MNNVoxTurnModel.ConfigFile));
        internal bool HasEchoCancellationPipeline => Task == MNNStudioTask.EchoCancellation && MNNDtlnAecModel.IsComplete(Directory);
        internal bool IsKeywordSpotter => File.Exists(Path.Combine(Directory, MNNKeywordSpotterConfig.FileName));
        internal bool HasKeywordSpottingPipeline => Task == MNNStudioTask.KeywordSpotting && MNNKwsModel.IsComplete(Directory);
        internal bool HasAudioProcessingPipeline => Task == MNNStudioTask.AudioProcessing && (MNNDualTurnEndpointingModel.IsComplete(Directory) || MNNTurnSenseModel.IsComplete(Directory) || MNNFsEendModel.IsComplete(Directory) || MNNCueLightModel.IsComplete(Directory) || MNNLsEendModel.IsComplete(Directory) || MNNVoxTurnModel.IsComplete(Directory) || MNNDccrnModel.IsComplete(Directory) || MNNTigerModel.IsComplete(Directory) || MNNResembleEnhancerModel.IsComplete(Directory) || MNNResembleDenoiserModel.IsComplete(Directory) || MNNSegModel.IsComplete(Directory) || MNNSpExPlusModel.IsComplete(Directory) || MNNAvConvTasNetModel.IsComplete(Directory) || MNNAvDprnnModel.IsComplete(Directory) || MNNSepReformerModel.IsComplete(Directory) || MNNDcunetModel.IsComplete(Directory) || MNNDccrnetModel.IsComplete(Directory) || MNNSudoRmRfModel.IsComplete(Directory) || MNNNamoTurnDetectorModel.IsComplete(Directory) || MNNSileroModel.IsComplete(Directory) || MNNSmartTurnModel.IsComplete(Directory) || MNNFsmnVadModel.IsComplete(Directory) || MNNFrcrnSeModel.IsComplete(Directory) || MNNMarbleNetVadModel.IsComplete(Directory) || MNNMossFormer2Model.IsComplete(Directory) || MNNLiveKitTurnDetectorModel.IsComplete(Directory) || MNNDeepFilterNet3Model.IsComplete(Directory) || MNNDtlnModel.IsComplete(Directory) || MNNGtcrnModel.IsComplete(Directory) || MNNFullSubNetModel.IsComplete(Directory) || MNNDptnetModel.IsComplete(Directory) || MNNSortformerModel.IsComplete(Directory) || MNNDprnnModel.IsComplete(Directory) || MNNConvTasNetModel.IsComplete(Directory) || MNNSepFormerModel.IsComplete(Directory) || MNNMossFormer2SeModel.IsComplete(Directory) || MNNMossFormerGanModel.IsComplete(Directory) || _hasAvMossFormer2Pipeline);
        internal bool HasDocumentLayoutPipeline => Task == MNNStudioTask.DocumentLayout && MNNDocumentLayoutModel.IsComplete(Directory);
        internal bool HasTextDecisionPipeline => Task == MNNStudioTask.NLP && MNNTextDecisionModel.IsComplete(Directory);
        internal bool HasImageEmbeddingPipeline => Task == MNNStudioTask.ImageEmbedding && MNNImageEmbeddingModel.IsComplete(Directory);
        internal bool HasSpeakerRecognitionPipeline => Task == MNNStudioTask.SpeakerRecognition && (MNNVbxModel.IsComplete(Directory) || MNNSpeakerModel.IsComplete(Directory) || MNNWeSpeakerModel.IsComplete(Directory));
        internal bool HasSpeechRecognitionPipeline => Task == MNNStudioTask.SpeechRecognition && (MNNZipformerModel.IsComplete(Directory) || MNNSenseVoiceModel.IsComplete(Directory) || MNNWhisperModel.IsComplete(Directory) || MNNQwen3AsrModel.IsComplete(Directory) || MNNParaformerModel.IsComplete(Directory));
        internal bool IsYoloV8 => File.Exists(Path.Combine(Directory, MNNYoloV8Config.FileName));
        internal bool IsImageTask => File.Exists(Path.Combine(Directory, MNNImageTaskConfig.FileName));
        internal static MNNStudioTask ImageTaskFor(MNNImageTaskProfile profile)
        {
            switch (profile)
            {
                case MNNImageTaskProfile.ModNet:
                    return MNNStudioTask.ImageMatting;
                case MNNImageTaskProfile.RealEsrganX4:
                    return MNNStudioTask.SuperResolution;
                case MNNImageTaskProfile.UVDoc:
                    return MNNStudioTask.DocumentRectification;
                case MNNImageTaskProfile.Scrfd500M:
                    return MNNStudioTask.FaceDetection;
                case MNNImageTaskProfile.MobileFaceNet:
                    return MNNStudioTask.FaceRecognition;
                case MNNImageTaskProfile.Landmark106:
                    return MNNStudioTask.FaceLandmarks;
                case MNNImageTaskProfile.MobileNetV2:
                    return MNNStudioTask.ImageClassification;
                case MNNImageTaskProfile.PglNet:
                    return MNNStudioTask.ImageRestoration;
                case MNNImageTaskProfile.CandyStyle:
                    return MNNStudioTask.StyleTransfer;
                case MNNImageTaskProfile.DeepLabV3:
                    return MNNStudioTask.Segmentation;
                case MNNImageTaskProfile.MoveNetLightning:
                    return MNNStudioTask.Pose;
                case MNNImageTaskProfile.DepthAnythingV2Small:
                    return MNNStudioTask.DepthEstimation;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        internal bool IsPaddleText => File.Exists(Path.Combine(Directory, MNNPaddleTextConfig.FileName));
        internal bool IsPaddlePage => File.Exists(Path.Combine(OcrPageRoot ?? Directory, MNNPaddleOcrConfig.FileName));
        internal bool HasVisionPipeline
        {
            get
            {
                if (!(IsImageTask || Task == MNNStudioTask.ObjectDetection && IsYoloV8 || Task == MNNStudioTask.OCR && IsPaddleText))
                    return false;
                try
                {
                    if (IsImageTask)
                    {
                        var profile = MNNImageTaskConfig.Read(Directory);
                        profile.Resolve(Directory, false);
                        return Task == ImageTaskFor(profile.profile);
                    }

                    if (IsPaddlePage)
                    {
                        MNNPaddleOcrConfig.ReadForProfile(OcrPageRoot ?? Directory, OcrRecognizerProfile).Validate(OcrPageRoot ?? Directory);
                        MNNPaddleOcrConfig.RecognizerDirectory(OcrPageRoot ?? Directory, OcrRecognizerProfile);
                    }
                    else if (IsPaddleText)
                        MNNPaddleTextConfig.Read(Directory).Validate(Directory);
                    else
                        MNNYoloV8Config.Read(Directory).ResolveGraph(Directory);
                    return true;
                }
                catch (Exception error)when (error is IOException || error is InvalidDataException || error is ArgumentException || error is UnauthorizedAccessException)
                {
                    return false;
                }
            }
        }

        internal bool HasGenerationPipeline
        {
            get
            {
                if (Task == MNNStudioTask.SpeechSynthesis)
                {
                    // Classification is intentionally broader than runtime support.
                    // Keep unadapted TTS families visible in Model Manager, but do
                    // not allow Studio to route them through Supertonic by default.
                    if (!MNNTtsModelProfiles.IsImplemented(TtsProfile))
                        return false;
                    if (IsIndicF5)
                        return MNNIndicF5Tts.IsComplete(Directory);
                    if (IsPocket)
                        return MNNPocketTts.IsComplete(Directory);
                    if (IsQwen3Explicit)
                        return MNNQwen3ExplicitTts.IsComplete(Directory);
                    if (TtsProfile == MNNTtsModelProfile.DhVaani)
                        return MNNDhVaani.IsComplete(Directory);
                    if (TtsProfile == MNNTtsModelProfile.MossNano)
                        return MNNMossNano.IsComplete(Directory);
                    if (TtsProfile == MNNTtsModelProfile.Kokoro)
                        return MNNKokoroModel.IsComplete(Directory);
                    if (TtsProfile == MNNTtsModelProfile.SherpaVitsChinese)
                        return MNNSherpaVitsChinese.IsComplete(Directory);
                    if (TtsProfile == MNNTtsModelProfile.MmsTamil)
                        return MNNMmsTamil.IsComplete(Directory);
                    if (TtsProfile == MNNTtsModelProfile.IndicVits)
                        return MNNIndicVits.IsComplete(Directory);
                    if (RequiresReferenceAudio)
                        return MNNReferenceSpeechModel.IsComplete(Directory, IsIndexTts);
                    if (IsCosyVoice300M)
                        return MNNCosyVoice300MModel.IsComplete(Directory);
                    if (IsCosyVoice)
                        return MNNCosyVoiceModel.IsComplete(Directory);
                    var metadata = ReadTaskConfig();
                    if (IsPiper)
                    {
                        if (metadata == null || metadata.sample_rate != 16000 && metadata.sample_rate != 22050 || string.IsNullOrWhiteSpace(metadata.model_path) || !File.Exists(Path.Combine(Directory, metadata.model_path)))
                            return false;
                        try
                        {
                            MNNStudioPiperSettings.ResolveDataDirectory(this);
                            return true;
                        }
                        catch (Exception error)when (error is IOException || error is ArgumentException)
                        {
                            return false;
                        }
                    }

                    if (IsBertVits2)
                        return metadata != null && metadata.sample_rate == 44100 && !string.IsNullOrWhiteSpace(metadata.model_path) && File.Exists(Path.Combine(Directory, metadata.model_path)) && new[]{"common/mnn_models/chinese_bert.mnn", "common/mnn_models/chinese_bert.mnn.weight", "common/text_processing_jsons/cn_bert_token.bin", "common/text_processing_jsons/pinyin_dict.bin", "common/text_processing_jsons/phrases_dict.bin", "common/text_processing_jsons/pinyin_to_symbol_map.bin", "common/text_processing_jsons/hotwords_cn.json", "common/text_processing_jsons/default_tone_words.json"}.All(file => File.Exists(Path.Combine(Directory, metadata.asset_folder ?? ".", file)));
                    return TtsProfile == MNNTtsModelProfile.Supertonic && MNNSupertonic.IsComplete(Directory);
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
            public string model_type = null, modelType = null, talker_type = null, model_name = null, model_path = null, asset_folder = null, tokenizer_file = null;
            public int hidden_size = 0, image_pad = 0, sample_rate = 0;
        }
    }
}
