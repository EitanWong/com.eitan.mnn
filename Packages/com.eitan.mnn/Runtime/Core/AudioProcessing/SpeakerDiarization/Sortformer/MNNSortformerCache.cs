using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    // Original synchronous batch-one eval policy: fifo=0, chunk/update/cache=188.
    // State belongs to one recording; it never crosses Analyze calls.
    internal sealed class MNNSortformerCache
    {
        internal const int Dimensions = 512, Capacity = 188, Speakers = 4;
        internal float[] Embeddings { get; private set; } = Array.Empty<float>();
        internal float[] Predictions { get; private set; }
        internal float[] SilenceMean { get; } = new float[Dimensions];
        internal int SilenceCount { get; private set; }
        internal int Count => Embeddings.Length / Dimensions;

        internal void Update(float[] chunk, float[] allPredictions, int leftContext, int rightContext)
        {
            int chunkFrames = chunk.Length / Dimensions - leftContext - rightContext;
            var selected = new float[checked(chunkFrames * Dimensions)];
            Array.Copy(chunk, leftContext * Dimensions, selected, 0, selected.Length);
            var predictions = new float[checked(chunkFrames * Speakers)];
            Array.Copy(allPredictions, (Count + leftContext) * Speakers, predictions, 0, predictions.Length);
            UpdateSilence(selected, predictions);
            int oldCount = Count;
            Embeddings = Concat(Embeddings, selected);
            if (Predictions != null) Predictions = Concat(Predictions, predictions);
            if (Count <= Capacity) return;
            if (Predictions == null)
            {
                Predictions = new float[checked(Count * Speakers)];
                Array.Copy(allPredictions, 0, Predictions, 0, oldCount * Speakers);
                Array.Copy(predictions, 0, Predictions, oldCount * Speakers, predictions.Length);
            }
            Compress();
        }

        private void UpdateSilence(float[] embeddings, float[] predictions)
        {
            var sum = new float[Dimensions];
            int added = 0;
            for (int frame = 0; frame < predictions.Length / Speakers; ++frame)
            {
                int offset = frame * Speakers;
                float activity = predictions[offset] + predictions[offset + 1] + predictions[offset + 2] + predictions[offset + 3];
                if (activity >= .2f) continue;
                ++added;
                for (int d = 0; d < Dimensions; ++d) sum[d] += embeddings[frame * Dimensions + d];
            }
            if (added == 0) return;
            int total = SilenceCount + added;
            for (int d = 0; d < Dimensions; ++d) SilenceMean[d] = (SilenceMean[d] * SilenceCount + sum[d]) / total;
            SilenceCount = total;
        }

        private void Compress()
        {
            int frames = Count;
            var scores = new float[frames * Speakers];
            for (int frame = 0; frame < frames; ++frame) ScoreFrame(frame, scores);
            for (int speaker = 0; speaker < Speakers; ++speaker)
            {
                int positive = 0;
                for (int frame = 0; frame < frames; ++frame)
                {
                    int index = frame * Speakers + speaker;
                    if (Predictions[index] <= .5f) scores[index] = float.NegativeInfinity;
                    if (scores[index] > 0) ++positive;
                }
                if (positive >= 22)
                    for (int frame = 0; frame < frames; ++frame)
                    {
                        int index = frame * Speakers + speaker;
                        if (scores[index] <= 0) scores[index] = float.NegativeInfinity;
                    }
            }
            for (int frame = Capacity; frame < frames; ++frame)
                for (int speaker = 0; speaker < Speakers; ++speaker) scores[frame * Speakers + speaker] += .05f;
            Boost(scores, frames, 33, 2);
            Boost(scores, frames, 66, 1);
            Gather(scores, frames);
        }

        private void ScoreFrame(int frame, float[] scores)
        {
            var absent = new float[Speakers];
            for (int s = 0; s < Speakers; ++s) absent[s] = (float)Math.Log(Math.Max(1f - Predictions[frame * Speakers + s], .25f));
            float sum = absent[0] + absent[1] + absent[2] + absent[3];
            for (int s = 0; s < Speakers; ++s)
            {
                int index = frame * Speakers + s;
                float present = (float)Math.Log(Math.Max(Predictions[index], .25f));
                scores[index] = present - absent[s] + sum - (float)Math.Log(.5);
            }
        }

        private static void Boost(float[] scores, int frames, int count, int scale)
        {
            for (int speaker = 0; speaker < Speakers; ++speaker)
            {
                var indices = new int[frames];
                for (int i = 0; i < frames; ++i) indices[i] = i * Speakers + speaker;
                Array.Sort(indices, (a, b) => scores[b].CompareTo(scores[a]));
                float adjustment = scale * (float)Math.Log(.5);
                for (int i = 0; i < count; ++i) scores[indices[i]] -= adjustment;
            }
        }

        private void Gather(float[] scores, int frames)
        {
            int extended = frames + 3;
            var indices = new int[extended * Speakers];
            var values = new float[indices.Length];
            for (int s = 0; s < Speakers; ++s)
                for (int frame = 0; frame < extended; ++frame)
                {
                    int index = s * extended + frame;
                    indices[index] = index;
                    values[index] = frame < frames ? scores[frame * Speakers + s] : float.PositiveInfinity;
                }
            Array.Sort(indices, (a, b) => values[b].CompareTo(values[a]));
            var chosen = new int[Capacity];
            for (int i = 0; i < Capacity; ++i) chosen[i] = float.IsNegativeInfinity(values[indices[i]]) ? 99999 : indices[i];
            Array.Sort(chosen);
            var embeddings = new float[Capacity * Dimensions];
            var predictions = new float[Capacity * Speakers];
            for (int i = 0; i < Capacity; ++i)
            {
                int frame = chosen[i] % extended;
                bool disabled = chosen[i] == 99999 || frame >= frames;
                Array.Copy(disabled ? SilenceMean : Embeddings, disabled ? 0 : frame * Dimensions, embeddings, i * Dimensions, Dimensions);
                if (!disabled) Array.Copy(Predictions, frame * Speakers, predictions, i * Speakers, Speakers);
            }
            Embeddings = embeddings;
            Predictions = predictions;
        }

        internal static float[] Concat(float[] first, float[] second)
        {
            var result = new float[checked(first.Length + second.Length)];
            Array.Copy(first, result, first.Length);
            Array.Copy(second, 0, result, first.Length, second.Length);
            return result;
        }
    }
}
