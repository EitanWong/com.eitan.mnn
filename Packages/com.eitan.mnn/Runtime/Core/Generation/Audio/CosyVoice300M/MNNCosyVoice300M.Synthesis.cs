using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    public sealed partial class MNNCosyVoice300M
    {
        /// <summary>Generates one complete utterance. Ten Flow steps and the full reference prompt
        /// are retained by default. Smaller step counts need separate quality evaluation.</summary>
        public MNNGeneratedAudio Synthesize(string text, int seed = 42, int flowSteps = 10, CancellationToken cancellationToken = default)
        {
            MNNSpeechDsp.ValidateText(text, 200);
            if (flowSteps < 1 || flowSteps > 20) throw new ArgumentOutOfRangeException(nameof(flowSteps));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNCosyVoice300M));
                cancellationToken.ThrowIfCancellationRequested();
                LastSynthesisSeconds = LastTokenSeconds = LastFlowSeconds = LastVocoderSeconds = LastRealTimeFactor = 0;
                LastSpeechTokens = 0;
                LastAcousticCpuRetry = false;
                var watch = Stopwatch.StartNew();
                var random = new MNNCosyVoice300MSampler(seed);
                try
                {
                    int[] speech = GenerateTokens(text, random, cancellationToken);
                    LastSpeechTokens = speech.Length;
                    LastTokenSeconds = watch.Elapsed.TotalSeconds;
                    var acousticRandom = random.Fork();
                    float[] pcm;
                    double pendingRetrySeconds = 0;
                    while (true)
                    {
                        double flowStart = watch.Elapsed.TotalSeconds;
                        float[] mel = RenderMel(speech, flowSteps, random, cancellationToken);
                        LastFlowSeconds += pendingRetrySeconds + watch.Elapsed.TotalSeconds - flowStart;
                        cancellationToken.ThrowIfCancellationRequested();
                        int frames = mel.Length / 80;
                        if (_vocoderLength != 0 && _vocoderLength != frames)
                        {
                            RecreateMetalStage(ref _f0, "hift_f0");
                        }
                        _vocoderLength = frames;
                        SelectMetalShape(ref _hift, _metalVocoderCores, frames, "hift_core");
                        var f0 = _f0.Run("f0", new Input("mel", mel, 1, 80, frames));
                        if (f0.Length != frames) throw new InvalidDataException("Invalid HiFT F0 length.");
                        var source = Source(f0, random, cancellationToken);
                        int spectralFrames = source.Length / 18;
                        var spectrum = _hift.Run("spectrum", new Input("mel", mel, 1, 80, frames),
                            new Input("source_stft", source, 1, 18, spectralFrames));
                        pcm = MNNSpeechSpectral16.Istft(spectrum, spectralFrames, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!pcm.Any(v => Math.Abs(v) > .0001f)) throw new InvalidDataException("CosyVoice-300M returned silent audio.");
                        if (!IsSaturated(pcm)) break;
                        if (LastAcousticCpuRetry || _flow.Backend == MNNBackendType.CPU && _hift.Backend == MNNBackendType.CPU)
                            throw new InvalidDataException("CosyVoice-300M returned an excessively saturated waveform.");
                        LastAcousticCpuRetry = true;
                        double retryStart = watch.Elapsed.TotalSeconds;
                        RetryAcousticsOnCpu(); random = acousticRandom.Fork();
                        pendingRetrySeconds = watch.Elapsed.TotalSeconds - retryStart;
                    }
                    LastSynthesisSeconds = watch.Elapsed.TotalSeconds;
                    LastVocoderSeconds = LastSynthesisSeconds - LastTokenSeconds - LastFlowSeconds;
                    LastRealTimeFactor = LastSynthesisSeconds / (pcm.Length / (double)OutputSampleRate);
                    return new MNNGeneratedAudio(pcm, OutputSampleRate, text);
                }
                finally { _decoder.Reset(); }
            }
        }

        internal static bool IsSaturated(float[] pcm)
        {
            int limited = 0;
            for (int i = 0; i < pcm.Length; ++i)
                if (Math.Abs(pcm[i]) >= .989f) ++limited;
            return limited > pcm.Length * .001;
        }

        private int[] GenerateTokens(string text, MNNCosyVoice300MSampler random, CancellationToken cancellation)
        {
            int[] tokens = _tokenizer.Encode(text);
            if (tokens.Length < 1 || tokens.Any(t => t < 0 || t >= 51866)) throw new ArgumentException("Invalid or empty Whisper token sequence.", nameof(text));
            var allText = _promptText.Concat(tokens).ToArray();
            cancellation.ThrowIfCancellationRequested();
            var encoded = _text.Run("encoded", new Input("tokens", allText, 1, allText.Length), new Input("lengths", new[] {allText.Length}, 1));
            if (encoded.Length != allText.Length * 1024) throw new InvalidDataException("Invalid text encoder output.");
            int query = 3 + allText.Length + _promptSpeech.Length;
            var x = new float[query * 1024];
            Array.Copy(_specialEmbedding, 0, x, 0, 1024);
            Array.Copy(_speakerEmbedding, 0, x, 1024, 1024);
            Array.Copy(encoded, 0, x, 2048, encoded.Length);
            int cursor = 2 + allText.Length;
            Array.Copy(_specialEmbedding, 1024, x, cursor++ * 1024, 1024);
            foreach (int token in _promptSpeech) Array.Copy(_speechEmbedding, token * 1024, x, cursor++ * 1024, 1024);
            var generated = new List<int>();
            int past = 1, maximum = Math.Min(1500, checked(tokens.Length * 40));
            _decoder.Reset();
            for (int step = 0; step < maximum; ++step)
            {
                cancellation.ThrowIfCancellationRequested();
                var logits = _decoder.Forward(x, query, past, step == 0);
                int selected = random.Sample(logits, generated, tokens.Length * 2);
                if (selected == 4096)
                {
                    if (generated.Count == 0) throw new InvalidDataException("CosyVoice-300M generated no speech.");
                    return generated.ToArray();
                }
                generated.Add(selected);
                past += query; query = 1;
                if (x.Length != 1024) x = new float[1024];
                Array.Copy(_speechEmbedding, selected * 1024, x, 0, 1024);
            }
            throw new InvalidDataException("CosyVoice-300M did not reach EOS within its token budget. Use a shorter text; truncated speech is refused.");
        }

        private float[] RenderMel(int[] speech, int steps, MNNCosyVoice300MSampler random, CancellationToken cancellation)
        {
            int[] both = _promptSpeech.Concat(speech).ToArray();
            int targetFrames = (int)(speech.Length / 50d * 22050 / 256), promptFrames = _promptMel.Length / 80;
            int length = promptFrames + targetFrames;
            // No acoustic Metal session may resize between utterance lengths.
            // Keep the expensive encoder/core in a bounded cache per exact shape.
            if (_flowLength != 0 && _flowLength != length)
            {
                RecreateMetalStage(ref _flow, "flow_estimator");
                RecreateMetalStage(ref _regulator, "flow_regulator");
            }
            _flowLength = length;
            SelectMetalShape(ref _flowEncoder, _metalEncoders, both.Length, "flow_encoder");
            var encoded = _flowEncoder.Run("encoded", new Input("tokens", both, 1, both.Length), new Input("lengths", new[] {both.Length}, 1));
            if (encoded.Length != both.Length * 80) throw new InvalidDataException("Invalid Flow encoder output.");
            var prompt = Interpolate(encoded, 0, _promptSpeech.Length, promptFrames);
            float[] target;
            if (speech.Length > 40)
            {
                target = Interpolate(encoded, _promptSpeech.Length, 20, 34)
                    .Concat(Interpolate(encoded, _promptSpeech.Length + 20, speech.Length - 40, targetFrames - 68))
                    .Concat(Interpolate(encoded, both.Length - 20, 20, 34)).ToArray();
            }
            else target = Interpolate(encoded, _promptSpeech.Length, speech.Length, targetFrames);
            var features = new float[80 * length];
            for (int c = 0; c < 80; ++c)
            {
                for (int t = 0; t < promptFrames; ++t) features[c * length + t] = prompt[t * 80 + c];
                for (int t = 0; t < targetFrames; ++t) features[c * length + promptFrames + t] = target[t * 80 + c];
            }
            var mu = _regulator.Run("mu", new Input("features", features, 1, 80, length));
            if (mu.Length != features.Length) throw new InvalidDataException("Invalid Flow regulator output.");
            var cond = new float[mu.Length * 2];
            for (int c = 0; c < 80; ++c)
                for (int t = 0; t < promptFrames; ++t) cond[c * length + t] = _promptMel[t * 80 + c];
            var cfgMu = new float[mu.Length * 2]; Array.Copy(mu, cfgMu, mu.Length);
            var speaker = new float[160]; Array.Copy(_flowSpeaker, speaker, 80);
            var mask = Enumerable.Repeat(1f, length * 2).ToArray();
            var z = new float[mu.Length];
            for (int i = 0; i < z.Length; ++i) z[i] = random.Normal();
            var input = new float[z.Length * 2];
            var span = new float[steps + 1];
            for (int i = 0; i <= steps; ++i) span[i] = (float)(1 - Math.Cos((double)i / steps * .5 * Math.PI));
            for (int i = 0; i < steps; ++i)
            {
                cancellation.ThrowIfCancellationRequested();
                Array.Copy(z, 0, input, 0, z.Length); Array.Copy(z, 0, input, z.Length, z.Length);
                var velocity = _flow.Run("estimator_out", new Input("x", input, 2, 80, length),
                    new Input("mask", mask, 2, 1, length), new Input("mu", cfgMu, 2, 80, length),
                    new Input("t", new[] {span[i], span[i]}, 2), new Input("spks", speaker, 2, 80), new Input("cond", cond, 2, 80, length));
                if (velocity.Length != input.Length) throw new InvalidDataException("Invalid Flow velocity shape.");
                float dt = span[i + 1] - span[i];
                for (int j = 0; j < z.Length; ++j) z[j] += dt * (1.7f * velocity[j] - .7f * velocity[z.Length + j]);
            }
            var mel = new float[80 * targetFrames];
            for (int c = 0; c < 80; ++c) Array.Copy(z, c * length + promptFrames, mel, c * targetFrames, targetFrames);
            return mel;
        }

        internal static float[] Interpolate(float[] data, int start, int count, int size)
        {
            if (start < 0 || count < 1 || size < 1 || checked((start + count) * 80) > data.Length)
                throw new InvalidDataException("Invalid Flow interpolation shape.");
            var result = new float[checked(size * 80)];
            for (int t = 0; t < size; ++t)
            {
                double position = Math.Max(0, (t + .5) * count / size - .5);
                int lower = (int)position, upper = Math.Min(lower + 1, count - 1);
                float weight = (float)(position - lower);
                for (int c = 0; c < 80; ++c)
                    result[t * 80 + c] = data[(start + lower) * 80 + c] * (1 - weight) + data[(start + upper) * 80 + c] * weight;
            }
            return result;
        }
    }
}
