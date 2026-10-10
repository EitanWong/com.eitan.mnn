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
    /// <summary>Managed Supertonic pipeline over official MNN Interpreter/Tensor APIs.
    /// Supports the ModelScope MNN/supertonic-tts-mnn graph format on macOS Mono.</summary>
    public sealed class MNNSupertonic : IDisposable
    {
        private readonly object _gate = new object ();
        private readonly MNNGenerationGraph _duration, _text, _estimator, _vocoder;
        private readonly int[] _indexer;
        private readonly Dictionary<string, Voice> _voices = new Dictionary<string, Voice>();
        private readonly int _sampleRate, _chunk, _latentDim;
        private bool _disposed;
        private sealed class Voice
        {
            internal float[] Ttl, Dp;
            internal int[] TtlShape, DpShape;
        }

        public string[] Voices
        {
            get
            {
                lock (_gate)
                {
                    Check();
                    return _voices.Keys.OrderBy(value => value).ToArray();
                }
            }
        }

        public int SampleRate => _sampleRate;
        /// <summary>Current stage backends; native unsupported operations may still run on CPU.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, MNNBackendType> StageBackends
        {
            get
            {
                lock (_gate)
                {
                    if (_disposed)
                        throw new ObjectDisposedException(nameof(MNNSupertonic));
                    return new System.Collections.Generic.Dictionary<string, MNNBackendType>{{"Duration", _duration.Backend}, {"TextEncoder", _text.Backend}, {"Estimator", _estimator.Backend}, {"Vocoder", _vocoder.Backend}, };
                }
            }
        }

        public static MNNSupertonic Load(string directory, int threads = 4, string precision = "fp16", MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16)
                throw new ArgumentOutOfRangeException(nameof(threads));
            if (precision != "fp16" && precision != "fp32" && precision != "int8")
                throw new ArgumentException("Use fp16, fp32 or int8.", nameof(precision));
            return new MNNSupertonic(Path.GetFullPath(directory), threads, precision, backendType);
        }

        private MNNSupertonic(string directory, int threads, string precision, MNNBackendType backendType)
        {
            var config = MNNModelData.Read(Path.Combine(directory, "config.json"));
            if ((string)MNNModelData.Get(config, "model_type") != "supertonic")
                throw new NotSupportedException("Expected a Supertonic repository, not another TTS architecture.");
            var metadata = MNNModelData.Read(Path.Combine(directory, "mnn_models", "tts.json"));
            var ae = MNNModelData.Get(metadata, "ae");
            var ttl = MNNModelData.Get(metadata, "ttl");
            _sampleRate = MNNModelData.Integer(MNNModelData.Get(ae, "sample_rate"));
            int compress = MNNModelData.Integer(MNNModelData.Get(ttl, "chunk_compress_factor"));
            _chunk = checked(MNNModelData.Integer(MNNModelData.Get(ae, "base_chunk_size")) * compress);
            _latentDim = checked(MNNModelData.Integer(MNNModelData.Get(ttl, "latent_dim")) * compress);
            if (_sampleRate != 44100 || _chunk != 3072 || _latentDim != 144)
                throw new NotSupportedException("Unsupported Supertonic graph configuration. Expected mono 44.1 kHz, chunk 3072 and latent width 144.");
            object indexer = MNNModelData.Read(Path.Combine(directory, "mnn_models", "unicode_indexer.json"));
            if (indexer is List<object>)
                _indexer = MNNModelData.Shape(indexer);
            else
            {
                _indexer = Enumerable.Repeat(-1, 65536).ToArray();
                foreach (var pair in MNNModelData.Object(indexer))
                    _indexer[int.Parse(pair.Key)] = MNNModelData.Integer(pair.Value);
            }

            foreach (string name in new[]{"M1", "M2", "F1", "F2"})
            {
                string path = Path.Combine(directory, "voice_styles", name + ".json");
                if (!File.Exists(path))
                    path = Path.Combine(directory, "mnn_models", "voice_styles", name + ".json");
                if (!File.Exists(path))
                    continue;
                var style = MNNModelData.Read(path);
                var t = MNNModelData.Get(style, "style_ttl");
                var d = MNNModelData.Get(style, "style_dp");
                var voice = new Voice{Ttl = MNNModelData.Floats(MNNModelData.Get(t, "data")), Dp = MNNModelData.Floats(MNNModelData.Get(d, "data")), TtlShape = MNNModelData.Shape(MNNModelData.Get(t, "dims")), DpShape = MNNModelData.Shape(MNNModelData.Get(d, "dims"))};
                if (!voice.TtlShape.SequenceEqual(new[]{1, 50, 256}) || voice.Ttl.Length != 12800 || !voice.DpShape.SequenceEqual(new[]{1, 8, 16}) || voice.Dp.Length != 128)
                    throw new InvalidDataException("Unsupported Supertonic voice style: " + name);
                _voices.Add(name, voice);
            }

            if (_voices.Count == 0)
                throw new FileNotFoundException("No Supertonic voice styles found.");
            string graphs = Path.Combine(directory, "mnn_models", precision);
            foreach (string name in new[]{"duration_predictor", "text_encoder", "vector_estimator", "vocoder"})
                if (!File.Exists(Path.Combine(graphs, name + ".mnn")))
                    throw new FileNotFoundException("Missing Supertonic " + precision + " model: " + name);
            try
            {
                _duration = new MNNGenerationGraph(Path.Combine(graphs, "duration_predictor.mnn"), threads, backendType: backendType);
                _text = new MNNGenerationGraph(Path.Combine(graphs, "text_encoder.mnn"), threads, backendType: backendType);
                _estimator = new MNNGenerationGraph(Path.Combine(graphs, "vector_estimator.mnn"), threads, backendType: backendType);
                _vocoder = new MNNGenerationGraph(Path.Combine(graphs, "vocoder.mnn"), threads, backendType: backendType);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public MNNGeneratedAudio Synthesize(string text, string voice = "M1", int steps = 5, float speed = 1, int seed = 42, CancellationToken cancellationToken = default)
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
                string normalized = Normalize(text);
                int[] ids = Encode(normalized, _indexer);
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
                // Clip float output for playback, preserving independent ownership of each result.
                for (int i = 0; i < wave.Length; ++i)
                    wave[i] = Math.Max(-1, Math.Min(1, wave[i]));
                return new MNNGeneratedAudio(wave, _sampleRate, normalized);
            }
        }

        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, string voice = "M1", int steps = 5, float speed = 1, int seed = 42, CancellationToken cancellationToken = default) => Task.Run(() => Synthesize(text, voice, steps, speed, seed, cancellationToken), cancellationToken);
        internal static string Normalize(string text)
        {
            text = Regex.Replace(Regex.Replace(text, @"\s+", " ").Trim(), @"[.!?]+$", ".").Normalize(NormalizationForm.FormKD);
            var result = new StringBuilder();
            const string marks = "\u0302\u0303\u0304\u0305\u0306\u0307\u0308\u030a\u030b\u030c\u0327\u0328\u0329\u032a\u032b\u032c\u032d\u032e\u032f";
            for (int i = 0; i < text.Length; ++i)
            {
                int cp = char.ConvertToUtf32(text, i);
                if (cp > 65535)
                    ++i;
                if (cp >= 0x1f300 && cp <= 0x1faff || cp >= 0x1f1e6 && cp <= 0x1f1ff || cp >= 0x2600 && cp <= 0x27bf || cp <= 65535 && marks.IndexOf((char)cp) >= 0 || cp == 0xa9 || cp == '\\')
                    continue;
                if (cp == 0x2013 || cp == 0x2011 || cp == 0x2014)
                    result.Append('-');
                else if (cp <= 65535 && "¯_[]|/#→←".IndexOf((char)cp) >= 0)
                    result.Append(' ');
                else if (cp == 0x201c || cp == 0x201d)
                    result.Append('"');
                else if (cp == 0x2018 || cp == 0x2019 || cp == 0xb4 || cp == '`')
                    result.Append('\'');
                else
                    result.Append(char.ConvertFromUtf32(cp));
            }

            string value = result.ToString().Replace("@", " at ").Replace("e.g.,", "for example, ").Replace("i.e.,", "that is, ");
            value = Regex.Replace(value, " ([,.!?;:'])", "$1");
            value = Regex.Replace(value, "([\"'])\\1+", "$1");
            value = Regex.Replace(value, @"\s+", " ").Trim();
            if (value.Length == 0)
                throw new ArgumentException("No pronounceable text remains after normalization.");
            if (".!?;:,'\")]}…。」』】〉》›»".IndexOf(value[value.Length - 1]) < 0)
                value += ".";
            return value;
        }

        internal static int[] Encode(string text, int[] indexer)
        {
            var ids = new List<int>();
            foreach (char c in text)
            {
                if (c >= indexer.Length || indexer[c] < 0)
                    throw new NotSupportedException("This Supertonic model has no token for U+" + ((int)c).ToString("X4") + ". Use text supported by its English model vocabulary.");
                ids.Add(indexer[c]);
            }

            return ids.ToArray();
        }

        private void Check()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MNNSupertonic));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _duration?.Dispose();
                _text?.Dispose();
                _estimator?.Dispose();
                _vocoder?.Dispose();
            }
        }
    }
}
