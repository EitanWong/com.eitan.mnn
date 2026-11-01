using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Explicit("Requires the pinned Depth Anything V2 Small graph and independent original ONNX references; no downloads.")]
    public class DepthIntegrationTests
    {
        [TestCase("street", 810, 1080, MNNBackendType.CPU)]
        [TestCase("square", 1080, 1080, MNNBackendType.CPU)]
        [TestCase("street", 810, 1080, MNNBackendType.Auto)]
        [TestCase("square", 1080, 1080, MNNBackendType.Auto)]
        public void DepthOriginalOnnxMatchesSharedApiAndStudio(string sample, int width, int height, MNNBackendType backend)
        {
            string models = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            string directory = Path.Combine(models, "DepthAnything--V2-Small");
            string reference = Path.GetFullPath(Path.Combine(models, "../../../../TestArtifacts~/TaskIntegration/Depth", sample));
            float[] Read(string name)
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(reference, name));
                var result = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length); return result;
            }
            var input = Read("input.f32"); var expected = Read("output.f32");
            var image = new MNNRgbImage(File.ReadAllBytes(Path.Combine(reference, "source.rgb")), width, height);
            var prepared = MNNImageTask.DepthTensor(image);
            Assert.AreEqual(input.Length, prepared.Length);
            double preprocessing = input.Select((v, i) => Math.Abs(v - prepared[i])).Max();
            Assert.Less(preprocessing, 1e-6);
            void Compare(MNNImageTaskResult result)
            {
                Assert.NotNull(result.Depth); var actual = result.Depth.RelativeInverseDepth;
                Assert.AreEqual(expected.Length, actual.Length);
                double max = actual.Select((v, i) => Math.Abs(v - expected[i])).Max(), mean = actual.Select((v, i) => Math.Abs(v - expected[i])).Average();
                double range = expected.Max() - expected.Min();
                TestContext.WriteLine("depth " + sample + " normalizedMax=" + max / range + " max=" + max + " mean=" + mean + " preprocessing=" + preprocessing);
                Assert.Less(max / range, .001); Assert.Less(mean / range, .0001);
                Assert.AreEqual(width, result.Image.Width); Assert.AreEqual(height, result.Image.Height);
            }
            using (var model = MNNImageTask.Load(directory, backendType: backend))
            {
                var timer = Stopwatch.StartNew(); Compare(model.Process(image));
                TestContext.WriteLine("first shared CPU depth ms=" + timer.Elapsed.TotalMilliseconds);
                Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                Compare(model.Process(image));
            }
            var studioModel = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.DepthEstimation, studioModel.Task);
            Assert.True(MNNStudioTasks.Get((int)studioModel.Task).CanRun(studioModel));
            using (var studio = MNNStudioNativeBackend.Load(studioModel, 4, false))
            {
                var result = studio.Run(new MNNStudioRequest {VisionImage = image}); Compare(result.ImageTask);
                StringAssert.Contains("CPU", result.BackendDescription);
                Assert.Greater(MNNChatStudio.EncodeDepthPfm(result.ImageTask.Depth).Length, expected.Length * 4);
            }
            Assert.Throws<NotSupportedException>(() => MNNImageTask.Load(directory, backendType: MNNBackendType.Metal));
            Assert.Throws<NotSupportedException>(() => MNNImageTask.Load(directory, backendType: MNNBackendType.OpenCL));
        }
    }
}
