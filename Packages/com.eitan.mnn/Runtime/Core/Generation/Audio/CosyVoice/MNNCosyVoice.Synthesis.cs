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
        public MNNGeneratedAudio Synthesize(string text, int seed = 42, CancellationToken cancellationToken = default)
        {
            ValidateText(text);
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNCosyVoice));
                cancellationToken.ThrowIfCancellationRequested();
                LastTokenSeconds = LastFlowSeconds = LastVocoderSeconds = 0;
                var chunks = SplitText(text);
                var parts = new List<float[]>();
                int count = 0;
                const int pause = 960; // 40 ms at 24 kHz; retain every generated sample.
                foreach (string chunk in chunks)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var part = SynthesizeSegment(chunk, seed, cancellationToken);
                    parts.Add(part);
                    count = checked(count + part.Length);
                }

                if (parts.Count == 1)
                    return new MNNGeneratedAudio(parts[0], SampleRate, text);
                var pcm = new float[checked(count + (parts.Count - 1) * pause)];
                int position = 0;
                foreach (var part in parts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Array.Copy(part, 0, pcm, position, part.Length);
                    position += part.Length + pause;
                }

                return new MNNGeneratedAudio(pcm, SampleRate, text);
            }
        }

        /// <summary>
        /// Generates the existing bounded text chunks one at a time and publishes each decoded
        /// waveform as soon as it is ready. This keeps the model single-owner while allowing the
        /// playback pipeline to start the first chunk before later chunks finish. The callback is
        /// invoked synchronously on the synthesis worker and must not retain the waveform.
        /// </summary>
        public int SynthesizeStreaming(string text, Action<float[], int, bool> onPcm, int seed = 42, CancellationToken cancellationToken = default)
        {
            if (onPcm == null)
                throw new ArgumentNullException(nameof(onPcm));
            ValidateText(text);
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNCosyVoice));
                cancellationToken.ThrowIfCancellationRequested();
                LastTokenSeconds = LastFlowSeconds = LastVocoderSeconds = 0;
                var chunks = SplitText(text);
                int total = 0;
                const int pause = 960; // 40 ms at 24 kHz between bounded text chunks.
                var gap = new float[pause];
                for (int i = 0; i < chunks.Length; ++i)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    float[] part = SynthesizeSegment(chunks[i], seed, cancellationToken);
                    total = checked(total + part.Length);
                    onPcm(part, part.Length, i == chunks.Length - 1);
                    if (i + 1 < chunks.Length)
                    {
                        onPcm(gap, gap.Length, false);
                        total = checked(total + gap.Length);
                    }
                }

                return total;
            }
        }

        private float[] SynthesizeSegment(string text, int seed, CancellationToken cancellationToken)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            int[] speech;
            try
            {
                speech = GenerateTokens(text, seed, cancellationToken);
            }
            catch (Exception error)when (Backend != MNNBackendType.CPU && MNNAcceleration.CanRetryOnCpu(error))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _llm.Dispose();
                _llm = null;
                _llmOptions.FallBackToCpu(error);
                _llm = _llmOptions.Load(MNNInterop.MNN_Llm_create);
                speech = GenerateTokens(text, seed, cancellationToken);
            }

            LastTokenSeconds += timer.Elapsed.TotalSeconds;
            timer.Restart();
            cancellationToken.ThrowIfCancellationRequested();
            var tokens = _prompt.Concat(speech).ToArray();
            int frames = checked(tokens.Length * 2), promptFrames = _cond.Length / 80;
            if (frames <= promptFrames || frames > 15000)
                throw new InvalidDataException("Invalid CosyVoice sequence length.");
            var mu = _conditioner.Run("mu", new Input("tokens", tokens, 1, tokens.Length));
            RequireLength(mu, 80 * frames, "conditioner");
            var x = new float[80 * frames];
            var cond = new float[x.Length];
            var mask = Enumerable.Repeat(1f, frames).ToArray();
            for (int c = 0; c < 80; ++c)
            {
                Array.Copy(_noise, c * 15000, x, c * frames, frames);
                Array.Copy(_cond, c * promptFrames, cond, c * frames, promptFrames);
            }

            double midpoint = 1 - Math.Cos(Math.PI / 4);
            for (int step = 0; step < 2; ++step)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float t = step == 0 ? 0 : (float)midpoint;
                float delta = step == 0 ? (float)midpoint : (float)(1 - midpoint);
                var velocity = _flow.Run("estimator_out", new Input("x", x, 1, 80, frames), new Input("mask", mask, 1, 1, frames), new Input("mu", mu, 1, 80, frames), new Input("spks", _speaker, 1, 80), new Input("cond", cond, 1, 80, frames), new Input("t", new[]{t}, 1));
                RequireLength(velocity, x.Length, "flow");
                for (int i = 0; i < x.Length; ++i)
                    x[i] += delta * velocity[i];
            }

            int targetFrames = frames - promptFrames;
            var mel = new float[80 * targetFrames];
            for (int c = 0; c < 80; ++c)
                Array.Copy(x, c * frames + promptFrames, mel, c * targetFrames, targetFrames);
            LastFlowSeconds += timer.Elapsed.TotalSeconds;
            timer.Restart();
            cancellationToken.ThrowIfCancellationRequested();
            var f0 = _f0.Run("f0", new Input("mel", mel, 1, 80, targetFrames));
            RequireLength(f0, targetFrames, "f0");
            var source = MNNCosyVoiceVocoder.Source(f0, _weight, _bias, cancellationToken);
            var stft = MNNCosyVoiceVocoder.Stft(source, cancellationToken);
            int sourceFrames = targetFrames * 120 + 1;
            cancellationToken.ThrowIfCancellationRequested();
            var spectral = _hift.Run("spectral", new Input("mel", mel, 1, 80, targetFrames), new Input("source_stft", stft, 1, 18, sourceFrames));
            var pcm = MNNCosyVoiceVocoder.Istft(spectral, sourceFrames, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!pcm.Any(value => Math.Abs(value) > .0001f))
                throw new InvalidDataException("CosyVoice returned silent audio.");
            LastVocoderSeconds += timer.Elapsed.TotalSeconds;
            return pcm;
        }
    }
}
