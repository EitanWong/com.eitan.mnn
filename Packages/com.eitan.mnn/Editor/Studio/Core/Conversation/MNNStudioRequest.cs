using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using UnityEditor;

namespace MNN.Unity.Editor
{
    internal sealed class MNNStudioRequest
    {
        internal string Prompt, Image, Audio, Document, RecognitionLanguage;
        internal string PlaybackReference;
        internal string TurnLanguage = "en", TurnSpeaker = "user";
        internal string[] TurnQuestions = new[] { "eot" };
        internal string SpeakerReference;
        internal string GestureFeatures;
        internal float[] VisualFrames;
        internal string Keywords = MNNKwsModel.DefaultKeywords;
        internal float KeywordThreshold = MNNKeywordSpotter.DefaultThreshold, KeywordScore = 1;
        internal MNNChatMessage[] Conversation;
        internal int TokenLimit = 256;
        internal bool Speech;
        internal string Voice = "M1", NegativePrompt = "", ReferenceText = "", SpeechLanguage = "zh";
        internal int Steps = 20, Seed = 42, EmotionId;
        internal float Speed = 1.05f, Guidance = 7.5f;
        internal byte[] ReferenceRgb;
        internal MNNRgbImage VisionImage;
        internal float LayoutScoreThreshold = .5f;
        internal MNNDecisionQuestion DecisionQuestion;
        internal bool OcrCroppedLine;
        internal string ContextSummary;
        internal MNNStudioMessage[] History;
        internal int SummarizedMessages, ContextTokens = MNNStudioContext.DefaultTokenBudget;
        internal Action<string, int> MemoryProgress;
        internal Action<bool> ContextProgress;
        internal Action<MNNGenerationUpdate> Progress;
        internal CancellationToken Cancellation;

        internal MNNStudioRequest Snapshot()
        {
            var copy = (MNNStudioRequest)MemberwiseClone();
            copy.ReferenceRgb = ReferenceRgb == null ? null : (byte[])ReferenceRgb.Clone();
            copy.VisualFrames = VisualFrames == null ? null : (float[])VisualFrames.Clone();
            copy.TurnQuestions = TurnQuestions == null ? null : (string[])TurnQuestions.Clone();
            copy.History = History == null ? null : (MNNStudioMessage[])History.Clone();
            copy.Conversation = Conversation == null ? null : (MNNChatMessage[])Conversation.Clone();
            return copy;
        }
    }
}
