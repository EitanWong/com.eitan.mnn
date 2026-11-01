using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Image and text vectors in the same 512-dimensional MobileCLIP space.</summary>
    public sealed class MNNImageEmbedding : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _vision, _text;
        private readonly MNNClipTokenizer _tokenizer;
        private bool _disposed;
        public int Dimension => 512;
        public MNNBackendType ImageBackend => _vision.Backend;
        public MNNBackendType TextBackend => _text.Backend;
        private MNNImageEmbedding(string directory, int threads, MNNBackendType backend)
        {
            MNNImageEmbeddingModel.Verify(directory);
            _tokenizer = new MNNClipTokenizer(directory, "tokenizer.json", 0, false);
            _vision = new MNNGenerationGraph(Path.Combine(directory, "vision_model.mnn"), threads, backendType: backend);
            try { _text = new MNNGenerationGraph(Path.Combine(directory, "text_model.mnn"), threads, backendType: backend); }
            catch { _vision.Dispose(); throw; }
        }
        public static MNNImageEmbedding Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNImageEmbedding(Path.GetFullPath(directory), threadCount, backendType);
        }
        public float[] EncodeImage(MNNRgbImage image, CancellationToken cancellationToken = default)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            lock (_gate)
            {
                ThrowIfDisposed();
                var pixels = Preprocess(image, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var vector = Normalize(_vision.Run("image_embeds", new MNNGenerationGraph.Input("pixel_values", pixels, 1, 3, 256, 256)));
                cancellationToken.ThrowIfCancellationRequested();
                return vector;
            }
        }
        // CLIPFeatureExtractor: resize shorter edge to 256 with bilinear sampling,
        // center crop, RGB /255. Integer resize/crop choices follow Transformers.
        internal static float[] Preprocess(MNNRgbImage image, CancellationToken token)
        {
            double scale = 256.0 / Math.Min(image.Width, image.Height);
            int width = (int)(image.Width * scale), height = (int)(image.Height * scale);
            int left = (width - 256) / 2, top = (height - 256) / 2;
            var resized = MNNImageSampling.ResizeBilinear(image, width, height, token);
            var values = new float[3 * 256 * 256];
            for (int y = 0; y < 256; ++y)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < 256; ++x)
                    for (int c = 0; c < 3; ++c)
                        values[c * 65536 + y * 256 + x] = resized.Pixels[((y + top) * width + x + left) * 3 + c] / 255f;
            }
            return values;
        }
        public float[] EncodeText(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Supply text to encode.", nameof(text));
            lock (_gate)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                var vector = Normalize(_text.Run("text_embeds", new MNNGenerationGraph.Input("input_ids", _tokenizer.Encode(text), 1, 77)));
                cancellationToken.ThrowIfCancellationRequested();
                return vector;
            }
        }
        public Task<float[]> EncodeImageAsync(MNNRgbImage image, CancellationToken cancellationToken = default) => Task.Run(() => EncodeImage(image, cancellationToken), cancellationToken);
        public Task<float[]> EncodeTextAsync(string text, CancellationToken cancellationToken = default) => Task.Run(() => EncodeText(text, cancellationToken), cancellationToken);
        public static float Similarity(float[] left, float[] right) => MNNVectorSimilarity.Cosine(left, right);
        private static float[] Normalize(float[] vector)
        {
            if (vector.Length != 512 || vector.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw new InvalidDataException("Invalid CLIP vector.");
            double norm = Math.Sqrt(vector.Sum(v => (double)v * v));
            if (norm <= 1e-12) throw new InvalidDataException("CLIP returned a zero vector.");
            for (int i = 0; i < vector.Length; ++i) vector[i] = (float)(vector[i] / norm);
            return vector;
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(MNNImageEmbedding)); }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _text.Dispose(); _vision.Dispose();
            }
        }
    }
}
