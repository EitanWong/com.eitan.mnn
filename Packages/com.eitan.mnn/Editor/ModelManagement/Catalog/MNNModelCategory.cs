using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity.Editor
{
    public enum MNNModelCategory
    {
        All,
        ImageClassification,
        ObjectDetection,
        SemanticSegmentation,
        PoseEstimation,
        FaceDetection,
        FaceRecognition,
        OCR,
        NLP,
        AudioProcessing,
        SuperResolution,
        StyleTransfer,
        Other,
        LargeLanguageModel,
        VisionLanguageModel,
        OmniModel,
        AudioLanguageModel,
        EmbeddingModel,
        Reranker,
        OCRModel,
        CodeModel,
        SafetyModel,
        ImageGenerationModel,
        TextGeneration,
        SpeechSynthesis,
        SpeechRecognition,
        ImageMatting,
        ImageRestoration,
        Reconstruction3D,
        AvatarAnimation,
        FaceLandmarks,
        DocumentLayout,
        ImageEmbedding,
        VideoGeneration,
        ModelCollection,
        RuntimeResources,
        ModelDiagnostics,
        Translation,
        DepthEstimation,
        Robotics,
        AudioGeneration,
        SpeakerRecognition,
        DocumentRectification,
        KeywordSpotting,
        EchoCancellation
    }
}
