using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    public sealed partial class MNNHojoTts
    {
        private static readonly string[] LmOutputs = { "logits", "last_hidden_state", "cache_updates" };
        private readonly struct CoarseSpeech
        {
            internal readonly int[] Tokens;
            internal readonly float[] Hidden;
            internal CoarseSpeech(List<int> tokens, List<float> hidden)
            { Tokens = tokens.ToArray(); Hidden = hidden.ToArray(); }
        }

        private MNNGeneratedAudio Generate(string text, int speaker, int seed, float temperature, int limit, CancellationToken token)
        {
            int[] ids = EncodePrompt(text);
            var coarse = GenerateCoarse(ids, speaker, seed, temperature, limit, token);
            return DecodeSpeech(coarse, speaker, text, token);
        }

        private int[] EncodePrompt(string text)
        {
            string prompt = "[target_text_start]" + text + "[target_text_end][spk_start]" +
                string.Concat(Enumerable.Range(0, 16).Select(i => "[spk_emb_" + i + "]")) + "[spk_end][target_speech_start]";
            int[] ids = _tokenizer.Encode(prompt);
            if (ids.Length >= 4096) throw new ArgumentException("Hojo prompt exhausts the 4096-token context.");
            return ids;
        }

        private float[] SpeakerEmbeddings(int[] ids, int speaker)
        {
            var embeds = Embed(ids);
            int slot = Array.IndexOf(ids, _tokenizer.Id("[spk_start]")) + 1;
            if (slot == 0 || slot + 16 > ids.Length) throw new InvalidDataException("Missing Hojo speaker slots.");
            Array.Copy(_speakers, speaker * 16 * 512, embeds, slot * 512, 16 * 512);
            return embeds;
        }

        private MNNGenerationGraph.Output[] Prefill(int[] ids, int speaker)
        {
            var mask = new float[checked(ids.Length * ids.Length)];
            for (int row = 0; row < ids.Length; ++row)
                for (int col = row + 1; col < ids.Length; ++col) mask[row * ids.Length + col] = -1e9f;
            return _prefill.RunOutputs(LmOutputs, new Input("inputs_embeds", SpeakerEmbeddings(ids, speaker), 1, ids.Length, 512),
                new Input("position_ids", Enumerable.Range(0, ids.Length).ToArray(), 1, ids.Length),
                new Input("attention_mask", mask, 1, 1, ids.Length, ids.Length));
        }

        private CoarseSpeech GenerateCoarse(int[] ids, int speaker, int seed, float temperature, int limit, CancellationToken token)
        {
            limit = Math.Min(limit, 4096 - ids.Length);
            var state = Prefill(ids, speaker);
            int length = ids.Length, capacity = 256;
            while (capacity < length) capacity *= 2;
            var cache = new float[20 * capacity * 128];
            CopyCache(state[2].Values, length, cache, capacity, 0);
            var coarse = new List<int>(); var hidden = new List<float>();
            var generated = new HashSet<int>(); var sampler = new MNNHojoSampler(state[0].Values.Length, seed);
            int end = _tokenizer.Id("[target_speech_end]"); bool ended = false;
            for (int step = 0; step < limit; ++step)
            {
                token.ThrowIfCancellationRequested();
                int next = sampler.Sample(state[0].Values, temperature, generated);
                generated.Add(next);
                if (next == end && step + 1 >= 10) { ended = true; break; }
                if (_audioIds.Contains(next)) { coarse.Add(next); hidden.AddRange(state[1].Values); }
                if (step + 1 == limit) break;
                if (length == capacity)
                {
                    int grown = Math.Min(4096, capacity * 2); var expanded = new float[20 * grown * 128];
                    CopyCache(cache, capacity, expanded, grown, 0); cache = expanded; capacity = grown;
                }
                var attention = new float[capacity + 1];
                for (int i = length; i < capacity; ++i) attention[i] = -1e9f;
                state = _decode.RunOutputs(LmOutputs, new Input("inputs_embeds", Embed(new[] { next }), 1, 1, 512),
                    new Input("position_ids", new[] { length }, 1, 1), new Input("cache", cache, 20, 1, capacity, 128),
                    new Input("attention_mask", attention, 1, 1, 1, capacity + 1));
                CopyCache(state[2].Values, 1, cache, capacity, length++);
            }
            if (!ended) throw new InvalidDataException("Hojo reached its token budget before the speech end marker; split the text.");
            if (coarse.Count == 0) throw new InvalidDataException("Hojo emitted no audio tokens.");
            return new CoarseSpeech(coarse, hidden);
        }

        private MNNGeneratedAudio DecodeSpeech(CoarseSpeech coarse, int speaker, string text, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int frames = coarse.Tokens.Length;
            var vector = new float[192]; Array.Copy(_vectors, speaker * 192, vector, 0, 192);
            float[] fine = _fine.Run("binary_logits", new Input("hidden_states", coarse.Hidden, 1, frames, 512),
                new Input("coarse_embeddings", Embed(coarse.Tokens), 1, frames, 512),
                new Input("speaker_embedding", vector, 1, 192), new Input("valid_mask", Enumerable.Repeat(1, frames).ToArray(), 1, frames));
            for (int i = 0; i < fine.Length; ++i) fine[i] = fine[i] > 0 ? 1 : -1;
            var spectrum = _decoder.RunOutputs(new[] { "mag", "phase" }, new Input("bits", fine, frames, 128));
            return new MNNGeneratedAudio(InvertSpectrum(spectrum[0], spectrum[1], token), SampleRate, text);
        }

        private static void CopyCache(float[] source, int sourceFrames, float[] target, int capacity, int offset)
        {
            if (source.Length != 20 * sourceFrames * 128 || offset + sourceFrames > capacity) throw new InvalidDataException("Hojo KV shape mismatch.");
            for (int i = 0; i < 20; ++i) Array.Copy(source, i * sourceFrames * 128, target, (i * capacity + offset) * 128, sourceFrames * 128);
        }
    }
}
