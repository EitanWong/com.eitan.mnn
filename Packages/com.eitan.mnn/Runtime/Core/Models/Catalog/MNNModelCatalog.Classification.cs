using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    public partial class MNNModelCatalog
    {
        // The rule set exceeds Regex's small process-wide cache. Retain these fixed
        // patterns so parallel manifest classification does not repeatedly parse them.
        private static readonly ConcurrentDictionary<string, Regex> TermPatterns =
            new ConcurrentDictionary<string, Regex>(StringComparer.Ordinal);
        private static readonly Func<string, Regex> CreateTermPattern = alternatives =>
            new Regex(@"(?<![a-z0-9])(?:" + alternatives + @")(?=$|[^a-z0-9])", RegexOptions.CultureInvariant);

        // Classify tasks, not descriptions, owners or internal pipeline stage names.
        // Metadata describes a candidate's purpose; it never establishes runtime support.
        internal static string Classify(string id, string name = null, string description = null,
            string[] tasks = null, string[] tags = null, string[] paths = null)
        {
            string repositoryName = (id ?? string.Empty).Split('/').Last();
            string identity = (repositoryName + " " + (name ?? string.Empty)).ToLowerInvariant();
            var taskSet = new HashSet<string>((tasks ?? Array.Empty<string>())
                .Concat((tags ?? Array.Empty<string>()).Select(tag =>
                    tag != null && tag.StartsWith("task:", StringComparison.OrdinalIgnoreCase) ? tag.Substring(5) : tag))
                .Where(task => !string.IsNullOrWhiteSpace(task)).Select(task => task.ToLowerInvariant()), StringComparer.Ordinal);
            string[] files = (paths ?? Array.Empty<string>()).Where(path => path != null)
                .Select(path => path.Replace('\\', '/').ToLowerInvariant()).ToArray();

            if (HasTerm(identity, "oob|external-oob|initnet-index") || files.Any(file => HasTerm(Path.GetFileName(file), "malicious|oob")))
                return MNNModelCatalogCategory.ModelDiagnostics.ToString();
            if (HasTerm(identity, "mnn_android_so|android-so|runtime-libraries") ||
                files.Length > 0 && !files.Any(file => file.EndsWith(".mnn", StringComparison.Ordinal)) && files.Any(file => file.EndsWith(".so", StringComparison.Ordinal)))
                return MNNModelCatalogCategory.RuntimeResources.ToString();
            if (HasTerm(identity, "bench-models|model-collection"))
                return MNNModelCatalogCategory.ModelCollection.ToString();
            // An archive collection can contain different tasks, rather than one model with many stages.
            var archiveTasks = files.Where(IsArchive).Select(file => FamilyCategory(Path.GetFileName(file)))
                .Where(category => category.HasValue).Select(category => category.Value).Distinct().ToArray();
            if (archiveTasks.Length > 1)
                return MNNModelCatalogCategory.ModelCollection.ToString();

            // This repository's author documents a latent diffusion style-transfer pipeline.
            if (string.Equals(id, "ArcherFD/SAFlow", StringComparison.OrdinalIgnoreCase))
                return MNNModelCatalogCategory.StyleTransfer.ToString();
            // Some MuseTalk exports retain the hub's default object-detection label.
            // The named UNet/VAE pair establishes the avatar pipeline's purpose.
            if (HasTerm(identity, "musetalk") &&
                files.Any(file => Path.GetFileName(file) == "musetalk_unet.mnn") &&
                files.Any(file => Path.GetFileName(file) == "musetalk_vae_decode.mnn"))
                return MNNModelCatalogCategory.AvatarAnimation.ToString();
            MNNModelCatalogCategory? explicitTask = TaskCategory(taskSet);
            MNNModelCatalogCategory? family = FamilyCategory(identity);
            if (AudioFamilyOverridesTask(family, explicitTask))
                return family.Value.ToString();
            if (family == MNNModelCatalogCategory.SpeechSynthesis && explicitTask == MNNModelCatalogCategory.AudioGeneration)
                return MNNModelCatalogCategory.SpeechSynthesis.ToString();
            if (family == MNNModelCatalogCategory.Reranker && explicitTask == MNNModelCatalogCategory.EmbeddingModel)
                return MNNModelCatalogCategory.Reranker.ToString();
            if (family == MNNModelCatalogCategory.EmbeddingModel && HasTerm(identity, "vl|vlm") &&
                (!explicitTask.HasValue || explicitTask == MNNModelCatalogCategory.VisionLanguageModel || explicitTask == MNNModelCatalogCategory.EmbeddingModel))
                return MNNModelCatalogCategory.ImageEmbedding.ToString();
            if (explicitTask.HasValue && !(explicitTask.Value == MNNModelCatalogCategory.VisionLanguageModel && HasTerm(identity, "omni|all-modal")))
                return explicitTask.Value.ToString();
            // Multimodal family names remain more specific than an image-text-to-text hub label.
            if (HasTerm(identity, "omni|all-modal"))
                return MNNModelCatalogCategory.OmniModel.ToString();
            if (family.HasValue)
                return family.Value.ToString();
            if (files.Any(file => HasTerm(Path.GetFileName(file), "clip_v[0-9]+")))
                return MNNModelCatalogCategory.ImageEmbedding.ToString();
            var graphTasks = files.Where(file => file.EndsWith(".mnn", StringComparison.Ordinal))
                .Select(file => FamilyCategory(Path.GetFileNameWithoutExtension(file))).Where(category => category.HasValue)
                .Select(category => category.Value).Distinct().ToArray();
            if (graphTasks.Length > 1)
                return MNNModelCatalogCategory.ModelCollection.ToString();
            if (graphTasks.Length == 1)
                return graphTasks[0].ToString();
            if (taskSet.Contains("any-to-any"))
                return MNNModelCatalogCategory.OmniModel.ToString();
            if (taskSet.Contains("text-generation") || taskSet.Contains("chatbot"))
                return MNNModelCatalogCategory.LargeLanguageModel.ToString();
            if (taskSet.Contains("text-classification") || taskSet.Contains("token-classification") || taskSet.Contains("question-answering"))
                return MNNModelCatalogCategory.NLP.ToString();
            if (taskSet.Contains("feature-extraction"))
                return MNNModelCatalogCategory.EmbeddingModel.ToString();

            if (files.Any(file => Path.GetFileName(file) == "llm_config.json" || Path.GetFileName(file) == "llm.mnn"))
                return MNNModelCatalogCategory.LargeLanguageModel.ToString();
            if (new[] { "text_encoder.mnn", "unet.mnn", "vae_decoder.mnn" }.All(required => files.Any(file => Path.GetFileName(file) == required)))
                return MNNModelCatalogCategory.ImageGenerationModel.ToString();
            if (archiveTasks.Length == 1)
                return archiveTasks[0].ToString();
            return MNNModelCatalogCategory.Other.ToString();
        }

        private static MNNModelCatalogCategory? TaskCategory(HashSet<string> tasks)
        {
            var audio = AudioTaskCategory(tasks);
            if (audio.HasValue) return audio;
            if (tasks.Overlaps(new[] { "face-2d-keypoints", "face-keypoints", "face-landmark", "face-landmarks" })) return MNNModelCatalogCategory.FaceLandmarks;
            if (tasks.Contains("face-reconstruction") || tasks.Contains("image-to-3d")) return MNNModelCatalogCategory.Reconstruction3D;
            if (tasks.Contains("avatar-animation")) return MNNModelCatalogCategory.AvatarAnimation;
            if (tasks.Contains("universal-matting") || tasks.Contains("image-matting")) return MNNModelCatalogCategory.ImageMatting;
            if (tasks.Overlaps(new[] { "image-dehazing", "image-denoising", "image-restoration", "image-deblurring" })) return MNNModelCatalogCategory.ImageRestoration;
            if (tasks.Contains("depth-estimation")) return MNNModelCatalogCategory.DepthEstimation;
            if (tasks.Overlaps(new[] { "audio-generation", "text-to-audio", "music-generation", "text-to-music" })) return MNNModelCatalogCategory.AudioGeneration;
            if (tasks.Contains("document-rectification")) return MNNModelCatalogCategory.DocumentRectification;
            if (tasks.Overlaps(new[] { "table-recognition", "document-layout", "document-layout-analysis" })) return MNNModelCatalogCategory.DocumentLayout;
            if (tasks.Contains("face-detection")) return MNNModelCatalogCategory.FaceDetection;
            if (tasks.Contains("face-recognition")) return MNNModelCatalogCategory.FaceRecognition;
            if (tasks.Overlaps(new[] { "text-to-video", "text-to-video-synthesis", "image-to-video" })) return MNNModelCatalogCategory.VideoGeneration;
            if (tasks.Overlaps(new[] { "text-to-image", "text-to-image-synthesis" })) return MNNModelCatalogCategory.ImageGenerationModel;
            if (tasks.Overlaps(new[] { "text-to-speech", "speech-synthesis" })) return MNNModelCatalogCategory.SpeechSynthesis;
            if (tasks.Overlaps(new[] { "speech-recognition", "auto-speech-recognition", "automatic-speech-recognition" })) return MNNModelCatalogCategory.SpeechRecognition;
            if (tasks.Contains("audio-to-audio")) return MNNModelCatalogCategory.AudioProcessing;
            if (tasks.Overlaps(new[] { "translation", "machine-translation" })) return MNNModelCatalogCategory.Translation;
            if (tasks.Contains("sentence-similarity") || tasks.Contains("sentence-embedding") || tasks.Contains("text-embedding")) return MNNModelCatalogCategory.EmbeddingModel;
            if (tasks.Contains("image-feature-extraction") || tasks.Contains("zero-shot-image-classification")) return MNNModelCatalogCategory.ImageEmbedding;
            if (tasks.Overlaps(new[] { "ocr", "ocr-recognition", "optical-character-recognition" })) return MNNModelCatalogCategory.OCRModel;
            if (tasks.Overlaps(new[] { "image-text-to-text", "image-to-text", "image-captioning", "visual-question-answering", "visual-grounding" })) return MNNModelCatalogCategory.VisionLanguageModel;
            if (tasks.Contains("object-detection") || tasks.Contains("image-object-detection")) return MNNModelCatalogCategory.ObjectDetection;
            if (tasks.Overlaps(new[] { "image-segmentation", "semantic-segmentation", "instance-segmentation" })) return MNNModelCatalogCategory.SemanticSegmentation;
            if (tasks.Contains("pose-estimation") || tasks.Contains("keypoint-detection")) return MNNModelCatalogCategory.PoseEstimation;
            if (tasks.Contains("image-classification")) return MNNModelCatalogCategory.ImageClassification;
            if (tasks.Contains("image-super-resolution") || tasks.Contains("super-resolution")) return MNNModelCatalogCategory.SuperResolution;
            if (tasks.Contains("style-transfer")) return MNNModelCatalogCategory.StyleTransfer;
            if (tasks.Contains("robotics")) return MNNModelCatalogCategory.Robotics;
            return null;
        }

        private static MNNModelCatalogCategory? FamilyCategory(string text)
        {
            var audio = AudioFamilyCategory(text);
            if (audio.HasValue) return audio;
            // Word boundaries avoid matching owners (handled separately) and unrelated substrings.
            if (HasTerm(text, "unitalker|taoavatar|musetalk")) return MNNModelCatalogCategory.AvatarAnimation;
            if (HasTerm(text, "indextts[0-9]*|index-tts|fireredtts[0-9]*|sherpa-vits|pipertts")) return MNNModelCatalogCategory.SpeechSynthesis;
            if (HasTerm(text, "stable-audio|music[0-9]+|music-generation|audio-generation")) return MNNModelCatalogCategory.AudioGeneration;
            if (HasTerm(text, "uvdoc|document-unwarping|doc-unwarping")) return MNNModelCatalogCategory.DocumentRectification;
            if (HasTerm(text, "vggt")) return MNNModelCatalogCategory.Reconstruction3D;
            if (HasTerm(text, "2d106det|face-landmarks?")) return MNNModelCatalogCategory.FaceLandmarks;
            if (HasTerm(text, "glint360k|glintr100|arcface|facenet")) return MNNModelCatalogCategory.FaceRecognition;
            if (HasTerm(text, "retinaface|scrfd")) return MNNModelCatalogCategory.FaceDetection;
            if (HasTerm(text, "modnet|matting")) return MNNModelCatalogCategory.ImageMatting;
            if (HasTerm(text, "doclayout")) return MNNModelCatalogCategory.DocumentLayout;
            if (HasTerm(text, "mobileclip|bioclip2?|siglip2?|openclip|clip-vit")) return MNNModelCatalogCategory.ImageEmbedding;
            if (HasTerm(text, "animatediff|wan2[._][0-9]|hunyuanvideo")) return MNNModelCatalogCategory.VideoGeneration;
            if (HasTerm(text, "omni|all-modal")) return MNNModelCatalogCategory.OmniModel;
            if (HasTerm(text, "reranker|re-ranker|rerank")) return MNNModelCatalogCategory.Reranker;
            if (HasTerm(text, "embedding|bge|gte|e5|all-minilm")) return MNNModelCatalogCategory.EmbeddingModel;
            if (HasTerm(text, "tts[0-9]*|piper|bert-vits2?|supertonic|cosyvoice|cosy-voice|kokoro|f5-tts|fish-speech")) return MNNModelCatalogCategory.SpeechSynthesis;
            if (HasTerm(text, "asr|whisper|zipformer|paraformer|sensevoice(?:small)?|quartznet")) return MNNModelCatalogCategory.SpeechRecognition;
            if (HasTerm(text, "ocr|pp-?ocr[v0-9]*|optical-character")) return MNNModelCatalogCategory.OCRModel;
            if (HasTerm(text, "vl|vlm|fastvlm|smolvlm[0-9]*|gui-owl|internvl[0-9]*|minicpm-v|moondream[0-9]*|llava")) return MNNModelCatalogCategory.VisionLanguageModel;
            if (HasTerm(text, "qwen[^ /]*-audio|lfm[0-9.]*-audio|fun-audio-chat|audio-language|ultravox")) return MNNModelCatalogCategory.AudioLanguageModel;
            if (HasTerm(text, "coder|code|starcoder[0-9]*|codellama")) return MNNModelCatalogCategory.CodeModel;
            if (HasTerm(text, "guard[0-9]*|qwen[0-9.]*guard|llama[0-9.]*guard|xguard|moderation")) return MNNModelCatalogCategory.SafetyModel;
            if (HasTerm(text, "stable-diffusion|sana|flux[0-9]*|sdxl|sd15|sd1[._]5|sd-mnn|mnnsd|text-to-image")) return MNNModelCatalogCategory.ImageGenerationModel;
            if (HasTerm(text, "opus-mt|hy-mt[0-9.]*")) return MNNModelCatalogCategory.Translation;
            if (HasTerm(text, "yolo[v0-9a-z]*")) return MNNModelCatalogCategory.ObjectDetection;
            if (HasTerm(text, "deeplab[v0-9]*|sam[0-9]*|segment-anything")) return MNNModelCatalogCategory.SemanticSegmentation;
            if (HasTerm(text, "movenet|rtmpose|openpose")) return MNNModelCatalogCategory.PoseEstimation;
            if (HasTerm(text, "esrgan|realesrgan|swinir")) return MNNModelCatalogCategory.SuperResolution;
            if (HasTerm(text, "mobilenet[v0-9]*|resnet[0-9]*|efficientnet")) return MNNModelCatalogCategory.ImageClassification;
            if (HasTerm(text, "mobilellm[0-9]*|tinyllama|qwen[0-9.]*|llama[0-9.]*|gemma[0-9.]*|lfm2|deepseek|chatglm[0-9.]*|internlm[0-9.]*|smollm[0-9.]*|hunyuan|minicpm[0-9.]*")) return MNNModelCatalogCategory.LargeLanguageModel;
            return null;
        }

        private static bool HasTerm(string text, string alternatives)
        {
            return TermPatterns.GetOrAdd(alternatives, CreateTermPattern).IsMatch(text);
        }
    }
}
