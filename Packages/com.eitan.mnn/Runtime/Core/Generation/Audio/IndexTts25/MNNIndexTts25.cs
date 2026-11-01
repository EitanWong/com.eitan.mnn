// Managed implementation of IndexTTS-2.5's published inference pipeline.
// Reference: yunfengwang/IndexTTS-2.5-mnn; model terms: Bilibili IndexTTS. See LICENSE.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using Input = MNN.Unity.MNNGenerationGraph.Input;
using Output = MNN.Unity.MNNGenerationGraph.Output;

namespace MNN.Unity
{
    /// <summary>IndexTTS-2.5 fp16 weights with FP32 CPU compute and a supplied reference voice.</summary>
    public sealed class MNNIndexTts25 : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _root;
        private readonly int _threads;
        private readonly MNNSpeechTokenizer _tokenizer;
        private readonly float[] _projection, _bias;
        private bool _disposed;
        private static readonly string[] GptOutputs = new[]{"logits"}.Concat(Enumerable.Range(0, 24).SelectMany(i => new[]{"present_kv_" + i + "_k", "present_kv_" + i + "_v"})).ToArray();
        private MNNIndexTts25(string root, int threads)
        {
            _root = root; _threads = threads; _tokenizer = new MNNSpeechTokenizer(root, true);
            using (var zip = ZipFile.OpenRead(Path.Combine(root, "spk_proj.npz")))
            {
                _projection = ReadProjection(zip, "weight.npy", 1280, 192);
                _bias = ReadProjection(zip, "bias.npy", 1280);
            }
        }

        public static MNNIndexTts25 Load(string directory, int threads = 4)
        {
            if (threads < 1 || threads > 64) throw new ArgumentOutOfRangeException(nameof(threads));
            MNNReferenceSpeechModel.Validate(directory, true);
            return new MNNIndexTts25(Path.GetFullPath(directory), threads);
        }

        /// <summary>Plain Chinese/English text, at most 120 BPE tokens; write numbers as spoken words. Reference: 0.5–15 seconds.</summary>
        public MNNGeneratedAudio Synthesize(string text, MNNReferenceAudio reference, string language = "zh", int seed = 42, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNIndexTts25));
                cancellationToken.ThrowIfCancellationRequested();
                if (reference == null) throw new ArgumentNullException(nameof(reference));
                var ids = _tokenizer.Encode(text, language);
                using (var graphs = new MNNSpeechGraphs(_root, _threads, true, cancellationToken))
                {
                    var audio = MNNSpeechDsp.Resample(reference.Samples, reference.SampleRate, 22050, cancellationToken);
                    var audio16 = MNNSpeechDsp.Resample(audio, 22050, 16000, cancellationToken);
                    var features = MNNSpeechDsp.Fbank(audio16, true, out int featureFrames, cancellationToken);
                    var mask = Enumerable.Repeat(1, featureFrames).ToArray();
                    int originalFrames = (audio16.Length - 400) / 160 + 1;
                    if (originalFrames % 2 != 0) mask[mask.Length - 1] = 0;
                    var semantic = graphs.Run("semantic_model", "semantic_feat", new Input("input_features", features, 1, featureFrames, 160), new Input("attention_mask", mask, 1, featureFrames));
                    graphs.Release("semantic_model");
                    var emotion = graphs.Run("emo_vec", "emo_vec", semantic.AsInput("feat"), new Input("ilens", new[]{semantic.Shape[1]}, 1));
                    graphs.Release("emo_vec");
                    var fbank = MNNSpeechDsp.Fbank(audio16, false, out int fbankFrames, cancellationToken);
                    var style = graphs.Run("campplus", "spk_emb", new Input("fbank", fbank, 1, fbankFrames, 80));
                    graphs.Release("campplus");
                    if (style.Values.Length != 192 || emotion.Values.Length != 1280) throw new InvalidDataException("Unexpected IndexTTS speaker shape.");
                    var conds = new float[3 * 1280];
                    for (int row = 0; row < 1280; ++row)
                    {
                        double sum = _bias[row] + emotion.Values[row];
                        for (int col = 0; col < 192; ++col) sum += (double)_projection[row * 192 + col] * style.Values[col];
                        conds[row] = (float)sum;
                    }
                    var mel = MNNSpeechDsp.Mel22050(audio, out int promptFrames, cancellationToken);
                    var promptCondition = Regulate(graphs, semantic, promptFrames);
                    var output = graphs.Run("gpt_prefill", GptOutputs, new Input("text_ids", ids, 1, ids.Length), new Input("conds", conds, 1, 3, 1280), new Input("lang_id", new[]{language == "zh" ? 1 : 0}, 1));
                    graphs.Release("gpt_prefill");
                    var codes = new List<int>(); var seen = new HashSet<int>{1, 8192};
                    bool stopped = false;
                    for (int i = 0; i < 1500; ++i)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int selected = Select(output[0].Values, seen);
                        if (selected == 8193) { stopped = true; break; }
                        if (selected >= 8192) throw new InvalidDataException("IndexTTS emitted a reserved audio code.");
                        codes.Add(selected); seen.Add(selected);
                        var inputs = new List<Input>{new Input("token", new[]{selected}, 1, 1), new Input("pos_idx", new[]{i + 2}, 1)};
                        for (int layer = 0; layer < 24; ++layer)
                        {
                            inputs.Add(output[1 + 2 * layer].AsInput("past_kv_" + layer + "_k"));
                            inputs.Add(output[2 + 2 * layer].AsInput("past_kv_" + layer + "_v"));
                        }
                        output = graphs.Run("gpt_step", GptOutputs, inputs.ToArray());
                    }
                    graphs.Release("gpt_step");
                    if (!stopped || codes.Count == 0) throw new InvalidDataException("IndexTTS did not return a complete speech sequence within 1500 codes.");
                    var latent = graphs.Run("semantic_codec_decode", "latent", new Input("codes", codes.ToArray(), 1, codes.Count));
                    graphs.Release("semantic_codec_decode");
                    int targetFrames = (int)(latent.Shape[1] * 1.72);
                    if (targetFrames < 1 || targetFrames > 2580) throw new InvalidDataException("Invalid IndexTTS generated duration.");
                    var generatedCondition = Regulate(graphs, latent, targetFrames); graphs.Release("length_regulator");
                    var condition = MNNSpeechDsp.Join(promptCondition.Values, generatedCondition.Values);
                    int total = promptFrames + targetFrames;
                    var x = MNNGenerationMath.Gaussian(80 * total, seed);
                    var promptX = new float[2 * 80 * total];
                    for (int bin = 0; bin < 80; ++bin)
                    {
                        Array.Clear(x, bin * total, promptFrames);
                        Array.Copy(mel, bin * promptFrames, promptX, bin * total, promptFrames);
                    }
                    var styleBatch = MNNSpeechDsp.Join(style.Values, new float[192]);
                    var conditionBatch = MNNSpeechDsp.Join(condition, new float[condition.Length]);
                    for (int step = 0; step < 25; ++step)
                    {
                        float time = step / 25f, dt = (step + 1) / 25f - time;
                        var v = graphs.Run("cfm_estimator", "v", new Input("x", MNNSpeechDsp.Join(x, x), 2, 80, total), new Input("prompt_x", promptX, 2, 80, total), new Input("x_lens", new[]{total}, 1), new Input("t", new[]{time, time}, 2), new Input("style", styleBatch, 2, 192), new Input("cond", conditionBatch, 2, total, 512)).Values;
                        if (v.Length != x.Length * 2) throw new InvalidDataException("Unexpected IndexTTS flow shape.");
                        for (int i = 0; i < x.Length; ++i) x[i] = i % total < promptFrames ? 0 : x[i] + dt * (1.7f * v[i] - .7f * v[x.Length + i]);
                    }
                    graphs.Release("cfm_estimator");
                    var finalMel = new float[80 * targetFrames];
                    for (int bin = 0; bin < 80; ++bin) Array.Copy(x, bin * total + promptFrames, finalMel, bin * targetFrames, targetFrames);
                    var wave = graphs.Run("bigvgan", "wav", new Input("mel", finalMel, 1, 80, targetFrames)).Values;
                    if (wave.Length < targetFrames * 256) throw new InvalidDataException("IndexTTS vocoder returned too few samples.");
                    Array.Resize(ref wave, targetFrames * 256);
                    return MNNSpeechDsp.Result(wave, 22050, text);
                }
            }
        }

        private static Output Regulate(MNNSpeechGraphs graphs, Output x, int length) => graphs.Run("length_regulator", "out", x.AsInput("x"), new Input("template", new[]{1, length, 1}));

        internal static int Select(float[] logits, HashSet<int> seen)
        {
            if (logits == null || logits.Length != 8194) throw new InvalidDataException("Unexpected IndexTTS logits.");
            int best = 0; float score = float.NegativeInfinity;
            for (int i = 0; i < logits.Length; ++i)
            {
                float value = logits[i];
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite IndexTTS logits.");
                if (seen.Contains(i)) value = value < 0 ? value * 10 : value / 10;
                if (value > score) { best = i; score = value; }
            }
            return best;
        }

        internal static float[] ReadProjection(ZipArchive zip, string name, params int[] shape)
        {
            int count = shape.Aggregate(1, (a, b) => checked(a * b));
            var entry = zip.GetEntry(name);
            if (entry == null || entry.Length > count * 4L + 65536) throw new InvalidDataException("Missing or invalid IndexTTS projection.");
            using (var stream = entry.Open())
            using (var reader = new BinaryReader(stream))
            {
                if (!reader.ReadBytes(6).SequenceEqual(new byte[]{147, 78, 85, 77, 80, 89})) throw new InvalidDataException("Invalid NumPy header.");
                byte major = reader.ReadByte(); reader.ReadByte();
                int size = major == 1 ? reader.ReadUInt16() : major == 2 ? checked((int)reader.ReadUInt32()) : throw new InvalidDataException("Unsupported NumPy version.");
                if (size > 65536) throw new InvalidDataException("Oversized NumPy header.");
                string header = Encoding.ASCII.GetString(reader.ReadBytes(size));
                if (!header.Contains("'<f4'") || !header.Contains("'fortran_order': False") || entry.Length != 8 + (major == 1 ? 2 : 4) + size + count * 4L) throw new InvalidDataException("Expected little-endian float32 NumPy projection.");
                var match = Regex.Match(header, @"'shape'\s*:\s*\(([^)]*)\)");
                var dimensions = match.Success ? match.Groups[1].Value.Split(',').Select(x => x.Trim()).Where(x => x.Length != 0).ToArray() : Array.Empty<string>();
                if (!dimensions.SequenceEqual(shape.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture))))
                    throw new InvalidDataException("Unexpected IndexTTS projection shape.");
                var values = new float[count];
                for (int i = 0; i < count; ++i)
                {
                    values[i] = reader.ReadSingle();
                    if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) throw new InvalidDataException("Non-finite projection.");
                }
                return values;
            }
        }

        public void Dispose() { lock (_gate) _disposed = true; }
    }
}
