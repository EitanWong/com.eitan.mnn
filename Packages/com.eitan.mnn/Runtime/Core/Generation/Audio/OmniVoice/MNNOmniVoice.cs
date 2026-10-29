using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    /// <summary>Reusable encoded reference for OmniVoice voice cloning.</summary>
    public sealed class MNNOmniVoicePrompt
    {
        internal readonly int[] Codes;
        internal readonly float Rms;
        internal readonly string Transcript;
        internal int Frames => Codes.Length / MNNOmniVoiceSampling.Codebooks;

        internal MNNOmniVoicePrompt(int[] codes, string transcript, float rms)
        {
            Codes = codes;
            Transcript = transcript;
            Rms = rms;
        }
    }

    /// <summary>OmniVoice noncausal speech synthesis through official MNN Interpreter and Tensor APIs.</summary>
    public sealed class MNNOmniVoice : IDisposable
    {
        public const int SampleRate = 24000;
        private readonly object _gate = new object();
        private readonly string _root;
        private readonly int _threads;
        private readonly MNNBackendType _lmBackend;
        private readonly MNNPrecisionMode _precision;
        private readonly bool _packedCfg;
        private readonly bool _compactSampling;
        private readonly MNNOmniVoiceText _text;
        private MNNGenerationGraph _lm, _encoder, _decoder;
        private bool _disposed;

        /// <summary>Whether this instance packs both CFG branches into one input sequence.</summary>
        public bool UsesPackedCfg => _packedCfg;

        /// <summary>Whether the optional graph returns predicted codes and confidence instead of full logits.</summary>
        public bool UsesCompactSampling => _compactSampling;

        private MNNOmniVoice(string root, int threads, MNNBackendType backend, MNNPrecisionMode precision, bool packedCfg, bool compactSampling)
        {
            _root = root;
            _threads = threads;
            _lmBackend = backend;
            _precision = precision;
            _packedCfg = packedCfg;
            _compactSampling = compactSampling;
            _text = new MNNOmniVoiceText(root);
        }

        public static MNNOmniVoice Load(string directory, int threads = 4,
            MNNBackendType lmBackend = MNNBackendType.Auto, MNNPrecisionMode precision = MNNPrecisionMode.High,
            bool enablePackedCfg = true)
            => LoadCore(directory, threads, lmBackend, precision, enablePackedCfg, false);

        /// <summary>Use the optional compact graph with High precision and packed CFG; shares the original external weights.</summary>
        public static MNNOmniVoice LoadCompact(string directory, int threads = 4, MNNBackendType lmBackend = MNNBackendType.Auto)
            => LoadCore(directory, threads, lmBackend, MNNPrecisionMode.High, true, true);

        private static MNNOmniVoice LoadCore(string directory, int threads, MNNBackendType lmBackend,
            MNNPrecisionMode precision, bool enablePackedCfg, bool compactSampling)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16) throw new ArgumentOutOfRangeException(nameof(threads));
            MNNAcceleration.Validate(lmBackend);
            if (!Enum.IsDefined(typeof(MNNPrecisionMode), precision)) throw new ArgumentOutOfRangeException(nameof(precision));
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Model directory is empty.", nameof(directory));
            string root = Path.GetFullPath(directory);
            foreach (string graph in new[] {"omnivoice_lm", "higgs_encoder", "higgs_decoder"})
            {
                if (!File.Exists(Path.Combine(root, graph + ".mnn")) || !File.Exists(Path.Combine(root, graph + ".mnn.weight")))
                    throw new FileNotFoundException("OmniVoice needs all three MNN graphs and external weights in one directory.", root);
            }
            if (compactSampling && !File.Exists(Path.Combine(root, "omnivoice_lm_compact.mnn")))
                throw new FileNotFoundException("Install the optional OmniVoice compact graph first.", Path.Combine(root, "omnivoice_lm_compact.mnn"));
            return new MNNOmniVoice(root, threads, lmBackend, precision,
                enablePackedCfg && precision == MNNPrecisionMode.High, compactSampling);
        }

        /// <summary>Encode a 0.5-15 second mono recording once. Supply its matching transcript; 3-10 seconds is recommended.</summary>
        public MNNOmniVoicePrompt CreateVoicePrompt(MNNReferenceAudio reference, string transcript, CancellationToken cancellationToken = default)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (string.IsNullOrWhiteSpace(transcript)) throw new ArgumentException("A reference transcript is required.", nameof(transcript));
            lock (_gate)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                float[] samples = reference.SampleRate == SampleRate ? (float[])reference.Samples.Clone() :
                    MNNSpeechDsp.Resample(reference.Samples, reference.SampleRate, SampleRate, cancellationToken);
                double energy = 0;
                foreach (float sample in samples) energy += (double)sample * sample;
                float rms = (float)Math.Sqrt(energy / samples.Length);
                if (rms > 0 && rms < 0.1f)
                    for (int i = 0; i < samples.Length; ++i) samples[i] *= 0.1f / rms;
                Array.Resize(ref samples, samples.Length / 960 * 960);
                if (samples.Length < 960) throw new ArgumentException("Reference audio is too short after frame alignment.", nameof(reference));
                if (_encoder == null) _encoder = Graph("higgs_encoder", MNNBackendType.CPU, MNNPrecisionMode.High);
                var encoded = _encoder.RunOutputs(new[] {"audio_codes"}, new Input("audio", samples, 1, 1, samples.Length))[0];
                if (encoded.Shape.Length != 3 || encoded.Shape[0] != 1 || encoded.Shape[1] != 8 || encoded.Shape[2] < 1)
                    throw new InvalidDataException("Unexpected Higgs encoder output shape.");
                int[] codes = Integers(encoded.Data);
                if (codes.Any(code => code < 0 || code >= MNNOmniVoiceSampling.Mask))
                    throw new InvalidDataException("Higgs encoder returned an invalid audio code.");
                string normalized = transcript.Trim();
                if (!";:,.!?…)]}\"'“”‘’；：，。！？".Contains(normalized[normalized.Length - 1]))
                    normalized += normalized.Any(ch => ch >= '\u4e00' && ch <= '\u9fff') ? "。" : ".";
                return new MNNOmniVoicePrompt(codes, normalized, rms);
            }
        }

        /// <summary>Synthesize up to 15 seconds. Duration sets the exact number of 25 Hz codec frames before postprocessing.</summary>
        public MNNGeneratedAudio Synthesize(string text, float durationSeconds, MNNOmniVoicePrompt voice = null,
            string language = "zh", string instruction = null, int seed = 47, int steps = 32,
            CancellationToken cancellationToken = default)
        {
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds < 0.4f || durationSeconds > 15)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            if (steps < 1 || steps > 128) throw new ArgumentOutOfRangeException(nameof(steps));
            lock (_gate)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                int targetFrames = Math.Max(1, (int)(durationSeconds * 25));
                int[] style = _text.Style(language, instruction, voice != null);
                int[] content = _text.Content(text, voice?.Transcript);
                int referenceFrames = voice?.Frames ?? 0;
                int sequence = checked(style.Length + content.Length + referenceFrames + targetFrames);
                if (sequence > 1024) throw new ArgumentException("OmniVoice sequence exceeds 1024 positions; use shorter text, reference or duration.", nameof(text));
                if (_lm == null)
                    _lm = Graph(_compactSampling ? "omnivoice_lm_compact" : "omnivoice_lm", _lmBackend, _precision,
                        _compactSampling ? Path.Combine(_root, "omnivoice_lm.mnn.weight") : null);
                int referenceStart = style.Length + content.Length;
                CfgInputs cfg = BuildCfgInputs(style, content, voice, referenceFrames, referenceStart, sequence, targetFrames);
                int[] tokens = Generate(cfg, targetFrames, steps, seed, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (_decoder == null) _decoder = Graph("higgs_decoder", MNNBackendType.CPU, MNNPrecisionMode.High);
                float[] wave = _decoder.Run("audio", new Input("audio_codes", tokens, 1, 8, targetFrames));
                if (wave.Length < 960 * targetFrames / 2 || wave.Length > 960 * targetFrames * 2)
                    throw new InvalidDataException("Unexpected Higgs decoder waveform length.");
                return new MNNGeneratedAudio(Postprocess(wave, voice?.Rms), SampleRate, text);
            }
        }

        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, float durationSeconds, MNNOmniVoicePrompt voice = null,
            string language = "zh", string instruction = null, int seed = 47, int steps = 32,
            CancellationToken cancellationToken = default)
            => Task.Run(() => Synthesize(text, durationSeconds, voice, language, instruction, seed, steps, cancellationToken), cancellationToken);

        private CfgInputs BuildCfgInputs(int[] style, int[] content, MNNOmniVoicePrompt voice,
            int referenceFrames, int referenceStart, int sequence, int frames)
        {
            int inputSequence = _packedCfg ? sequence + frames : sequence;
            int batch = _packedCfg ? 1 : 2;
            var cfg = new CfgInputs(batch, inputSequence, sequence - frames, _packedCfg ? sequence : 0);
            Array.Fill(cfg.Ids, MNNOmniVoiceSampling.Mask);
            for (int codebook = 0; codebook < 8; ++codebook)
            {
                int offset = codebook * inputSequence;
                Array.Copy(style, 0, cfg.Ids, offset, style.Length);
                Array.Copy(content, 0, cfg.Ids, offset + style.Length, content.Length);
                if (voice != null)
                    Array.Copy(voice.Codes, codebook * referenceFrames, cfg.Ids, offset + referenceStart, referenceFrames);
            }
            for (int i = referenceStart; i < sequence; ++i) cfg.AudioMask[i] = 1;
            int unconditionalMaskStart = _packedCfg ? sequence : inputSequence;
            for (int i = 0; i < frames; ++i) cfg.AudioMask[unconditionalMaskStart + i] = 1;
            for (int row = 0; row < sequence; ++row)
                Array.Fill(cfg.Attention, 1, row * inputSequence, sequence);
            int unconditionalAttentionStart = _packedCfg ? sequence * inputSequence + sequence : inputSequence * inputSequence;
            for (int row = 0; row < frames; ++row)
                Array.Fill(cfg.Attention, 1, unconditionalAttentionStart + row * inputSequence, frames);
            if (!_packedCfg)
                for (int i = frames; i < sequence; ++i)
                    cfg.Attention[inputSequence * inputSequence + i * inputSequence + i] = 1;
            for (int i = 0; i < sequence; ++i) cfg.Positions[i] = i;
            for (int i = 0; i < (_packedCfg ? frames : sequence); ++i)
                cfg.Positions[unconditionalMaskStart + i] = i;
            return cfg;
        }

        private sealed class CfgInputs
        {
            internal readonly int Batch, InputSequence, ConditionalStart, UnconditionalStart;
            internal readonly int[] Ids, AudioMask, Attention, Positions;

            internal CfgInputs(int batch, int inputSequence, int conditionalStart, int unconditionalStart)
            {
                Batch = batch; InputSequence = inputSequence; ConditionalStart = conditionalStart;
                UnconditionalStart = unconditionalStart;
                Ids = new int[batch * 8 * inputSequence];
                AudioMask = new int[batch * inputSequence];
                Attention = new int[batch * inputSequence * inputSequence];
                Positions = new int[batch * inputSequence];
            }
        }

        private int[] Generate(CfgInputs cfg, int frames, int steps, int seed, CancellationToken cancellationToken)
        {
            int[] tokens = Enumerable.Repeat(MNNOmniVoiceSampling.Mask, 8 * frames).ToArray();
            int[] schedule = MNNOmniVoiceSampling.Schedule(tokens.Length, steps);
            var random = new Random(seed);
            for (int step = 0; step < steps; ++step)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var predictions = Predict(cfg, tokens, frames);
                var candidates = new List<Candidate>(tokens.Length);
                for (int index = 0; index < tokens.Length; ++index)
                {
                    if (tokens[index] != MNNOmniVoiceSampling.Mask) continue;
                    double score = (predictions.Confidence[index] - index / frames * 5.0) / 5.0 + MNNOmniVoiceSampling.Gumbel(random);
                    candidates.Add(new Candidate(index, predictions.Codes[index], score));
                }
                candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
                if (schedule[step] > candidates.Count) throw new InvalidDataException("OmniVoice mask schedule exceeds remaining tokens.");
                for (int i = 0; i < schedule[step]; ++i)
                {
                    Candidate selected = candidates[i];
                    tokens[selected.Index] = selected.Code;
                    int codebook = selected.Index / frames, frame = selected.Index % frames;
                    cfg.Ids[codebook * cfg.InputSequence + cfg.ConditionalStart + frame] = selected.Code;
                    int unconditionalBatchOffset = _packedCfg ? 0 : 8 * cfg.InputSequence;
                    cfg.Ids[unconditionalBatchOffset + codebook * cfg.InputSequence + cfg.UnconditionalStart + frame] = selected.Code;
                }
            }
            if (tokens.Any(code => code == MNNOmniVoiceSampling.Mask)) throw new InvalidDataException("OmniVoice left audio positions masked.");
            return tokens;
        }

        private (int[] Codes, float[] Confidence) Predict(CfgInputs cfg, int[] tokens, int frames)
        {
            var inputs = new[] {
                new Input("input_ids", cfg.Ids, cfg.Batch, 8, cfg.InputSequence),
                new Input("audio_mask", cfg.AudioMask, cfg.Batch, cfg.InputSequence),
                new Input("attention_mask", cfg.Attention, cfg.Batch, 1, cfg.InputSequence, cfg.InputSequence),
                new Input("position_ids", cfg.Positions, cfg.Batch, cfg.InputSequence)
            };
            if (_compactSampling)
            {
                int[] indices = Enumerable.Range(cfg.ConditionalStart, frames * 2).ToArray();
                var outputs = _lm.RunOutputs(new[] {"codes", "confidence"},
                    inputs.Concat(new[] {new Input("target_indices", indices, indices.Length)}).ToArray());
                int[] predicted = Integers(outputs[0].Data);
                float[] confidence = outputs[1].Values;
                int[] shape = {1, 8, frames};
                if (!outputs[0].Shape.SequenceEqual(shape) || !outputs[1].Shape.SequenceEqual(shape) ||
                    predicted.Any(code => code < 0 || code >= MNNOmniVoiceSampling.Mask))
                    throw new InvalidDataException("Unexpected OmniVoice compact predictions.");
                return (predicted, confidence);
            }
            float[] logits = _lm.Run("logits", inputs);
            if (logits.Length != cfg.Batch * 8 * cfg.InputSequence * MNNOmniVoiceSampling.Vocabulary)
                throw new InvalidDataException("Unexpected OmniVoice logits shape.");
            var codes = new int[tokens.Length];
            var confidences = new float[tokens.Length];
            for (int index = 0; index < tokens.Length; ++index)
            {
                if (tokens[index] != MNNOmniVoiceSampling.Mask) continue;
                int codebook = index / frames, frame = index % frames;
                int c = (codebook * cfg.InputSequence + cfg.ConditionalStart + frame) * MNNOmniVoiceSampling.Vocabulary;
                int u = ((_packedCfg ? codebook : 8 + codebook) * cfg.InputSequence + cfg.UnconditionalStart + frame) * MNNOmniVoiceSampling.Vocabulary;
                codes[index] = MNNOmniVoiceSampling.Predict(logits, c, logits, u, 2, out confidences[index]);
            }
            return (codes, confidences);
        }

        private readonly struct Candidate
        {
            internal readonly int Index, Code;
            internal readonly double Score;
            internal Candidate(int index, int code, double score) { Index = index; Code = code; Score = score; }
        }

        private static float[] Postprocess(float[] wave, float? referenceRms)
        {
            float scale = 1;
            if (referenceRms.HasValue && referenceRms.Value < 0.1f) scale = referenceRms.Value / 0.1f;
            else if (!referenceRms.HasValue)
            {
                float peak = wave.Max(sample => Math.Abs(sample));
                if (peak > 1e-6f) scale = 0.5f / peak;
            }
            const int pad = SampleRate / 10;
            int fade = Math.Min(pad, wave.Length / 2);
            var output = new float[wave.Length + 2 * pad];
            for (int i = 0; i < wave.Length; ++i)
            {
                float envelope = i < fade ? (float)i / Math.Max(1, fade - 1) :
                    i >= wave.Length - fade ? (float)(wave.Length - 1 - i) / Math.Max(1, fade - 1) : 1;
                output[pad + i] = wave[i] * scale * envelope;
            }
            return output;
        }

        private static int[] Integers(Array values)
        {
            if (values is int[] ints) return ints;
            if (values is long[] longs) return longs.Select(value => checked((int)value)).ToArray();
            throw new InvalidDataException("Expected integer Higgs audio codes.");
        }

        private MNNGenerationGraph Graph(string name, MNNBackendType backend, MNNPrecisionMode precision, string externalWeightsPath = null)
            => new MNNGenerationGraph(Path.Combine(_root, name + ".mnn"), _threads, backendType: backend, precisionMode: precision,
                externalWeightsPath: externalWeightsPath);

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MNNOmniVoice));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _lm?.Dispose();
                _encoder?.Dispose();
                _decoder?.Dispose();
            }
        }
    }
}
