using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;
using Output = MNN.Unity.MNNGenerationGraph.Output;

namespace MNN.Unity
{
    /// <summary>MOSS Nano fixed sampled-frame export with publisher-supplied reference codes.</summary>
    public sealed class MNNMossNano : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _root, _codecRoot;
        private readonly bool _cached;
        private readonly int _threads;
        private readonly MNNMossTokenizer _tokenizer;
        private readonly Dictionary<string, int[][]> _voices = new Dictionary<string, int[][]>(StringComparer.Ordinal);
        private readonly int[] _prefix, _afterReference, _assistant;
        private bool _disposed;
        private static readonly string[] Outputs = new[]{"global_hidden"}.Concat(Enumerable.Range(0, 12).SelectMany(i => new[]{"present_key_" + i, "present_value_" + i})).ToArray();
        public string[] Voices => _voices.Keys.ToArray();
        private static string TtsRoot(string directory) => File.Exists(Path.Combine(directory, "tts", "tokenizer.model")) ? Path.Combine(directory, "tts") : directory;
        public static string[] InstalledVoices(string directory) => MNNModelData.Array(MNNModelData.Get(MNNModelData.Read(Path.Combine(TtsRoot(directory), "browser_poc_manifest.json")), "builtin_voices"))
            .Select(voice => (string)MNNModelData.Get(voice, "voice")).ToArray();
        internal static bool IsComplete(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return false;
            string root = TtsRoot(directory);
            bool cached = root != directory;
            return new[]{cached ? "moss_tts_prefill_nocumsum.mnn" : "moss_tts_prefill.mnn", "moss_tts_decode_step.mnn", cached ? "moss_tts_local_cached_step.mnn" : "moss_tts_local_fixed_sampled_frame.mnn", cached ? "tokenizer.model" : "tokenizer_mnn.txt", "browser_poc_manifest.json"}
                .All(file => File.Exists(Path.Combine(root, file))) && File.Exists(Path.Combine(cached ? Path.Combine(directory, "audio_tokenizer") : root, "moss_audio_tokenizer_decode_full.mnn"));
        }

        public static MNNMossNano Load(string directory, int threads = 4)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16) throw new ArgumentOutOfRangeException(nameof(threads));
            return new MNNMossNano(Path.GetFullPath(directory), threads);
        }

        private MNNMossNano(string root, int threads)
        {
            if (!IsComplete(root)) throw new InvalidDataException("Incomplete MOSS Nano stages or tokenizer.");
            _root = TtsRoot(root); _cached = _root != root; _threads = threads;
            _codecRoot = _cached ? Path.Combine(root, "audio_tokenizer") : root;
            _tokenizer = new MNNMossTokenizer(Path.Combine(_root, _cached ? "tokenizer.model" : "tokenizer_mnn.txt"));
            var manifest = MNNModelData.Read(Path.Combine(_root, "browser_poc_manifest.json"));
            var config = MNNModelData.Get(manifest, "tts_config");
            if (MNNModelData.Integer(MNNModelData.Get(config, "n_vq")) != 16 || MNNModelData.Integer(MNNModelData.Get(config, "vocab_size")) != 16384)
                throw new NotSupportedException("Unsupported MOSS Nano codebook configuration.");
            foreach (var voice in MNNModelData.Array(MNNModelData.Get(manifest, "builtin_voices")))
            {
                string name = (string)MNNModelData.Get(voice, "voice");
                var frames = MNNModelData.Array(MNNModelData.Get(voice, "prompt_audio_codes")).Select(row => MNNModelData.Array(row).Select(MNNModelData.Integer).ToArray()).ToArray();
                if (frames.Length < 1 || frames.Length > 300 || frames.Any(row => row.Length != 16 || row.Any(code => code < 0 || code > 1023)))
                    throw new InvalidDataException("Invalid MOSS reference voice codes.");
                _voices.Add(name, frames);
            }
            var template = MNNModelData.Get(manifest, "prompt_templates");
            _prefix = ReadIds(template, "user_prompt_prefix_token_ids");
            _afterReference = ReadIds(template, "user_prompt_after_reference_token_ids");
            _assistant = ReadIds(template, "assistant_prompt_prefix_token_ids");
        }

        private static int[] ReadIds(object parent, string key)
        {
            int[] ids = MNNModelData.Array(MNNModelData.Get(parent, key)).Select(MNNModelData.Integer).ToArray();
            if (ids.Length == 0 || ids.Any(id => id < 0 || id >= 16384)) throw new InvalidDataException("Invalid MOSS prompt token IDs.");
            return ids;
        }

        public MNNGeneratedAudio Synthesize(string text, string voice = "Junhao", int seed = 1234, int maxFrames = 375, CancellationToken cancellationToken = default)
        {
            if (maxFrames < 1 || maxFrames > 375) throw new ArgumentOutOfRangeException(nameof(maxFrames));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNMossNano));
                cancellationToken.ThrowIfCancellationRequested();
                if (!_voices.TryGetValue(voice, out var reference)) throw new ArgumentException("Select an installed MOSS voice.", nameof(voice));
                int[] tokens = _tokenizer.Encode(text);
                var rows = new List<int>();
                AddText(rows, _prefix.Concat(new[]{6}));
                foreach (var frame in reference) { rows.Add(8); rows.AddRange(frame); }
                AddText(rows, new[]{7}.Concat(_afterReference).Concat(tokens).Concat(_assistant).Concat(new[]{6}));
                int length = rows.Count / 17;
                var random = new Random(seed);
                var codes = new List<int>();
                var seen = new int[16 * 1024];
                bool stopped = false;
                using (var graphs = new MNNSpeechGraphs(_root, _threads, false, cancellationToken))
                {
                    string prefill = _cached ? "moss_tts_prefill_nocumsum" : "moss_tts_prefill";
                    var promptInputs = new List<Input>{new Input("input_ids", rows.ToArray(), 1, length, 17), new Input("attention_mask", Enumerable.Repeat(1, length).ToArray(), 1, length)};
                    if (_cached) promptInputs.Add(new Input("position_ids", Enumerable.Range(0, length).ToArray(), 1, length));
                    var state = graphs.Run(prefill, Outputs, promptInputs.ToArray());
                    graphs.Release(prefill);
                    for (int frame = 0; frame < maxFrames; ++frame)
                    {
                        var hidden = LastHidden(state[0]);
                        Output[] sample;
                        if (_cached) sample = SampleCachedFrame(graphs, hidden, seen, random);
                        else sample = graphs.Run("moss_tts_local_fixed_sampled_frame", new[]{"should_continue", "frame_token_ids"},
                            new Input("global_hidden", hidden, 1, 768), new Input("repetition_seen_mask", seen, 1, 16, 1024),
                            new Input("assistant_random_u", new[]{Uniform(random)}, 1), new Input("audio_random_u", Enumerable.Range(0, 16).Select(_ => Uniform(random)).ToArray(), 1, 16));
                        if (sample[0].Values.Length != 1 || sample[1].Values.Length != 16) throw new InvalidDataException("Invalid MOSS sampled frame shape.");
                        if (sample[0].Values[0] == 0) { stopped = true; break; }
                        var next = new int[17]; next[0] = 9;
                        for (int channel = 0; channel < 16; ++channel)
                        {
                            float value = sample[1].Values[channel]; int code = (int)value;
                            if (code != value || code < 0 || code > 1023) throw new InvalidDataException("Invalid MOSS audio token.");
                            codes.Add(code); seen[channel * 1024 + code] = 1; next[channel + 1] = code;
                        }
                        if (frame + 1 == maxFrames) break;
                        var inputs = new List<Input>{new Input("input_ids", next, 1, 1, 17), new Input("past_valid_lengths", new[]{length + frame}, 1)};
                        for (int i = 1; i < state.Length; ++i) inputs.Add(state[i].AsInput(Outputs[i].Replace("present_", "past_")));
                        state = graphs.Run("moss_tts_decode_step", Outputs, inputs.ToArray());
                    }
                }
                if (!stopped || codes.Count == 0) throw new InvalidDataException("MOSS did not finish a speech sequence within the frame limit.");
                cancellationToken.ThrowIfCancellationRequested();
                int frames = codes.Count / 16;
                string path = Path.Combine(_codecRoot, "moss_audio_tokenizer_decode_full.mnn");
                byte[] graph = MNNGraphConstant.FreezeInputs(File.ReadAllBytes(path), new Input("audio_code_lengths", new[]{frames}, 1));
                float[] stereo;
                using (var decoder = new MNNSingleInputModule(path, "audio_codes", "audio", _threads, MNNBackendType.CPU, graph, MNNPrecisionMode.High))
                    stereo = decoder.Run(codes.ToArray(), 1, frames, 16);
                int samples = frames * 3840;
                if (stereo.Length != samples * 2) throw new InvalidDataException("Unexpected MOSS stereo waveform length.");
                var mono = new float[samples];
                for (int i = 0; i < samples; ++i) mono[i] = .5f * (stereo[i] + stereo[samples + i]);
                cancellationToken.ThrowIfCancellationRequested();
                return MNNSpeechDsp.Result(mono, 48000, text);
            }
        }

        private static Output[] SampleCachedFrame(MNNSpeechGraphs graphs, float[] hidden, int[] seen, Random random)
        {
            var empty = new Output(new float[0], new[]{1, 0, 12, 64});
            Output[] state = {empty, empty};
            int length = 0;
            Func<int, int, int, int, Output[]> run = (text, audio, channel, step) =>
            {
                var values = graphs.Run("moss_tts_local_cached_step", new[]{"text_logits", "audio_logits", "local_present_key_0", "local_present_value_0"},
                    new Input("global_hidden", hidden, 1, 768), new Input("text_token_id", new[]{text}, 1), new Input("audio_token_id", new[]{audio}, 1),
                    new Input("channel_index", new[]{channel}, 1), new Input("step_type", new[]{step}, 1), new Input("past_valid_lengths", new[]{length}, 1),
                    state[0].AsInput("local_past_key_0"), state[1].AsInput("local_past_value_0"));
                state = new[]{values[2], values[3]}; ++length; return values;
            };
            var first = run(0, 0, 0, 0);
            if (first[0].Values.Length != 16384) throw new InvalidDataException("Invalid MOSS text logits.");
            int nextText = Sample(new[]{first[0].Values[9], first[0].Values[7]}, 1, 2, 1, random) == 0 ? 9 : 7;
            if (nextText != 9) return new[]{new Output(new[]{0f}, new[]{1}), new Output(new float[16], new[]{1, 16})};
            var result = new float[16];
            var audioState = run(9, 0, 0, 1);
            for (int channel = 0; channel < 16; ++channel)
            {
                if (channel > 0) audioState = run(0, (int)result[channel - 1], channel - 1, 2);
                var logits = audioState[1].Values;
                if (logits.Length != 16 * 1024) throw new InvalidDataException("Invalid MOSS audio logits.");
                var scores = new float[1024];
                for (int code = 0; code < 1024; ++code)
                {
                    float value = logits[channel * 1024 + code];
                    scores[code] = seen[channel * 1024 + code] == 0 ? value : value < 0 ? value * 1.2f : value / 1.2f;
                }
                result[channel] = Sample(scores, .8f, 25, .95f, random);
            }
            return new[]{new Output(new[]{1f}, new[]{1}), new Output(result, new[]{1, 16})};
        }

        private static int Sample(float[] logits, float temperature, int topK, float topP, Random random)
        {
            int[] order = Enumerable.Range(0, logits.Length).OrderByDescending(i => logits[i]).ThenBy(i => i).ToArray();
            float threshold = logits[order[Math.Min(topK, order.Length) - 1]];
            double max = logits[order[0]] / temperature, total = 0;
            var probabilities = new double[logits.Length];
            foreach (int i in order)
                if (logits[i] >= threshold) { probabilities[i] = Math.Exp(logits[i] / temperature - max); total += probabilities[i]; }
            if (!(total > 0) || double.IsInfinity(total)) throw new InvalidDataException("Invalid MOSS sampling distribution.");
            double cumulative = 0, retained = 0;
            foreach (int i in order)
            {
                if (cumulative > topP) probabilities[i] = 0;
                else { cumulative += probabilities[i] / total; retained += probabilities[i]; }
            }
            double target = random.NextDouble() * retained;
            for (int i = 0; i < probabilities.Length; ++i) { target -= probabilities[i]; if (target < 0) return i; }
            return order[0];
        }

        private static float Uniform(Random random) => Math.Min(.99999994f, (float)random.NextDouble());
        private static float[] LastHidden(Output output)
        {
            if (output.Values.Length < 768 || output.Values.Length % 768 != 0) throw new InvalidDataException("Invalid MOSS global hidden state.");
            var last = new float[768]; Array.Copy(output.Values, output.Values.Length - 768, last, 0, 768); return last;
        }
        private static void AddText(List<int> rows, IEnumerable<int> tokens)
        {
            foreach (int token in tokens) { rows.Add(token); for (int i = 0; i < 16; ++i) rows.Add(1024); }
        }
        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, string voice = "Junhao", int seed = 1234, int maxFrames = 375, CancellationToken cancellationToken = default) => Task.Run(() => Synthesize(text, voice, seed, maxFrames, cancellationToken), cancellationToken);
        public void Dispose() { lock (_gate) _disposed = true; }
    }
}
