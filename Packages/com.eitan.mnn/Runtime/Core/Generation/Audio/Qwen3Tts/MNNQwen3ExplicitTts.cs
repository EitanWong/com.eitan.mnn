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
    /// <summary>Qwen3-TTS explicit-KV export with its bundled prepared reference voice.
    /// Requires the package's bounded macOS Mono MNN ABI contract.</summary>
    public sealed class MNNQwen3ExplicitTts : IDisposable
    {
        public static string RecommendedRepository => MNNVerifiedTtsModel.Qwen3ExplicitRepository;

        private readonly object _gate = new object();
        private readonly string _root;
        private readonly int _threads;
        private readonly MNNSpeechGraphs _graphs;
        private readonly MNNSpeechTokenizer _tokenizer;
        private readonly MNNHalfTable _text, _codec;
        private readonly MNNHalfTable[] _embeddings = new MNNHalfTable[15];
        private readonly MNNSingleInputModule _fc1, _fc2, _head;
        private readonly MNNSingleInputModule[] _heads = new MNNSingleInputModule[15];
        private readonly float[] _pad;
        private MNNQwen3TtsVoice _voice;
        private string _voiceName;
        private bool _disposed;

        internal static bool HasLayout(string root) => !string.IsNullOrWhiteSpace(root) &&
            File.Exists(Path.Combine(root, "talker_step.mnn")) && File.Exists(Path.Combine(root, "cp_step.mnn"));

        internal static bool IsComplete(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return false;
            try
            {
                // This frontend implements one pinned graph/tensor format. Model Manager
                // additionally verifies SHA-256; discovery checks sizes without rehashing GBs.
                foreach (var file in MNNVerifiedTtsModel.Qwen3ExplicitFiles())
                {
                    if (file.Path == "README.md" || file.Path == "weights/meta.json" || file.Path.StartsWith("voices/", StringComparison.Ordinal))
                        continue;
                    var info = new FileInfo(Path.Combine(root, file.Path));
                    if (!info.Exists || info.Length != file.Bytes) return false;
                }
                return InstalledVoices(root).Length > 0;
            }
            catch (InvalidDataException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (ArgumentException) { return false; }
        }

        /// <summary>Names of valid prepared voice folders under voices/. Raw WAV files are not voice profiles.</summary>
        public static string[] InstalledVoices(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return Array.Empty<string>();
            string root = Path.Combine(directory, "voices");
            try
            {
                if (!Directory.Exists(root)) return Array.Empty<string>();
                return Directory.GetDirectories(root).Where(path =>
                {
                    try { MNNQwen3TtsVoice.Read(path); return true; }
                    catch (InvalidDataException) { return false; }
                    catch (IOException) { return false; }
                    catch (UnauthorizedAccessException) { return false; }
                    catch (ArgumentException) { return false; }
                }).Select(Path.GetFileName).OrderBy(name => name == "demo" ? 0 : 1)
                    .ThenBy(name => name, StringComparer.Ordinal).ToArray();
            }
            catch (IOException) { return Array.Empty<string>(); }
            catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
        }

        private void SelectVoice(string voice)
        {
            if (voice == null) voice = InstalledVoices(_root).FirstOrDefault();
            // Only a folder name is accepted; no relative or absolute paths.
            if (string.IsNullOrWhiteSpace(voice) || voice == "." || voice == ".." ||
                voice.IndexOfAny(new[]{'/', '\\', ':'}) >= 0)
                throw new ArgumentException("Choose an installed prepared Qwen voice.", nameof(voice));
            if (voice == _voiceName) return;
            var prepared = MNNQwen3TtsVoice.Read(Path.Combine(_root, "voices", voice));
            _voice = prepared;
            _voiceName = voice;
        }

        /// <param name="cacheCodePredictorShapes">Keep fifteen CPU sessions to reduce repeated shape preparation.
        /// This substantially increases resident memory and cold startup cost; disabled by default.</param>
        public static MNNQwen3ExplicitTts Load(string directory, int threads = 4, bool cacheCodePredictorShapes = false)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16) throw new ArgumentOutOfRangeException(nameof(threads));
            return new MNNQwen3ExplicitTts(Path.GetFullPath(directory), threads, cacheCodePredictorShapes);
        }

        private MNNQwen3ExplicitTts(string root, int threads, bool cacheCodePredictor = false)
        {
            _root = root; _threads = threads;
            try
            {
                if (!IsComplete(root)) throw new InvalidDataException("Incomplete Qwen3 explicit KV model and prepared voice assets.");
                _graphs = new MNNSpeechGraphs(root, threads, false, CancellationToken.None, cacheCodePredictor);
                _tokenizer = new MNNSpeechTokenizer(Path.Combine(root, "tokenizer"));
                string weights = Path.Combine(root, "weights");
                _text = new MNNHalfTable(Path.Combine(weights, "text_embedding.bin"), 151936, 2048);
                _codec = new MNNHalfTable(Path.Combine(weights, "codec_embedding.bin"), 3072, 1024);
                _fc1 = Projection("text_proj_fc1_w", "text_proj_fc1_b", 2048, 2048);
                _fc2 = Projection("text_proj_fc2_w", "text_proj_fc2_b", 2048, 1024);
                _head = Projection("codec_head", null, 1024, 3072);
                for (int i = 0; i < 15; ++i)
                {
                    _embeddings[i] = new MNNHalfTable(Path.Combine(weights, "cp_embed_" + i + ".bin"), 2048, 1024);
                    _heads[i] = Projection("cp_head_" + i, null, 1024, 2048);
                }
                SelectVoice(null);
                _pad = Project(new[]{151671});
            }
            catch { Dispose(); throw; }
        }

        private MNNSingleInputModule Projection(string name, string biasName, int channels, int outputs)
        {
            string weights = Path.Combine(_root, "weights");
            float[] matrix = MNNHalfTable.Load(Path.Combine(weights, name + ".bin"), outputs, channels);
            float[] bias = biasName == null ? new float[outputs] : MNNHalfTable.Load(Path.Combine(weights, biasName + ".bin"), 1, outputs);
            return new MNNSingleInputModule("", "input", MNNGraphLinearHead.OutputName, _threads, MNNBackendType.CPU,
                MNNGraphLinearHead.Create(matrix, bias, channels), MNNPrecisionMode.High);
        }

        private float[] Project(int[] ids)
        {
            var values = new float[ids.Length * 2048];
            for (int i = 0; i < ids.Length; ++i) Array.Copy(_text.Row(ids[i]), 0, values, i * 2048, 2048);
            float[] hidden = _fc1.Run(values, ids.Length, 2048);
            for (int i = 0; i < hidden.Length; ++i) hidden[i] = (float)(hidden[i] / (1 + Math.Exp(-hidden[i])));
            return _fc2.Run(hidden, ids.Length, 2048);
        }

        private static float[] Add(float[] a, float[] b)
        {
            if (a.Length != b.Length) throw new InvalidDataException("Invalid Qwen embedding shape.");
            var result = new float[a.Length];
            for (int i = 0; i < result.Length; ++i) result[i] = a[i] + b[i];
            return result;
        }
        private float[] Codec(int[] frame)
        {
            float[] value = _codec.Row(frame[0]);
            for (int group = 1; group < 16; ++group)
            {
                float[] row = _embeddings[group - 1].Row(frame[group]);
                for (int i = 0; i < 1024; ++i) value[i] += row[i];
            }
            return value;
        }
        private float[][] Prefill(string text, int language, out float[][] trailing)
        {
            int[] ids = new[]{151644}.Concat(_tokenizer.EncodeBare("assistant\n")).Concat(_tokenizer.EncodeBare(text))
                .Concat(new[]{151645}).Concat(_tokenizer.EncodeBare("\n")).Concat(new[]{151644}).Concat(_tokenizer.EncodeBare("assistant\n")).ToArray();
            var rows = new List<float[]>(); float[] role = Project(ids.Take(3).ToArray());
            for (int i = 0; i < 3; ++i) rows.Add(role.Skip(i * 1024).Take(1024).ToArray());
            float[] bos = Project(new[]{151672});
            int[] prefix = {2154, 2156, language, 2157};
            foreach (int id in prefix) rows.Add(Add(_pad, _codec.Row(id)));
            rows.Add(Add(_pad, _voice.Speaker)); rows.Add(Add(bos, _codec.Row(2148)));
            int[] iclIds = _voice.TextIds.Skip(3).Take(_voice.TextIds.Length - 5).Concat(ids.Skip(3).Take(ids.Length - 8)).ToArray();
            float[] projected = Project(iclIds).Concat(Project(new[]{151673})).ToArray();
            int textFrames = projected.Length / 1024, codecFrames = _voice.Codes.Length / 16 + 1;
            var iclText = new float[textFrames][];
            for (int i = 0; i < textFrames; ++i) iclText[i] = projected.Skip(i * 1024).Take(1024).ToArray();
            for (int i = 0; i < codecFrames; ++i)
            {
                float[] codec = i == 0 ? _codec.Row(2149) : Codec(_voice.Codes.Skip((i - 1) * 16).Take(16).ToArray());
                rows.Add(Add(i < textFrames ? iclText[i] : _pad, codec));
            }
            trailing = textFrames > codecFrames ? iclText.Skip(codecFrames).ToArray() : new[]{_pad};
            return rows.ToArray();
        }

        private static Output[] Step(MNNSpeechGraphs graphs, string graph, float[] values, int position, Output kv, int layers)
        {
            int length = values.Length / 1024;
            var result = graphs.Run(graph, new[]{"last_hidden", "new_kv"}, new Input("inputs_embeds", values, 1, length, 1024),
                new Input("position_ids", Enumerable.Range(position, length).ToArray(), 1, length),
                kv == null ? new Input("past_kv", Array.Empty<float>(), layers, 1, 8, 0, 128) : kv.AsInput("past_kv"));
            if (result[0].Values.Length < 1024 || result[0].Values.Length % 1024 != 0 ||
                !result[1].Shape.SequenceEqual(new[]{layers, 1, 8, position + length, 128}))
                throw new InvalidDataException("Unexpected Qwen step output shape.");
            return result;
        }
        private static float[] Last(Output output) => output.Values.Skip(output.Values.Length - 1024).ToArray();

        private static int Sample(float[] logits, Random rng, bool codec, bool suppressEnd, HashSet<int> history)
        {
            var order = new List<int>();
            for (int i = 0; i < logits.Length; ++i)
            {
                if (float.IsNaN(logits[i]) || float.IsInfinity(logits[i])) throw new InvalidDataException("Non-finite Qwen logits.");
                if (codec && i >= 2048 && (i != 2150 || suppressEnd)) continue;
                if (history != null && history.Contains(i)) logits[i] = logits[i] > 0 ? logits[i] / 1.05f : logits[i] * 1.05f;
                order.Add(i);
            }
            order.Sort((a, b) => logits[b].CompareTo(logits[a]));
            int count = Math.Min(50, order.Count); var weights = new double[count]; double total = 0;
            for (int i = 0; i < count; ++i) total += weights[i] = Math.Exp((logits[order[i]] - logits[order[0]]) / .9);
            double draw = rng.NextDouble() * total;
            for (int i = 0; i < count; ++i) { draw -= weights[i]; if (draw <= 0) return order[i]; }
            return order[count - 1];
        }

        /// <summary>Synthesizes Chinese or English with a prepared voice (demo by default), at 24 kHz mono.
        /// The export does not include a raw-WAV voice encoder. A frame limit without EOS is an error.</summary>
        public MNNGeneratedAudio Synthesize(string text, string language = "zh", int seed = 42, int maxFrames = 360, CancellationToken cancellationToken = default, string voice = null)
        {
            MNNSpeechDsp.ValidateText(text, 200);
            if (language != "zh" && language != "en") throw new ArgumentException("Validated Qwen speech languages are zh and en.", nameof(language));
            if (maxFrames < 2 || maxFrames > 360) throw new ArgumentOutOfRangeException(nameof(maxFrames));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNQwen3ExplicitTts));
                cancellationToken.ThrowIfCancellationRequested();
                SelectVoice(voice);
                float[][] prefill = Prefill(text, language == "zh" ? 2055 : 2050, out var trailing);
                var graphs = _graphs;
                {
                    var step = Step(graphs, "talker_step", prefill.SelectMany(row => row).ToArray(), 0, null, 56);
                    float[] hidden = Last(step[0]); Output kv = step[1];
                    var codes = new List<int>(); var history = new HashSet<int>(); var rng = new Random(seed); bool ended = false;
                    for (int t = 0; t < maxFrames; ++t)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int g0 = Sample(_head.Run(hidden, 1, 1024), rng, true, t < 2, history);
                        if (g0 == 2150) { ended = true; break; }
                        history.Add(g0);
                        var cp = Step(graphs, "cp_step", hidden.Concat(_codec.Row(g0)).ToArray(), 0, null, 10);
                        var frame = new int[16]; frame[0] = g0;
                        for (int i = 0; i < 15; ++i)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            frame[i + 1] = Sample(_heads[i].Run(Last(cp[0]), 1, 1024), rng, false, false, null);
                            if (i < 14) cp = Step(graphs, "cp_step", _embeddings[i].Row(frame[i + 1]), i + 2, cp[1], 10);
                        }
                        codes.AddRange(frame);
                        var embedding = Add(Codec(frame), t < trailing.Length ? trailing[t] : _pad);
                        step = Step(graphs, "talker_step", embedding, prefill.Length + t, kv, 56);
                        hidden = Last(step[0]); kv = step[1];
                    }
                    if (!ended) throw new InvalidDataException("Qwen speech reached its frame limit without EOS; truncated speech is not accepted.");
                    cancellationToken.ThrowIfCancellationRequested();
                    var pcm = graphs.Run("vocoder", "wav", new Input("codes", codes.ToArray(), 1, codes.Count / 16, 16)).Values;
                    cancellationToken.ThrowIfCancellationRequested();
                    return MNNSpeechDsp.Result(pcm, 24000, text);
                }
            }
        }

        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, string language = "zh", int seed = 42, int maxFrames = 360, CancellationToken cancellationToken = default, string voice = null) =>
            Task.Run(() => Synthesize(text, language, seed, maxFrames, cancellationToken, voice), cancellationToken);

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return; _disposed = true;
                _text?.Dispose(); _codec?.Dispose(); _fc1?.Dispose(); _fc2?.Dispose(); _head?.Dispose();
                _graphs?.Dispose();
                foreach (var item in _embeddings) item?.Dispose();
                foreach (var item in _heads) item?.Dispose();
            }
        }
    }
}
