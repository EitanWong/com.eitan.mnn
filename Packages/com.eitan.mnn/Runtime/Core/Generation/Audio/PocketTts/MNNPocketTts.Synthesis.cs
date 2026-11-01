using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;
using Output = MNN.Unity.MNNGenerationGraph.Output;

namespace MNN.Unity
{
    public sealed partial class MNNPocketTts
    {
        /// <summary>Generate up to 30 seconds from at most 50 English tokens and a 0.5–15 second reference WAV.</summary>
        public MNNGeneratedAudio Synthesize(string text, MNNReferenceAudio reference, int seed = 42, int maxFrames = 375, CancellationToken cancellationToken = default)
        {
            float[] waveform;
            SynthesizeCore(text, reference, seed, maxFrames, cancellationToken, null, out waveform);
            return new MNNGeneratedAudio(waveform, SampleRate, text);
        }

        /// <summary>
        /// Generate speech and synchronously publish each decoded 80 ms PCM chunk as soon as it is available.
        /// The callback runs on the synthesis worker while this model is exclusively owned; copy or enqueue the
        /// samples before returning, do not retain the array, re-enter this model, or call Unity APIs.
        /// The final callback receives endsSegment=true. Returns the number of generated mono PCM samples.
        /// </summary>
        public int SynthesizeStreaming(string text, MNNReferenceAudio reference, Action<float[], int, int, bool> onPcmChunk, int seed = 42, int maxFrames = 375, CancellationToken cancellationToken = default)
        {
            if (onPcmChunk == null)
                throw new ArgumentNullException(nameof(onPcmChunk));
            return SynthesizeCore(text, reference, seed, maxFrames, cancellationToken, onPcmChunk, out _);
        }

        private int SynthesizeCore(string text, MNNReferenceAudio reference, int seed, int maxFrames, CancellationToken cancellationToken, Action<float[], int, int, bool> onPcmChunk, out float[] waveform)
        {
            waveform = null;
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNPocketTts));
                if (_synthesizing)
                    throw new InvalidOperationException("Pocket-TTS callbacks cannot re-enter synthesis on the same model.");
                cancellationToken.ThrowIfCancellationRequested();
                if (reference == null)
                    throw new ArgumentNullException(nameof(reference));
                if (maxFrames < 12 || maxFrames > 375)
                    throw new ArgumentOutOfRangeException(nameof(maxFrames));
                string prepared = MNNPocketText.Prepare(text);
                int[] ids = _text.Encode(prepared);
                if (ids.Length > 50)
                    throw new ArgumentException("Split Pocket speech into at most 50 tokens per request.", nameof(text));
                EnsureGraphs();
                _synthesizing = true;
                try
                {
                    PrepareVoice(reference, cancellationToken);
                    int step = _voiceSteps, capacity = checked(step + ids.Length + maxFrames);
                    if (capacity > 1000)
                        throw new ArgumentException("Pocket cache capacity exceeded; shorten the text or reference.");
                    // Reserve only this request's upper bound. Keep the cached voice snapshot
                    // compact and immutable so cancellation or a failed consumer cannot alter it.
                    var state = new List<Input>(_voiceState);
                    for (int layer = 0; layer < 6; ++layer)
                        state[layer * 3] = ResizeCache(state[layer * 3], capacity, step);
                    var embeddings = _conditioner.RunOutputs(new[]{"embeddings"}, new Input("token_ids", ids, 1, ids.Length))[0];
                    Update(state, Run(_priming, _cacheNames, state, embeddings.AsInput("text_embeddings")), 0, step += ids.Length);
                    var current = Enumerable.Repeat(float.NaN, 32).ToArray();
                    List<float> latents = onPcmChunk == null ? new List<float>() : null;
                    List<Input> decoderState = onPcmChunk == null ? null : InitialState(_mimiManifest);
                    string[] decoderStateInputNames = onPcmChunk == null ? null : _mimiManifest.Select(entry => (string)MNNModelData.Get(entry, "input_name")).ToArray();
                    string[] decoderOutputs = onPcmChunk == null ? null : new[]{"audio_frame"}.Concat(_mimiManifest.Select(entry => (string)MNNModelData.Get(entry, "output_name"))).ToArray();
                    Input[] decoderInputs = null;
                    if (onPcmChunk != null)
                    {
                        decoderInputs = new Input[decoderState.Count + 1];
                        decoderState.CopyTo(decoderInputs, 0);
                        decoderInputs[decoderState.Count] = new Input("latent", current, 1, 1, 32);
                    }

                    var random = new Random(seed);
                    int eos = -1, trailing = prepared.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries).Length <= 4 ? 5 : 3;
                    bool finished = false;
                    int generatedSamples = 0;
                    var outputs = new[]{"conditioning", "eos_logit"}.Concat(_cacheNames).ToArray();
                    for (int frame = 0; frame < maxFrames; ++frame)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var result = Run(_main, outputs, state, new Input("sequence", current, 1, 1, 32));
                        Finite(result[0].Values, 1024);
                        Finite(result[1].Values, 1);
                        Update(state, result, 2, ++step);
                        if (eos < 0 && result[1].Values[0] > -4)
                            eos = frame;
                        bool endsSegment = eos >= 0 && frame == eos + trailing - 1;
                        var noise = MNNGenerationMath.Gaussian(32, random.Next());
                        for (int i = 0; i < noise.Length; ++i)
                            noise[i] *= (float)Math.Sqrt(.7);
                        var velocity = _flow.Run("flow_dir", result[0].AsInput("c"), new Input("s", new[]{0f}, 1, 1), new Input("t", new[]{1f}, 1, 1), new Input("x", noise, 1, 32));
                        Finite(velocity, 32);
                        current = noise.Zip(velocity, (a, b) => a + b).ToArray();
                        if (onPcmChunk == null)
                        {
                            latents.AddRange(current);
                        }
                        else
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            decoderInputs[decoderState.Count] = new Input("latent", current, 1, 1, 32);
                            var decoded = _decoder.RunOutputs(decoderOutputs, decoderInputs);
                            float[] pcmChunk = decoded[0].Values;
                            if (pcmChunk.Length != 1920 || pcmChunk.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                                throw new InvalidDataException("Unexpected Pocket incremental decoder output.");
                            for (int i = 0; i < decoderState.Count; ++i)
                            {
                                decoderState[i] = decoded[i + 1].AsInput(decoderStateInputNames[i]);
                                decoderInputs[i] = decoderState[i];
                            }

                            onPcmChunk(pcmChunk, 0, pcmChunk.Length, endsSegment);
                            generatedSamples = checked(generatedSamples + pcmChunk.Length);
                        }

                        // EOS and its trailing frames already determine completion.
                        // Do not run another full main step after the final audio frame;
                        // that step adds latency and incorrectly rejects an exact frame limit.
                        if (endsSegment)
                        {
                            finished = true;
                            break;
                        }
                    }

                    if (!finished || (onPcmChunk == null ? latents.Count : generatedSamples) < (onPcmChunk == null ? 32 : 1920))
                        throw new InvalidDataException("Pocket-TTS did not finish within the frame limit; shorten the text or change the reference.");
                    cancellationToken.ThrowIfCancellationRequested();
                    if (onPcmChunk == null)
                    {
                        waveform = _decoder.Run("audio_frame", InitialState(_mimiManifest).Concat(new[]{new Input("latent", latents.ToArray(), 1, latents.Count / 32, 32)}).ToArray());
                        if (waveform.Length != latents.Count / 32 * 1920)
                            throw new InvalidDataException("Unexpected Pocket decoded audio length.");
                        generatedSamples = waveform.Length;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    return generatedSamples;
                }
                finally
                {
                    _synthesizing = false;
                }
            }
        }

        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, MNNReferenceAudio reference, int seed = 42, int maxFrames = 375, CancellationToken cancellationToken = default) => Task.Run(() => Synthesize(text, reference, seed, maxFrames, cancellationToken), cancellationToken);
    }
}
