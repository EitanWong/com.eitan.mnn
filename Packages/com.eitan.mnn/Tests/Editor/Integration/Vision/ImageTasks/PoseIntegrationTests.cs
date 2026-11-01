using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Explicit("Requires prepared MoveNet weights and independent ONNX/LiteRT references; never downloads.")]
    public class PoseIntegrationTests
    {
        [TestCase("person-a", 205, 550, MNNBackendType.CPU)]
        [TestCase("person-b", 165, 500, MNNBackendType.CPU)]
        [TestCase("person-a", 205, 550, MNNBackendType.Auto)]
        [TestCase("person-b", 165, 500, MNNBackendType.Auto)]
        public void MoveNetMatchesIndependentJointsAndStudio(string sample, int width, int height, MNNBackendType backend)
        {
            string models = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            string directory = Path.Combine(models, "TensorFlow--MoveNet-SinglePose-Lightning");
            string reference = Path.GetFullPath(Path.Combine(models, "../../../../TestArtifacts~/TaskIntegration/Pose", sample));
            byte[] bytes = File.ReadAllBytes(Path.Combine(reference, "input.i32"));
            var input = new int[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, input, 0, bytes.Length);
            bytes = File.ReadAllBytes(Path.Combine(reference, "output.f32"));
            var expected = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length);
            var image = new MNNRgbImage(File.ReadAllBytes(Path.Combine(reference, "source.rgb")), width, height);
            var artifacts = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT"), "Pose");
            Directory.CreateDirectory(artifacts);
            var managedInput = MNNImageTask.PoseTensor(image);
            byte[] managedBytes = new byte[managedInput.Length * 4]; Buffer.BlockCopy(managedInput, 0, managedBytes, 0, managedBytes.Length);
            File.WriteAllBytes(Path.Combine(artifacts, sample + "-managed-input.i32"), managedBytes);
            double max, mean;
            using (var graph = new MNNGenerationGraph(MNNImageTaskConfig.Read(directory).Resolve(directory, true), 4, backendType: backend, precisionMode: MNNPrecisionMode.High))
            {
                var output = graph.RunOutputs(new[] {"output_0"}, new MNNGenerationGraph.Input("input", input, 1, 192, 192, 3))[0];
                MNNImageTask.RequireShape(output, 1, 1, 17, 3);
                max = expected.Select((v, i) => Math.Abs(v - output.Values[i])).Max();
                mean = expected.Select((v, i) => Math.Abs(v - output.Values[i])).Average();
                TestContext.WriteLine("MoveNet " + sample + " " + graph.Backend + " max=" + max + " mean=" + mean);
                File.WriteAllText(Path.Combine(artifacts, sample + "-" + backend + "-raw.json"), string.Format(CultureInfo.InvariantCulture, "{{\"max_error\":{0},\"mean_error\":{1},\"actual_backend\":\"{2}\"}}", max, mean, graph.Backend));
                Assert.Less(max, .001); Assert.Less(mean, .0001);
                Assert.AreEqual(MNNAcceleration.Resolve(backend), graph.Backend);
            }
            CollectionAssert.AreEqual(input, managedInput);
            var timer = Stopwatch.StartNew();
            long before = Process.GetCurrentProcess().WorkingSet64;
            using (var model = MNNImageTask.Load(directory, backendType: backend))
            {
                double load = timer.Elapsed.TotalMilliseconds;
                timer.Restart(); var result = model.Process(image); double first = timer.Elapsed.TotalMilliseconds;
                timer.Restart(); var repeat = model.Process(image); double warm = timer.Elapsed.TotalMilliseconds;
                var referencePose = MNNImageTask.DecodePose(new MNNGenerationGraph.Output(expected, new[] {1, 1, 17, 3}), width, height);
                for (int k = 0; k < 17; ++k)
                {
                    var point = result.Pose.Keypoints[k]; var truth = referencePose.Keypoints[k];
                    Assert.That(point.X, Is.EqualTo(truth.X).Within(1)); Assert.That(point.Y, Is.EqualTo(truth.Y).Within(1));
                    Assert.That(point.Confidence, Is.EqualTo(truth.Confidence).Within(.001));
                    Assert.That(repeat.Pose.Keypoints[k].X, Is.EqualTo(point.X).Within(.001));
                }
                Assert.Greater(result.Pose.GetKeypoint(MNNPoseJoint.LeftAnkle).Y, result.Pose.GetKeypoint(MNNPoseJoint.Nose).Y + height / 2f);
                Assert.Greater(result.Pose.Keypoints.Count(p => p.Confidence >= .2 && p.IsInBounds), 13);
                File.WriteAllText(Path.Combine(artifacts, sample + "-" + backend + "-measurements.json"), string.Format(CultureInfo.InvariantCulture,
                    "{{\"load_ms\":{0},\"first_ms\":{1},\"warm_ms\":{2},\"rss_before\":{3},\"rss_after\":{4},\"peak_rss\":{5},\"actual_backend\":\"{6}\"}}",
                    load, first, warm, before, Process.GetCurrentProcess().WorkingSet64, Process.GetCurrentProcess().PeakWorkingSet64, model.Backend));
                var texture = MNNStudioImage.CreateTexture(MNNChatStudio.RenderPoseOverlay(result, .2f));
                try { File.WriteAllBytes(Path.Combine(artifacts, sample + "-" + backend + "-overlay.png"), texture.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            using (var studio = MNNStudioNativeBackend.Load(MNNStudioModel.Read(directory), 4, false))
            {
                var result = studio.Run(new MNNStudioRequest {VisionImage = image});
                Assert.AreEqual(17, result.ImageTask.Pose.Keypoints.Count);
                Assert.AreEqual(width, result.GeneratedImage.Width);
            }
        }
    }
}
