using System;
using System.IO;
using System.Linq;
using System.Threading;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    /// <summary>Managed Sana Edit V2 pipeline for the ModelScope MNN model format.
    /// Uses the bundled official Interpreter/Tensor APIs, with no additional native ABI.</summary>
    public sealed class MNNSana : IDisposable
    {
        private readonly object _gate = new object ();
        private readonly MNNGenerationGraph _llm, _connector, _projector, _dit, _encoder, _decoder;
        private readonly MNNSanaTokenizer _tokenizer;
        private readonly MNNSanaEmbedding _embedding;
        private readonly float[] _queries;
        private bool _disposed;
        /// <summary>Current stage backends; native unsupported operations may still run on CPU.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, MNNBackendType> StageBackends
        {
            get
            {
                lock (_gate)
                {
                    if (_disposed)
                        throw new ObjectDisposedException(nameof(MNNSana));
                    return new System.Collections.Generic.Dictionary<string, MNNBackendType>{{"TextEncoder", _llm.Backend}, {"Connector", _connector.Backend}, {"Projector", _projector.Backend}, {"Transformer", _dit.Backend}, {"VaeEncoder", _encoder.Backend}, {"VaeDecoder", _decoder.Backend}, };
                }
            }
        }

        public static MNNSana Load(string directory, int threads = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16)
                throw new ArgumentOutOfRangeException(nameof(threads));
            return new MNNSana(Path.GetFullPath(directory), threads, backendType);
        }

        private MNNSana(string root, int threads, MNNBackendType backendType)
        {
            var config = MNNModelData.Read(Path.Combine(root, "config.json"));
            if ((string)MNNModelData.Get(config, "model_name") != "Sana-Distill-v2")
                throw new NotSupportedException("Expected MNN-Sana-Edit-V2.");
            // This export produces non-finite encoder/DiT tensors and saturated
            // images with the bundled Metal runtime, including FP32 retries.
            // Keep its coupled pipeline on the validated CPU path until GPU
            // numerical and image-quality regression tests pass.
            if (MNNAcceleration.Resolve(backendType) != MNNBackendType.CPU)
            {
                UnityEngine.Debug.Log("[MNN] Sana Edit V2 uses CPU for numerical compatibility with the bundled MNN build.");
                backendType = MNNBackendType.CPU;
            }

            string llm = Path.Combine(root, "llm");
            _tokenizer = new MNNSanaTokenizer(Path.Combine(llm, "tokenizer.txt"));
            try
            {
                _embedding = new MNNSanaEmbedding(llm);
                _queries = ReadQueries(Path.Combine(llm, "meta_queries.mnn"));
                if (_queries.Length != 256 * 1024)
                    throw new InvalidDataException("Expected Sana meta queries [256,1024].");
                string llmGraph = Path.Combine(llm, "llm.mnn");
                _llm = new MNNGenerationGraph(llmGraph, threads, MNNGraphConstant.FreezeIntInput(File.ReadAllBytes(llmGraph), "logits_index", 0), backendType: backendType);
                _connector = new MNNGenerationGraph(Path.Combine(root, "connector.mnn"), threads, backendType: backendType);
                _projector = new MNNGenerationGraph(Path.Combine(root, "projector.mnn"), threads, backendType: backendType);
                _dit = new MNNGenerationGraph(Path.Combine(root, "transformer.mnn"), threads, backendType: backendType);
                _encoder = new MNNGenerationGraph(Path.Combine(root, "vae_encoder.mnn"), threads, backendType: backendType);
                _decoder = new MNNGenerationGraph(Path.Combine(root, "vae_decoder.mnn"), threads, backendType: backendType);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>Edit a top-left RGB24 image already resized to 512×512.</summary>
        public MNNGeneratedImage Edit(string prompt, byte[] referenceRgb, int steps = 10, int seed = 42, CancellationToken cancellationToken = default, Action<int> progress = null, float guidance = 4.5f)
        {
            if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 1000)
                throw new ArgumentException("Enter an edit instruction of 1–1000 characters.", nameof(prompt));
            if (referenceRgb == null || referenceRgb.Length != 512 * 512 * 3)
                throw new ArgumentException("Sana requires a 512×512 RGB24 reference image.", nameof(referenceRgb));
            if (steps < 2 || steps > 30)
                throw new ArgumentOutOfRangeException(nameof(steps));
            if (float.IsNaN(guidance) || guidance < 1 || guidance > 10)
                throw new ArgumentOutOfRangeException(nameof(guidance));
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNSana));
                cancellationToken.ThrowIfCancellationRequested();
                float[] EncodeContext(string instruction)
                {
                    int[] ids = _tokenizer.Encode("<|im_start|>user\nGenerate an image: " + instruction + "<|im_end|>\n<|im_start|>assistant\n<|vision_start|>");
                    if (ids.Length > 256)
                        throw new ArgumentException("The edit instruction exceeds 256 text tokens.", nameof(prompt));
                    int length = ids.Length + 256;
                    float[] embeddings = new float[length * 1024];
                    Array.Copy(_embedding.Read(ids), embeddings, ids.Length * 1024);
                    Array.Copy(_queries, 0, embeddings, ids.Length * 1024, _queries.Length);
                    // This repository exports explicit KV tensors, not fused Attention ops.
                    // Every invocation starts with a zero-length KV cache; no previous request leaks in.
                    var mask = new float[length * length];
                    for (int i = 0; i < length; ++i)
                        for (int j = i + 1; j < length; ++j)
                            mask[i * length + j] = -float.MaxValue;
                    float[] hidden = _llm.Run("hidden_states", new Input("input_ids", embeddings, length, 1, 1024), new Input("attention_mask", mask, 1, 1, length, length), new Input("position_ids", Enumerable.Range(0, length).ToArray(), 1, length), new Input("past_key_values", Array.Empty<float>(), 28, 2, 1, 0, 8, 128));
                    if (hidden.Length != length * 1024)
                        throw new InvalidDataException("Expected full Sana Qwen3 hidden states.");
                    var queryHidden = new float[256 * 1024];
                    Array.Copy(hidden, ids.Length * 1024, queryHidden, 0, queryHidden.Length);
                    cancellationToken.ThrowIfCancellationRequested();
                    var connector = _connector.Run("connector_out", new Input("llm_out", queryHidden, 1, 256, 1024));
                    var context = _projector.Run("prompt_embeds", new Input("connector_out", connector, 1, 256, 1024));
                    if (context.Length != 256 * 2304)
                        throw new InvalidDataException("Expected Sana context [1,256,2304].");
                    return context;
                }

                int batch = guidance > 1 ? 2 : 1;
                var positive = EncodeContext(prompt);
                var contextBatch = batch == 2 ? EncodeContext("").Concat(positive).ToArray() : positive;
                const int pixels = 512 * 512;
                float[] image = new float[pixels * 3];
                for (int i = 0; i < pixels; ++i)
                    for (int c = 0; c < 3; ++c)
                        image[c * pixels + i] = referenceRgb[i * 3 + c] / 127.5f - 1;
                var reference = _encoder.Run("latent", new Input("image", image, 1, 3, 512, 512));
                if (reference.Length != 32 * 16 * 16)
                    throw new InvalidDataException("Expected Sana reference latent [1,32,16,16].");
                for (int i = 0; i < reference.Length; ++i)
                    reference[i] *= .41407f;
                var refInput = new Input("ref_latents", batch == 2 ? reference.Concat(reference).ToArray() : reference, batch, 32, 16, 16);
                var promptInput = new Input("encoder_hidden_states", contextBatch, batch, 256, 2304);
                var attention = new Input("encoder_attention_mask", Enumerable.Repeat(1f, batch * 256).ToArray(), batch, 256);
                var latent = MNNGenerationMath.Gaussian(reference.Length, seed);
                float[] times = Timesteps(steps);
                for (int step = 0; step < steps; ++step)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var prediction = _dit.Run("noise_pred", new Input("sample", batch == 2 ? latent.Concat(latent).ToArray() : latent, batch, 32, 16, 16), promptInput, new Input("timestep", Enumerable.Repeat(times[step], batch).ToArray(), batch), attention, refInput);
                    if (prediction.Length != latent.Length * batch)
                        throw new InvalidDataException("Sana DiT output shape mismatch.");
                    float dt = ((step + 1 < times.Length ? times[step + 1] : 0) - times[step]) / 1000;
                    for (int i = 0; i < latent.Length; ++i)
                    {
                        float noise = batch == 2 ? prediction[i] + guidance * (prediction[i + latent.Length] - prediction[i]) : prediction[i];
                        latent[i] += noise * dt;
                    }

                    progress?.Invoke((step + 1) * 95 / steps);
                }

                cancellationToken.ThrowIfCancellationRequested();
                for (int i = 0; i < latent.Length; ++i)
                    latent[i] /= .41407f;
                var decoded = _decoder.Run("sample", new Input("latent_sample", latent, 1, 32, 16, 16));
                if (decoded.Length != pixels * 3)
                    throw new InvalidDataException("Expected Sana image [1,3,512,512].");
                var rgb = new byte[decoded.Length];
                for (int i = 0; i < pixels; ++i)
                    for (int c = 0; c < 3; ++c)
                        rgb[i * 3 + c] = (byte)Math.Round(Math.Max(0, Math.Min(1, decoded[c * pixels + i] * .5f + .5f)) * 255);
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Invoke(100);
                return new MNNGeneratedImage(rgb, 512, 512, prompt, seed);
            }
        }

        internal static float[] Timesteps(int steps)
        {
            if (steps < 2 || steps > 30)
                throw new ArgumentOutOfRangeException(nameof(steps));
            return Enumerable.Range(0, steps).Select(i =>
            {
                double sigma = (steps - 1 - i) * .999 / (steps - 1);
                return (float)(3000 * sigma / (1 + 2 * sigma));
            }).ToArray();
        }

        // Official schema: Net.oplists -> Op.main(Blob) -> Blob.float32s.
        // This file contains only the constant meta-query matrix.
        private static float[] ReadQueries(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            int Read(int offset)
            {
                if (offset < 0 || offset > data.Length - 4)
                    throw new InvalidDataException("Truncated MNN constant.");
                return BitConverter.ToInt32(data, offset);
            }

            int Follow(int offset) => checked(offset + Read(offset));
            int Field(int table, int slot)
            {
                int vtable = checked(table - Read(table)), entry = checked(vtable + 4 + slot * 2);
                if (vtable < 0 || entry > data.Length - 2 || vtable > data.Length - 2)
                    throw new InvalidDataException("Invalid MNN constant table.");
                if (4 + slot * 2 >= BitConverter.ToUInt16(data, vtable))
                    throw new InvalidDataException("Missing MNN constant field.");
                int relative = BitConverter.ToUInt16(data, entry);
                if (relative == 0)
                    throw new InvalidDataException("Missing MNN constant field.");
                return checked(table + relative);
            }

            int net = Read(0), ops = Follow(Field(net, 3));
            if (Read(ops) != 1)
                throw new InvalidDataException("Expected one Sana constant op.");
            int op = Follow(ops + 4);
            if (Read(Field(op, 5)) != 11)
                throw new InvalidDataException("Expected Const op.");
            int blob = Follow(Field(op, 2)), shape = Follow(Field(blob, 0));
            if (Read(shape) != 2 || Read(shape + 4) != 256 || Read(shape + 8) != 1024)
                throw new InvalidDataException("Expected meta-query shape [256,1024].");
            int values = Follow(Field(blob, 7)), count = Read(values);
            if (count != 256 * 1024 || values + 4L + count * 4L > data.Length)
                throw new InvalidDataException("Invalid meta-query values.");
            var result = new float[count];
            Buffer.BlockCopy(data, values + 4, result, 0, count * 4);
            if (result.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                throw new InvalidDataException("Non-finite meta queries.");
            return result;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _llm?.Dispose();
                _connector?.Dispose();
                _projector?.Dispose();
                _dit?.Dispose();
                _encoder?.Dispose();
                _decoder?.Dispose();
                _embedding?.Dispose();
            }
        }
    }
}
