using System;
using System.Linq;

namespace MNN.Unity.Editor
{
    // Append tasks to preserve serialized values and their All array indices.
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
        Other,
        ImageMatting,
        ImageRestoration,
        Reconstruction3D,
        AvatarAnimation,
        FaceLandmarks,
        DocumentLayout,
        ImageEmbedding,
        VideoGeneration,
        Resources,
        Translation,
        DepthEstimation,
        Robotics,
        AudioGeneration,
        SpeakerRecognition,
        DocumentRectification,
        KeywordSpotting,
        EchoCancellation,
        VoiceActivityDetection,
        TurnDetection,
        SpeechEnhancement,
        SpeechSeparation,
        SpeakerDiarization
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
            if (Task == MNNStudioTask.KeywordSpotting)
                return model.Task == Task;
            if (Task >= MNNStudioTask.VoiceActivityDetection && Task <= MNNStudioTask.SpeakerDiarization)
                return model.Task == MNNStudioTask.AudioProcessing && model.AudioSubtask == Task;
            if (Task == MNNStudioTask.AudioProcessing && model.Task == Task)
                return true;
            if (Task == MNNStudioTask.ObjectDetection && model.Task == Task)
                return true;
            if (Task == MNNStudioTask.OCR && model.Task == Task)
                return true;
            if (model.Task == Task)
                return true;
            if (Required != MNNModelCapabilities.Text)
                return model.Task == MNNStudioTask.Chat && model.Supports(Required);
            if (Task == MNNStudioTask.Code || Task == MNNStudioTask.Safety || Task == MNNStudioTask.Translation)
                return model.Task == MNNStudioTask.Chat && MatchesCatalog(new MNNModelInfo{name = model.Name, category = model.Category});
            return model.Task == Task;
        }

        internal bool CanRun(MNNStudioModel model) => Runnable && Accepts(model) && (model.Task == MNNStudioTask.Chat || model.Task == MNNStudioTask.Embedding || model.Task == MNNStudioTask.Reranker || model.HasGenerationPipeline || model.HasVisionPipeline || model.HasSpeechRecognitionPipeline || model.HasAudioProcessingPipeline || model.HasKeywordSpottingPipeline || model.HasSpeakerRecognitionPipeline || model.HasImageEmbeddingPipeline || model.HasDocumentLayoutPipeline || model.HasTextDecisionPipeline || model.HasEchoCancellationPipeline);
        internal bool MatchesCatalog(MNNModelInfo model)
        {
            if (model == null)
                return false;
            if (Task == MNNStudioTask.Other)
                return true; // Escape hatch for new tasks/resources in the official catalog.
            var category = model.category ?? MNNModelRepository.Classify(model.modelScopeId ?? model.name, model.displayName, model.description, model.tasks, model.tags);
            if (Task >= MNNStudioTask.VoiceActivityDetection && Task <= MNNStudioTask.SpeakerDiarization)
                return MNNStudioModel.AudioSubtaskForCategory(category,
                    model.repositoryId ?? model.modelScopeId ?? model.name, model.tasks) == Task;
            if (Task == MNNStudioTask.AudioProcessing && MNNModelCatalog.IsAudioProcessingCategory(category)) return true;
            return Categories.Any(value => value.ToString() == category);
        }

        internal string UnavailableReason => Task >= MNNStudioTask.VoiceActivityDetection && Task <= MNNStudioTask.SpeakerDiarization ? "Install a verified profile for this audio task with Model Manager." : Task == MNNStudioTask.EchoCancellation ? "Install DTLN AEC 128, 256 or 512 with Model Manager and choose synchronized 16 kHz microphone and playback reference WAV files." : Task == MNNStudioTask.KeywordSpotting ? "Install the verified Jeryuk/kws-zipformer-wenetspeech-3.3M-MNN profile with Model Manager. Choose a WAV, record audio or start live listening with tokenized keywords." : Task == MNNStudioTask.AudioProcessing ? "Install a verified Silero, MarbleNet, FSMN VAD, Smart Turn, FRCRN, DeepFilterNet3, MossFormer2 or LiveKit profile with Model Manager. AV-MossFormer2-TSE requires the mixture WAV and synchronized 25 FPS target-face frames." : Task == MNNStudioTask.SpeechRecognition ? "Install verified Qwen3-ASR 0.6B INT8, Whisper tiny.en/tiny/base/small, SenseVoiceSmall or English/Chinese-English Zipformer profile with Model Manager, or select an audio-language model. Zipformer needs encoder, decoder, joiner, tokens and mnn_zipformer.json; an unprepared repository cannot run." : Task == MNNStudioTask.SpeechSynthesis ? "Supported speech profiles include Qwen3-TTS explicit KV (prepared voice, Chinese/English), IndexTTS 2.5, FireRedTTS3, Supertonic, Bert-VITS2, Piper and the complete CosyVoice3 release. Other discovered TTS repositories remain downloadable until their tokenizer and graph-specific runtime is implemented. Download CosyVoice with Model Manager to verify and extract all required assets. Piper also needs the eSpeak-NG executable (install it or set MNN_ESPEAK_NG_PATH)." : Task == MNNStudioTask.ImageGeneration ? "Select a complete SD 1.5 general or Sana Edit V2 repository, including tokenizer and external weights." : "This task's model pipeline is not connected to Studio yet. You can download or locate its models here; inference will become available when its runtime is implemented.";
    }

    internal static class MNNStudioTasks
    {
        private const string LlmFiles = "Load the complete MNN repository: config.json, llm_config.json, tokenizer, .mnn weights and external weight files. Keep the original folder structure.";
        internal static readonly MNNStudioTaskInfo[] All = {Llm(MNNStudioTask.Chat, "Chat", "Language", "Text generation and multi-turn local conversations.", "", "Qwen3.5-0.8B-MNN", MNNModelCategory.LargeLanguageModel, MNNModelCategory.TextGeneration, MNNModelCategory.VisionLanguageModel, MNNModelCategory.AudioLanguageModel, MNNModelCategory.OmniModel, MNNModelCategory.CodeModel), new MNNStudioTaskInfo(MNNStudioTask.Embedding, "Embeddings", "Retrieval", "Encode text and compare semantic similarity.", LlmFiles, "", "Qwen3-Embedding-0.6B-MNN", new[]{"MNN/Qwen3-Embedding-0.6B-MNN"}, new[]{MNNModelCategory.EmbeddingModel}, runnable: true), new MNNStudioTaskInfo(MNNStudioTask.Reranker, "Reranking", "Retrieval", "Score query/document relevance.", LlmFiles, "", "Qwen3-Reranker-0.6B-MNN", new[]{"MNN/Qwen3-Reranker-0.6B-MNN"}, new[]{MNNModelCategory.Reranker}, runnable: true), new MNNStudioTaskInfo(MNNStudioTask.ImageGeneration, "Image generation", "Generation", "Stable Diffusion 1.5 text-to-image and Sana Edit V2 reference-image editing.", "For SD 1.5, download general with CLIP vocabulary/merges, text encoder, UNet, VAE and external weights. For Sana Edit V2, keep the complete repository including llm, connector, projector, transformer and both VAE graphs; select a reference image to edit.", "A red bicycle parked beside a white wall, photograph, daylight", "MNN/stable-diffusion-v1-5-mnn (general)", new[]{"MNN/stable-diffusion-v1-5-mnn", "MNN/MNN-Sana-Edit-V2"}, new[]{MNNModelCategory.ImageGenerationModel}, runnable: true), Media(MNNStudioTask.SpeechRecognition, "Speech recognition", "Audio", "Transcribe a WAV recording with Zipformer English/Chinese-English ASR or an audio-language model.", "Transcribe this audio verbatim. Return only the transcript in its original language.", "LFM2.5-Audio-1.5B-MNN", MNNModelCapabilities.AudioInput, MNNModelCategory.AudioLanguageModel, MNNModelCategory.OmniModel, MNNModelCategory.SpeechRecognition), new MNNStudioTaskInfo(MNNStudioTask.SpeechSynthesis, "Speech synthesis", "Audio", "Qwen3-TTS prepared voices in Chinese/English; IndexTTS 2.5 and FireRedTTS3 with your reference WAV; CosyVoice3 reference voice; Supertonic and Piper English speech; Bert-VITS2 Chenxi Chinese/English and mixed speech.", "Qwen3-TTS: install yunfengwang/Qwen3-TTS-12Hz-0.6B-Base-MNN with all graphs, tokenizer, weights and prepared voice assets. IndexTTS / FireRedTTS3: download the complete profile with Model Manager; supply a 0.5–15 second reference WAV. FireRed also requires its exact transcript. CosyVoice: complete fp16 archive (automatically verified and extracted to model/). Supertonic: duration_predictor, text_encoder, vector_estimator and vocoder for one precision, tts.json, unicode_indexer.json and voice_styles. Bert-VITS2: Chenxi generator, Chinese BERT graph/weight and common/text_processing_jsons. Piper: voice graphs, config.json, espeak-ng-data; install eSpeak-NG and set MNN_ESPEAK_NG_PATH if needed.", "The capital of France is Paris.", "MNN/supertonic-tts-mnn (fp16)", new[]{MNNVerifiedTtsModel.Qwen3ExplicitRepository, MNNReferenceSpeechModel.IndexRepository, MNNReferenceSpeechModel.FireRedRepository, "VicenTrent/Cosy-Voice-MNN", "MNN/supertonic-tts-mnn", "MNN/bert-vits2-MNN", "MNN/piper-voices-MNN"}, new[]{MNNModelCategory.SpeechSynthesis}, runnable: true), Media(MNNStudioTask.Vision, "Image understanding", "Multimodal", "Ask questions about an image.", "Describe this image in detail.", "SmolVLM-256M-Instruct-MNN", MNNModelCapabilities.Vision, MNNModelCategory.VisionLanguageModel, MNNModelCategory.OmniModel), Media(MNNStudioTask.AudioUnderstanding, "Audio understanding", "Multimodal", "Understand, summarize or translate recorded audio.", "Describe and summarize this audio.", "LFM2.5-Audio-1.5B-MNN", MNNModelCapabilities.AudioInput, MNNModelCategory.AudioLanguageModel, MNNModelCategory.OmniModel), Media(MNNStudioTask.Omni, "Omni", "Multimodal", "Combine text, an image and audio; generate spoken replies when supported. Use Chat for multi-turn voice conversations.", "Respond to the supplied image and/or audio.", "Qwen2.5-Omni-3B-MNN", MNNModelCapabilities.Vision | MNNModelCapabilities.AudioInput, MNNModelCategory.OmniModel), Media(MNNStudioTask.OCR, "OCR / document reading", "Vision", "Read full images with installed PP-OCRv5 detection and a matched language recognizer, returning text regions and an annotated image. Older recognition-only profiles accept upright cropped lines. Vision-language models can also read documents.", "Extract all visible text from this image. Preserve reading order and return only the text.", "SmolVLM-256M-Instruct-MNN", MNNModelCapabilities.Vision, MNNModelCategory.OCRModel, MNNModelCategory.OCR, MNNModelCategory.VisionLanguageModel, MNNModelCategory.OmniModel), Llm(MNNStudioTask.Code, "Code generation", "Language", "Generate and explain code with a code language model.", "Write a C# example that ", "", MNNModelCategory.CodeModel), Llm(MNNStudioTask.Safety, "Safety / moderation", "Language", "Evaluate text with a guard model. Follow its model card's required prompt format and label definitions.", "", "", MNNModelCategory.SafetyModel), ImageTask(MNNStudioTask.ImageClassification, "Image classification", "Vision", "Image labels and class scores.", "Load MNN weights, class labels and the model's resize, normalization and input/output specifications.", "Browse the official catalog for classification models.", Array.Empty<string>(), MNNModelCategory.ImageClassification), new MNNStudioTaskInfo(MNNStudioTask.ObjectDetection, "Object detection", "Vision", "Find COCO objects and bounding boxes with YOLOv8.", "Install rap20171201/YOLOv8-N-MNN with Model Manager, or supply a verified mnn_yolov8.json profile and its graph. Choose a PNG or JPEG image.", "Detect the objects in this image.", MNNYoloV8Model.Repository, new[] {MNNYoloV8Model.Repository}, new[] {MNNModelCategory.ObjectDetection}, MNNModelCapabilities.Vision, true), ImageTask(MNNStudioTask.Segmentation, "Segmentation", "Vision", "Semantic segmentation masks.", "Load MNN weights, labels/palette and the documented image preprocessing and output mask format.", "Browse the official catalog for segmentation models.", Array.Empty<string>(), MNNModelCategory.SemanticSegmentation), ImageTask(MNNStudioTask.Pose, "Pose estimation", "Vision", "Estimate one primary person with 17 COCO joints using MoveNet SinglePose Lightning.", "Prepare the pinned MoveNet ONNX profile with Tools~/Models/prepare_image_profile.py, then choose its local folder. Use a full-body photo or person crop.", "MoveNet SinglePose Lightning (prepared local profile)", Array.Empty<string>(), MNNModelCategory.PoseEstimation), ImageTask(MNNStudioTask.FaceDetection, "Face detection", "Vision", "Detect faces and landmarks.", "Load MNN weights and model-specific anchors, preprocessing and landmark/box decoder.", "Browse the official catalog for face detection models.", Array.Empty<string>(), MNNModelCategory.FaceDetection), ImageTask(MNNStudioTask.FaceRecognition, "Face recognition", "Vision", "Encode aligned faces and compare identity features.", "Load recognition weights and the matching face detector/alignment configuration specified by the model.", "Browse the official catalog for face recognition models.", Array.Empty<string>(), MNNModelCategory.FaceRecognition), ImageTask(MNNStudioTask.SuperResolution, "Super resolution", "Generation", "Upscale images.", "Load MNN weights with the documented scale factor, color range and tiling/preprocessing settings.", "Browse the official catalog for super resolution models.", Array.Empty<string>(), MNNModelCategory.SuperResolution), ImageTask(MNNStudioTask.StyleTransfer, "Style transfer", "Generation", "Transform an image with the fixed Candy style network.", "", "", Array.Empty<string>(), MNNModelCategory.StyleTransfer), new MNNStudioTaskInfo(MNNStudioTask.NLP, "NLP / structured decisions", "Language", "English Laya choice, ordinal score and Boolean decisions.", "Select the pinned English FP16 Laya folder with tokenizer and manifest. INT8 and multilingual variants remain unverified. Auto uses CPU / High.", "Hi, we were billed twice for March. Please refund the duplicate or we will cancel our plan.", MNNTextDecisionModel.Repository, new[] {MNNTextDecisionModel.Repository}, new[] {MNNModelCategory.NLP}, runnable: true), new MNNStudioTaskInfo(MNNStudioTask.AudioProcessing, "Audio processing", "Audio", "Detect speech intervals with Silero VAD, FSMN VAD, or turn completion with Smart Turn; enhance speech with FRCRN-SE.", "Install Silero VAD, Smart Turn, FSMN VAD or FRCRN-SE with Model Manager. Choose or record a WAV (8–96 kHz, up to ten minutes). VAD profiles return speech intervals, Smart Turn evaluates the last eight seconds, and FRCRN returns an enhanced waveform at the input WAV sample rate.", "Find speech intervals in this recording.", MNNSileroModel.Repository, new[] {MNNSileroModel.Repository, MNNSmartTurnModel.Repository, "iic/speech_fsmn_vad_zh-cn-16k-common-onnx", "manyeyes/frcrn-se-16k-onnx"}, new[] {MNNModelCategory.AudioProcessing}, MNNModelCapabilities.AudioInput, true), Guide(MNNStudioTask.Other, "All models / other tasks", "Catalog", "Browse every official MNN resource, including new tasks and auxiliary models.", "Read the model card for required files and its inference pipeline. A downloadable resource is not necessarily a runnable Studio model.", "Browse the full MNN catalog; search by model name or task.", Array.Empty<string>(), MNNModelCategory.Other),
            ImageTask(MNNStudioTask.ImageMatting, "Image matting", "Vision", "Estimate foreground alpha.", "Load the graph with its RGB normalization, resize policy and alpha decoder.", "Inspect the matching model card before preparing its task adapter.", Array.Empty<string>(), MNNModelCategory.ImageMatting),
            ImageTask(MNNStudioTask.ImageRestoration, "Image restoration", "Vision", "Remove haze, noise or blur.", "Load the matching restoration graph, pixel range and output conversion.", "Inspect the matching model card before preparing its task adapter.", Array.Empty<string>(), MNNModelCategory.ImageRestoration),
            Guide(MNNStudioTask.Reconstruction3D, "3D reconstruction", "Vision", "Reconstruct geometry from images.", "Load camera, mesh and depth conventions with the complete reconstruction pipeline.", "Inspect the matching model card before preparing its task adapter.", Array.Empty<string>(), MNNModelCategory.Reconstruction3D),
            Guide(MNNStudioTask.AvatarAnimation, "Avatar animation", "Vision", "Drive a digital avatar from image or audio inputs.", "Load the complete motion pipeline, audio frontend and topology or rig resources.", "Inspect the matching model card before preparing its task adapter.", Array.Empty<string>(), MNNModelCategory.AvatarAnimation),
            ImageTask(MNNStudioTask.FaceLandmarks, "Face landmarks", "Vision", "Estimate facial keypoints.", "Load face crops, landmark order, normalization and inverse coordinate transforms.", "Inspect the matching model card before preparing its task adapter.", Array.Empty<string>(), MNNModelCategory.FaceLandmarks),
            new MNNStudioTaskInfo(MNNStudioTask.DocumentLayout, "Document layout", "Vision", "Find page regions and predicted reading order with PP-DocLayoutV3.", "Install the pinned tansen87/PP-DocLayoutV3_mnn graph and layout-meta.json. Choose a page image. Table regions are detected; text recognition and table cells are separate tasks.", "Analyze the document layout.", MNNDocumentLayoutModel.Repository, new[] {MNNDocumentLayoutModel.Repository}, new[] {MNNModelCategory.DocumentLayout}, MNNModelCapabilities.Vision, true),
            new MNNStudioTaskInfo(MNNStudioTask.ImageEmbedding, "Image and multimodal embeddings", "Vision", "Compare an image and text in the same MobileCLIP vector space.", "Install the pinned MobileCLIP image/text encoders and tokenizer. Choose an image and enter a description; the result includes both vectors and their cosine similarity.", "A photograph of a woman.", MNNImageEmbeddingModel.Repository, new[] {MNNImageEmbeddingModel.Repository}, new[] {MNNModelCategory.ImageEmbedding}, MNNModelCapabilities.Vision, true),
            Guide(MNNStudioTask.VideoGeneration, "Video generation", "Generation", "Generate video frames.", "Load all diffusion stages, scheduler, tokenizer, temporal layout and video decoder.", "Inspect the matching model card before preparing its task adapter.", Array.Empty<string>(), MNNModelCategory.VideoGeneration),
            Guide(MNNStudioTask.Resources, "Collections and resources", "Catalog", "Inspect mixed model collections, runtime libraries and diagnostic samples.", "Choose a supported task model from a collection. Libraries and diagnostic graphs are not task inference profiles.", "Inspect repository files and the model card.", Array.Empty<string>(), MNNModelCategory.ModelCollection, MNNModelCategory.RuntimeResources, MNNModelCategory.ModelDiagnostics),
            Llm(MNNStudioTask.Translation, "Translation", "Language", "Translate with a complete language model. Encoder-decoder translation graphs need a separate tokenizer and decoder adapter.", "Translate this text: ", "", MNNModelCategory.Translation),
            ImageTask(MNNStudioTask.DepthEstimation, "Depth estimation", "Vision", "Depth Anything V2 Small relative inverse depth; larger values mean closer. CPU / High.", "", "", Array.Empty<string>(), MNNModelCategory.DepthEstimation),
            Guide(MNNStudioTask.Robotics, "Robotics", "Vision", "Run a perception or action policy.", "Load the complete policy with observation encoding, action units and matching control integration.", "Inspect the matching model card before preparing its task adapter.", Array.Empty<string>(), MNNModelCategory.Robotics),
            Guide(MNNStudioTask.AudioGeneration, "Audio generation", "Audio", "Generate music or sound effects.", "Load all autoregressive or diffusion stages, audio codec, tokenizer and conditioning resources.", "Inspect the matching model card before preparing its task adapter.", Array.Empty<string>(), MNNModelCategory.AudioGeneration),
            new MNNStudioTaskInfo(MNNStudioTask.SpeakerRecognition, "Speaker recognition", "Audio", "Extract a model-specific speaker identity vector.", "Install ERes2NetV2, CAM++, ECAPA-TDNN, or TitaNet (192 dimensions), WeSpeaker ResNet34-LM, or VBx ResNet101 (256 dimensions; VBx retains its raw scale). Choose a WAV within the selected model's documented duration range. Raw cosine scores require application-specific identity thresholds.", "Extract the speaker identity vector.", MNNSpeakerModel.Repository, new[] {MNNSpeakerModel.Repository, MNNWeSpeakerModel.Repository}, new[] {MNNModelCategory.SpeakerRecognition}, MNNModelCapabilities.AudioInput, true),
            ImageTask(MNNStudioTask.DocumentRectification, "Document rectification", "Vision", "Correct document perspective or curved pages.", "Load the documented RGB transform, grid decoder and image resampling pipeline.", "Inspect the matching model card before preparing its task adapter.", Array.Empty<string>(), MNNModelCategory.DocumentRectification),
            new MNNStudioTaskInfo(MNNStudioTask.KeywordSpotting, "Keyword spotting (KWS)", "Audio", "Detect custom Chinese wake words in recordings or live microphone audio.", "Install the verified Chinese Zipformer2 KWS profile with Model Manager. Enter tone-marked pinyin tokens with @display-name, then choose a WAV, record audio, or start live listening. WAV input supports 8–96 kHz, up to ten minutes.", "Detect wake words in this recording.", MNNKwsModel.Repository, new[] {MNNKwsModel.Repository}, new[] {MNNModelCategory.KeywordSpotting}, MNNModelCapabilities.AudioInput, true),
            new MNNStudioTaskInfo(MNNStudioTask.EchoCancellation, "Echo cancellation (AEC)", "Audio", "Remove playback echo with DTLN AEC 128, 256 or 512.", "Choose equal-length 16 kHz WAV files: microphone recording and synchronized playback reference. Align device delay before processing. Output retains 24 ms model delay.", "Cancel playback echo.", MNNDtlnAecModel.Repository(128), MNNDtlnAecModel.Variants.Select(MNNDtlnAecModel.Repository).ToArray(), new[] {MNNModelCategory.EchoCancellation}, MNNModelCapabilities.AudioInput, true),
            AudioSubtask(MNNStudioTask.VoiceActivityDetection, "Voice activity detection", "Detect speech intervals.", "Find speech intervals in this recording."),
            AudioSubtask(MNNStudioTask.TurnDetection, "Turn detection", "Detect when a speaker has finished a turn.", "Determine whether this turn is complete."),
            AudioSubtask(MNNStudioTask.SpeechEnhancement, "Speech enhancement", "Reduce noise and improve speech clarity.", "Enhance this recording."),
            AudioSubtask(MNNStudioTask.SpeechSeparation, "Speech separation", "Separate speakers or extract a target voice.", "Separate the speech sources."),
            AudioSubtask(MNNStudioTask.SpeakerDiarization, "Speaker diarization", "Identify when each speaker is active.", "Identify speaker intervals.")};
        internal static MNNStudioTaskInfo Get(int index) => All[index >= 0 && index < All.Length ? index : 0];
        internal static MNNStudioTask FromCategory(string category)
        {
            if (MNNModelCatalog.IsAudioProcessingCategory(category)) return MNNStudioTask.AudioProcessing;
            if (category == MNNModelCategory.VisionLanguageModel.ToString() || category == MNNModelCategory.AudioLanguageModel.ToString() || category == MNNModelCategory.OmniModel.ToString())
                return MNNStudioTask.Chat;
            // Only identify dedicated model formats here. LLM media flags select their workspaces.
            foreach (var info in All)
                if (info.Task != MNNStudioTask.Chat && info.Categories.Any(value => value.ToString() == category))
                    return info.Task;
            if (category == MNNModelCategory.SpeechRecognition.ToString())
                return MNNStudioTask.SpeechRecognition;
            if (category == MNNModelCategory.KeywordSpotting.ToString())
                return MNNStudioTask.KeywordSpotting;
            if (category == MNNModelCategory.AudioProcessing.ToString())
                return MNNStudioTask.AudioProcessing;
            if (category == MNNModelCategory.OCRModel.ToString() || category == MNNModelCategory.OCR.ToString())
                return MNNStudioTask.OCR;
            return MNNStudioTask.Other;
        }

        private static MNNStudioTaskInfo Llm(MNNStudioTask task, string name, string group, string description, string example, string model, params MNNModelCategory[] categories) => new MNNStudioTaskInfo(task, name, group, description, LlmFiles, example, model, string.IsNullOrEmpty(model) ? Array.Empty<string>() : new[]{"MNN/" + model}, categories, runnable: true);
        private static MNNStudioTaskInfo AudioSubtask(MNNStudioTask task, string name, string description, string example) =>
            new MNNStudioTaskInfo(task, name, "Audio", description, "Install a compatible verified profile with Model Manager and select a WAV recording.",
                example, "", Array.Empty<string>(), AudioSubtaskCategories(task), MNNModelCapabilities.AudioInput, true);

        private static MNNModelCategory[] AudioSubtaskCategories(MNNStudioTask task)
        {
            var category = (MNNModelCategory)Enum.Parse(typeof(MNNModelCategory), task.ToString());
            return task == MNNStudioTask.SpeechSeparation
                ? new[] { category, MNNModelCategory.TargetSpeakerExtraction } : new[] { category };
        }
        private static MNNStudioTaskInfo Media(MNNStudioTask task, string name, string group, string description, string example, string model, MNNModelCapabilities required, params MNNModelCategory[] categories)
        {
            if (task == MNNStudioTask.SpeechRecognition)
            {
                description = "Transcribe WAV recordings with verified Paraformer Chinese, Qwen3-ASR 0.6B INT8, Whisper tiny.en/tiny/base/small, SenseVoiceSmall, English/Chinese-English Zipformer profiles or audio-language models.";
                return new MNNStudioTaskInfo(task, name, group, description, LlmFiles + " Paraformer: install a pinned KindBrave or guoera 8404-token online profile (Chinese, 85 ms to ten minutes, CPU; no timestamps). Bilingual speech may contain errors; English quality is not validated. Qwen3-ASR: install the pinned hb-dev or huangzhengxiang 0.6B INT8 profile (English/Chinese-English, 0.1–30 seconds, Metal/CPU; no timestamps). Whisper: install a pinned tiny.en, tiny, base or small profile (English, 25 ms to 29.5 seconds, CPU; no timestamps). Small additionally offers Chinese recognition. SenseVoiceSmall: install wo105090/SenseVoiceSmall-MNN or xinliu/sensevoice_mnn (85 ms to 60 seconds, CPU). Zipformer: install encoder/decoder/joiner .mnn files and tokens.txt with Model Manager to create the verified mnn_zipformer.json profile. Mono/stereo PCM16/24/32 or float32 WAV at 8–96 kHz, up to ten minutes.", example, model, new[]{MNNParaformerModel.Repository, MNNParaformerModel.DuplicateRepository, MNNQwen3AsrModel.Repository, MNNQwen3AsrModel.ModelScopeRepository, MNNWhisperModel.Repository, "rap20171201/sherpa-mnn-whisper-tiny.en", "developerabu/whisper-tiny-mnn", "developerabu/whisper-base-mnn", "developerabu/whisper-small-mnn", MNNSenseVoiceModel.Repository, MNNSenseVoiceModel.HuggingFaceRepository, "MNN/" + model, "MNN/sherpa-mnn-streaming-zipformer-bilingual-zh-en-2023-02-20", "MNN/sherpa-mnn-streaming-zipformer-en-2023-02-21"}, categories, required, true);
            }

            return new MNNStudioTaskInfo(task, name, group, description, LlmFiles + " Use PNG/JPEG/BMP images or WAV audio as applicable.", example, model, new[]{"MNN/" + model}, categories, required, true);
        }
        private static MNNStudioTaskInfo ImageTask(MNNStudioTask task, string name, string group, string description, string requirements, string recommendation, string[] repositories, params MNNModelCategory[] categories)
        {
            var source = MNNImageTaskProfiles.All.First(value => MNNStudioModel.ImageTaskFor(value.Profile) == task);
            if (source.Url == null)
            {
                string profile = source.Profile == MNNImageTaskProfile.CandyStyle ? "candy" : source.Profile == MNNImageTaskProfile.MoveNetLightning ? "movenet" : source.Profile == MNNImageTaskProfile.DepthAnythingV2Small ? "depth" : "deeplab";
                return new MNNStudioTaskInfo(task, name, group, description,
                    "Prepare the pinned source using Tools~/Models/prepare_image_profile.py --profile " + profile + " and an existing official MNN 3.6.1 converter. Select the resulting local folder.",
                    "Run " + name + " on this image.", name + " · local prepared profile", Array.Empty<string>(), categories, MNNModelCapabilities.Vision, true);
            }
            string instructions = "Install the pinned " + source.Repository + " profile using Model Manager. Choose an RGB image.";
            if (task == MNNStudioTask.FaceRecognition) instructions += " Supply a five-point aligned 112x112 face crop; the runtime AlignFace helper accepts SCRFD keypoints.";
            if (task == MNNStudioTask.FaceLandmarks) instructions += " Supply a face crop with 1.5x bounding-box margin; use the runtime CropFace helper.";
            return new MNNStudioTaskInfo(task, name, group, description, instructions, "Run " + name + " on this image.", source.Repository, new[] {source.Repository}, categories, MNNModelCapabilities.Vision, true);
        }

        private static MNNStudioTaskInfo Guide(MNNStudioTask task, string name, string group, string description, string requirements, string recommendation, string[] repositories, params MNNModelCategory[] categories) => new MNNStudioTaskInfo(task, name, group, description, requirements, "", recommendation, repositories, categories);
    }
}
