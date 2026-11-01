using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Explicit("Requires the prepared official DeepLab TFLite/MNN graph and independent LiteRT reference; never downloads.")]
    public class SegmentationIntegrationTests
    {
        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void ConvertedDeepLabLogitsMatchIndependentLiteRt(MNNBackendType backend)
        {
            string models = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            string artifacts = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT"), "DeepLab");
            Directory.CreateDirectory(artifacts);
            string reference = Path.GetFullPath(Path.Combine(models, "../../../../TestArtifacts~/TaskIntegration/Segmentation"));
            float[] input = Read(Path.Combine(reference, "input.f32"));
            float[] expected = Read(Path.Combine(reference, "output.f32"));
            using (var graph = new MNNGenerationGraph(Path.Combine(models, "TensorFlow--DeepLabV3-MobileNetV2/deeplabv3.mnn"), 4,
                backendType: backend, precisionMode: MNNPrecisionMode.High))
            {
                var output = graph.RunOutputs(new[] {"ResizeBilinear_3"}, new MNNGenerationGraph.Input("sub_7", input, 1, 257, 257, 3))[0];
                TestContext.WriteLine("Output shape=" + string.Join(",", output.Shape));
                MNNImageTask.RequireShape(output, 1, 257, 257, 21);
                float[] actual = output.Values;
                double max = expected.Select((value, index) => Math.Abs(value - actual[index])).Max();
                double mae = expected.Select((value, index) => Math.Abs(value - actual[index])).Average();
                int matches = 0;
                for (int pixel = 0; pixel < 257 * 257; ++pixel)
                {
                    int a = 0, b = 0;
                    for (int c = 1; c < 21; ++c)
                    {
                        if (actual[pixel * 21 + c] > actual[pixel * 21 + a]) a = c;
                        if (expected[pixel * 21 + c] > expected[pixel * 21 + b]) b = c;
                    }
                    if (a == b) ++matches;
                }
                double agreement = (double)matches / (257 * 257);
                File.WriteAllText(Path.Combine(artifacts, backend + "-reference.json"), string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"requested\":\"{0}\",\"actual\":\"{1}\",\"max_error\":{2},\"mean_error\":{3},\"class_agreement\":{4}}}", backend, graph.Backend, max, mae, agreement));
                Assert.Less(max, .01);
                Assert.Less(mae, .001);
                Assert.Greater(agreement, .999);
            }
            string directory = Path.Combine(models, "TensorFlow--DeepLabV3-MobileNetV2");
            var image = new MNNRgbImage(File.ReadAllBytes(Path.Combine(reference, "source.rgb")), 810, 1080);
            CollectionAssert.AreEqual(input, MNNImageTask.SegmentationTensor(image), "Managed RGB resize/normalization must match Pillow/LiteRT input.");
            byte[] expectedIds = File.ReadAllBytes(Path.Combine(reference, "classes.u8"));
            var before = Process.GetCurrentProcess().WorkingSet64;
            var timer = Stopwatch.StartNew();
            using (var model = MNNImageTask.Load(directory, backendType: backend))
            {
                double load = timer.Elapsed.TotalMilliseconds;
                long loaded = Process.GetCurrentProcess().WorkingSet64;
                timer.Restart(); var result = model.Process(image); double first = timer.Elapsed.TotalMilliseconds;
                timer.Restart(); var repeat = model.Process(image); double warm = timer.Elapsed.TotalMilliseconds;
                long after = Process.GetCurrentProcess().WorkingSet64;
                var mask = result.Segmentation;
                Assert.AreEqual(810, mask.Width); Assert.AreEqual(1080, mask.Height);
                CollectionAssert.AreEqual(mask.ClassIds, repeat.Segmentation.ClassIds);
                int matches = 0;
                float minConfidence = 1, maxConfidence = 0;
                for (int y = 0; y < mask.Height; ++y)
                    for (int x = 0; x < mask.Width; ++x)
                    {
                        int gy = Math.Min(256, (int)((y + .5) * 257 / mask.Height));
                        int gx = Math.Min(256, (int)((x + .5) * 257 / mask.Width));
                        if (mask.ClassAt(x, y) == expectedIds[gy * 257 + gx]) ++matches;
                        float score = mask.ConfidenceAt(x, y);
                        minConfidence = Math.Min(minConfidence, score); maxConfidence = Math.Max(maxConfidence, score);
                    }
                double agreement = (double)matches / (mask.Width * mask.Height);
                Assert.Greater(agreement, .999);
                Assert.GreaterOrEqual(minConfidence, 1f / 21);
                Assert.LessOrEqual(maxConfidence, 1);
                Assert.Greater(mask.ClassPixelCounts[6], mask.Width * mask.Height / 5, "The bus covers over one fifth of the image.");
                Assert.Greater(mask.ClassPixelCounts[15], mask.Width * mask.Height / 20, "The visible people must be segmented.");
                Assert.AreEqual(MNNAcceleration.Resolve(backend), model.Backend);
                File.WriteAllText(Path.Combine(artifacts, backend + "-measurements.json"), string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"requested\":\"{0}\",\"actual\":\"{1}\",\"load_ms\":{2},\"first_ms\":{3},\"warm_ms\":{4},\"rss_before\":{5},\"rss_loaded\":{6},\"rss_after\":{7},\"class_agreement\":{8}}}", backend, model.Backend, load, first, warm, before, loaded, after, agreement));
                var texture = MNNStudioImage.CreateTexture(result.Image);
                try { File.WriteAllBytes(Path.Combine(artifacts, backend + "-mask.png"), texture.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.Throws<OperationCanceledException>(() => model.Process(image, cancelled.Token));
                }
                model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Process(image));
            }
            using (var studio = MNNStudioNativeBackend.Load(MNNStudioModel.Read(directory), 4, false))
            {
                var result = studio.Run(new MNNStudioRequest {VisionImage = image});
                Assert.NotNull(result.ImageTask.Segmentation);
                Assert.NotNull(result.GeneratedImage);
                Assert.AreEqual(810, result.GeneratedImage.Width);
            }
        }

        private static float[] Read(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
