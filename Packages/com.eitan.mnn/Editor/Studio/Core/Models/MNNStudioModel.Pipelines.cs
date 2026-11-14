using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioModel
    {
        private readonly Lazy<bool> _snapshotIsDualTurn;
        internal bool IsDualTurn => _snapshotIsDualTurn.Value;
        private readonly Lazy<bool> _snapshotIsTurnSense;
        internal bool IsTurnSense => _snapshotIsTurnSense.Value;
        private readonly Lazy<bool> _snapshotIsVoxTurn;
        internal bool IsVoxTurn => _snapshotIsVoxTurn.Value;
        private readonly Lazy<bool> _snapshotHasEchoCancellationPipeline;
        internal bool HasEchoCancellationPipeline => _snapshotHasEchoCancellationPipeline.Value;
        private readonly Lazy<bool> _snapshotIsKeywordSpotter;
        internal bool IsKeywordSpotter => _snapshotIsKeywordSpotter.Value;
        private readonly Lazy<bool> _snapshotHasKeywordSpottingPipeline;
        internal bool HasKeywordSpottingPipeline => _snapshotHasKeywordSpottingPipeline.Value;
        private readonly Lazy<bool> _snapshotHasAudioProcessingPipeline;
        internal bool HasAudioProcessingPipeline => _snapshotHasAudioProcessingPipeline.Value;
        private readonly Lazy<bool> _snapshotHasDocumentLayoutPipeline;
        internal bool HasDocumentLayoutPipeline => _snapshotHasDocumentLayoutPipeline.Value;
        private readonly Lazy<bool> _snapshotHasTextDecisionPipeline;
        internal bool HasTextDecisionPipeline => _snapshotHasTextDecisionPipeline.Value;
        private readonly Lazy<bool> _snapshotHasImageEmbeddingPipeline;
        internal bool HasImageEmbeddingPipeline => _snapshotHasImageEmbeddingPipeline.Value;
        private readonly Lazy<bool> _snapshotHasSpeakerRecognitionPipeline;
        internal bool HasSpeakerRecognitionPipeline => _snapshotHasSpeakerRecognitionPipeline.Value;
        private readonly Lazy<bool> _snapshotHasSpeechRecognitionPipeline;
        internal bool HasSpeechRecognitionPipeline => _snapshotHasSpeechRecognitionPipeline.Value;
        private readonly Lazy<bool> _snapshotIsYoloV8;
        internal bool IsYoloV8 => _snapshotIsYoloV8.Value;
        private readonly Lazy<bool> _snapshotIsImageTask;
        internal bool IsImageTask => _snapshotIsImageTask.Value;
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

        private readonly Lazy<bool> _snapshotIsPaddleText;
        internal bool IsPaddleText => _snapshotIsPaddleText.Value;
        private readonly Lazy<bool> _snapshotIsPaddlePage;
        internal bool IsPaddlePage => _snapshotIsPaddlePage.Value;
        private readonly Lazy<bool> _snapshotHasVisionPipeline;
        internal bool HasVisionPipeline => _snapshotHasVisionPipeline.Value;
        private bool CheckHasVisionPipeline()
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

        private readonly Lazy<bool> _snapshotHasGenerationPipeline;
        internal bool HasGenerationPipeline => _snapshotHasGenerationPipeline.Value;
        private bool CheckHasGenerationPipeline()
        {
            if (Task == MNNStudioTask.SpeechSynthesis)
            {
                string dependencies = null;
                if (IsPiper)
                {
                    try { dependencies = MNNStudioPiperSettings.ResolveDataDirectory(this); }
                    catch (Exception error) when (error is IOException || error is ArgumentException) { return false; }
                }
                return MNNSpeechSynthesizer.IsComplete(Directory, TtsProfile, dependencies);
            }

            if (Task != MNNStudioTask.ImageGeneration)
                return false;
            if (IsSana)
                return new[]{"connector", "projector", "transformer", "vae_encoder", "vae_decoder", "llm/llm"}.All(stage => File.Exists(Path.Combine(Directory, stage + ".mnn")) && File.Exists(Path.Combine(Directory, stage + ".mnn.weight"))) && new[]{"llm/llm_config.json", "llm/tokenizer.txt", "llm/meta_queries.mnn"}.All(file => File.Exists(Path.Combine(Directory, file)));
            string root = System.IO.Directory.Exists(Path.Combine(Directory, "general")) ? Path.Combine(Directory, "general") : Directory;
            return new[]{"text_encoder", "unet", "vae_decoder"}.All(stage => File.Exists(Path.Combine(root, stage + ".mnn")) && File.Exists(Path.Combine(root, stage + ".mnn.weight"))) && File.Exists(Path.Combine(root, "vocab.json")) && File.Exists(Path.Combine(root, "merges.txt"));
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
