using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    /// <summary>Single-speaker Tamil MMS VITS exports with their character frontend.</summary>
    public sealed class MNNMmsTamil : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<char, int> _tokens = new Dictionary<char, int>();
        private readonly string _path;
        private readonly byte[] _graph;
        private readonly int _threads;
        private MNNSingleInputModule _module;
        private int _length;
        private float _speed;
        private bool _disposed;
        public int SampleRate => 16000;
        internal static readonly string[] Precisions = {"int8", "fp16_int8", "fp32"};
        public static string[] InstalledPrecisions(string directory) => Precisions.Where(precision => File.Exists(Path.Combine(directory, "model_" + precision + ".mnn"))).ToArray();

        internal static bool IsComplete(string directory) => File.Exists(Path.Combine(directory, "mnn_mms_tamil.json"))
            && File.Exists(Path.Combine(directory, "model_int8.mnn")) && File.Exists(Path.Combine(directory, "tokens.txt"));

        public static MNNMmsTamil Load(string directory, int threads = 4, string precision = "int8")
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16) throw new ArgumentOutOfRangeException(nameof(threads));
            if (!Precisions.Contains(precision, StringComparer.Ordinal)) throw new ArgumentException("Choose int8, fp16_int8 or fp32.", nameof(precision));
            return new MNNMmsTamil(Path.GetFullPath(directory), threads, precision);
        }

        private MNNMmsTamil(string directory, int threads, string precision)
        {
            _path = Path.Combine(directory, "model_" + precision + ".mnn");
            _graph = File.ReadAllBytes(_path);
            _threads = threads;
            var values = MNNSerializedGraph.Metadata(_graph);
            if (!values.TryGetValue("model_type", out string type) || type != "vits" ||
                !values.TryGetValue("comment", out string comment) || comment != "mms" ||
                !values.TryGetValue("add_blank", out string blank) || blank != "1" ||
                !values.TryGetValue("frontend", out string frontend) || frontend != "characters" ||
                !values.TryGetValue("sample_rate", out string rate) || rate != "16000")
                throw new NotSupportedException("Unsupported MMS graph metadata.");
            foreach (string line in File.ReadAllLines(Path.Combine(directory, "tokens.txt")))
            {
                int separator = line.LastIndexOf(' ');
                if (separator != 1 || !int.TryParse(line.Substring(separator + 1), out int id) || id < 0 || id >= 58)
                    throw new InvalidDataException("Invalid MMS Tamil character vocabulary.");
                _tokens.Add(line[0], id);
            }
            if (_tokens.Count != 59 || !_tokens.TryGetValue('3', out int pad) || pad != 0 || !_tokens.TryGetValue(' ', out int space) || space != 7)
                throw new NotSupportedException("Unsupported MMS Tamil token IDs.");
        }

        internal static int[] Encode(string text, IReadOnlyDictionary<char, int> vocabulary)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 300)
                throw new ArgumentException("Enter 1–300 Tamil characters for MMS.", nameof(text));
            var ids = new List<int>();
            foreach (char c in text.ToLowerInvariant())
            {
                if (char.IsPunctuation(c) && !vocabulary.ContainsKey(c)) continue;
                char token = char.IsWhiteSpace(c) ? ' ' : c;
                if (!vocabulary.TryGetValue(token, out int id))
                    throw new NotSupportedException("No MMS Tamil token for U+" + ((int)c).ToString("X4") + ".");
                if (token == ' ' && (ids.Count == 0 || ids[ids.Count - 1] == 7)) continue;
                ids.Add(id);
            }
            if (ids.Count > 0 && ids[ids.Count - 1] == 7) ids.RemoveAt(ids.Count - 1);
            if (ids.Count == 0 || !text.Any(char.IsLetterOrDigit)) throw new ArgumentException("MMS requires pronounceable Tamil text.", nameof(text));
            var result = new int[ids.Count * 2 + 1];
            for (int i = 0; i < ids.Count; ++i) result[2 * i + 1] = ids[i];
            return result;
        }

        public MNNGeneratedAudio Synthesize(string text, float speed = 1, CancellationToken cancellationToken = default)
        {
            if (float.IsNaN(speed) || speed < .5f || speed > 2) throw new ArgumentOutOfRangeException(nameof(speed));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNMmsTamil));
                cancellationToken.ThrowIfCancellationRequested();
                int[] ids = Encode(text, _tokens);
                if (_module == null || _length != ids.Length || _speed != speed)
                {
                    _module?.Dispose();
                    _module = null;
                    byte[] prepared = MNNGraphConstant.FreezeInputs(_graph,
                        new Input("x_length", new[]{ids.Length}, 1), new Input("noise_scale", new[]{.667f}, 1),
                        new Input("length_scale", new[]{1 / speed}, 1), new Input("noise_scale_w", new[]{.8f}, 1));
                    _module = new MNNSingleInputModule(_path, "x", "y", _threads, MNNBackendType.CPU, prepared, MNNPrecisionMode.High);
                    _length = ids.Length;
                    _speed = speed;
                }
                float[] wave = _module.Run(ids, 1, ids.Length);
                cancellationToken.ThrowIfCancellationRequested();
                if (wave.Length < SampleRate / 10 || wave.Length > SampleRate * 30 || wave.Any(v => float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 2))
                    throw new InvalidDataException("MMS Tamil returned an invalid waveform.");
                float peak = wave.Max(v => Math.Abs(v));
                if (peak < .001f) throw new InvalidDataException("MMS Tamil returned silence.");
                if (peak > 1) for (int i = 0; i < wave.Length; ++i) wave[i] /= peak;
                return new MNNGeneratedAudio(wave, SampleRate, text);
            }
        }

        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, float speed = 1, CancellationToken cancellationToken = default)
            => Task.Run(() => Synthesize(text, speed, cancellationToken), cancellationToken);

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _module?.Dispose();
                _module = null;
            }
        }
    }
}
