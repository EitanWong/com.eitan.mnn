using System;
using System.Threading;

namespace MNN.Unity
{
    internal static partial class MNNSpeechDsp
    {
        // Detect only boundary silence. Keep 20 ms of onset and 80 ms of release;
        // speech and pauses between words remain untouched, including soft consonants.
        internal static float[] TrimSpeechEdges(float[] audio, int rate, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int window = Math.Max(1, rate / 100), first = -1, last = 0;
            for (int start = 0; start < audio.Length; start += window)
            {
                token.ThrowIfCancellationRequested();
                int end = Math.Min(audio.Length, start + window);
                if (!IsSpeechWindow(audio, start, end)) continue;
                if (first < 0) first = start;
                last = end;
            }
            if (first < 0) return audio;
            first = Math.Max(0, first - rate / 50);
            last = Math.Min(audio.Length, last + rate * 80 / 1000);
            if (first == 0 && last == audio.Length) return audio;
            var result = new float[last - first];
            Array.Copy(audio, first, result, 0, result.Length);
            return result;
        }

        private static bool IsSpeechWindow(float[] audio, int first, int last)
        {
            double energy = 0;
            float peak = 0;
            for (int i = first; i < last; ++i)
            {
                energy += (double)audio[i] * audio[i];
                peak = Math.Max(peak, Math.Abs(audio[i]));
            }
            return peak > .003162278f || energy > (last - first) * 1e-6;
        }

        // Waveform similarity overlap-add (WSOLA). Analysis advances at the requested
        // tempo while synthesis uses the original samples, preserving pitch and 24 kHz.
        internal static float[] ChangeTempo(float[] audio, int rate, float speed, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed < .25f || speed > 4)
                throw new ArgumentOutOfRangeException(nameof(speed));
            if (speed == 1 || audio.Length < 2) return audio;
            int length = Math.Max(1, checked((int)Math.Round(audio.Length / (double)speed)));
            int window = Math.Max(1, Math.Min(Math.Min(audio.Length, length) / 2, rate * 40 / 1000));
            int hop = Math.Max(1, window / 2), search = Math.Max(1, rate / 100);
            var result = new float[length];
            Array.Copy(audio, result, window);
            int previous = 0;
            while (previous + window < length)
            {
                token.ThrowIfCancellationRequested();
                int position = Math.Min(previous + hop, length - window);
                int overlap = previous + window - position;
                int expected = (int)Math.Round(position * (double)(audio.Length - window) / (length - window));
                int source = position + window == length ? audio.Length - window
                    : FindSpeechAlignment(audio, result, position, overlap, expected, search, window);
                OverlapSpeech(audio, source, result, position, overlap, window);
                previous = position;
            }
            return result;
        }

        private static int FindSpeechAlignment(float[] audio, float[] result, int position, int overlap,
            int expected, int search, int window)
        {
            int best = expected;
            double score = SpeechCorrelation(audio, expected, result, position, overlap);
            int first = Math.Max(0, expected - search), last = Math.Min(audio.Length - window, expected + search);
            for (int candidate = first; candidate <= last; ++candidate)
            {
                double next = SpeechCorrelation(audio, candidate, result, position, overlap);
                if (next > score + 1e-9)
                {
                    best = candidate;
                    score = next;
                }
            }
            return best;
        }

        private static double SpeechCorrelation(float[] audio, int source, float[] result, int target, int count)
        {
            double dot = 0, a = 0, b = 0;
            for (int i = 0; i < count; i += 4)
            {
                double x = audio[source + i], y = result[target + i];
                dot += x * y;
                a += x * x;
                b += y * y;
            }
            return a * b > 1e-20 ? dot / Math.Sqrt(a * b) : 0;
        }

        private static void OverlapSpeech(float[] audio, int source, float[] result, int target, int overlap, int window)
        {
            for (int i = 0; i < overlap; ++i)
            {
                float mix = (i + 1f) / (overlap + 1);
                result[target + i] = result[target + i] * (1 - mix) + audio[source + i] * mix;
            }
            Array.Copy(audio, source + overlap, result, target + overlap, window - overlap);
        }
    }
}
