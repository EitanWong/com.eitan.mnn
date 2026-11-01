using System;

namespace MNN.Unity
{
    /// <summary>Five original post-dummy posterior columns per 100 ms frame. Speaker slots are local to a recording.</summary>
    public sealed class MNNFsEendActivity
    {
        public const int Columns = 5;
        public const double FrameSeconds = .1;
        public float[] Probabilities { get; }
        public int FrameCount => Probabilities.Length / Columns;
        public double DurationSeconds { get; }

        internal MNNFsEendActivity(float[] probabilities, double durationSeconds)
        {
            if (probabilities == null || probabilities.Length == 0 || probabilities.Length % Columns != 0 || durationSeconds <= 0)
                throw new ArgumentException("Expected nonempty FS-EEND posteriors and a positive duration.");
            for (int i = 0; i < probabilities.Length; ++i)
                if (!(probabilities[i] >= 0 && probabilities[i] <= 1)) throw new ArgumentException("Invalid posterior probability.");
            Probabilities = probabilities; DurationSeconds = durationSeconds;
        }

        /// <summary>Original strict threshold 0.5, zero-padded 11-frame median and 100 ms RTTM grid.</summary>
        public string ToRttm(string recordingId) => MNNEendRttm.Format(Probabilities, Columns, recordingId);
    }
}
