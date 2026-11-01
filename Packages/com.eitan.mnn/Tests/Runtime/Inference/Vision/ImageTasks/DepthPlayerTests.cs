using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("DepthPlayer")]
    public class DepthPlayerTests
    {
        [TestCase("street", MNNBackendType.CPU)]
        [TestCase("square", MNNBackendType.CPU)]
        [TestCase("street", MNNBackendType.Auto)]
        [TestCase("square", MNNBackendType.Auto)]
        public void DepthRealWeightsOriginalOnnxMemoryAndSpeed(string sample, MNNBackendType requested)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_DEPTH_ROOT");
            if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Application.persistentDataPath, "depth");
            if (!File.Exists(Path.Combine(root, "depth-anything-v2-small.mnn")))
                Assert.Ignore("Stage the pinned depth graph/config with both independent fixtures in persistentDataPath/depth or provide --depth-root.");
            bool street = sample == "street";
            string reference = Path.Combine(root, sample);
            int width = street ? 810 : 1080, height = 1080;
            var image = new MNNRgbImage(ReadVerified(reference, "source.rgb", street ? "8739ae2c34b8713bcc185201414014011b601159d5ec717275e4a2625f9fbded" : "b44e2cb68310f0a6872812a05ddabbc084ba90bfdcf7b67eadacfce3426b6e3f"), width, height);
            float[] input = Floats(ReadVerified(reference, "input.f32", street ? "a40f998d84f50e6cdd318b3d007363fd82c715387ef9ec5d9fb0c2c5259e1a39" : "09ba19e1afc23f64d78fa44eb7b5182ccbdbed8eec8c28f88edd930dbe7f141d"));
            float[] expected = Floats(ReadVerified(reference, "output.f32", street ? "8ec82c7116973a2f24a6c546e05c5850339fbbec84b907f04898c4134741c62e" : "10a1105e6e76ca3ba867a22502274eee3ed34f2ba6d2fac7de25044c4717de69"));
            float[] prepared = MNNImageTask.DepthTensor(image);
            Assert.AreEqual(input.Length, prepared.Length);
            double preprocessingError = 0;
            for (int i = 0; i < input.Length; ++i) preprocessingError = Math.Max(preprocessingError, Math.Abs(input[i] - prepared[i]));
            Assert.Less(preprocessingError, 1e-6, "Independent Pillow bicubic/CHW/ImageNet preprocessing mismatch.");
            var before = PlayerMeasurements.Read(); var timer = Stopwatch.StartNew();
            using (var model = MNNImageTask.Load(root, backendType: requested))
            {
                double load = timer.Elapsed.TotalMilliseconds; var loaded = PlayerMeasurements.Read();
                timer.Restart(); var result = model.Process(image); double first = timer.Elapsed.TotalMilliseconds;
                timer.Restart(); var repeat = model.Process(image); double warm = timer.Elapsed.TotalMilliseconds;
                var after = PlayerMeasurements.Read(); var depth = result.Depth;
                Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                Assert.AreEqual(width, depth.Width); Assert.AreEqual(height, depth.Height);
                Assert.AreEqual(518, depth.GridWidth); Assert.AreEqual(street ? 686 : 518, depth.GridHeight);
                float[] actual = depth.RelativeInverseDepth, again = repeat.Depth.RelativeInverseDepth;
                Assert.AreEqual(expected.Length, actual.Length);
                double max = 0, mean = 0, repeatError = 0;
                float minimum = float.MaxValue, maximum = 0;
                for (int i = 0; i < actual.Length; ++i)
                {
                    if (!(actual[i] >= 0 && actual[i] <= float.MaxValue)) Assert.Fail("Invalid relative inverse depth at " + i);
                    double error = Math.Abs(actual[i] - expected[i]); max = Math.Max(max, error); mean += error;
                    minimum = Math.Min(minimum, expected[i]); maximum = Math.Max(maximum, expected[i]);
                    repeatError = Math.Max(repeatError, Math.Abs(actual[i] - again[i]));
                }
                mean /= actual.Length; double range = maximum - minimum;
                Assert.Less(max / range, .001); Assert.Less(mean / range, .0001); Assert.Less(repeatError / range, .001);
                Assert.AreEqual(width * height * 3, result.Image.Rgb.Length);
                Assert.That(depth.SampleAt(width / 2, height / 2), Is.InRange(depth.Minimum, depth.Maximum));
                actual[0] = -1; Assert.GreaterOrEqual(depth.RelativeInverseDepth[0], 0, "Result arrays must be defensive copies.");
                string measurement = string.Format(CultureInfo.InvariantCulture,
                    "{{\"device\":\"{0}\",\"sample\":\"{1}\",\"requested\":\"{2}\",\"actual\":\"{3}\",\"precision\":\"High\",\"load_ms\":{4},\"first_ms\":{5},\"warm_ms\":{6},\"rss_before\":{7},\"rss_loaded\":{8},\"rss_after\":{9},\"peak_rss\":{10},\"physical_footprint\":{11},\"max_error\":{12},\"mean_error\":{13},\"normalized_max_error\":{14},\"preprocessing_max_error\":{15}}}",
                    SystemInfo.deviceModel, sample, requested, model.Backend, load, first, warm, before.residentBytes, loaded.residentBytes, after.residentBytes, after.peakResidentBytes, after.physicalFootprintBytes, max, mean, max / range, preprocessingError);
                File.WriteAllText(Path.Combine(root, sample + "-" + requested + "-measurements.json"), measurement); TestContext.WriteLine(measurement);
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel(); Assert.Throws<OperationCanceledException>(() => model.Process(image, cancelled.Token));
                }
                model.Dispose(); model.Dispose(); Assert.Throws<ObjectDisposedException>(() => model.Process(image));
            }
        }
        private static float[] Floats(byte[] bytes)
        {
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        private static byte[] ReadVerified(string root, string name, string hash)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, name));
            using (var sha = SHA256.Create())
                Assert.AreEqual(hash, BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(), "Changed independent depth reference: " + name);
            return bytes;
        }
    }
}
