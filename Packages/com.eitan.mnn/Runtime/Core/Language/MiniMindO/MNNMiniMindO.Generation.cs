using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    public sealed partial class MNNMiniMindO
    {
        private static readonly string[] Outputs = { "text_logits", "audio_logits", "present" };

        private MNNMiniMindOResult GenerateCore(MNNMiniMindORequest request, MNNMiniMindOOptions options,
            Action<MNNMiniMindOUpdate> callback, CancellationToken cancellation)
        {
            var inputs = PrepareInputs(request, options.MaxNewTokens, cancellation);
            MNNGenerationGraph.Output[] output;
            // Retire the prefill session before allocating the recurrent decoder.
            using (var graph = Graph(inputs.Length == 2 ? "prefill" : "multimodal_prefill"))
                output = graph.RunOutputs(Outputs, inputs);
            var random = new Random(options.Seed);
            var text = new List<int>();
            var frames = new List<int[]>();
            var waveform = new List<float>();
            var codes = Enumerable.Range(0, 8).Select(_ => new List<int>()).ToArray();
            var stop = Enumerable.Repeat(-1, 8).ToArray();
            bool textFinished = false, firstFinished = true, complete = false;
            int steps = 0;
            for (int step = 0; step < options.MaxNewTokens; ++step)
            {
                cancellation.ThrowIfCancellationRequested();
                int token = MNNMiniMindOSampling.Sample(output[0].Values, 0, 6400, options.Temperature, options.TopP, 0, random);
                if (textFinished) { token = firstFinished ? 201 : 0; firstFinished = false; }
                var nextAudio = new int[8];
                int audioStep = step - 1;
                for (int i = 0; i < 8; ++i)
                {
                    int code = 2049;
                    if (options.GenerateSpeech && audioStep >= i)
                    {
                        code = MNNMiniMindOSampling.Sample(output[1].Values, i * 2112, 2112, 0.2f, 1, 50, random,
                            codes[i].Skip(Math.Max(0, codes[i].Count - 3)));
                        if (stop[i] < 0 && code >= 2048) stop[i] = step;
                    }
                    codes[i].Add(code);
                    nextAudio[i] = audioStep >= i ? code : 2049;
                }
                if (textFinished && (!options.GenerateSpeech || stop.All(s => s >= 0))) { complete = true; break; }
                int[] frame = AlignedFrame(codes, stop, step);
                if (frame != null && options.GenerateSpeech) frames.Add(frame);
                if (!textFinished)
                {
                    text.Add(token);
                    if (token == 2) textFinished = true;
                }
                ++steps;
                float[] pcm = frame == null ? Array.Empty<float>() : DecodeChunk(frames, options.GenerateSpeech, false, cancellation);
                waveform.AddRange(pcm);
                callback?.Invoke(new MNNMiniMindOUpdate(_tokenizer.Decode(text), frame, pcm, steps));
                if (!options.GenerateSpeech && textFinished) { complete = true; break; }
                if (step + 1 == options.MaxNewTokens) break;
                cancellation.ThrowIfCancellationRequested();
                _decode = _decode ?? Graph("decode");
                var past = output[2];
                output = _decode.RunOutputs(Outputs, new Input("tokens", new[] { token }, 1, 1),
                    new Input("audio_codes", nextAudio, 1, 8, 1), new Input("past", past.Data, past.Shape));
            }
            cancellation.ThrowIfCancellationRequested();
            float[] tail = DecodeChunk(frames, options.GenerateSpeech, true, cancellation);
            waveform.AddRange(tail);
            string answer = _tokenizer.Decode(text);
            if (tail.Length > 0) callback?.Invoke(new MNNMiniMindOUpdate(answer, null, tail, steps));
            return new MNNMiniMindOResult(answer, frames.ToArray(), waveform.ToArray(), steps, !complete);
        }

        internal static int[] AlignedFrame(List<int>[] codes, int[] stop, int step)
        {
            if (step < 8) return null;
            var frame = new int[8];
            for (int i = 0; i < 8; ++i)
            {
                int at = step - 7 + i;
                if (stop[i] >= 0 && at >= stop[i]) return null;
                frame[i] = codes[i][at];
                if (frame[i] < 0 || frame[i] >= 2048) return null;
            }
            return frame;
        }
    }
}
