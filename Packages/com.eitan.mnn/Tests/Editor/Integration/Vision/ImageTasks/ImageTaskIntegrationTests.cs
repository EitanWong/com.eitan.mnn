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
    [Explicit("Requires existing pinned image-task weights and the explicit fixture directory; never downloads.")]
    public class ImageTaskIntegrationTests
    {
        private string _models, _artifacts;
        private MNNRgbImage _image;

        [OneTimeSetUp]
        public void Prepare()
        {
            _models = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_MODEL_ROOT.");
            _artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_ARTIFACT_ROOT.");
            string fixtures = Environment.GetEnvironmentVariable("MNN_TEST_FIXTURE_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_FIXTURE_ROOT.");
            _image = MNNStudioImage.ReadRgb(Path.Combine(fixtures, "face.jpg"));
            Directory.CreateDirectory(_artifacts);
            // Converted local profiles have their own source/geometry fixtures.
            // This suite exercises the eight publisher-hosted image exports.
            foreach (var source in MNNImageTaskProfiles.All.Where(source => source.Url != null))
                MNNImageTaskProfiles.Prepare(Path.Combine(_models, source.Repository.Replace("/", "--")), source.Repository);
        }

        [TestCase(MNNImageTaskProfile.ModNet)]
        [TestCase(MNNImageTaskProfile.RealEsrganX4)]
        [TestCase(MNNImageTaskProfile.UVDoc)]
        [TestCase(MNNImageTaskProfile.Scrfd500M)]
        [TestCase(MNNImageTaskProfile.MobileFaceNet)]
        [TestCase(MNNImageTaskProfile.Landmark106)]
        [TestCase(MNNImageTaskProfile.MobileNetV2)]
        [TestCase(MNNImageTaskProfile.PglNet)]
        public void ActualWeightsProduceTypedResultsAndCanBeReused(MNNImageTaskProfile profile)
        {
            var source = MNNImageTaskProfiles.Get(profile);
            string directory = Path.Combine(_models, source.Repository.Replace("/", "--"));
            var timer = Stopwatch.StartNew();
            using (var model = MNNImageTask.Load(directory, backendType: MNNBackendType.CPU))
            {
                double load = timer.Elapsed.TotalMilliseconds;
                MNNRgbImage input = _image;
                if (profile == MNNImageTaskProfile.MobileFaceNet || profile == MNNImageTaskProfile.Landmark106)
                {
                    using (var detector = MNNImageTask.Load(Path.Combine(_models, MNNImageTaskProfiles.Get(MNNImageTaskProfile.Scrfd500M).Repository.Replace("/", "--")), backendType: MNNBackendType.CPU))
                    {
                        var faces = detector.Process(_image);
                        Assert.Greater(faces.Detections.Length, 0, "The frontal face fixture must be detected.");
                        input = profile == MNNImageTaskProfile.MobileFaceNet ? MNNImageTask.AlignFace(_image, faces.FaceKeypoints[0])
                            : MNNImageTask.CropFace(_image, faces.Detections[0]);
                    }
                }
                if (profile == MNNImageTaskProfile.RealEsrganX4)
                {
                    // Small image spans two tiles and exercises the halo crop.
                    var pixels = new byte[129 * 48 * 3];
                    for (int y = 0; y < 48; ++y)
                        for (int x = 0; x < 129; ++x)
                            for (int c = 0; c < 3; ++c)
                                pixels[(y * 129 + x) * 3 + c] = MNNImageSampling.Byte(MNNImageSampling.Pixel(_image.Pixels, _image.Width, _image.Height, x * 3, y * 3, c));
                    input = new MNNRgbImage(pixels, 129, 48);
                }
                long rssBefore = Process.GetCurrentProcess().WorkingSet64;
                timer.Restart();
                var result = model.Process(input);
                double first = timer.Elapsed.TotalMilliseconds;
                timer.Restart();
                var repeat = model.Process(input);
                double second = timer.Elapsed.TotalMilliseconds;
                long rssAfter = Process.GetCurrentProcess().WorkingSet64;
                TestContext.WriteLine($"{profile}: load={load:F2}ms first={first:F2}ms repeat={second:F2}ms rssBefore={rssBefore} rssAfter={rssAfter}");
                File.WriteAllText(Path.Combine(_artifacts, profile + ".json"), string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"profile\":\"{0}\",\"backend\":\"{1}\",\"load_ms\":{2},\"first_ms\":{3},\"repeat_ms\":{4},\"rss_before\":{5},\"rss_after\":{6}}}", profile, model.Backend, load, first, second, rssBefore, rssAfter));
                if (result.Image != null)
                {
                    CollectionAssert.AreEqual(result.Image.Rgb, repeat.Image.Rgb);
                    var texture = MNNStudioImage.CreateTexture(result.Image);
                    try { File.WriteAllBytes(Path.Combine(_artifacts, profile + ".png"), texture.EncodeToPNG()); }
                    finally { UnityEngine.Object.DestroyImmediate(texture); }
                    Assert.Greater(result.Image.Rgb.Max() - result.Image.Rgb.Min(), 40);
                }
                if (profile == MNNImageTaskProfile.RealEsrganX4)
                {
                    Assert.AreEqual(input.Width * 4, result.Image.Width);
                    Assert.AreEqual(input.Height * 4, result.Image.Height);
                    // Compare tiling with one full invocation of the same official graph.
                    using (var graph = new MNNGenerationGraph(Path.Combine(directory, source.Graph), 4, backendType: MNNBackendType.CPU))
                    {
                        var full = graph.RunOutputs(new[] {"output"}, new MNNGenerationGraph.Input("input", MNNImageSampling.Tensor(input, input.Width, input.Height, 0, 255), 1, 3, input.Height, input.Width))[0];
                        int plane = result.Image.Width * result.Image.Height;
                        float[] values = full.Values;
                        int maxError = 0;
                        for (int i = 0; i < plane; ++i)
                            for (int c = 0; c < 3; ++c)
                                maxError = Math.Max(maxError, Math.Abs(result.Image.Rgb[i * 3 + c] - MNNImageSampling.Byte(values[c * plane + i] * 255)));
                        Assert.LessOrEqual(maxError, 1, "Tile halo must preserve the full-graph result.");
                    }
                }
                if (result.Alpha != null)
                {
                    Assert.AreEqual(input.Width * input.Height, result.Alpha.Length);
                    Assert.That(result.Alpha.Min(), Is.InRange(0, 1));
                    Assert.That(result.Alpha.Max(), Is.InRange(0, 1));
                    Assert.Greater(result.Alpha.Max() - result.Alpha.Min(), .5);
                }
                if (result.Detections != null)
                {
                    Assert.Greater(result.Detections.Length, 0);
                    var face = result.Detections[0];
                    Assert.Greater(face.Score, .8);
                    Assert.That(face.X, Is.InRange(150, 280));
                    Assert.That(face.Y, Is.InRange(150, 280));
                    Assert.AreEqual(5, result.FaceKeypoints[0].Length);
                }
                if (result.Vector != null)
                {
                    Assert.AreEqual(512, result.Vector.Length);
                    Assert.That(result.Vector.Sum(v => v * v), Is.EqualTo(1).Within(1e-5));
                    Assert.That(MNNStudioPrompt.Cosine(result.Vector, repeat.Vector), Is.GreaterThan(.99999));
                }
                if (result.Points != null)
                {
                    Assert.AreEqual(106, result.Points.Length);
                    Assert.True(result.Points.All(p => p.X >= 0 && p.X <= input.Width && p.Y >= 0 && p.Y <= input.Height));
                }
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.Throws<OperationCanceledException>(() => model.Process(input, cancelled.Token));
                }
                model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Process(input));
            }
        }
    }
}
