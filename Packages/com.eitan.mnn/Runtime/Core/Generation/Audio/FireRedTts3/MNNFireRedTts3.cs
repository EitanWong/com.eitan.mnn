// Managed implementation of the published FireRedTTS3 DiTAR inference pipeline.
// Algorithm reference: yunfengwang/fireredtts3-mnn (Apache-2.0). See LICENSE.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Input = MNN.Unity.MNNGenerationGraph.Input;
using Output = MNN.Unity.MNNGenerationGraph.Output;

namespace MNN.Unity
{
    /// <summary>FireRedTTS3 reference-conditioned speech, using official MNN Interpreter graphs on CPU.</summary>
    public sealed class MNNFireRedTts3 : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _root;
        private readonly int _threads;
        private readonly MNNSpeechTokenizer _tokenizer;
        private bool _disposed;
        private MNNFireRedTts3(string root, int threads)
        { _root = root; _threads = threads; _tokenizer = new MNNSpeechTokenizer(root, false); }

        public static MNNFireRedTts3 Load(string directory, int threads = 4)
        {
            if (threads < 1 || threads > 64) throw new ArgumentOutOfRangeException(nameof(threads));
            MNNReferenceSpeechModel.Validate(directory, false);
            return new MNNFireRedTts3(Path.GetFullPath(directory), threads);
        }

        /// <summary>Reference must be 0.5–15 seconds. Its exact transcript is required. Languages: zh/en.</summary>
        public MNNGeneratedAudio Synthesize(string text, MNNReferenceAudio reference, string referenceText, string language = "zh", int seed = 42, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNFireRedTts3));
                cancellationToken.ThrowIfCancellationRequested();
                if (reference == null) throw new ArgumentNullException(nameof(reference));
                MNNSpeechDsp.ValidateText(text);
                MNNSpeechDsp.ValidateText(referenceText, 400);
                int[] ids = _tokenizer.Encode(referenceText + text, language);
                using (var graphs = new MNNSpeechGraphs(_root, _threads, false, cancellationToken))
                {
                    var wav = MNNSpeechDsp.Resample(reference.Samples, reference.SampleRate, 24000, cancellationToken);
                    int paddedLength = (wav.Length + 3839) / 3840 * 3840;
                    var padded = new float[paddedLength]; Array.Copy(wav, 0, padded, padded.Length - wav.Length, wav.Length);
                    int maskLength = paddedLength / 480;
                    var prompt = graphs.Run("redae_encoder", "latents", new Input("audio", padded, 1, paddedLength), new Input("mask1", MNNSpeechDsp.CausalMask(maskLength, 64), 1, 1, maskLength, maskLength), new Input("mask2", MNNSpeechDsp.CausalMask(3), 1, 1, 3, 3));
                    graphs.Release("redae_encoder");
                    if (prompt.Values.Length != paddedLength / 960 * 64) throw new InvalidDataException("Unexpected reference codec shape.");
                    var features = MNNSpeechDsp.Fbank(MNNSpeechDsp.Resample(padded, 24000, 16000, cancellationToken), false, out int frames, cancellationToken);
                    var speaker = graphs.Run("campp", "spk_emb", new Input("fbank", features, 1, frames, 80));
                    graphs.Release("campp");
                    var speakerLlm = graphs.Run("spk_proj_llm", "y", speaker.AsInput("x")); graphs.Release("spk_proj_llm");
                    var speakerDit = graphs.Run("spk_proj_dit", "y", speaker.AsInput("x")); graphs.Release("spk_proj_dit");
                    var patches = graphs.Run("patch_encoder", "patch_embs", prompt.AsInput("latents"));
                    var textEmbeds = graphs.Run("embed", "text_embs", new Input("input_ids", ids, 1, ids.Length)); graphs.Release("embed");
                    int promptPatches = patches.Values.Length / 2048, sequence = 1 + ids.Length + promptPatches;
                    float[] hidden = MNNSpeechDsp.Join(speakerLlm.Values, textEmbeds.Values, patches.Values);
                    var past = new Output[56];
                    hidden = Backbone(graphs, hidden, sequence, Enumerable.Range(0, sequence).ToArray(), past, true);
                    var history = MNNSpeechDsp.Join(new float[8 * 64], prompt.Values);
                    var backboneHistory = new float[2 * 2048];
                    var generated = new List<float>();
                    var random = new Random(seed);
                    bool stopped = false;
                    for (int step = 0; step < 400; ++step)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var last = MNNSpeechDsp.Tail(hidden, 2048);
                        float stopLogit = graphs.Run("stop_head", "y", new Input("x", last, 1, 1, 2048)).Values[0];
                        if (stopLogit >= 0 && step >= 6) { stopped = true; break; }
                        backboneHistory = MNNSpeechDsp.Join(backboneHistory, MNNSpeechDsp.Tail(hidden, (step == 0 ? promptPatches : 1) * 2048));
                        backboneHistory = MNNSpeechDsp.Tail(backboneHistory, 3 * 2048);
                        var bc = graphs.Run("dit_head", "y", new Input("x", backboneHistory, 1, 3, 2048)).Values;
                        if (bc.Length != 3 * 1024 || speakerDit.Values.Length != 512) throw new InvalidDataException("Unexpected FireRed conditioning shape.");
                        var cond = new float[12 * 1536];
                        for (int row = 0; row < 12; ++row)
                        {
                            Array.Copy(bc, row / 4 * 1024, cond, row * 1536, 1024);
                            Array.Copy(speakerDit.Values, 0, cond, row * 1536 + 1024, 512);
                        }
                        var x = MNNSpeechDsp.Join(MNNSpeechDsp.Tail(history, 8 * 64), MNNGenerationMath.Gaussian(4 * 64, random.Next()));
                        for (int t = 0; t < 10; ++t)
                        {
                            var input = new float[2 * 12 * 1600];
                            for (int batch = 0; batch < 2; ++batch)
                                for (int row = 0; row < 12; ++row)
                                {
                                    Array.Copy(x, row * 64, input, (batch * 12 + row) * 1600, 64);
                                    if (batch == 0) Array.Copy(cond, row * 1536, input, row * 1600 + 64, 1536);
                                }
                            float time = (float)(1 - Math.Cos(t * Math.PI / 20)), dt = (float)(Math.Cos(t * Math.PI / 20) - Math.Cos((t + 1) * Math.PI / 20));
                            var velocity = graphs.Run("dit", "v", new Input("x", input, 2, 12, 1600), new Input("t", new[]{time, time}, 2)).Values;
                            if (velocity.Length != 2 * 12 * 64) throw new InvalidDataException("Unexpected FireRed flow shape.");
                            for (int i = 8 * 64; i < 12 * 64; ++i) x[i] += dt * (3 * velocity[i] - 2 * velocity[12 * 64 + i]);
                        }
                        var one = MNNSpeechDsp.Tail(x, 4 * 64); generated.AddRange(one);
                        history = MNNSpeechDsp.Tail(MNNSpeechDsp.Join(history, one), 8 * 64);
                        var next = graphs.Run("patch_encoder", "patch_embs", new Input("latents", one, 1, 4, 64));
                        hidden = Backbone(graphs, next.Values, 1, new[]{sequence + step}, past, false);
                    }
                    if (!stopped) throw new InvalidDataException("FireRedTTS3 did not emit a stop signal within 400 patches; shorten the text or change the reference.");
                    for (int s = 1; s <= 4; ++s) graphs.Release("step_seg" + s);
                    graphs.Release("dit"); graphs.Release("patch_encoder");
                    var all = MNNSpeechDsp.Join(prompt.Values, generated.ToArray()); int latentLength = all.Length / 64, decoderMask = latentLength * 2;
                    var spectrum = graphs.Run("redae_decoder", "feats", new Input("latents", all, 1, latentLength, 64), new Input("mask", MNNSpeechDsp.CausalMask(decoderMask, 64), 1, 1, decoderMask, decoderMask));
                    graphs.Release("redae_decoder");
                    var wave = MNNSpeechDsp.FireRedIstft(spectrum.Values, cancellationToken);
                    if (wave.Length <= paddedLength) throw new InvalidDataException("FireRedTTS3 returned no generated audio.");
                    return MNNSpeechDsp.Result(MNNSpeechDsp.Tail(wave, wave.Length - paddedLength), 24000, text);
                }
            }
        }

        private static float[] Backbone(MNNSpeechGraphs graphs, float[] hidden, int length, int[] positions, Output[] past, bool prefill)
        {
            for (int segment = 0; segment < 4; ++segment)
            {
                var inputs = new List<Input>{new Input("hidden_states", hidden, 1, length, 2048), new Input("position_ids", positions, 1, length)};
                if (!prefill) for (int j = segment * 14; j < (segment + 1) * 14; ++j) inputs.Add(past[j].AsInput("past_" + j));
                var names = new[]{"hidden"}.Concat(Enumerable.Range(segment * 14, 14).Select(j => "kv_" + j)).ToArray();
                string stage = (prefill ? "prefill_seg" : "step_seg") + (segment + 1);
                var output = graphs.Run(stage, names, inputs.ToArray()); hidden = output[0].Values;
                for (int j = 0; j < 14; ++j) past[segment * 14 + j] = output[j + 1];
                if (prefill) graphs.Release(stage);
            }
            return graphs.Run("norm", "normed", new Input("hidden", hidden, 1, length, 2048)).Values;
        }

        public void Dispose() { lock (_gate) _disposed = true; }
    }
}
