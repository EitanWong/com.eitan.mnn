using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNEasyTurn
    {
        private const int CacheLength = 512, Layers = 24, KvHeads = 2, HeadSize = 64;
        private int[] Generate(float[] embeddings, CancellationToken token)
        {
            var key = new float[Layers * KvHeads * CacheLength * HeadSize]; var value = new float[key.Length];
            var generated = new List<int>(); int position = 0;
            for (int step = 0; step < 100; ++step)
            {
                token.ThrowIfCancellationRequested();
                int count = embeddings.Length / 896;
                if (position + count > CacheLength) throw new InvalidDataException("Easy-Turn context budget exceeded.");
                Rotary(position, count, out var cos, out var sin);
                var output = _decoder.RunOutputs(new[] { "logits", "new_key", "new_value" },
                    new MNNGenerationGraph.Input("embeddings", embeddings, 1, count, 896),
                    new MNNGenerationGraph.Input("past_key", key, Layers, 1, KvHeads, CacheLength, HeadSize),
                    new MNNGenerationGraph.Input("past_value", value, Layers, 1, KvHeads, CacheLength, HeadSize),
                    new MNNGenerationGraph.Input("mask", Mask(position, count), 1, 1, count, CacheLength + count),
                    new MNNGenerationGraph.Input("cos", cos, 1, 1, count, HeadSize),
                    new MNNGenerationGraph.Input("sin", sin, 1, 1, count, HeadSize),
                    new MNNGenerationGraph.Input("last_token_index", new[] { count - 1 }, 1));
                AppendCache(key, output[1].Values, position, count); AppendCache(value, output[2].Values, position, count);
                position += count;
                int next = Select(output[0].Values, generated); generated.Add(next);
                if (next == 151643) return generated.ToArray();
                embeddings = Embed(new[] { next });
            }
            throw new InvalidDataException("Easy-Turn reached its token limit without EOS; no complete turn is returned.");
        }
        private static int Select(float[] logits, List<int> generated)
        {
            if (logits.Length != 151936) throw new InvalidDataException("Invalid Easy-Turn vocabulary head.");
            foreach (int id in new HashSet<int>(generated)) logits[id] = logits[id] < 0 ? logits[id] * 1.05f : logits[id] / 1.05f;
            int best = 0;
            for (int i = 0; i < logits.Length; ++i)
            {
                if (float.IsNaN(logits[i]) || float.IsInfinity(logits[i])) throw new InvalidDataException("Invalid Easy-Turn logits.");
                if (logits[i] > logits[best]) best = i;
            }
            return best;
        }
        private static float[] Mask(int position, int count)
        {
            int width = CacheLength + count; var mask = new float[count * width];
            for (int q = 0; q < count; ++q)
                for (int k = 0; k < width; ++k)
                    mask[q * width + k] = k < position || k >= CacheLength && k - CacheLength <= q ? 0 : -10000;
            return mask;
        }
        private static void Rotary(int position, int count, out float[] cos, out float[] sin)
        {
            cos = new float[count * HeadSize]; sin = new float[cos.Length];
            for (int t = 0; t < count; ++t)
                for (int k = 0; k < HeadSize; ++k)
                {
                    double phase = (position + t) / Math.Pow(1000000, (k % 32) * 2.0 / 64);
                    cos[t * HeadSize + k] = (float)Math.Cos(phase); sin[t * HeadSize + k] = (float)Math.Sin(phase);
                }
        }
        private static void AppendCache(float[] cache, float[] current, int position, int count)
        {
            if (current.Length != Layers * KvHeads * count * HeadSize) throw new InvalidDataException("Invalid Easy-Turn KV cache.");
            for (int i = 0; i < Layers * KvHeads; ++i)
                Array.Copy(current, i * count * HeadSize, cache, (i * CacheLength + position) * HeadSize, count * HeadSize);
        }
    }
}
