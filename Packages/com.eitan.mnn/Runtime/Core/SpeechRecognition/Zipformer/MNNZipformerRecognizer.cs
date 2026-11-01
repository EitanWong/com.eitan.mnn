using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Zipformer v1 greedy ASR for the 2023-02-20 bilingual and 2023-02-21 English graph protocols.</summary>
    public sealed class MNNZipformerRecognizer : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNInterpreter _encoder;
        private readonly MNNSession _session;
        private readonly MNNSingleInputModule _decoder;
        private readonly MNNGenerationGraph _joiner;
        private readonly Dictionary<string, int[]> _shapes = new Dictionary<string, int[]>();
        private readonly Dictionary<string, int[]> _integerStates = new Dictionary<string, int[]>();
        private readonly Dictionary<string, float[]> _floatStates = new Dictionary<string, float[]>();
        private readonly string[] _tokens;
        private readonly int _unknown;
        private bool _disposed;
        public MNNBackendType Backend => _session.ActualBackend;

        private MNNZipformerRecognizer(string directory, MNNZipformerConfig profile, int threads, MNNBackendType backend)
        {
            var config = profile.Clone();
            _tokens = ReadTokens(MNNZipformerConfig.Resolve(directory, config.tokens), out _unknown);
            MNNZipformerModel.Verify(directory, config, _tokens.Length == 6254);
            int[] layers = {2, 4, 3, 2, 4}, context = {64, 32, 16, 8, 32};
            _shapes["x"] = new[] {1, 39, 80};
            for (int i = 0; i < layers.Length; ++i)
            {
                AddState("cached_len_" + i, true, layers[i], 1);
                AddState("cached_avg_" + i, false, layers[i], 1, 384);
                AddState("cached_key_" + i, false, layers[i], context[i], 1, 192);
                AddState("cached_val_" + i, false, layers[i], context[i], 1, 96);
                AddState("cached_val2_" + i, false, layers[i], context[i], 1, 96);
                AddState("cached_conv1_" + i, false, layers[i], 1, 384, 30);
                AddState("cached_conv2_" + i, false, layers[i], 1, 384, 30);
            }
            // Measured CPU whole-recording latency was lower than Metal for both pinned profiles.
            var selected = backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend;
            try
            {
                _encoder = MNNInterpreter.CreateFromFile(MNNZipformerConfig.Resolve(directory, config.encoder));
                _session = _encoder.CreateSession(new MNNSessionConfig {BackendType = selected, ThreadCount = threads});
                _session.ResizeInputs(_shapes);
                _decoder = new MNNSingleInputModule(MNNZipformerConfig.Resolve(directory, config.decoder), "y", "decoder_out", threads, selected);
                _joiner = new MNNGenerationGraph(MNNZipformerConfig.Resolve(directory, config.joiner), threads, backendType: selected);
            }
            catch
            {
                _joiner?.Dispose();
                _decoder?.Dispose();
                _session?.Dispose();
                _encoder?.Dispose();
                throw;
            }
        }

        private void AddState(string name, bool integer, params int[] shape)
        {
            _shapes[name] = shape;
            int size = shape.Aggregate(1, (a, b) => checked(a * b));
            if (integer) _integerStates[name] = new int[size];
            else _floatStates[name] = new float[size];
        }

        public static MNNZipformerRecognizer Load(string directory, MNNZipformerConfig config = null,
            int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (!Enum.IsDefined(typeof(MNNBackendType), backendType)) throw new ArgumentOutOfRangeException(nameof(backendType));
            return new MNNZipformerRecognizer(Path.GetFullPath(directory), config ?? MNNZipformerConfig.Read(directory), threadCount, backendType);
        }

        /// <summary>Recognize finite normalized mono PCM at 8–96 kHz, up to ten minutes. Silence is valid.</summary>
        public MNNSpeechRecognition Recognize(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
        {
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples == null || samples.Length == 0 || samples.Length > (long)sampleRate * 600)
                throw new ArgumentException("Expected nonempty mono audio of at most ten minutes.", nameof(samples));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNZipformerRecognizer));
                cancellationToken.ThrowIfCancellationRequested();
                for (int i = 0; i < samples.Length; ++i)
                {
                    if ((i & 16383) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                        throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(samples));
                }
                var pcm = MNNSpeechDsp.Resample(samples, sampleRate, 16000, cancellationToken);
                Array.Resize(ref pcm, checked(pcm.Length + 16000)); // Flush delayed tokens and the final partial chunk.
                var features = MNNZipformerFeatures.Extract(pcm, out int frames, cancellationToken);
                foreach (var state in _integerStates)
                {
                    Array.Clear(state.Value, 0, state.Value.Length);
                    _session.GetInput(state.Key).CopyFromArray(state.Value);
                }
                foreach (var state in _floatStates)
                {
                    Array.Clear(state.Value, 0, state.Value.Length);
                    _session.GetInput(state.Key).CopyFromArray(state.Value);
                }
                var ids = new List<int>();
                var times = new List<float>();
                var text = new StringBuilder();
                var context = new[] {-1, 0};
                var decoder = _decoder.Run(context, 1, 2);
                if (decoder.Length != 512) throw new InvalidDataException("Expected 512-wide Zipformer decoder output.");
                var chunk = new float[39 * 80];
                var frame = new float[512];
                var encoderInput = new MNNGenerationGraph.Input("encoder_out", frame, 1, 512);
                int offset = 0;
                for (int start = 0; start + 39 < frames; start += 32)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Array.Copy(features, start * 80, chunk, 0, chunk.Length);
                    _session.GetInput("x").CopyFromArray(chunk);
                    _session.Run();
                    var encoded = _session.GetOutput("encoder_out").CopyToArray<float>();
                    if (!_session.GetOutput("encoder_out").Shape.SequenceEqual(new[] {1, 8, 512}) ||
                        encoded.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                        throw new InvalidDataException("Expected finite [1,8,512] Zipformer encoder output.");
                    // Copy state before the next Run can overwrite the native outputs.
                    foreach (var state in _integerStates)
                    {
                        var tensor = _session.GetOutput("new_" + state.Key);
                        if (!tensor.Shape.SequenceEqual(_shapes[state.Key])) throw new InvalidDataException("Invalid Zipformer state: " + state.Key);
                        CopyState(tensor, state.Value);
                    }
                    foreach (var state in _floatStates)
                    {
                        var tensor = _session.GetOutput("new_" + state.Key);
                        if (!tensor.Shape.SequenceEqual(_shapes[state.Key])) throw new InvalidDataException("Invalid Zipformer state: " + state.Key);
                        CopyState(tensor, state.Value);
                        if (state.Value.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw new InvalidDataException("Non-finite Zipformer state: " + state.Key);
                    }
                    foreach (var state in _integerStates) _session.GetInput(state.Key).CopyFromArray(state.Value);
                    foreach (var state in _floatStates) _session.GetInput(state.Key).CopyFromArray(state.Value);
                    for (int f = 0; f < 8; ++f)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Array.Copy(encoded, f * 512, frame, 0, 512);
                        var logits = _joiner.Run("logit", encoderInput, new MNNGenerationGraph.Input("decoder_out", decoder, 1, 512));
                        if (logits.Length != _tokens.Length) throw new InvalidDataException("Zipformer vocabulary and joiner output disagree.");
                        int best = 0;
                        for (int k = 1; k < logits.Length; ++k) if (logits[k] > logits[best]) best = k;
                        if (best == 0 || best == _unknown) continue;
                        ids.Add(best);
                        times.Add((offset + f) * .04f);
                        text.Append(_tokens[best]);
                        context[0] = context[1]; context[1] = best;
                        decoder = _decoder.Run(context, 1, 2);
                        if (decoder.Length != 512) throw new InvalidDataException("Invalid Zipformer decoder output.");
                    }
                    offset += 8;
                }
                return new MNNSpeechRecognition(text.ToString().Replace("▁", " ").Trim(), ids.ToArray(), times.ToArray());
            }
        }

        /// <summary>Read a mono/stereo PCM16/24/32 or float32 RIFF WAV and transcribe it.</summary>
        public MNNSpeechRecognition RecognizeWaveFile(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var samples = MNNWaveAudio.Read(path, out int sampleRate);
            return Recognize(samples, sampleRate, cancellationToken);
        }

        public Task<MNNSpeechRecognition> RecognizeAsync(float[] samples, int sampleRate, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Recognize(copy, sampleRate, cancellationToken), cancellationToken);
        }

        internal static string[] ReadTokens(string path, out int unknown)
        {
            var symbols = new Dictionary<int, string>();
            foreach (string line in File.ReadAllLines(path, new UTF8Encoding(false, true)))
            {
                int split = line.LastIndexOf(' ');
                if (split <= 0 || !int.TryParse(line.Substring(split + 1), out int id) || id < 0 || id > 100000 || symbols.ContainsKey(id))
                    throw new InvalidDataException("Invalid Zipformer token table.");
                symbols[id] = line.Substring(0, split);
            }
            // Kaldi disambiguation symbols follow the network's vocabulary and are not logits.
            int size = symbols.Where(pair => !pair.Value.StartsWith("#", StringComparison.Ordinal)).Max(pair => pair.Key) + 1;
            if (size != 500 && size != 6254 || !symbols.TryGetValue(0, out string blank) || blank != "<blk>")
                throw new InvalidDataException("Expected the tested English/bilingual Zipformer v1 vocabulary.");
            var result = new string[size];
            unknown = -1;
            for (int i = 0; i < size; ++i)
            {
                if (!symbols.TryGetValue(i, out result[i]) || result[i].StartsWith("#", StringComparison.Ordinal))
                    throw new InvalidDataException("Non-contiguous Zipformer vocabulary.");
                if (result[i] == "<unk>") unknown = i;
            }
            return result;
        }

        private static void CopyState<T>(MNNTensor tensor, T[] target) where T : unmanaged
        {
            var source = tensor.MapForRead<T>();
            try
            {
                source.CopyTo(target);
            }
            finally
            {
                tensor.Unmap();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _joiner.Dispose(); _decoder.Dispose(); _session.Dispose(); _encoder.Dispose();
            }
        }
    }
}
