// Adapted from Nian27/CosyVoice3-MNN (Copyright 2026 Nian27), Apache-2.0.
// Modified for managed execution, validation and cancellation. See LICENSE.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Interop;
using MNN.Unity.Interop.Handles;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    public sealed partial class MNNCosyVoice
    {
        private int[] GenerateTokens(string text, int seed, CancellationToken token)
        {
            text = NormalizeEnding(text);
            // The default release's 87-token reference must match this transcript.
            string prompt = "<|sos|>You are a helpful assistant.<|endofprompt|>希望你以后能够做的比我还好呀。" + text + "<|task_id|>";
            int[] ids = MNNInterop.CosyVoiceEncode(_llm, prompt).Concat(_prompt.Select(id => id + MNNCosyVoiceSampler.Offset)).ToArray();
            var output = new List<int>();
            var random = new Random(seed);
            var logits = new float[MNNCosyVoiceSampler.CompactSize];
            var sampler = new MNNCosyVoiceSampler.Workspace();
            var decode = new int[1];
            // The model's minimum speech/text ratio is measured in tokenizer
            // tokens, not characters. Character counts force long English
            // requests past their natural EOS into repeated/hallucinated tails.
            int minimum = Math.Max(2, MNNInterop.CosyVoiceEncode(_llm, text).Length * 2);
            int maximum = Math.Min(1000, Math.Max(300, minimum * 5));
            MNNInterop.CosyVoiceReset(_llm);
            try
            {
                for (int step = 0; step <= maximum; ++step)
                {
                    token.ThrowIfCancellationRequested();
                    MNNInterop.CosyVoiceForward(_llm, ids, step == 0, logits);
                    int selected = MNNCosyVoiceSampler.Sample(logits, output, minimum, random, sampler);
                    if (selected == MNNCosyVoiceSampler.Eos)
                    {
                        if (output.Count == 0)
                            throw new InvalidDataException("CosyVoice generated no speech tokens.");
                        return output.Select(id => id - MNNCosyVoiceSampler.Offset).ToArray();
                    }

                    if (step == maximum)
                        throw new InvalidDataException("CosyVoice reached its speech token limit. Use a shorter text.");
                    output.Add(selected);
                    decode[0] = selected;
                    ids = decode;
                }

                throw new InvalidDataException("CosyVoice generation did not finish.");
            }
            finally
            {
                MNNInterop.CosyVoiceReset(_llm);
            }
        }
    }
}
