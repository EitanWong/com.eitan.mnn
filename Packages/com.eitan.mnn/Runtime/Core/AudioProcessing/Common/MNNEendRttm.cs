using System;
using System.Globalization;
using System.Text;

namespace MNN.Unity
{
    internal static class MNNEendRttm
    {
        internal static string Format(float[] probabilities, int columns, string recordingId)
        {
            if (string.IsNullOrWhiteSpace(recordingId)) throw new ArgumentException("Supply an RTTM recording ID.", nameof(recordingId));
            foreach (char character in recordingId)
                if (char.IsWhiteSpace(character) || char.IsControl(character)) throw new ArgumentException("RTTM IDs cannot contain whitespace.", nameof(recordingId));
            int frames = probabilities.Length / columns;
            var text = new StringBuilder();
            for (int column = 0; column < columns; ++column)
            {
                int start = -1;
                for (int t = 0; t <= frames; ++t)
                {
                    int active = 0;
                    if (t < frames)
                        for (int k = Math.Max(0, t - 5); k < Math.Min(frames, t + 6); ++k)
                            if (probabilities[k * columns + column] > .5f) ++active;
                    bool speaking = active >= 6;
                    if (speaking && start < 0) start = t;
                    if (!speaking && start >= 0)
                    {
                        text.AppendFormat(CultureInfo.InvariantCulture, "SPEAKER {0} 1 {1,7:F2} {2,7:F2} <NA> <NA> {0}_{3} <NA>\n",
                            recordingId, start * .1, (t - start) * .1, column);
                        start = -1;
                    }
                }
            }
            return text.ToString();
        }
    }
}
