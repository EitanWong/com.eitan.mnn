using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Explicit("Requires the existing pinned PP-DocLayoutV3 graph and real document fixture; never downloads.")]
    public class DocumentLayoutIntegrationTests
    {
        [Test]
        public void PinnedInstallerUsesCorrectProviderVerifiesBytesAndReusesExistingAssets()
        {
            string models = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            string artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT");
            Assert.NotNull(models); Assert.NotNull(artifacts);
            string source = Path.Combine(models, MNNDocumentLayoutModel.Repository.Replace("/", "--"));
            string staging = Path.Combine(artifacts, "LayoutInstaller");
            Directory.CreateDirectory(staging);
            try
            {
                using (var handler = new ExistingLayoutAssets(source))
                using (var http = new HttpClient(handler))
                using (var service = new MNNModelDownloadService(http, options: new MNNModelDownloadOptions {MaxConnections = 1, PreservePartialDownloads = true, RetryCount = 0}))
                {
                    long bytes = service.DownloadDocumentLayoutProfileAsync(MNNDocumentLayoutModel.Repository, staging, null, CancellationToken.None).GetAwaiter().GetResult();
                    Assert.AreEqual(MNNDocumentLayoutModel.Sizes.Sum(), bytes);
                    Assert.AreEqual(2, handler.Requests);
                    MNNDocumentLayoutModel.Verify(staging);
                    service.DownloadDocumentLayoutProfileAsync(MNNDocumentLayoutModel.Repository, staging, null, CancellationToken.None).GetAwaiter().GetResult();
                    Assert.AreEqual(2, handler.Requests, "Verified assets must be reused without additional requests.");
                }
            }
            finally { Directory.Delete(staging, true); }
        }

        private sealed class ExistingLayoutAssets : HttpMessageHandler
        {
            private readonly string _directory;
            internal int Requests;
            internal ExistingLayoutAssets(string directory) { _directory = directory; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                ++Requests;
                Assert.AreEqual("www.modelscope.cn", request.RequestUri.Host);
                Assert.AreEqual("/api/v1/models/" + MNNDocumentLayoutModel.Repository + "/repo", request.RequestUri.AbsolutePath);
                StringAssert.Contains("Revision=" + MNNDocumentLayoutModel.Revision, request.RequestUri.Query);
                string name = request.RequestUri.Query.Split(new[] {"FilePath="}, StringSplitOptions.None).Last();
                Assert.Contains(name, MNNDocumentLayoutModel.Files);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content = new StreamContent(File.OpenRead(Path.Combine(_directory, name)))});
            }
        }

        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void RealPageMatchesVisibleRegionsAndStudioUsesTypedResults(MNNBackendType backend)
        {
            string models = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            string fixtures = Environment.GetEnvironmentVariable("MNN_TEST_FIXTURE_ROOT");
            string artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT");
            Assert.NotNull(models); Assert.NotNull(fixtures); Assert.NotNull(artifacts);
            string directory = Path.Combine(models, MNNDocumentLayoutModel.Repository.Replace("/", "--"));
            var image = MNNStudioImage.ReadRgb(Path.Combine(fixtures, "layout.jpg"));
            long beforeLoad = Process.GetCurrentProcess().WorkingSet64;
            var timer = Stopwatch.StartNew();
            using (var model = MNNDocumentLayout.Load(directory, backendType: backend))
            {
                double load = timer.Elapsed.TotalMilliseconds;
                long loaded = Process.GetCurrentProcess().WorkingSet64;
                timer.Restart(); var result = model.Analyze(image); double first = timer.Elapsed.TotalMilliseconds;
                timer.Restart(); var repeat = model.Analyze(image); double warm = timer.Elapsed.TotalMilliseconds;
                long after = Process.GetCurrentProcess().WorkingSet64;
                var regions = result.Regions;
                Assert.AreEqual(13, regions.Length);
                Assert.AreEqual(2, regions.Count(region => region.Detection.Label == "table"));
                Assert.AreEqual(2, regions.Count(region => region.Detection.Label == "figure_title"));
                Assert.AreEqual(4, regions.Count(region => region.Detection.Label == "paragraph_title"));
                Assert.AreEqual(5, regions.Count(region => region.Detection.Label == "text"));
                CollectionAssert.AreEqual(new[] {"figure_title", "table", "paragraph_title", "text", "paragraph_title", "text",
                    "figure_title", "table", "text", "paragraph_title", "text", "paragraph_title", "text"}, regions.Select(region => region.Detection.Label));
                CollectionAssert.AreEqual(regions.Select(region => region.ReadingOrder).OrderBy(order => order), regions.Select(region => region.ReadingOrder));
                var tables = regions.Where(region => region.Detection.Label == "table").ToArray();
                // Independently marked visible table extents in the 760x865 Paddle demo page.
                AssertBox(tables[0].Detection, 75,108,248,192);
                AssertBox(tables[1].Detection, 438,108,226,207);
                for (int i = 0; i < regions.Length; ++i)
                {
                    Assert.AreEqual(regions[i].Detection.Label, repeat.Regions[i].Detection.Label);
                    Assert.That(repeat.Regions[i].Detection.X, Is.EqualTo(regions[i].Detection.X).Within(.2));
                }
                Assert.AreEqual(7, model.Analyze(image, .95f).Regions.Length, "Confidence filtering must retain the five body blocks and two tables.");
                var blank = new byte[image.Width * image.Height * 3];
                for (int i = 0; i < blank.Length; ++i) blank[i] = 255;
                Assert.IsEmpty(model.Analyze(new MNNRgbImage(blank, image.Width, image.Height)).Regions, "Blank pages must not produce confident regions.");
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.Throws<OperationCanceledException>(() => model.Analyze(image, cancellationToken: cancelled.Token));
                    Assert.Throws<TaskCanceledException>(() => model.AnalyzeAsync(image, cancellationToken: cancelled.Token).GetAwaiter().GetResult());
                }
                File.WriteAllText(Path.Combine(artifacts, backend + "-measurements.json"), string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"requested\":\"{0}\",\"actual\":\"{1}\",\"load_ms\":{2},\"first_ms\":{3},\"warm_ms\":{4},\"rss_before_load\":{5},\"rss_loaded\":{6},\"rss_after\":{7},\"regions\":{8}}}",
                    backend, model.Backend, load, first, warm, beforeLoad, loaded, after, regions.Length));
                model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Analyze(image));
            }
            // Full Studio backend with default backend selection, not a mocked decoder.
            using (var studio = MNNStudioNativeBackend.Load(MNNStudioModel.Read(directory), 4, false))
            {
                var result = studio.Run(new MNNStudioRequest {VisionImage = image, LayoutScoreThreshold = .5f});
                Assert.AreEqual(13, result.DocumentLayout.Regions.Length);
                StringAssert.Contains("table", result.Text);
                Assert.NotNull(result.GeneratedImage);
                var texture = MNNStudioImage.CreateTexture(result.GeneratedImage);
                try { File.WriteAllBytes(Path.Combine(artifacts, backend + "-regions.png"), texture.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
        }

        private static void AssertBox(MNNDetection box, float x, float y, float width, float height)
        {
            Assert.That(box.Score, Is.GreaterThan(.9f));
            Assert.That(box.X, Is.EqualTo(x).Within(12));
            Assert.That(box.Y, Is.EqualTo(y).Within(12));
            Assert.That(box.Width, Is.EqualTo(width).Within(16));
            Assert.That(box.Height, Is.EqualTo(height).Within(16));
        }

        [Test]
        public void OfficialGraphAuxiliaryInputsDetermineCoordinateSpace()
        {
            string models = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            string fixtures = Environment.GetEnvironmentVariable("MNN_TEST_FIXTURE_ROOT");
            string artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT");
            Assert.NotNull(models); Assert.NotNull(fixtures); Assert.NotNull(artifacts);
            var image = MNNStudioImage.ReadRgb(Path.Combine(fixtures, "layout.jpg"));
            float[] pixels = MNNImageSampling.Tensor(image, 800, 800, 0, 255);
            var clock = Stopwatch.StartNew();
            using (var graph = new MNNGenerationGraph(Path.Combine(models, "tansen87--PP-DocLayoutV3_mnn/PP-DocLayoutV3.mnn"), 4, backendType: MNNBackendType.CPU))
            {
                TestContext.WriteLine("load_ms=" + clock.Elapsed.TotalMilliseconds);
                MNNGenerationGraph.Output Run(float scale) => graph.RunOutputs(new[] {"fetch_name_0"},
                    new MNNGenerationGraph.Input("image", pixels, 1, 3, 800, 800),
                    new MNNGenerationGraph.Input("im_shape", new[] {800f, 800f}, 1, 2),
                    new MNNGenerationGraph.Input("scale_factor", new[] {scale, scale}, 1, 2))[0];
                clock.Restart();
                var neutral = Run(1);
                TestContext.WriteLine("run_ms=" + clock.Elapsed.TotalMilliseconds + " shape=" + string.Join(",", neutral.Shape));
                Assert.AreEqual(2, neutral.Shape.Length);
                Assert.AreEqual(7, neutral.Shape[1]);
                File.WriteAllLines(Path.Combine(artifacts, "neutral.csv"), Enumerable.Range(0, neutral.Shape[0])
                    .Select(i => string.Join(",", neutral.Values.Skip(i * 7).Take(7).Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))));
                var scaled = Run(2);
                CollectionAssert.AreEqual(neutral.Shape, scaled.Shape);
                for (int row = 0; row < neutral.Shape[0]; ++row)
                    for (int column = 0; column < 7; ++column)
                        Assert.That(scaled.Values[row * 7 + column], Is.EqualTo(neutral.Values[row * 7 + column] / (column >= 2 && column <= 5 ? 2 : 1)).Within(.01), "scale_factor convention");
            }
        }
    }
}
