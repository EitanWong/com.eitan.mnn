using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using UnityEditor;

namespace MNN.Unity.Editor
{
    internal sealed class MNNStudioResult
    {
        internal string Text;
        internal int Tokens, SampleRate;
        internal bool Limited;
        internal bool RepetitionStopped;
        internal bool Cancelled;
        internal double Seconds;
        internal double AudioSeconds;
        internal string BackendDescription;
        // Missing/partial audio and historical results without duration have no RTF.
        internal double? RealTimeFactor => !Cancelled && AudioSeconds > 0 && Seconds > 0 && !double.IsInfinity(AudioSeconds) && !double.IsInfinity(Seconds) ? Seconds / AudioSeconds : (double? )null;
        internal float[] Waveform = Array.Empty<float>(), Vector;
        internal float? Score;
        internal MNNGeneratedImage GeneratedImage;
        internal MNNDetection[] Detections;
        internal MNNImageTaskResult ImageTask;
        internal MNNDocumentLayoutResult DocumentLayout;
        internal MNNDecisionResult Decision;
        internal MNNTextRecognition RecognizedText;
        internal MNNOcrResult Ocr;
        internal MNNVoiceActivityResult VoiceActivity;
        internal MNNTurnDetectionResult TurnDetection;
        internal IReadOnlyList<MNNDualTurnEndpointingResult> DualTurnFrames;
        internal MNNVoxTurnResult TurnDecisions;
        internal MNNKeywordDetection[] KeywordDetections;
        internal string ContextSummary;
    }
}
