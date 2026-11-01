using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>PP-OCR DB text detection using official Interpreter/Tensor APIs and managed geometry.</summary>
    public sealed class MNNPaddleTextDetector : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNPaddleOcrConfig _config;
        private readonly MNNGenerationGraph _graph;
        private readonly int _preprocessingThreads;
        private float[] _inputBuffer, _previousInputBuffer;
        private readonly MNNPaddleDbPostprocessor.Workspace _workspace = new MNNPaddleDbPostprocessor.Workspace();
        private static readonly float[] NormalizedPixels = CreateNormalizedPixels();
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNPaddleTextDetector(string directory, MNNPaddleOcrConfig config, int threads, MNNBackendType backend)
        {
            _config = config.Clone();
            _preprocessingThreads = Math.Min(4, threads);
            _config.Validate(directory);
            if (backend == MNNBackendType.Auto)
                backend = _config.preferredDetectionBackend;
            _graph = new MNNGenerationGraph(MNNPaddleTextConfig.ResolveFile(directory, _config.graph), threads, backendType: backend);
        }

        public static MNNPaddleTextDetector Load(string directory, MNNPaddleOcrConfig config = null, int threadCount = 4,
            MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64)
                throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNPaddleTextDetector(Path.GetFullPath(directory), config ?? MNNPaddleOcrConfig.Read(directory), threadCount, backendType);
        }

        public MNNTextBox[] Detect(MNNRgbImage image, CancellationToken cancellationToken = default)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNPaddleTextDetector));
                cancellationToken.ThrowIfCancellationRequested();
                InputShape(image, _config.maximumSide, out int width, out int height, out _, out _);
                int length = checked(width * height * 3);
                if (_inputBuffer == null || _inputBuffer.Length != length)
                {
                    var previous = _inputBuffer;
                    _inputBuffer = _previousInputBuffer != null && _previousInputBuffer.Length == length
                        ? _previousInputBuffer : new float[length];
                    _previousInputBuffer = length <= 8000000 ? previous : null;
                }
                var input = Preprocess(image, _config.maximumSide, out width, out height,
                    cancellationToken, _preprocessingThreads, _inputBuffer);
                var output = _graph.RunOutputs(new[] {_config.output},
                    new MNNGenerationGraph.Input(_config.input, input, 1, 3, height, width))[0];
                return MNNPaddleDbPostprocessor.Decode(output.Values, output.Shape, image.Width, image.Height, _config, cancellationToken, _workspace);
            }
        }

        public Task<MNNTextBox[]> DetectAsync(MNNRgbImage image, CancellationToken cancellationToken = default) =>
            Task.Run(() => Detect(image, cancellationToken), cancellationToken);

        private static void InputShape(MNNRgbImage image, int maximumSide, out int width, out int height, out int sourceWidth, out int sourceHeight)
        {
            sourceWidth = image.Width;
            sourceHeight = image.Height;
            if (sourceWidth + sourceHeight < 64)
            {
                sourceWidth = Math.Max(32, sourceWidth);
                sourceHeight = Math.Max(32, sourceHeight);
            }
            double ratio = Math.Min(1, (double)maximumSide / Math.Max(sourceWidth, sourceHeight));
            width = Math.Max(32, (int)Math.Round((int)(sourceWidth * ratio) / 32.0) * 32);
            height = Math.Max(32, (int)Math.Round((int)(sourceHeight * ratio) / 32.0) * 32);
        }

        private static float[] CreateNormalizedPixels()
        {
            float[] mean = {.485f, .456f, .406f}, std = {.229f, .224f, .225f};
            var table = new float[3 * 256];
            for (int channel = 0; channel < 3; ++channel)
                for (int pixel = 0; pixel < 256; ++pixel)
                    table[channel * 256 + pixel] = (pixel / 255f - mean[channel]) / std[channel];
            return table;
        }

        internal static float[] Preprocess(MNNRgbImage image, int maximumSide, out int width, out int height,
            CancellationToken token = default, int threadCount = 1, float[] destinationBuffer = null)
        {
            InputShape(image, maximumSide, out width, out height, out int sourceWidth, out int sourceHeight);
            int plane = checked(width * height), length = checked(plane * 3);
            var result = destinationBuffer ?? new float[length];
            if (result.Length != length)
                throw new ArgumentException("DB input workspace size does not match the resized image.", nameof(destinationBuffer));
            var pixels = image.Pixels;
            if (sourceWidth != image.Width || sourceHeight != image.Height)
            {
                pixels = new byte[checked(sourceWidth * sourceHeight * 3)];
                for (int y = 0; y < image.Height; ++y)
                    Array.Copy(image.Pixels, y * image.Width * 3, pixels, y * sourceWidth * 3, image.Width * 3);
            }
            int targetWidth = width, targetHeight = height;
            var xOffsets = new int[width];
            var xNextOffsets = new int[width];
            var xWeights = new float[width];
            for (int x = 0; x < width; ++x)
            {
                float sx = Math.Max(0, (x + .5f) * sourceWidth / width - .5f);
                int x0 = Math.Min((int)sx, sourceWidth - 1);
                xOffsets[x] = x0 * 3;
                xNextOffsets[x] = Math.Min(x0 + 1, sourceWidth - 1) * 3;
                xWeights[x] = sx - x0;
            }
            bool sameSize = sourceWidth == width && sourceHeight == height;
            Action<int> row = y =>
            {
                token.ThrowIfCancellationRequested();
                int destination = y * targetWidth;
                if (sameSize)
                {
                    int source = y * sourceWidth * 3;
                    for (int x = 0; x < targetWidth; ++x)
                    {
                        result[destination + x] = NormalizedPixels[pixels[source + x * 3 + 2]];
                        result[plane + destination + x] = NormalizedPixels[256 + pixels[source + x * 3 + 1]];
                        result[2 * plane + destination + x] = NormalizedPixels[512 + pixels[source + x * 3]];
                    }
                    return;
                }
                float sy = Math.Max(0, (y + .5f) * sourceHeight / targetHeight - .5f);
                int y0 = Math.Min((int)sy, sourceHeight - 1), y1 = Math.Min(y0 + 1, sourceHeight - 1);
                float fy = sy - y0;
                int row0 = y0 * sourceWidth * 3, row1 = y1 * sourceWidth * 3;
                for (int x = 0; x < targetWidth; ++x)
                {
                    int x0 = xOffsets[x], x1 = xNextOffsets[x];
                    float fx = xWeights[x];
                    for (int channel = 0; channel < 3; ++channel)
                    {
                        int c = 2 - channel;
                        float a = pixels[row0 + x0 + c] * (1 - fx) + pixels[row0 + x1 + c] * fx;
                        float b = pixels[row1 + x0 + c] * (1 - fx) + pixels[row1 + x1 + c] * fx;
                        int pixel = (int)Math.Round(a * (1 - fy) + b * fy);
                        result[channel * plane + destination + x] = NormalizedPixels[channel * 256 + pixel];
                    }
                }
            };
            if (threadCount > 1 && plane >= 128000)
                Parallel.For(0, height, new ParallelOptions {MaxDegreeOfParallelism = threadCount, CancellationToken = token}, row);
            else
                for (int y = 0; y < height; ++y)
                    row(y);
            return result;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _graph.Dispose();
                _inputBuffer = _previousInputBuffer = null;
                _workspace.Clear();
            }
        }
    }
}
