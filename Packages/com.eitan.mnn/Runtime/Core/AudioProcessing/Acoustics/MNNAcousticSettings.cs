using System;

namespace MNN.Unity
{
    public enum MNNAcousticProfile { Realtime, AutomaticSpeaker }

    /// <summary>Independent of the microphone transport and ASR provider. Models must already be installed.</summary>
    [Serializable]
    public sealed class MNNAcousticSettings
    {
        public bool enabled;
        public MNNAcousticProfile profile;
        public bool denoise;
        public bool enhanceVoiceActivity = true;
        public bool enhanceKeywords;
        public bool enhanceRecognition;
        public bool wakeWord;
        public int maximumCaptureAgeMilliseconds = 250;
        public string keywords = MNN.Unity.MNNKeywordSpotterConfig.EnglishDefaultWords;
        public string[] WakeWords
        {
            get => MNN.Unity.MNNKeywordText.GetDisplayText(keywords).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            set => keywords = MNNWakeWordSettings.MergeDisplayWords(keywords, value);
        }
        public MNN.Unity.MNNKeywordLanguage keywordLanguage;
        public float keywordThreshold = MNN.Unity.MNNKeywordSpotter.DefaultThreshold;
        public float speakerMinimum = .35f;
        public float speakerMargin = .06f;
        public string denoiserDirectory = "DTLN-MNN";
        public string separationDirectory = "MossFormer2-SS-16K-MNN";
        public string speakerDirectory = "ECAPA-TDNN-MNN";
        public string vadDirectory = "Silero-v5-MNN"; // Legacy serialized setting; no post-capture VAD.
        public string keywordDirectory = "Jeryuk--kws-zipformer-wenetspeech-3.3M-MNN";
        public string englishKeywordDirectory = MNN.Unity.MNNKeywordSpotterConfig.EnglishDirectoryName;
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(MNNAcousticProfile), profile) ||
                !Unit(keywordThreshold) || keywordThreshold == 0 || !Unit(speakerMinimum) || !Unit(speakerMargin) ||
                maximumCaptureAgeMilliseconds < 64 || maximumCaptureAgeMilliseconds > 1000)
                throw new ArgumentException("Choose finite acoustic thresholds and bounded wake words.");
            if (!enabled) return;
            if (wakeWord && (string.IsNullOrWhiteSpace(keywords) || keywords.Length > 4096))
                throw new ArgumentException("Enter wake words before enabling wake detection (up to 4096 characters).");
            var wakeGroups = wakeWord ? MNNWakeWordSettings.Group(this) : null;
            if (denoise) RequireDirectory(denoiserDirectory, "noise reduction");
            if (wakeGroups != null)
                foreach (var group in wakeGroups) RequireDirectory(MNNWakeWordSettings.ModelDirectory(this, group.Key), "wake detection");
            if (profile == MNNAcousticProfile.AutomaticSpeaker)
            {
                RequireDirectory(separationDirectory, "separation");
                RequireDirectory(speakerDirectory, "speaker embedding");
            }
        }
        private static void RequireDirectory(string value, string task)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.IndexOf('\0') >= 0)
                throw new ArgumentException("Choose an installed model directory for " + task + ".");
        }
        private static bool Unit(float value) => !float.IsNaN(value) && value >= 0 && value <= 1;
        public MNNAcousticSettings Snapshot() => (MNNAcousticSettings)MemberwiseClone();
        internal bool SamePipeline(MNNAcousticSettings other) => other != null && enabled == other.enabled &&
            profile == other.profile && denoise == other.denoise && enhanceVoiceActivity == other.enhanceVoiceActivity &&
            enhanceKeywords == other.enhanceKeywords && enhanceRecognition == other.enhanceRecognition &&
            maximumCaptureAgeMilliseconds == other.maximumCaptureAgeMilliseconds && speakerMinimum == other.speakerMinimum &&
            speakerMargin == other.speakerMargin && denoiserDirectory == other.denoiserDirectory &&
            separationDirectory == other.separationDirectory && speakerDirectory == other.speakerDirectory &&
            vadDirectory == other.vadDirectory && MNNWakeWordSettings.SameWake(this, other);
    }
}
