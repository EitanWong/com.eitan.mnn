using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>YOLOv8 detection exports using the existing official Interpreter/Tensor calls.</summary>
    public sealed class MNNYoloV8 : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNYoloV8Config _config;
        private readonly MNNGenerationGraph _graph;
        private readonly int _preprocessingThreads;
        private readonly float[] _inputBuffer;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNYoloV8(string directory, MNNYoloV8Config config, int threads, MNNBackendType backend)
        {
            _config = config.Clone();
            _preprocessingThreads = Math.Min(4, threads);
            string graph = _config.ResolveGraph(directory);
            _inputBuffer = new float[checked(_config.width * _config.height * 3)];
            _graph = new MNNGenerationGraph(graph, threads, backendType: backend);
        }

        public static MNNYoloV8 Load(string directory, MNNYoloV8Config config = null, int threadCount = 4,
            MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64)
                throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNYoloV8(Path.GetFullPath(directory), config ?? MNNYoloV8Config.Read(directory), threadCount, backendType);
        }

        public MNNDetection[] Detect(MNNRgbImage image, float scoreThreshold = .25f, float iouThreshold = .45f,
            int maximumDetections = 300, CancellationToken cancellationToken = default)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));
            if (!(scoreThreshold > 0 && scoreThreshold <= 1) || !(iouThreshold > 0 && iouThreshold <= 1) || maximumDetections < 1 || maximumDetections > 30000)
                throw new ArgumentOutOfRangeException(nameof(scoreThreshold), "Use thresholds in (0,1] and 1–30000 maximum detections.");
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNYoloV8));
                cancellationToken.ThrowIfCancellationRequested();
                var tensor = Letterbox(image, _config.width, _config.height, out float scale, out int left, out int top, cancellationToken, _preprocessingThreads, _inputBuffer);
                var output = _graph.RunOutputs(new[] {_config.output},
                    new MNNGenerationGraph.Input(_config.input, tensor, 1, 3, _config.height, _config.width))[0];
                cancellationToken.ThrowIfCancellationRequested();
                return Decode(output.Values, output.Shape, _config, image.Width, image.Height, scale, left, top,
                    scoreThreshold, iouThreshold, maximumDetections, cancellationToken);
            }
        }

        public Task<MNNDetection[]> DetectAsync(MNNRgbImage image, float scoreThreshold = .25f, float iouThreshold = .45f,
            int maximumDetections = 300, CancellationToken cancellationToken = default) =>
            Task.Run(() => Detect(image, scoreThreshold, iouThreshold, maximumDetections, cancellationToken), cancellationToken);

        internal static float[] Letterbox(MNNRgbImage image, int width, int height, out float scale, out int left, out int top, CancellationToken token = default, int threadCount = 1, float[] destinationBuffer = null)
        {
            scale = Math.Min((float)width / image.Width, (float)height / image.Height);
            int resizedWidth = Math.Max(1, (int)Math.Round(image.Width * scale));
            int resizedHeight = Math.Max(1, (int)Math.Round(image.Height * scale));
            left = (width - resizedWidth) / 2;
            top = (height - resizedHeight) / 2;
            int plane = checked(width * height);
            int length = checked(3 * plane);
            var result = destinationBuffer ?? new float[length];
            if (result.Length != length)
                throw new ArgumentException("Letterbox workspace size does not match the configured input.", nameof(destinationBuffer));
            Array.Fill(result, 114f / 255);
            byte[] pixels = image.Pixels;
            var xOffsets = new int[resizedWidth];
            var xNextOffsets = new int[resizedWidth];
            var xWeights = new float[resizedWidth];
            for (int x = 0; x < resizedWidth; ++x)
            {
                float sx = Math.Max(0, (x + .5f) * image.Width / resizedWidth - .5f);
                int x0 = Math.Min((int)sx, image.Width - 1);
                xOffsets[x] = x0 * 3;
                xNextOffsets[x] = Math.Min(x0 + 1, image.Width - 1) * 3;
                xWeights[x] = sx - x0;
            }
            int destinationLeft = left, destinationTop = top;
            Action<int> resizeRow = y =>
            {
                token.ThrowIfCancellationRequested();
                float sy = Math.Max(0, (y + .5f) * image.Height / resizedHeight - .5f);
                int y0 = Math.Min((int)sy, image.Height - 1), y1 = Math.Min(y0 + 1, image.Height - 1);
                float fy = sy - y0;
                int row0 = y0 * image.Width * 3, row1 = y1 * image.Width * 3;
                int destination = (y + destinationTop) * width + destinationLeft;
                for (int x = 0; x < resizedWidth; ++x)
                {
                    int x0 = xOffsets[x], x1 = xNextOffsets[x];
                    float fx = xWeights[x];
                    for (int c = 0; c < 3; ++c)
                    {
                        float a = pixels[row0 + x0 + c] * (1 - fx) + pixels[row0 + x1 + c] * fx;
                        float b = pixels[row1 + x0 + c] * (1 - fx) + pixels[row1 + x1 + c] * fx;
                        result[c * plane + destination + x] = (float)Math.Round(a * (1 - fy) + b * fy) / 255;
                    }
                }
            };
            if (threadCount > 1 && resizedWidth * resizedHeight >= 128000)
                Parallel.For(0, resizedHeight, new ParallelOptions {MaxDegreeOfParallelism = threadCount, CancellationToken = token}, resizeRow);
            else
                for (int y = 0; y < resizedHeight; ++y)
                    resizeRow(y);
            return result;
        }

        internal static MNNDetection[] Decode(float[] values, int[] shape, MNNYoloV8Config config, int width, int height,
            float scale, int left, int top, float threshold, float iou, int maximum, CancellationToken token = default)
        {
            string[] labels = config.labels;
            int channels = labels.Length + 4;
            if (shape.Length == 3 && shape[0] == 1)
                shape = shape.Skip(1).ToArray();
            if (shape.Length != 2 || (shape[0] != channels && shape[1] != channels))
                throw new InvalidDataException("Expected decoded YOLOv8 output [1,4+classes,anchors] or [1,anchors,4+classes]; DFL logits, raw heads and objectness exports need another profile.");
            bool channelFirst = shape[0] == channels;
            int anchors = channelFirst ? shape[1] : shape[0];
            if (anchors < 1 || values.Length != checked(channels * anchors) || !(scale > 0))
                throw new InvalidDataException("Invalid YOLOv8 output size or image transform.");
            if (config.boxEncoding == MNNYoloV8BoxEncoding.AnchorDistances &&
                anchors != config.strides.Sum(stride => config.width / stride * (config.height / stride)))
                throw new InvalidDataException("Anchor count does not match the configured YOLOv8 feature strides.");
            var candidates = new List<MNNDetection>();
            for (int anchor = 0; anchor < anchors; ++anchor)
            {
                if ((anchor & 255) == 0)
                    token.ThrowIfCancellationRequested();
                float Read(int channel) => values[channelFirst ? channel * anchors + anchor : anchor * channels + channel];
                int best = 0;
                float score = -1;
                for (int c = 0; c < labels.Length; ++c)
                {
                    float probability = Read(c + 4);
                    if (float.IsNaN(probability) || float.IsInfinity(probability) || probability < 0 || probability > 1)
                        throw new InvalidDataException("YOLOv8 graph must return finite class probabilities in [0,1], not logits.");
                    if (probability > score)
                    {
                        best = c;
                        score = probability;
                    }
                }
                if (score < threshold)
                    continue;
                for (int coordinate = 0; coordinate < 4; ++coordinate)
                    if (float.IsNaN(Read(coordinate)) || float.IsInfinity(Read(coordinate)) ||
                        config.boxEncoding == MNNYoloV8BoxEncoding.AnchorDistances && Read(coordinate) < 0)
                        throw new InvalidDataException("YOLOv8 graph returned invalid box coordinates.");
                float w, h, x, y;
                if (config.boxEncoding == MNNYoloV8BoxEncoding.AnchorDistances)
                {
                    int index = anchor, stride = 0, columns = 0;
                    foreach (int level in config.strides)
                    {
                        stride = level;
                        columns = config.width / stride;
                        int count = columns * (config.height / stride);
                        if (index < count)
                            break;
                        index -= count;
                    }
                    float cx = (index % columns + .5f) * stride, cy = (index / columns + .5f) * stride;
                    x = (cx - Read(0) * stride - left) / scale;
                    y = (cy - Read(1) * stride - top) / scale;
                    w = (Read(0) + Read(2)) * stride / scale;
                    h = (Read(1) + Read(3)) * stride / scale;
                }
                else
                {
                    w = Read(2) / scale;
                    h = Read(3) / scale;
                    x = (Read(0) - left) / scale - w / 2;
                    y = (Read(1) - top) / scale - h / 2;
                }
                if (w > 0 && h > 0)
                    candidates.Add(new MNNDetection(best, labels[best], score, x, y, w, h));
            }
            // Suppress before clipping, as upstream YOLO does, and only within the same class.
            var sorted = candidates.Select((box, index) => new {box, index}).OrderByDescending(item => item.box.Score)
                .ThenBy(item => item.index).Take(30000).Select(item => item.box).ToArray();
            var selected = new List<MNNDetection>();
            foreach (var box in sorted)
            {
                token.ThrowIfCancellationRequested();
                if (selected.Any(other => other.ClassIndex == box.ClassIndex && IntersectionOverUnion(other, box) > iou))
                    continue;
                selected.Add(box);
                if (selected.Count >= maximum)
                    break;
            }
            return selected.Select(box =>
            {
                float x = Math.Max(0, Math.Min(width, box.X)), y = Math.Max(0, Math.Min(height, box.Y));
                float right = Math.Max(0, Math.Min(width, box.X + box.Width)), bottom = Math.Max(0, Math.Min(height, box.Y + box.Height));
                return new MNNDetection(box.ClassIndex, box.Label, box.Score, x, y, right - x, bottom - y);
            }).Where(box => box.Width > 0 && box.Height > 0).ToArray();
        }

        internal static float IntersectionOverUnion(MNNDetection a, MNNDetection b)
        {
            float w = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X));
            float h = Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
            float intersection = w * h;
            return intersection / (a.Width * a.Height + b.Width * b.Height - intersection);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _graph.Dispose();
            }
        }
    }
}
