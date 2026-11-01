using System;

namespace MNN.Unity
{
    /// <summary>Four concurrent speaker-slot probabilities per 80 ms frame.</summary>
    public sealed class MNNSpeakerActivity
    {
        public const int Speakers = 4;
        public const double FrameSeconds = .08;
        public float[] Probabilities { get; }
        public int FrameCount => Probabilities.Length / Speakers;
        public double DurationSeconds { get; }

        internal MNNSpeakerActivity(float[] probabilities, double durationSeconds)
        {
            Probabilities = probabilities ?? throw new ArgumentNullException(nameof(probabilities));
            if (probabilities.Length == 0 || probabilities.Length % Speakers != 0 || durationSeconds <= 0)
                throw new ArgumentException("Expected nonempty four-speaker probabilities and positive audio duration.");
            DurationSeconds = durationSeconds;
        }
    }
}
