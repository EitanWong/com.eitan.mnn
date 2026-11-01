using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    /// <summary>DhVaani v1.1 Hindi/English reference speech using the bounded macOS Mono MNN ABI.</summary>
    // Managed signal processing follows the publisher's reference; see LICENSE.md.
    public sealed class MNNDhVaani : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _root;
        private readonly int _threads;
        private readonly Dictionary<string, int> _tokens = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly float[] _filters, _melWindow, _window;
        private readonly byte[] _vocoderGraph;
        private MNNSingleInputModule _flow, _vocoder;
        private bool _disposed;

        internal static bool IsComplete(string root) => new[]{"text_encoder_int8.mnn", "fm_decoder_int8.mnn", "vocoder_backbone.mnn", "mel_fb.bin", "vocos_head.bin", "tokens.txt", "model.json"}
            .All(name => File.Exists(Path.Combine(root, name)));

        public static MNNDhVaani Load(string directory, int threads = 4)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16) throw new ArgumentOutOfRangeException(nameof(threads));
            return new MNNDhVaani(Path.GetFullPath(directory), threads);
        }

        private MNNDhVaani(string root, int threads)
        {
            if (!IsComplete(root)) throw new InvalidDataException("Incomplete DhVaani v1.1 assets.");
            var config = MNNModelData.Read(Path.Combine(root, "model.json"));
            if (MNNModelData.Integer(MNNModelData.Get(MNNModelData.Get(config, "model"), "feat_dim")) != 100 ||
                MNNModelData.Integer(MNNModelData.Get(MNNModelData.Get(config, "feature"), "sampling_rate")) != 24000)
                throw new NotSupportedException("Unsupported DhVaani model configuration.");
            _root = root; _threads = threads;
            using (var reader = new BinaryReader(File.OpenRead(Path.Combine(root, "mel_fb.bin"))))
            {
                ExpectHeader(reader, "DVMF", new[]{1, 1024, 256, 100, 513, 100});
                _filters = ReadFloats(reader, 513 * 100); _melWindow = ReadFloats(reader, 1024);
                if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Unexpected DhVaani mel asset size.");
            }
            using (var reader = new BinaryReader(File.OpenRead(Path.Combine(root, "vocos_head.bin"))))
            {
                ExpectHeader(reader, "DVVH", new[]{1, 1026, 512, 1024, 256, 1024});
                var projection = ReadFloats(reader, 1026 * 512); var bias = ReadFloats(reader, 1026); _window = ReadFloats(reader, 1024);
                _vocoderGraph = MNNGraphLinearHead.Append(File.ReadAllBytes(Path.Combine(root, "vocoder_backbone.mnn")), "hidden", projection, bias, 512);
                if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Unexpected DhVaani vocoder asset size.");
            }
            foreach (string line in File.ReadLines(Path.Combine(root, "tokens.txt")))
            {
                int tab = line.LastIndexOf('\t');
                if (tab <= 0 || !int.TryParse(line.Substring(tab + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id < 0)
                    throw new InvalidDataException("Invalid DhVaani vocabulary.");
                _tokens.Add(line.Substring(0, tab), id);
            }
        }

        private static void ExpectHeader(BinaryReader reader, string magic, int[] values)
        {
            if (System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)) != magic || values.Any(value => reader.ReadInt32() != value))
                throw new InvalidDataException("Unsupported DhVaani binary asset header.");
        }
        private static float[] ReadFloats(BinaryReader reader, int count)
        {
            byte[] bytes = reader.ReadBytes(checked(count * 4));
            if (bytes.Length != count * 4) throw new InvalidDataException("Truncated DhVaani asset.");
            var values = new float[count]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            if (values.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw new InvalidDataException("Non-finite DhVaani asset.");
            return values;
        }

        private int[] Encode(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 500) throw new ArgumentException("Supply 1–500 speech characters.", nameof(text));
            text = text.Trim();
            if (!";:,.!?；：，。！？".Contains(text[text.Length - 1])) text += ".";
            var ids = new List<int>();
            for (int i = 0; i < text.Length; ++i)
            {
                int width = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                string piece = text.Substring(i, width); i += width - 1;
                if (!_tokens.TryGetValue(piece, out int id)) throw new ArgumentException("Unsupported DhVaani character: " + piece, nameof(text));
                ids.Add(id);
            }
            return ids.ToArray();
        }

        public MNNGeneratedAudio Synthesize(string text, MNNReferenceAudio reference, string referenceText,
            int steps = 8, float speed = 1, int seed = 666, CancellationToken cancellationToken = default)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (steps < 1 || steps > 32) throw new ArgumentOutOfRangeException(nameof(steps));
            if (float.IsNaN(speed) || speed < .5f || speed > 2) throw new ArgumentOutOfRangeException(nameof(speed));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNDhVaani));
                cancellationToken.ThrowIfCancellationRequested();
                int[] tokens = Encode(text), promptTokens = Encode(referenceText);
                float[] prompt = MNNSpeechDsp.Resample(reference.Samples, reference.SampleRate, 24000, cancellationToken);
                double rms = Math.Sqrt(prompt.Average(v => (double)v * v));
                if (rms < .1) for (int i = 0; i < prompt.Length; ++i) prompt[i] *= (float)(.1 / rms);
                float[] features = Mel(prompt, out int promptFrames, cancellationToken);
                string path = Path.Combine(_root, "text_encoder_int8.mnn");
                byte[] graph = MNNGraphConstant.FreezeInputs(File.ReadAllBytes(path), new Input("prompt_tokens", promptTokens, 1, promptTokens.Length),
                    new Input("prompt_features_len", (Array)new[]{promptFrames}, Array.Empty<int>()), new Input("speed", new[]{speed}));
                float[] textCondition;
                using (var encoder = new MNNSingleInputModule(path, "tokens", "text_condition", _threads, MNNBackendType.CPU, graph, MNNPrecisionMode.High))
                    textCondition = encoder.Run(tokens, 1, tokens.Length);
                if (textCondition.Length % 100 != 0) throw new InvalidDataException("Invalid DhVaani text conditioning.");
                int frames = textCondition.Length / 100;
                if (frames <= promptFrames || frames - promptFrames > 2812) throw new InvalidDataException("DhVaani speech exceeds the 30 second limit or has no generated frames.");
                var speechCondition = new float[textCondition.Length]; Array.Copy(features, speechCondition, features.Length);
                path = Path.Combine(_root, "fm_decoder_int8.mnn");
                if (_flow == null)
                {
                    graph = MNNGraphPackedInputs.Pack(File.ReadAllBytes(path), new[]{3, 2, 100},
                        new MNNGraphPackedInputs.Slice("t", new[]{0, 0, 0}, new[]{1, 1, 1}),
                        new MNNGraphPackedInputs.Slice("x", new[]{0, 1, 0}, new[]{1, -1, 100}, 1, -1, 100),
                        new MNNGraphPackedInputs.Slice("text_condition", new[]{1, 1, 0}, new[]{1, -1, 100}, 1, -1, 100),
                        new MNNGraphPackedInputs.Slice("speech_condition", new[]{2, 1, 0}, new[]{1, -1, 100}, 1, -1, 100),
                        new MNNGraphPackedInputs.Slice("guidance_scale", new[]{1, 0, 0}, new[]{1, 1, 1}));
                    _flow = new MNNSingleInputModule(path, MNNGraphPackedInputs.InputName, "v", _threads, MNNBackendType.CPU, graph, MNNPrecisionMode.High);
                }
                var x = MNNGenerationMath.Gaussian(textCondition.Length, seed);
                int block = (frames + 1) * 100;
                var packed = new float[block * 3]; packed[block] = 1;
                Array.Copy(textCondition, 0, packed, block + 100, textCondition.Length);
                Array.Copy(speechCondition, 0, packed, block * 2 + 100, speechCondition.Length);
                for (int step = 0; step < steps; ++step)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    float u = (float)step / steps, nextU = (float)(step + 1) / steps;
                    float time = .5f * u / (1 - .5f * u), nextTime = .5f * nextU / (1 - .5f * nextU);
                    packed[0] = time; Array.Copy(x, 0, packed, 100, x.Length);
                    float[] velocity = _flow.Run(packed, 3, frames + 1, 100);
                    if (velocity.Length != x.Length) throw new InvalidDataException("Invalid DhVaani flow output.");
                    for (int i = 0; i < x.Length; ++i) x[i] += velocity[i] * (nextTime - time);
                }
                int generated = frames - promptFrames;
                var mels = new float[generated * 100];
                for (int t = 0; t < generated; ++t)
                    for (int channel = 0; channel < 100; ++channel) mels[channel * generated + t] = x[(promptFrames + t) * 100 + channel] / .1f;
                path = Path.Combine(_root, "vocoder_backbone.mnn");
                float[] hidden;
                if (_vocoder == null)
                    _vocoder = new MNNSingleInputModule(path, "mels", MNNGraphLinearHead.OutputName, _threads, MNNBackendType.CPU, _vocoderGraph, MNNPrecisionMode.High);
                hidden = _vocoder.Run(mels, 1, 100, generated);
                float[] pcm = Decode(hidden, cancellationToken);
                if (rms < .1) for (int i = 0; i < pcm.Length; ++i) pcm[i] *= (float)(rms / .1);
                return MNNSpeechDsp.Result(pcm, 24000, text);
            }
        }

        private float[] Mel(float[] pcm, out int frames, CancellationToken token)
        {
            frames = (pcm.Length + 128) / 256;
            var result = new float[frames * 100]; var re = new double[1024]; var im = new double[1024]; var mag = new double[513];
            for (int t = 0; t < frames; ++t)
            {
                token.ThrowIfCancellationRequested(); Array.Clear(im, 0, im.Length);
                for (int k = 0; k < 1024; ++k)
                {
                    int at = Math.Min(t, pcm.Length / 256) * 256 + k - 512;
                    re[k] = at < 0 || at >= pcm.Length ? 0 : pcm[at] * _melWindow[k];
                }
                MNNSpeechDsp.Fft(re, im, false);
                for (int k = 0; k < 513; ++k) mag[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                for (int m = 0; m < 100; ++m)
                {
                    double sum = 0;
                    for (int k = 0; k < 513; ++k) sum += mag[k] * _filters[k * 100 + m];
                    result[t * 100 + m] = (float)(Math.Log(Math.Max(1e-7, sum)) * .1);
                }
            }
            return result;
        }

        private float[] Decode(float[] projected, CancellationToken token)
        {
            if (projected.Length == 0 || projected.Length % 1026 != 0) throw new InvalidDataException("Invalid DhVaani vocoder output.");
            int frames = projected.Length / 1026, size = (frames - 1) * 256 + 1024;
            var sum = new double[size]; var envelope = new double[size];
            var re = new double[1024]; var im = new double[1024];
            for (int t = 0; t < frames; ++t)
            {
                token.ThrowIfCancellationRequested();
                for (int f = 0; f < 513; ++f)
                {
                    double mag = Math.Exp(Math.Min(projected[t * 1026 + f], Math.Log(100))), phase = projected[t * 1026 + 513 + f];
                    re[f] = mag * Math.Cos(phase); im[f] = mag * Math.Sin(phase);
                    if (f > 0 && f < 512) { re[1024 - f] = re[f]; im[1024 - f] = -im[f]; }
                }
                MNNSpeechDsp.Fft(re, im, true);
                for (int i = 0; i < 1024; ++i)
                {
                    sum[t * 256 + i] += re[i] * _window[i]; envelope[t * 256 + i] += _window[i] * _window[i];
                }
            }
            var pcm = new float[size - 768];
            for (int i = 0; i < pcm.Length; ++i) pcm[i] = (float)(sum[i + 384] / Math.Max(1e-11, envelope[i + 384]));
            return pcm;
        }

        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, MNNReferenceAudio reference, string referenceText,
            int steps = 8, float speed = 1, int seed = 666, CancellationToken cancellationToken = default)
            => Task.Run(() => Synthesize(text, reference, referenceText, steps, speed, seed, cancellationToken), cancellationToken);
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _flow?.Dispose(); _flow = null;
                _vocoder?.Dispose(); _vocoder = null;
            }
        }
    }
}
