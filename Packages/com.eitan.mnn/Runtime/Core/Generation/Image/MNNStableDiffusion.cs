using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    /// <summary>Managed SD 1.5 accelerator-first pipeline for the legacy MNN general model repository.
    /// Calls the official Interpreter/Tensor APIs through the supported macOS Mono ABI.</summary>
    public sealed class MNNStableDiffusion : IDisposable
    {
        private readonly object _gate = new object ();
        private readonly MNNGenerationGraph _text, _unet, _vae;
        private readonly MNNClipTokenizer _tokenizer;
        private bool _disposed;
        /// <summary>Current stage backends; native unsupported operations may still run on CPU.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, MNNBackendType> StageBackends
        {
            get
            {
                lock (_gate)
                {
                    if (_disposed)
                        throw new ObjectDisposedException(nameof(MNNStableDiffusion));
                    return new System.Collections.Generic.Dictionary<string, MNNBackendType>{{"TextEncoder", _text.Backend}, {"UNet", _unet.Backend}, {"VaeDecoder", _vae.Backend}, };
                }
            }
        }

        public static MNNStableDiffusion Load(string directory, int threads = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16)
                throw new ArgumentOutOfRangeException(nameof(threads));
            directory = Path.GetFullPath(directory);
            if (Directory.Exists(Path.Combine(directory, "general")))
                directory = Path.Combine(directory, "general");
            return new MNNStableDiffusion(directory, threads, backendType);
        }

        private MNNStableDiffusion(string directory, int threads, MNNBackendType backendType)
        {
            foreach (string stage in new[]{"text_encoder", "unet", "vae_decoder"})
                foreach (string suffix in new[]{".mnn", ".mnn.weight"})
                    if (!File.Exists(Path.Combine(directory, stage + suffix)))
                        throw new FileNotFoundException("Missing SD 1.5 general model: " + stage + suffix);
            _tokenizer = new MNNClipTokenizer(directory);
            try
            {
                _text = new MNNGenerationGraph(Path.Combine(directory, "text_encoder.mnn"), threads, backendType: backendType);
                _unet = new MNNGenerationGraph(Path.Combine(directory, "unet.mnn"), threads, backendType: backendType);
                _vae = new MNNGenerationGraph(Path.Combine(directory, "vae_decoder.mnn"), threads, backendType: backendType);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public MNNGeneratedImage Generate(string prompt, string negativePrompt = "", int steps = 20, float guidance = 7.5f, int seed = 42, CancellationToken cancellationToken = default, Action<int> progress = null)
        {
            if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 2000 || (negativePrompt?.Length ?? 0) > 2000)
                throw new ArgumentException("Enter a prompt of 1–2000 characters.", nameof(prompt));
            if (steps < 2 || steps > 50)
                throw new ArgumentOutOfRangeException(nameof(steps));
            if (float.IsNaN(guidance) || guidance < 1 || guidance > 20)
                throw new ArgumentOutOfRangeException(nameof(guidance));
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNStableDiffusion));
                cancellationToken.ThrowIfCancellationRequested();
                int[] ids = _tokenizer.Encode(negativePrompt ?? "").Concat(_tokenizer.Encode(prompt)).ToArray();
                var embeds = _text.Run("last_hidden_state", new Input("input_ids", ids, 2, 77));
                if (embeds.Length != 2 * 77 * 768)
                    throw new InvalidDataException("Expected SD 1.5 CLIP embedding [2,77,768].");
                var context = new Input("encoder_hidden_states", embeds, 2, 77, 768);
                float[] latent = MNNGenerationMath.Gaussian(4 * 64 * 64, seed);
                var scheduler = new MNNPlmsScheduler(steps);
                float[] batch = new float[latent.Length * 2];
                float[] noise = new float[latent.Length];
                for (int i = 0; i < steps; ++i)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Array.Copy(latent, batch, latent.Length);
                    Array.Copy(latent, 0, batch, latent.Length, latent.Length);
                    var prediction = _unet.Run("out_sample", new Input("sample", batch, 2, 4, 64, 64), new Input("timestep", new[]{scheduler.Timesteps[i]}, 1), context);
                    if (prediction.Length != batch.Length)
                        throw new InvalidDataException("Expected SD 1.5 UNet prediction [2,4,64,64].");
                    for (int n = 0; n < noise.Length; ++n)
                        noise[n] = prediction[n] + guidance * (prediction[n + noise.Length] - prediction[n]);
                    latent = scheduler.Step(latent, noise, i);
                    progress?.Invoke((i + 1) * 95 / steps);
                }

                cancellationToken.ThrowIfCancellationRequested();
                for (int i = 0; i < latent.Length; ++i)
                    latent[i] /= .18215f;
                var decoded = _vae.Run("sample", new Input("latent_sample", latent, 1, 4, 64, 64));
                const int pixels = 512 * 512;
                if (decoded.Length != pixels * 3)
                    throw new InvalidDataException("Expected SD 1.5 VAE output [1,3,512,512].");
                var rgb = new byte[decoded.Length];
                for (int i = 0; i < pixels; ++i)
                    for (int c = 0; c < 3; ++c)
                        rgb[i * 3 + c] = (byte)Math.Round(Math.Max(0, Math.Min(1, decoded[c * pixels + i] * .5f + .5f)) * 255);
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Invoke(100);
                return new MNNGeneratedImage(rgb, 512, 512, prompt, seed);
            }
        }

        public Task<MNNGeneratedImage> GenerateAsync(string prompt, string negativePrompt = "", int steps = 20, float guidance = 7.5f, int seed = 42, CancellationToken cancellationToken = default, Action<int> progress = null) => Task.Run(() => Generate(prompt, negativePrompt, steps, guidance, seed, cancellationToken, progress), cancellationToken);
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _text?.Dispose();
                _unet?.Dispose();
                _vae?.Dispose();
            }
        }
    }
}
