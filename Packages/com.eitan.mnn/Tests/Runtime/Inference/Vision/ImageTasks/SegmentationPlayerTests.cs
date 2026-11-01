using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("SegmentationPlayer")]
    public class SegmentationPlayerTests
    {
        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void DeepLabRealImageClassesLogitsMemoryAndSpeed(MNNBackendType requested)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_SEGMENTATION_ROOT");
            if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Application.persistentDataPath, "segmentation");
            if (!File.Exists(Path.Combine(root, "deeplabv3.mnn")))
                Assert.Ignore("Stage the pinned DeepLab profile and independent source.rgb, input.f32, output.f32, classes.u8 in persistentDataPath/segmentation, or provide --segmentation-root.");
            byte[] rgb = ReadVerified(root, "source.rgb", "8739ae2c34b8713bcc185201414014011b601159d5ec717275e4a2625f9fbded");
            float[] input = Floats(ReadVerified(root, "input.f32", "71e6ac44e49cc1853585f203828dc701603d3f99a88513ce029890864ff1e02d"));
            float[] expected = Floats(ReadVerified(root, "output.f32", "26a23de7f06205aecb838eefe5d71336e430a6fa01b8c560db50fdce9460aa59"));
            byte[] grid = ReadVerified(root, "classes.u8", "8f94e9c3870570989db1446baed3c33a1b00a70ab7520317a15b05b6f38f2e54");
            var image = new MNNRgbImage(rgb, 810, 1080);
            CollectionAssert.AreEqual(input, MNNImageTask.SegmentationTensor(image), "Preprocessing must match independent Pillow/LiteRT pixels.");
            double maxError = 0, meanError = 0;
            using (var graph = new MNNGenerationGraph(MNNImageTaskConfig.Read(root).Resolve(root, true), 4,
                backendType: requested, precisionMode: MNNPrecisionMode.High))
            {
                var output = graph.RunOutputs(new[] {"ResizeBilinear_3"}, new MNNGenerationGraph.Input("sub_7", input, 1, 257, 257, 3))[0];
                MNNImageTask.RequireShape(output, 1, 257, 257, 21);
                float[] values = output.Values;
                Assert.AreEqual(expected.Length, values.Length);
                for (int i = 0; i < values.Length; ++i)
                {
                    double error = Math.Abs(expected[i] - values[i]);
                    maxError = Math.Max(maxError, error); meanError += error;
                }
                meanError /= values.Length;
                TestContext.WriteLine("Independent logits max=" + maxError + "; mean=" + meanError);
                Assert.Less(maxError, .01);
                Assert.Less(meanError, .001);
                Assert.AreEqual(MNNAcceleration.Resolve(requested), graph.Backend);
            }
            var before = PlayerMeasurements.Read();
            var timer = Stopwatch.StartNew();
            using (var model = MNNImageTask.Load(root, backendType: requested))
            {
                double load = timer.Elapsed.TotalMilliseconds;
                var loaded = PlayerMeasurements.Read();
                timer.Restart(); var result = model.Process(image); double first = timer.Elapsed.TotalMilliseconds;
                timer.Restart(); var repeat = model.Process(image); double warm = timer.Elapsed.TotalMilliseconds;
                var after = PlayerMeasurements.Read();
                var mask = result.Segmentation;
                Assert.AreEqual(810, mask.Width); Assert.AreEqual(1080, mask.Height);
                Assert.AreEqual(MNNAcceleration.Resolve(requested), model.Backend);
                CollectionAssert.AreEqual(mask.ClassIds, repeat.Segmentation.ClassIds);
                int matches = 0;
                float minConfidence = 1, maxConfidence = 0;
                for (int y = 0; y < mask.Height; ++y)
                    for (int x = 0; x < mask.Width; ++x)
                    {
                        int gy = Math.Min(256, (int)((y + .5) * 257 / mask.Height));
                        int gx = Math.Min(256, (int)((x + .5) * 257 / mask.Width));
                        if (mask.ClassAt(x, y) == grid[gy * 257 + gx]) ++matches;
                        float score = mask.ConfidenceAt(x, y);
                        minConfidence = Math.Min(minConfidence, score); maxConfidence = Math.Max(maxConfidence, score);
                    }
                double agreement = (double)matches / (mask.Width * mask.Height);
                Assert.Greater(agreement, .999);
                Assert.GreaterOrEqual(minConfidence, 1f / 21);
                Assert.LessOrEqual(maxConfidence, 1);
                int[] counts = mask.ClassPixelCounts;
                Assert.Greater(counts[6], mask.Width * mask.Height / 5);
                Assert.Greater(counts[15], mask.Width * mask.Height / 20);
                string measurement = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"device\":\"{0}\",\"requested\":\"{1}\",\"actual\":\"{2}\",\"precision\":\"High\",\"load_ms\":{3},\"first_ms\":{4},\"warm_ms\":{5},\"rss_before\":{6},\"rss_loaded\":{7},\"rss_after\":{8},\"peak_rss\":{9},\"physical_footprint\":{10},\"max_logit_error\":{11},\"mean_logit_error\":{12},\"class_agreement\":{13}}}",
                    SystemInfo.deviceModel, requested, model.Backend, load, first, warm, before.residentBytes, loaded.residentBytes,
                    after.residentBytes, after.peakResidentBytes, after.physicalFootprintBytes, maxError, meanError, agreement);
                File.WriteAllText(Path.Combine(root, requested + "-measurements.json"), measurement);
                TestContext.WriteLine(measurement);
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.Throws<OperationCanceledException>(() => model.Process(image, cancelled.Token));
                }
                model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Process(image));
            }
        }

        private static byte[] ReadVerified(string root, string name, string expected)
        {
            byte[] data = File.ReadAllBytes(Path.Combine(root, name));
            using (var sha = SHA256.Create())
                Assert.AreEqual(expected, BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant(), "Changed independent reference: " + name);
            return data;
        }

        private static float[] Floats(byte[] bytes)
        {
            Assert.AreEqual(0, bytes.Length % 4);
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
