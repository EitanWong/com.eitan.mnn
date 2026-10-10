using System;
using System.Linq;

namespace MNN.Unity.Editor
{
    // Keep the original four values stable for restored windows.
    internal enum MNNStudioTask
    {
        Chat,
        Embedding,
        Reranker,
        ImageGeneration,
        SpeechRecognition,
        SpeechSynthesis,
        Vision,
        AudioUnderstanding,
        Omni,
        OCR,
        Code,
        Safety,
        ImageClassification,
        ObjectDetection,
        Segmentation,
        Pose,
        FaceDetection,
        FaceRecognition,
        SuperResolution,
        StyleTransfer,
        NLP,
        AudioProcessing,
        Other
    }

    internal sealed class MNNStudioTaskInfo
    {
        internal readonly MNNStudioTask Task;
        internal readonly string Name, Group, Description, Requirements, Example, Recommendation;
        internal readonly string[] Repositories;
        internal readonly MNNModelCategory[] Categories;
        internal readonly MNNModelCapabilities Required;
        internal readonly bool Runnable;
        internal MNNStudioTaskInfo(MNNStudioTask task, string name, string group, string description, string requirements, string example, string recommendation, string[] repositories, MNNModelCategory[] categories, MNNModelCapabilities required = MNNModelCapabilities.Text, bool runnable = false)
        {
            Task = task;
            Name = name;
            Group = group;
            Description = description;
            Requirements = requirements;
            Example = example;
            Recommendation = recommendation;
            Repositories = repositories;
            Categories = categories;
            Required = required;
            Runnable = runnable;
        }

        // A workspace can use several model types; the backend type remains on the model.
        internal bool Accepts(MNNStudioModel model)
        {
            if (model == null)
                return false;
            if (Task == MNNStudioTask.Other)
                return true;
            if (Task == MNNStudioTask.Chat)
                return model.Task == MNNStudioTask.Chat;
            if (Task == MNNStudioTask.SpeechRecognition)
                return model.Task == Task || model.Task == MNNStudioTask.Chat && model.Supports(Required);
            if (Task == MNNStudioTask.OCR && model.Task == Task)
                return true;
            if (Required != MNNModelCapabilities.Text)
                return model.Task == MNNStudioTask.Chat && model.Supports(Required);
            if (Task == MNNStudioTask.Code || Task == MNNStudioTask.Safety)
                return model.Task == MNNStudioTask.Chat && MatchesCatalog(new MNNModelInfo{name = model.Name, category = model.Category});
            return model.Task == Task;
        }

        internal bool CanRun(MNNStudioModel model) => Runnable && Accepts(model) && (model.Task == MNNStudioTask.Chat || model.Task == MNNStudioTask.Embedding || model.Task == MNNStudioTask.Reranker || model.HasGenerationPipeline);
        internal bool MatchesCatalog(MNNModelInfo model)
        {
            if (model == null)
                return false;
            if (Task == MNNStudioTask.Other)
                return true; // Escape hatch for new tasks/resources in the official catalog.
            var category = model.category ?? MNNModelRepository.Classify(model.modelScopeId ?? model.name, model.displayName, model.description, model.tasks, model.tags);
            return Categories.Any(value => value.ToString() == category);
        }

        internal string UnavailableReason => Task == MNNStudioTask.SpeechRecognition ? "Sherpa-MNN Zipformer repositories are available for download and model preparation, but Studio does not yet include the Sherpa streaming recognizer runtime. Audio-language models can still transcribe audio here." : Task == MNNStudioTask.SpeechSynthesis ? "Select a complete Supertonic, Bert-VITS2 or Piper repository. Piper also needs the eSpeak-NG executable (install it or set MNN_ESPEAK_NG_PATH)." : Task == MNNStudioTask.ImageGeneration ? "Select a complete SD 1.5 general or Sana Edit V2 repository, including tokenizer and external weights." : "This task's model pipeline is not connected to Studio yet. You can download or locate its models here; inference will become available when its runtime is implemented.";
    }

    internal static class MNNStudioTasks
    {
        private const string LlmFiles = "Load the complete MNN repository: config.json, llm_config.json, tokenizer, .mnn weights and external weight files. Keep the original folder structure.";
        internal static readonly MNNStudioTaskInfo[] All = {Llm(MNNStudioTask.Chat, "Chat", "Language", "Text generation and multi-turn local conversations.", "", "Qwen3.5-0.8B-MNN", MNNModelCategory.LargeLanguageModel, MNNModelCategory.TextGeneration, MNNModelCategory.VisionLanguageModel, MNNModelCategory.AudioLanguageModel, MNNModelCategory.OmniModel, MNNModelCategory.CodeModel), new MNNStudioTaskInfo(MNNStudioTask.Embedding, "Embeddings", "Retrieval", "Encode text and compare semantic similarity.", LlmFiles, "", "Qwen3-Embedding-0.6B-MNN", new[]{"MNN/Qwen3-Embedding-0.6B-MNN"}, new[]{MNNModelCategory.EmbeddingModel}, runnable: true), new MNNStudioTaskInfo(MNNStudioTask.Reranker, "Reranking", "Retrieval", "Score query/document relevance.", LlmFiles, "", "Qwen3-Reranker-0.6B-MNN", new[]{"MNN/Qwen3-Reranker-0.6B-MNN"}, new[]{MNNModelCategory.Reranker}, runnable: true), new MNNStudioTaskInfo(MNNStudioTask.ImageGeneration, "Image generation", "Generation", "Stable Diffusion 1.5 text-to-image and Sana Edit V2 reference-image editing.", "For SD 1.5, download general with CLIP vocabulary/merges, text encoder, UNet, VAE and external weights. For Sana Edit V2, keep the complete repository including llm, connector, projector, transformer and both VAE graphs; select a reference image to edit.", "A red bicycle parked beside a white wall, photograph, daylight", "MNN/stable-diffusion-v1-5-mnn (general)", new[]{"MNN/stable-diffusion-v1-5-mnn", "MNN/MNN-Sana-Edit-V2"}, new[]{MNNModelCategory.ImageGenerationModel}, runnable: true), Media(MNNStudioTask.SpeechRecognition, "Speech recognition", "Audio", "Transcribe a WAV recording to text using an audio-language model. Standalone ASR models are listed for preparation.", "Transcribe this audio verbatim. Return only the transcript in its original language.", "LFM2.5-Audio-1.5B-MNN", MNNModelCapabilities.AudioInput, MNNModelCategory.AudioLanguageModel, MNNModelCategory.OmniModel, MNNModelCategory.SpeechRecognition), new MNNStudioTaskInfo(MNNStudioTask.SpeechSynthesis, "Speech synthesis", "Audio", "Supertonic and Piper English speech; Bert-VITS2 Chenxi Chinese speech.", "Supertonic: duration_predictor, text_encoder, vector_estimator and vocoder for one precision, tts.json, unicode_indexer.json and voice_styles. Bert-VITS2: Chenxi generator, Chinese BERT graph/weight and common/text_processing_jsons. Piper: voice graphs, config.json, espeak-ng-data; install eSpeak-NG and set MNN_ESPEAK_NG_PATH if needed.", "The capital of France is Paris.", "MNN/supertonic-tts-mnn (fp16)", new[]{"MNN/supertonic-tts-mnn", "MNN/bert-vits2-MNN", "MNN/piper-voices-MNN"}, new[]{MNNModelCategory.SpeechSynthesis}, runnable: true), Media(MNNStudioTask.Vision, "Image understanding", "Multimodal", "Ask questions about an image.", "Describe this image in detail.", "SmolVLM-256M-Instruct-MNN", MNNModelCapabilities.Vision, MNNModelCategory.VisionLanguageModel, MNNModelCategory.OmniModel), Media(MNNStudioTask.AudioUnderstanding, "Audio understanding", "Multimodal", "Understand, summarize or translate recorded audio.", "Describe and summarize this audio.", "LFM2.5-Audio-1.5B-MNN", MNNModelCapabilities.AudioInput, MNNModelCategory.AudioLanguageModel, MNNModelCategory.OmniModel), Media(MNNStudioTask.Omni, "Omni", "Multimodal", "Combine text, an image and audio; generate spoken replies when supported. Use Chat for multi-turn voice conversations.", "Respond to the supplied image and/or audio.", "Qwen2.5-Omni-3B-MNN", MNNModelCapabilities.Vision | MNNModelCapabilities.AudioInput, MNNModelCategory.OmniModel), Media(MNNStudioTask.OCR, "OCR / document reading", "Vision", "Extract text from an image with a vision-language model. Dedicated OCR pipelines require their own runtime.", "Extract all visible text from this image. Preserve reading order and return only the text.", "SmolVLM-256M-Instruct-MNN", MNNModelCapabilities.Vision, MNNModelCategory.OCRModel, MNNModelCategory.OCR, MNNModelCategory.VisionLanguageModel, MNNModelCategory.OmniModel), Llm(MNNStudioTask.Code, "Code generation", "Language", "Generate and explain code with a code language model.", "Write a C# example that ", "", MNNModelCategory.CodeModel), Llm(MNNStudioTask.Safety, "Safety / moderation", "Language", "Evaluate text with a guard model. Follow its model card's required prompt format and label definitions.", "", "", MNNModelCategory.SafetyModel), Guide(MNNStudioTask.ImageClassification, "Image classification", "Vision", "Image labels and class scores.", "Load MNN weights, class labels and the model's resize, normalization and input/output specifications.", "Browse the official catalog for classification models.", Array.Empty<string>(), MNNModelCategory.ImageClassification), Guide(MNNStudioTask.ObjectDetection, "Object detection", "Vision", "Detect objects and bounding boxes.", "Load MNN weights and labels; retain input preprocessing, box decoding and NMS settings from the model card.", "Browse the official catalog for detection models.", Array.Empty<string>(), MNNModelCategory.ObjectDetection), Guide(MNNStudioTask.Segmentation, "Segmentation", "Vision", "Semantic segmentation masks.", "Load MNN weights, labels/palette and the documented image preprocessing and output mask format.", "Browse the official catalog for segmentation models.", Array.Empty<string>(), MNNModelCategory.SemanticSegmentation), Guide(MNNStudioTask.Pose, "Pose estimation", "Vision", "Estimate joints and poses.", "Load MNN weights and the documented keypoint layout, input transform and decoder.", "Browse the official catalog for pose models.", Array.Empty<string>(), MNNModelCategory.PoseEstimation), Guide(MNNStudioTask.FaceDetection, "Face detection", "Vision", "Detect faces and landmarks.", "Load MNN weights and model-specific anchors, preprocessing and landmark/box decoder.", "Browse the official catalog for face detection models.", Array.Empty<string>(), MNNModelCategory.FaceDetection), Guide(MNNStudioTask.FaceRecognition, "Face recognition", "Vision", "Encode aligned faces and compare identity features.", "Load recognition weights and the matching face detector/alignment configuration specified by the model.", "Browse the official catalog for face recognition models.", Array.Empty<string>(), MNNModelCategory.FaceRecognition), Guide(MNNStudioTask.SuperResolution, "Super resolution", "Generation", "Upscale images.", "Load MNN weights with the documented scale factor, color range and tiling/preprocessing settings.", "Browse the official catalog for super resolution models.", Array.Empty<string>(), MNNModelCategory.SuperResolution), Guide(MNNStudioTask.StyleTransfer, "Style transfer", "Generation", "Transform image style.", "Load MNN weights, any required style inputs and documented color normalization/output conversion.", "Browse the official catalog for style transfer models.", Array.Empty<string>(), MNNModelCategory.StyleTransfer), Guide(MNNStudioTask.NLP, "NLP / text analysis", "Language", "Text classification and other task-specific text models.", "Load MNN weights, vocabulary/tokenizer, labels and the model's tokenization/input specification.", "Browse the official catalog for NLP models.", Array.Empty<string>(), MNNModelCategory.NLP), Guide(MNNStudioTask.AudioProcessing, "Audio processing", "Audio", "Other task-specific speech and audio models.", "Load all MNN weights and documented sample rate, frontend, normalization and decoder resources.", "Browse the official catalog for audio models.", Array.Empty<string>(), MNNModelCategory.AudioProcessing), Guide(MNNStudioTask.Other, "All models / other tasks", "Catalog", "Browse every official MNN resource, including new tasks and auxiliary models.", "Read the model card for required files and its inference pipeline. A downloadable resource is not necessarily a runnable Studio model.", "Browse the full MNN catalog; search by model name or task.", Array.Empty<string>(), MNNModelCategory.Other)};
        internal static MNNStudioTaskInfo Get(int index) => All[index >= 0 && index < All.Length ? index : 0];
        internal static MNNStudioTask FromCategory(string category)
        {
            // Only identify dedicated model formats here. LLM media flags select their workspaces.
            foreach (var info in All)
                if (info.Task != MNNStudioTask.Chat && info.Task != MNNStudioTask.OCR && info.Required == MNNModelCapabilities.Text && info.Categories.Any(value => value.ToString() == category))
                    return info.Task;
            if (category == MNNModelCategory.SpeechRecognition.ToString())
                return MNNStudioTask.SpeechRecognition;
            if (category == MNNModelCategory.OCRModel.ToString() || category == MNNModelCategory.OCR.ToString())
                return MNNStudioTask.OCR;
            return MNNStudioTask.Other;
        }

        private static MNNStudioTaskInfo Llm(MNNStudioTask task, string name, string group, string description, string example, string model, params MNNModelCategory[] categories) => new MNNStudioTaskInfo(task, name, group, description, LlmFiles, example, model, string.IsNullOrEmpty(model) ? Array.Empty<string>() : new[]{"MNN/" + model}, categories, runnable: true);
        private static MNNStudioTaskInfo Media(MNNStudioTask task, string name, string group, string description, string example, string model, MNNModelCapabilities required, params MNNModelCategory[] categories)
        {
            if (task == MNNStudioTask.SpeechRecognition)
            {
                description = "Transcribe a WAV recording to text using an audio-language model. Sherpa-MNN Zipformer repositories are also listed for model preparation.";
                return new MNNStudioTaskInfo(task, name, group, description, LlmFiles + " Sherpa-MNN streaming Zipformer files: config.json, configuration.json, encoder/decoder/joiner .mnn files, and tokens.txt. The Sherpa recognizer runtime is not included.", example, model, new[]{"MNN/" + model, "MNN/sherpa-mnn-streaming-zipformer-bilingual-zh-en-2023-02-20", "MNN/sherpa-mnn-streaming-zipformer-en-2023-02-21"}, categories, required, true);
            }

            return new MNNStudioTaskInfo(task, name, group, description, LlmFiles + " Use PNG/JPEG/BMP images or WAV audio as applicable.", example, model, new[]{"MNN/" + model}, categories, required, true);
        }
        private static MNNStudioTaskInfo Guide(MNNStudioTask task, string name, string group, string description, string requirements, string recommendation, string[] repositories, params MNNModelCategory[] categories) => new MNNStudioTaskInfo(task, name, group, description, requirements, "", recommendation, repositories, categories);
    }
}
