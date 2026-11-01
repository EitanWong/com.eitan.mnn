using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("StyleTransferPlayer")]
    public class StyleTransferPlayerTests
    {
        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void CandyMatchesIndependentOnnxPixelsMemoryAndSpeed(MNNBackendType requested)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_STYLE_ROOT");
            if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Application.persistentDataPath, "style-transfer");
            if (!File.Exists(Path.Combine(root, "style-candy-8.mnn")))
                Assert.Ignore("Stage the pinned Candy MNN profile, source.rgb and independent reference-{input,output}.f32 in persistentDataPath/style-transfer, or provide --style-root.");
            byte[] source = ReadVerified(root, "source.rgb", "b44e2cb68310f0a6872812a05ddabbc084ba90bfdcf7b67eadacfce3426b6e3f");
            float[] input = Floats(ReadVerified(root, "reference-input.f32", "107c2630829bce51d6c336724e0300285283fe91e0a7f81f55a13c52204bc1bd"));
            float[] expected = Floats(ReadVerified(root, "reference-output.f32", "1ee914533b51911b6c02ef03b0e4d5ecba86eae68967930bed09f3280799cebe"));
            var image = new MNNRgbImage(source, 1080, 1080);
            var resized = MNNImageSampling.ResizeBilinear(image, 224, 224, CancellationToken.None);
            CollectionAssert.AreEqual(input, MNNImageSampling.Tensor(resized, 224, 224, 0, 1), "Preprocessing must match independent Pillow pixels.");
            double maxFloat = 0, meanFloat = 0;
            using (var graph = new MNNGenerationGraph(Path.Combine(root, "style-candy-8.mnn"), 4,
                backendType: requested, precisionMode: MNNPrecisionMode.High))
            {
                var output = graph.RunOutputs(new[] {"output1"}, new MNNGenerationGraph.Input("input1", input, 1, 3, 224, 224))[0];
                MNNImageTask.RequireShape(output, 1, 3, 224, 224);
                float[] values = output.Values;
                Assert.AreEqual(expected.Length, values.Length);
                for (int i = 0; i < values.Length; ++i)
                {
                    double error = Math.Abs(expected[i] - values[i]);
                    maxFloat = Math.Max(maxFloat, error);
                    meanFloat += error;
                }
                meanFloat /= values.Length;
                Assert.AreEqual(MNNAcceleration.Resolve(requested), graph.Backend, "Reference correctness must hold on the requested available backend.");
                TestContext.WriteLine("Raw pixel error max=" + maxFloat + "; mean=" + meanFloat);
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
                Assert.AreEqual(MNNAcceleration.Resolve(requested), model.Backend);
                Assert.AreEqual(224, result.Image.Width);
                Assert.AreEqual(224, result.Image.Height);
                CollectionAssert.AreEqual(result.Image.Rgb, repeat.Image.Rgb);
                const int plane = 224 * 224;
                int maxPixel = 0;
                double meanPixel = 0;
                for (int i = 0; i < plane; ++i)
                    for (int c = 0; c < 3; ++c)
                    {
                        int error = Math.Abs(result.Image.Rgb[i * 3 + c] - MNNImageSampling.Byte(expected[c * plane + i]));
                        maxPixel = Math.Max(maxPixel, error);
                        meanPixel += error;
                    }
                meanPixel /= 3 * plane;
                Assert.LessOrEqual(maxPixel, 1);
                Assert.Less(meanPixel, .01);
                string measurement = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"device\":\"{0}\",\"requested\":\"{1}\",\"actual\":\"{2}\",\"precision\":\"High\",\"load_ms\":{3},\"first_ms\":{4},\"warm_ms\":{5},\"rss_before\":{6},\"rss_loaded\":{7},\"rss_after\":{8},\"peak_rss\":{9},\"physical_footprint\":{10},\"max_float_error\":{11},\"mean_float_error\":{12},\"max_pixel_error\":{13},\"mean_pixel_error\":{14}}}",
                    SystemInfo.deviceModel, requested, model.Backend, load, first, warm, before.residentBytes, loaded.residentBytes,
                    after.residentBytes, after.peakResidentBytes, after.physicalFootprintBytes, maxFloat, meanFloat, maxPixel, meanPixel);
                File.WriteAllText(Path.Combine(root, requested + "-measurements.json"), measurement);
                TestContext.WriteLine(measurement);
                // Raw RGB spans 0–255. FP32 convolution reductions differ by CPU
                // kernel (Android max .155, mean .0041). Keep a quarter-level
                // bound plus independent byte checks: <=1 error and >99% exact.
                // FP16 Metal (max 2.15, mean .098) fails these same gates.
                Assert.Less(maxFloat, .25);
                Assert.Less(meanFloat, .01);
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
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, name));
            using (var sha = SHA256.Create())
                Assert.AreEqual(expected, BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(), "Independent reference was changed: " + name);
            return bytes;
        }

        private static float[] Floats(byte[] bytes)
        {
            Assert.AreEqual(3 * 224 * 224 * 4, bytes.Length);
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
