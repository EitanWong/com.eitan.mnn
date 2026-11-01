using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>PP-DocLayoutV3 page regions and reading order using the pinned MNN export.
    /// Auto selects CPU; this export has failed Metal correctness checks.</summary>
    public sealed class MNNDocumentLayout : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;
        private MNNDocumentLayout(string directory, int threads, MNNBackendType backend)
        {
            MNNDocumentLayoutModel.Verify(directory);
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNDocumentLayoutModel.Graph), threads, backendType: backend);
        }
        public static MNNDocumentLayout Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            // Metal Normal produced invalid geometry; High missed twelve of thirteen
            // visible regions in the real-page test. Do not accept plausible but wrong output.
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU)
                throw new NotSupportedException("This PP-DocLayoutV3 export requires CPU. Metal failed real-page correctness checks at Normal and High precision.");
            MNNPlatformSupport.RequireSupported();
            return new MNNDocumentLayout(directory, threadCount, MNNBackendType.CPU);
        }
        /// <summary>Stretch RGB to 800x800 with pixel/255 normalization. Native execution is synchronous;
        /// cancellation is observed before and after that call. This returns regions, not text or table cells.</summary>
        public MNNDocumentLayoutResult Analyze(MNNRgbImage image, float scoreThreshold = .5f, CancellationToken cancellationToken = default)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            ValidateThreshold(scoreThreshold);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNDocumentLayout));
                cancellationToken.ThrowIfCancellationRequested();
                float[] pixels = MNNImageSampling.Tensor(image, 800, 800, 0, 255, token: cancellationToken);
                var output = _graph.RunOutputs(new[] {"fetch_name_0"},
                    new MNNGenerationGraph.Input("image", pixels, 1, 3, 800, 800),
                    new MNNGenerationGraph.Input("im_shape", new[] {800f, 800f}, 1, 2),
                    new MNNGenerationGraph.Input("scale_factor", new[] {1f, 1f}, 1, 2))[0];
                cancellationToken.ThrowIfCancellationRequested();
                return Decode(output, image.Width, image.Height, scoreThreshold);
            }
        }
        public Task<MNNDocumentLayoutResult> AnalyzeAsync(MNNRgbImage image, float scoreThreshold = .5f, CancellationToken cancellationToken = default) =>
            Task.Run(() => Analyze(image, scoreThreshold, cancellationToken), cancellationToken);

        internal static MNNDocumentLayoutResult Decode(MNNGenerationGraph.Output output, int width, int height, float threshold)
        {
            ValidateThreshold(threshold);
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (!(output.Data is float[]) || output.Shape.Length != 2 || output.Shape[1] != 7 || output.Shape[0] > 300)
                throw new InvalidDataException("PP-DocLayoutV3 requires float32 [N,7] rows (at most 300).");
            var values = (float[])output.Data;
            var regions = new List<MNNDocumentRegion>();
            for (int offset = 0; offset < values.Length; offset += 7)
            {
                for (int c = 0; c < 7; ++c)
                    if (float.IsNaN(values[offset + c]) || float.IsInfinity(values[offset + c]))
                        throw new InvalidDataException("Non-finite document layout output.");
                float category = values[offset], score = values[offset + 1], order = values[offset + 6];
                if (category < 0 || category >= MNNDocumentLayoutModel.Labels.Length || category != (int)category ||
                    score < 0 || score > 1 || order < 0 || order >= 300 || order != (int)order)
                    throw new InvalidDataException("Invalid layout class, confidence or reading order.");
                if (score < threshold) continue;
                float x1 = values[offset + 2], y1 = values[offset + 3], x2 = values[offset + 4], y2 = values[offset + 5];
                if (x2 <= x1 || y2 <= y1) throw new InvalidDataException("Invalid layout box geometry.");
                // Aux inputs preserve model-space pixels; the graph's scale_factor
                // convention is verified by the real-weights integration test.
                x1 = Clamp(x1 / 800 * width, width); x2 = Clamp(x2 / 800 * width, width);
                y1 = Clamp(y1 / 800 * height, height); y2 = Clamp(y2 / 800 * height, height);
                if (x2 <= x1 || y2 <= y1) continue;
                regions.Add(new MNNDocumentRegion(new MNNDetection((int)category, MNNDocumentLayoutModel.Labels[(int)category],
                    score, x1, y1, x2 - x1, y2 - y1), (int)order));
            }
            return new MNNDocumentLayoutResult(regions.OrderBy(region => region.ReadingOrder).ToArray(), width, height);
        }
        private static float Clamp(float value, int limit) => Math.Max(0, Math.Min(limit, value));
        private static void ValidateThreshold(float value)
        {
            if (!(value > 0 && value <= 1)) throw new ArgumentOutOfRangeException(nameof(value), "Confidence must be in (0,1].");
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _graph.Dispose();
            }
        }
    }
}
