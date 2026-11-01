using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    public sealed partial class MNNSupertonic
    {
        public MNNGeneratedAudio Synthesize(string text, string voice = "M1", int steps = 5, float speed = 1.05f, int seed = 42, CancellationToken cancellationToken = default, string language = null)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 500)
                throw new ArgumentException("Enter 1–500 characters for this Supertonic model.", nameof(text));
            if (steps < 1 || steps > 30)
                throw new ArgumentOutOfRangeException(nameof(steps));
            if (float.IsNaN(speed) || speed < .5f || speed > 2)
                throw new ArgumentOutOfRangeException(nameof(speed));
            lock (_gate)
            {
                Check();
                cancellationToken.ThrowIfCancellationRequested();
                if (voice == null || !_voices.TryGetValue(voice, out var style))
                    throw new ArgumentException("Select an installed voice.", nameof(voice));
                if (!MNNSpeechLanguages.SupportsText(text, Languages))
                    return _languageFallback.Synthesize(text, Languages, () => throw new MNNSpeechLanguageException("This Supertonic voice does not support this script."), cancellationToken);
                language = string.IsNullOrWhiteSpace(language) ? MNNSpeechLanguages.Select(text, Languages) : MNNSpeechLanguages.Normalize(language);
                if (!Languages.Contains(language, StringComparer.Ordinal))
                    throw new MNNSpeechLanguageException("This Supertonic version does not support language: " + language);
                if (text.IndexOf('<') >= 0 || text.IndexOf('>') >= 0)
                    throw new ArgumentException("Pass the language parameter instead of embedded language tags.", nameof(text));
                string normalized = Normalize(text, language == "en");
                // Validate the entire request before running any segment.
                Encode(normalized, _indexer);
                var segments = SplitText(normalized, language == "en");
                var waves = new List<float[]>();
                foreach (string segment in segments)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string tagged = Version == "v1.5.0" ? segment : "<" + language + ">" + segment + "</" + language + ">";
                    waves.Add(SynthesizePart(tagged, style, steps, speed, seed, cancellationToken));
                }

                cancellationToken.ThrowIfCancellationRequested();
                return new MNNGeneratedAudio(MNNSpeechDsp.JoinWaveforms(waves, _sampleRate), _sampleRate, normalized);
            }
        }

        private float[] SynthesizePart(string text, Voice style, int steps, float speed, int seed, CancellationToken cancellationToken)
        {
            int[] ids = Encode(text, _indexer);
            float[] mask = Enumerable.Repeat(1f, ids.Length).ToArray();
            var textIds = new Input("text_ids", ids, 1, ids.Length);
            var textMask = new Input("text_mask", mask, 1, 1, ids.Length);
            float duration = _duration.Run("duration", textIds, new Input("style_dp", style.Dp, style.DpShape), textMask)[0] / speed;
            if (duration <= 0 || duration > 30)
                throw new InvalidDataException("Predicted speech duration must be between 0 and 30 seconds. Shorten the text.");
            int length = Math.Max(1, (int)Math.Ceiling((int)(duration * _sampleRate) / (double)_chunk));
            cancellationToken.ThrowIfCancellationRequested();
            float[] emb = _text.Run("text_emb", textIds, new Input("style_ttl", style.Ttl, style.TtlShape), textMask);
            if (emb.Length % ids.Length != 0)
                throw new InvalidDataException("Invalid Supertonic text embedding shape.");
            var textEmb = new Input("text_emb", emb, 1, emb.Length / ids.Length, ids.Length);
            var ttlStyle = new Input("style_ttl", style.Ttl, style.TtlShape);
            var latentMask = new Input("latent_mask", Enumerable.Repeat(1f, length).ToArray(), 1, 1, length);
            float[] latent = MNNGenerationMath.Gaussian(checked(_latentDim * length), seed);
            for (int step = 0; step < steps; ++step)
            {
                cancellationToken.ThrowIfCancellationRequested();
                latent = _estimator.Run("denoised_latent", new Input("noisy_latent", latent, 1, _latentDim, length), textEmb, ttlStyle, latentMask, textMask, new Input("current_step", new[]{(float)step}, 1), new Input("total_step", new[]{(float)steps}, 1));
                if (latent.Length != _latentDim * length)
                    throw new InvalidDataException("Supertonic estimator changed latent shape.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            float[] wave = _vocoder.Run("wav_tts", new Input("latent", latent, 1, _latentDim, length));
            cancellationToken.ThrowIfCancellationRequested();
            PrepareWaveform(wave, checked(length * _chunk));
            return wave;
        }

        internal static float[] JoinWaveforms(IReadOnlyList<float[]> waves, int sampleRate) =>
            MNNSpeechDsp.JoinWaveforms(waves, sampleRate);

        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, string voice = "M1", int steps = 5, float speed = 1.05f, int seed = 42, CancellationToken cancellationToken = default, string language = null) => Task.Run(() => Synthesize(text, voice, steps, speed, seed, cancellationToken, language), cancellationToken);
        internal static void PrepareWaveform(float[] wave, int expectedSamples)
        {
            if (wave.Length != expectedSamples)
                throw new InvalidDataException("Supertonic vocoder returned an unexpected audio length.");
            float peak = 0;
            foreach (float value in wave)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new InvalidDataException("Supertonic vocoder returned non-finite audio.");
                peak = Math.Max(peak, Math.Abs(value));
            }

            // Finite tensors can still be corrupt. Do not turn the observed
            // multi-unit spikes into full-scale square waves by hard clipping.
            if (peak > 2)
                throw new InvalidDataException("Supertonic vocoder returned excessive audio amplitude.");
            if (peak > 1)
                for (int i = 0; i < wave.Length; ++i)
                    wave[i] /= peak;
        }
    }
}
