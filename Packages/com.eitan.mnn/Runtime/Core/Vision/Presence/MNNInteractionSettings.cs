using System;

namespace MNN.Unity
{
    public enum MNNPresenceSleepDisplay { Hidden, Dimmed, BlackScreen }

    /// <summary>Independent opt-in capabilities. Enabling a capability requires a ready provider;
    /// defaults never infer camera consent, identity or a lowest-error model without measurement.</summary>
    [Serializable]
    public sealed class MNNInteractionSettings
    {
        public bool visualPresence;
        public bool faceRecognition;
        public bool imageContext;
        public bool audioVisualSpeech;
        public bool referenceSpeaker;
        public bool separateSpeakers;
        public bool requireFace = true;
        public float confirmationSeconds = .35f;
        public float graceSeconds = 3;
        public float sleepSeconds = 15;
        public MNNPresenceSleepDisplay sleepDisplay;
        public string[] allowedIdentities = Array.Empty<string>();
        public string detectorDirectory = "budaoshou--InsightFace-Det500M-MNN";
        public string recognizerDirectory = "budaoshou--InsightFace-MobileFaceNet-MNN";
        public string measuredConfigurationId;

        public MNNInteractionSettings Snapshot()
        {
            var copy = (MNNInteractionSettings)MemberwiseClone();
            copy.allowedIdentities = allowedIdentities == null ? Array.Empty<string>() : (string[])allowedIdentities.Clone();
            return copy;
        }

        public void Validate()
        {
            ToPresenceOptions();
            if (!Enum.IsDefined(typeof(MNNPresenceSleepDisplay), sleepDisplay)) throw new ArgumentException("Select a sleep display mode.");
            if (faceRecognition && (!visualPresence || allowedIdentities == null || allowedIdentities.Length == 0))
                throw new ArgumentException("Identity gating requires visual presence and explicitly enrolled allowed identities.");
            if (audioVisualSpeech && !visualPresence) throw new ArgumentException("Visual speech extraction requires target presence.");
            if (imageContext && !visualPresence) throw new ArgumentException("Image context requires target presence.");
        }

        public MNNPresenceOptions ToPresenceOptions() => new MNNPresenceOptions(confirmationSeconds,
            graceSeconds: graceSeconds, sleepSeconds: sleepSeconds, requireFace: requireFace);
    }
}
