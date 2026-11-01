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
    [Category("PosePlayer")]
    public class PosePlayerTests
    {
        [TestCase("person-a", MNNBackendType.CPU)]
        [TestCase("person-b", MNNBackendType.CPU)]
        [TestCase("person-a", MNNBackendType.Auto)]
        [TestCase("person-b", MNNBackendType.Auto)]
        public void MoveNetRealPersonJointsMemoryAndSpeed(string sample, MNNBackendType requested)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_POSE_ROOT");
            if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Application.persistentDataPath, "pose");
            if (!File.Exists(Path.Combine(root, "movenet-lightning.mnn")))
                Assert.Ignore("Stage the prepared MoveNet graph/config and both independent person fixtures in persistentDataPath/pose or provide --pose-root.");
            bool firstSample = sample == "person-a";
            string reference = Path.Combine(root, sample);
            int width = firstSample ? 205 : 165, height = firstSample ? 550 : 500;
            byte[] rgb = ReadVerified(reference, "source.rgb", firstSample ? "e68bcda7bb528e6d6ee4bf7c1033927d2d37e76aa38a05bfecc7ef8a6429c2f3" : "9282f1c4eb4b02acfef9a359ae963fddb16502b375f79ceb26c34cc3d57e4db9");
            byte[] bytes = ReadVerified(reference, "input.i32", firstSample ? "5340ec55dd39caee5df546b52b319201bbcb3cd095e6a14291c743e8b8634e1b" : "7f1c9858bb472e1c7db857eef6de5c97ccb69568a0edd4bc35bc2fee557a5445");
            var input = new int[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, input, 0, bytes.Length);
            bytes = ReadVerified(reference, "output.f32", firstSample ? "39b2f10f3432cfd11e3b000c391a5a0af7330dddee4208e261eda4786d4a9058" : "004555a4af3443855dca4a58fa43afb64cd9c92fea0e57865507eab61286b067");
            var expected = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length);
            var image = new MNNRgbImage(rgb, width, height);
            CollectionAssert.AreEqual(input, MNNImageTask.PoseTensor(image), "Separable antialiased RGB resize must match independent Pillow pixels exactly.");
            double maxError = 0, meanError = 0;
            using (var graph = new MNNGenerationGraph(MNNImageTaskConfig.Read(root).Resolve(root, true), 4, backendType: requested, precisionMode: MNNPrecisionMode.High))
            {
                var output = graph.RunOutputs(new[] {"output_0"}, new MNNGenerationGraph.Input("input", input, 1, 192, 192, 3))[0];
                MNNImageTask.RequireShape(output, 1, 1, 17, 3);
                Assert.AreEqual(51, expected.Length);
                var actual = output.Values;
                for (int i = 0; i < expected.Length; ++i)
                {
                    double error = Math.Abs(expected[i] - actual[i]);
                    maxError = Math.Max(maxError, error); meanError += error;
                }
                meanError /= expected.Length;
                TestContext.WriteLine("MoveNet independent ONNX error max=" + maxError + "; mean=" + meanError);
                Assert.Less(maxError, .001); Assert.Less(meanError, .0001);
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
                Assert.AreEqual(MNNAcceleration.Resolve(requested), model.Backend);
                Assert.AreEqual(width, result.Pose.Width); Assert.AreEqual(height, result.Pose.Height);
                Assert.AreEqual(17, result.Pose.Keypoints.Count);
                int visible = 0;
                double maxPixelError = 0;
                for (int k = 0; k < 17; ++k)
                {
                    var p = result.Pose.Keypoints[k];
                    double x = (expected[k * 3 + 1] * 192 - (firstSample ? 60 : 64)) * width / (firstSample ? 72.0 : 63.0);
                    double y = expected[k * 3] * height;
                    maxPixelError = Math.Max(maxPixelError, Math.Max(Math.Abs(p.X - x), Math.Abs(p.Y - y)));
                    Assert.That(p.Confidence, Is.EqualTo(expected[k * 3 + 2]).Within(.001));
                    Assert.That(repeat.Pose.Keypoints[k].X, Is.EqualTo(p.X).Within(.001));
                    Assert.That(repeat.Pose.Keypoints[k].Y, Is.EqualTo(p.Y).Within(.001));
                    if (p.IsInBounds && p.Confidence >= .2) ++visible;
                }
                Assert.Less(maxPixelError, .5);
                Assert.Greater(visible, 13);
                Assert.Greater(result.Pose.GetKeypoint(MNNPoseJoint.LeftAnkle).Y, result.Pose.GetKeypoint(MNNPoseJoint.Nose).Y + height / 2f);
                string measurement = string.Format(CultureInfo.InvariantCulture,
                    "{{\"device\":\"{0}\",\"sample\":\"{1}\",\"requested\":\"{2}\",\"actual\":\"{3}\",\"precision\":\"High\",\"load_ms\":{4},\"first_ms\":{5},\"warm_ms\":{6},\"rss_before\":{7},\"rss_loaded\":{8},\"rss_after\":{9},\"peak_rss\":{10},\"physical_footprint\":{11},\"max_error\":{12},\"mean_error\":{13},\"max_source_pixel_error\":{14}}}",
                    SystemInfo.deviceModel, sample, requested, model.Backend, load, first, warm, before.residentBytes, loaded.residentBytes,
                    after.residentBytes, after.peakResidentBytes, after.physicalFootprintBytes, maxError, meanError, maxPixelError);
                File.WriteAllText(Path.Combine(root, sample + "-" + requested + "-measurements.json"), measurement);
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

        private static byte[] ReadVerified(string directory, string name, string expected)
        {
            byte[] data = File.ReadAllBytes(Path.Combine(directory, name));
            using (var sha = SHA256.Create())
                Assert.AreEqual(expected, BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant(), "Changed independent reference: " + name);
            return data;
        }
    }
}
