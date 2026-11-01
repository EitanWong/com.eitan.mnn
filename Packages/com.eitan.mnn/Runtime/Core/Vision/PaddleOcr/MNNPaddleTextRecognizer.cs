using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>PP-OCR CTC recognition of an upright cropped text line using official Interpreter/Tensor calls.</summary>
    public sealed class MNNPaddleTextRecognizer : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNPaddleTextConfig _config;
        private readonly MNNGenerationGraph _graph;
        private readonly string[] _labels;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNPaddleTextRecognizer(string directory, MNNPaddleTextConfig config, int threads, MNNBackendType backend)
        {
            _config = config.Clone();
            _config.Validate(directory);
            var labels = new List<string> {string.Empty};
            labels.AddRange(File.ReadAllLines(MNNPaddleTextConfig.ResolveFile(directory, _config.dictionary), new UTF8Encoding(false, true)));
            if (_config.appendSpace)
                labels.Add(" ");
            if (labels.Count < 3 || labels.Count > 100000 || labels.Skip(1).Any(string.IsNullOrEmpty))
                throw new InvalidDataException("The Paddle dictionary must contain one nonempty symbol per line; blank is added at index zero.");
            _labels = labels.ToArray();
            _graph = new MNNGenerationGraph(MNNPaddleTextConfig.ResolveFile(directory, _config.graph), threads,
                backendType: backend == MNNBackendType.Auto ? _config.preferredBackend : backend);
        }

        public static MNNPaddleTextRecognizer Load(string directory, MNNPaddleTextConfig config = null, int threadCount = 4,
            MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64)
                throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNPaddleTextRecognizer(Path.GetFullPath(directory), config ?? MNNPaddleTextConfig.Read(directory), threadCount, backendType);
        }

        public MNNTextRecognition Recognize(MNNRgbImage line, CancellationToken cancellationToken = default)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNPaddleTextRecognizer));
                cancellationToken.ThrowIfCancellationRequested();
                // A constant image has no glyph edges. Some language heads nevertheless emit
                // high-probability tokens on blank input; skip both their false positives and inference.
                if (IsUniform(line, cancellationToken))
                    return new MNNTextRecognition(string.Empty, 0);
                var input = Preprocess(line, _config, out int width, cancellationToken);
                var output = _graph.RunOutputs(new[] {_config.output},
                    new MNNGenerationGraph.Input(_config.input, input, 1, 3, _config.height, width))[0];
                cancellationToken.ThrowIfCancellationRequested();
                var result = Decode(output.Values, output.Shape, _labels, cancellationToken);
                return _config.reverseArabic
                    ? new MNNTextRecognition(ReverseArabic(result.Text), result.Confidence)
                    : result;
            }
        }

        public Task<MNNTextRecognition> RecognizeAsync(MNNRgbImage line, CancellationToken cancellationToken = default) =>
            Task.Run(() => Recognize(line, cancellationToken), cancellationToken);

        internal static bool IsUniform(MNNRgbImage image, CancellationToken token = default)
        {
            var pixels = image.Pixels;
            for (int i = 3; i < pixels.Length; i += 3)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (pixels[i] != pixels[0] || pixels[i + 1] != pixels[1] || pixels[i + 2] != pixels[2])
                    return false;
            }
            token.ThrowIfCancellationRequested();
            return true;
        }

        internal static float[] Preprocess(MNNRgbImage line, MNNPaddleTextConfig config, out int width, CancellationToken token = default)
        {
            int resized = checked((int)Math.Ceiling((double)config.height * line.Width / line.Height));
            if (resized > config.maximumWidth)
                throw new ArgumentException("Text line exceeds the configured aspect ratio; split it into shorter crops.", nameof(line));
            width = Math.Max(config.minimumWidth, (resized + 7) / 8 * 8);
            int plane = checked(config.height * width);
            var result = new float[checked(3 * plane)]; // Padding is zero after normalization, per PaddleOCR.
            var pixels = line.Pixels;
            for (int y = 0; y < config.height; ++y)
            {
                token.ThrowIfCancellationRequested();
                float sy = Math.Max(0, (y + .5f) * line.Height / config.height - .5f);
                int y0 = Math.Min((int)sy, line.Height - 1), y1 = Math.Min(y0 + 1, line.Height - 1);
                float fy = sy - y0;
                for (int x = 0; x < resized; ++x)
                {
                    float sx = Math.Max(0, (x + .5f) * line.Width / resized - .5f);
                    int x0 = Math.Min((int)sx, line.Width - 1), x1 = Math.Min(x0 + 1, line.Width - 1);
                    float fx = sx - x0;
                    for (int channel = 0; channel < 3; ++channel)
                    {
                        int c = 2 - channel; // PaddleOCR receives OpenCV BGR, while the public image is RGB.
                        float a = pixels[(y0 * line.Width + x0) * 3 + c] * (1 - fx) + pixels[(y0 * line.Width + x1) * 3 + c] * fx;
                        float b = pixels[(y1 * line.Width + x0) * 3 + c] * (1 - fx) + pixels[(y1 * line.Width + x1) * 3 + c] * fx;
                        result[channel * plane + y * width + x] = (float)Math.Round(a * (1 - fy) + b * fy) / 127.5f - 1;
                    }
                }
            }
            return result;
        }

        internal static MNNTextRecognition Decode(float[] values, int[] shape, string[] labels, CancellationToken token = default)
        {
            if (shape.Length != 3 || shape[0] != 1 || shape[1] < 1 || shape[2] != labels.Length ||
                values.Length != checked(shape[1] * labels.Length))
                throw new InvalidDataException("Expected Paddle CTC probabilities [1,time,dictionary+blank+optional-space].");
            var text = new StringBuilder();
            int previous = -1, count = 0;
            double total = 0;
            for (int step = 0; step < shape[1]; ++step)
            {
                token.ThrowIfCancellationRequested();
                int offset = step * labels.Length, best = 0;
                for (int c = 0; c < labels.Length; ++c)
                {
                    float probability = values[offset + c];
                    if (float.IsNaN(probability) || float.IsInfinity(probability) || probability < 0 || probability > 1)
                        throw new InvalidDataException("Paddle CTC output must contain finite probabilities in [0,1].");
                    if (probability > values[offset + best])
                        best = c;
                }
                if (best != 0 && best != previous)
                {
                    text.Append(labels[best]);
                    total += values[offset + best];
                    ++count;
                }
                previous = best; // Blank separates repeated characters (e.g. two l's in Hello).
            }
            return new MNNTextRecognition(text.ToString(), count == 0 ? 0 : (float)(total / count));
        }

        // PaddleOCR BaseRecLabelDecode.pred_reverse: reverse Unicode symbols while preserving
        // contiguous ASCII letters, digits and the explicitly permitted punctuation as a unit.
        // This restores logical Arabic output; it is not a general Unicode bidi renderer.
        internal static string ReverseArabic(string text)
        {
            var groups = new List<string>();
            var ascii = new StringBuilder();
            for (int i = 0; i < text.Length; ++i)
            {
                char c = text[i];
                if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' ||
                    " :*./%+-".IndexOf(c) >= 0)
                    ascii.Append(c);
                else
                {
                    if (ascii.Length > 0) { groups.Add(ascii.ToString()); ascii.Clear(); }
                    int length = char.IsSurrogatePair(text, i) ? 2 : 1;
                    groups.Add(text.Substring(i, length));
                    i += length - 1;
                }
            }
            if (ascii.Length > 0) groups.Add(ascii.ToString());
            groups.Reverse();
            return string.Concat(groups);
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
