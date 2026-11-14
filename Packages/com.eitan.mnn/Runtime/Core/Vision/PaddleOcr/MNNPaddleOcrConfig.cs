using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>DB text detection and CTC page recognition using an explicitly matched PP-OCR profile.</summary>
    [Serializable]
    public sealed class MNNPaddleOcrConfig
    {
        public const string FileName = "mnn_paddle_ocr.json";
        public string graph = "PP-OCRv5_mobile_det.mnn";
        public string input = "x", output = "fetch_name_0";
        public int maximumSide = 960, maximumCandidates = 1000;
        public float pixelThreshold = .3f, boxThreshold = .6f, unclipRatio = 1.5f, recognitionThreshold = .5f;
        // This pinned detector's Metal outputs fail the rotated-page quality gate on the supported runtime.
        // Rectified crops also fail exact text gates on Metal, including High precision. Auto selects the
        // verified CPU page pipeline. Independent upright-line profiles retain their own backend policy.
        public MNNBackendType preferredDetectionBackend = MNNBackendType.CPU;
        public MNNBackendType preferredRecognitionBackend = MNNBackendType.CPU;

        public static MNNPaddleOcrConfig Read(string directory) =>
            JsonUtility.FromJson<MNNPaddleOcrConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid Paddle page OCR profile.");

        public static MNNPaddleOcrConfig ReadForProfile(string directory, string profile)
        {
            string recognizerDirectory = RecognizerDirectory(directory, profile);
            string name = MNNPaddleTextConfig.Read(recognizerDirectory).pageConfig;
            return JsonUtility.FromJson<MNNPaddleOcrConfig>(File.ReadAllText(MNNPaddleTextConfig.ResolveFile(directory, name)))
                ?? throw new InvalidDataException("Invalid Paddle page OCR variant.");
        }

        internal MNNPaddleOcrConfig Clone() => (MNNPaddleOcrConfig)MemberwiseClone();

        /// <summary>Validate the declared OCR profile before loading native resources.</summary>
        public void Validate(string directory)
        {
            if (maximumSide < 32 || maximumSide > 4096 || maximumSide % 32 != 0 ||
                maximumCandidates < 1 || maximumCandidates > 10000 || !Probability(pixelThreshold) ||
                !Probability(boxThreshold) || !Probability(recognitionThreshold) ||
                float.IsNaN(unclipRatio) || unclipRatio < .1f || unclipRatio > 5 ||
                !Enum.IsDefined(typeof(MNNBackendType), preferredDetectionBackend) ||
                !Enum.IsDefined(typeof(MNNBackendType), preferredRecognitionBackend) ||
                string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
                throw new InvalidDataException("Invalid Paddle DB dimensions, candidate limit, thresholds or tensor names.");
            MNNPaddleTextConfig.ResolveFile(directory, graph);
        }

        private static bool Probability(float value) => !float.IsNaN(value) && value >= 0 && value <= 1;

        public static string RecognizerDirectory(string directory, string profile)
        {
            string metadata = MNNPaddleTextConfig.ResolveFile(directory, Path.Combine(profile ?? ".", MNNPaddleTextConfig.FileName));
            string resolved = Path.GetDirectoryName(metadata);
            MNNPaddleTextConfig.Read(resolved).Validate(resolved);
            return resolved;
        }
    }
}
