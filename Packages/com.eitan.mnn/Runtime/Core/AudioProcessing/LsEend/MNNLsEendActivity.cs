using System;
using System.Globalization;
using System.Text;

namespace MNN.Unity
{
    /// <summary>Nine original post-dummy posterior columns per 100 ms frame. Speaker slots are local to a recording.</summary>
    public sealed class MNNLsEendActivity
    {
        public const int Columns = 9;
        public const double FrameSeconds = .1;
        public float[] Probabilities { get; }
        public int FrameCount => Probabilities.Length / Columns;
        public double DurationSeconds { get; }

        internal MNNLsEendActivity(float[] probabilities, double durationSeconds)
        {
            if (probabilities == null || probabilities.Length == 0 || probabilities.Length % Columns != 0 || durationSeconds <= 0)
                throw new ArgumentException("Expected nonempty LS-EEND posteriors and a positive duration.");
            for (int i = 0; i < probabilities.Length; ++i)
                if (!(probabilities[i] >= 0 && probabilities[i] <= 1)) throw new ArgumentException("Invalid posterior probability.");
            Probabilities = probabilities; DurationSeconds = durationSeconds;
        }

        /// <summary>Original strict threshold 0.5, zero-padded 11-frame median and 100 ms RTTM grid.</summary>
        public string ToRttm(string recordingId)
        {
            if (string.IsNullOrWhiteSpace(recordingId)) throw new ArgumentException("Supply an RTTM recording ID.", nameof(recordingId));
            foreach (char character in recordingId)
                if (char.IsWhiteSpace(character) || char.IsControl(character)) throw new ArgumentException("RTTM IDs cannot contain whitespace.", nameof(recordingId));
            var text = new StringBuilder();
            for (int column = 0; column < Columns; ++column)
            {
                int start = -1;
                for (int t = 0; t <= FrameCount; ++t)
                {
                    int active = 0;
                    if (t < FrameCount)
                        for (int k = Math.Max(0, t - 5); k < Math.Min(FrameCount, t + 6); ++k)
                            if (Probabilities[k * Columns + column] > .5f) ++active;
                    bool speaking = active >= 6;
                    if (speaking && start < 0) start = t;
                    if (!speaking && start >= 0)
                    {
                        text.AppendFormat(CultureInfo.InvariantCulture, "SPEAKER {0} 1 {1,7:F2} {2,7:F2} <NA> <NA> {0}_{3} <NA>\n",
                            recordingId, start * FrameSeconds, (t - start) * FrameSeconds, column);
                        start = -1;
                    }
                }
            }
            return text.ToString();
        }
    }
}
