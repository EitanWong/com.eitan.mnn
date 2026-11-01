using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Explicit("Requires the existing verified YOLOv8 graph and bus.jpg fixture; no downloads.")]
    public class YoloV8IntegrationTests
    {
        private string _root;
        private string _directory;
        private MNNRgbImage _image;

        [OneTimeSetUp]
        public void Prepare()
        {
            _root = Environment.GetEnvironmentVariable("MNN_TEST_VISION_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_VISION_ROOT.");
            _directory = Path.Combine(_root, "modelscope", MNNYoloV8Model.Repository);
            MNNYoloV8Model.Prepare(_directory);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(Path.Combine(_root, "bus.jpg"))));
                var pixels = texture.GetPixels32();
                var rgb = new byte[pixels.Length * 3];
                for (int y = 0; y < texture.height; ++y)
                    for (int x = 0; x < texture.width; ++x)
                    {
                        var pixel = pixels[(texture.height - 1 - y) * texture.width + x];
                        int offset = (y * texture.width + x) * 3;
                        rgb[offset] = pixel.r;
                        rgb[offset + 1] = pixel.g;
                        rgb[offset + 2] = pixel.b;
                    }
                _image = new MNNRgbImage(rgb, texture.width, texture.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void PackedTensorMapsLogicalRgbAndPreservesThePublishedOutputShape()
        {
            using (var interpreter = MNNInterpreter.CreateFromFile(Path.Combine(_directory, "yolov8n.mnn")))
            using (var session = interpreter.CreateSession(new MNNSessionConfig {BackendType = MNNBackendType.CPU}))
            {
                var input = session.GetInput("images");
                TestContext.WriteLine("Input=" + string.Join(",", input.Shape) + "; layout=" + input.DimensionType + "; count=" + input.ElementCount + "; bytes=" + input.ByteSize);
                CollectionAssert.AreEqual(new[] {1, 3, 640, 640}, input.Shape);
                Assert.AreEqual(3 * 640 * 640, input.ElementCount);
                Assert.Greater(input.ByteSize, input.ElementCount * sizeof(float), "This publisher graph exercises padded native RGB storage.");
                var data = MNNYoloV8.Letterbox(_image, 640, 640, out _, out _, out _);
                input.CopyFromArray(data);
                Assert.That(input.CopyToArray<float>(), Is.EqualTo(data).Within(1e-6));
                session.Run();
                var output = session.GetOutput("output0");
                var values = output.CopyToArray<float>();
                CollectionAssert.AreEqual(new[] {1, 84, 8400}, output.Shape);
                Assert.AreEqual(84 * 8400, values.Length);
                TestContext.WriteLine("Output=" + string.Join(",", output.Shape) + "; count=" + values.Length + "; min=" + values.Min() + "; max=" + values.Max());
                TestContext.WriteLine("First=" + string.Join(",", values.Take(12)));
                TestContext.WriteLine("Scores=" + string.Join(",", values.Skip(4 * 8400).OrderByDescending(v => v).Take(12)));
                using (var writer = new BinaryWriter(File.Create(Path.Combine(_root, "yolo-output.f32"))))
                    foreach (float value in values) writer.Write(value);
            }
        }

        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void DetectsBusAndPeopleAtTheCorrectImageCoordinates(MNNBackendType backend)
        {
            using (var model = MNNYoloV8.Load(_directory, backendType: backend))
            {
                var detections = model.Detect(_image);
                foreach (var box in detections)
                {
                    TestContext.WriteLine(model.Backend + ": " + box.Label + " " + box.Score + " " + box.X + "," + box.Y + "," + box.Width + "," + box.Height);
                    Assert.That(box.X, Is.InRange(0, _image.Width));
                    Assert.That(box.Y, Is.InRange(0, _image.Height));
                    Assert.LessOrEqual(box.X + box.Width, _image.Width + .01f);
                    Assert.LessOrEqual(box.Y + box.Height, _image.Height + .01f);
                }
                Assert.IsTrue(detections.Any(box => box.Label == "bus" && box.Score > .6 && IoU(box, 10, 225, 800, 550) > .7));
                Assert.IsTrue(detections.Any(box => box.Label == "person" && IoU(box, 50, 395, 195, 515) > .6));
                Assert.IsTrue(detections.Any(box => box.Label == "person" && IoU(box, 220, 400, 135, 460) > .6));
                Assert.IsEmpty(model.Detect(new MNNRgbImage(Enumerable.Repeat((byte)255, 320 * 180 * 3).ToArray(), 320, 180)));
                Assert.IsEmpty(model.Detect(_image, scoreThreshold: 1));
                var repeated = model.Detect(_image);
                CollectionAssert.AreEqual(detections.Select(box => box.Label), repeated.Select(box => box.Label));
                Assert.That(repeated.Select(box => box.Score), Is.EqualTo(detections.Select(box => box.Score)).Within(1e-5));
                model.Dispose();
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Detect(_image));
            }
        }

        [UnityTest]
        public IEnumerator StudioRunsDetectionOnWorkerAndExportsAnUnmodifiedSourceWithOverlay()
        {
            var image = MNNStudioImage.ReadRgb(Path.Combine(_root, "bus.jpg"));
            byte[] original = image.ToArray();
            var studioModel = MNNStudioModel.Read(_directory);
            Assert.AreEqual(MNNStudioTask.ObjectDetection, studioModel.Task);
            Assert.True(MNNStudioTasks.Get((int)studioModel.Task).CanRun(studioModel));
            using (var session = new MNNStudioSession(MNNStudioNativeBackend.Load(studioModel, 2, false)))
            {
                session.Send(new MNNStudioRequest {Prompt = "Detect the objects in this image.", VisionImage = image});
                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (session.Busy && DateTime.UtcNow < deadline)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }
                Assert.False(session.Busy, "Studio detection did not finish.");
                Assert.IsNull(session.Error);
                Assert.IsTrue(session.Result.Detections.Any(box => box.Label == "bus"));
                StringAssert.Contains("bus", session.Result.Text);
                StringAssert.Contains("YOLOv8", session.Result.BackendDescription);
                CollectionAssert.AreEqual(original, image.ToArray());
                CollectionAssert.AreNotEqual(original, session.Result.GeneratedImage.Rgb);
                var texture = MNNStudioImage.CreateTexture(session.Result.GeneratedImage);
                try
                {
                    string path = Path.Combine(_root, "studio-detections.png");
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    var roundtrip = MNNStudioImage.ReadRgb(path);
                    CollectionAssert.AreEqual(session.Result.GeneratedImage.Rgb, roundtrip.ToArray());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
        }

        [UnityTest]
        public IEnumerator WorkerInferenceCancellationAndReloadUseOwnedImageData()
        {
            using (var model = MNNYoloV8.Load(_directory, backendType: MNNBackendType.CPU))
            {
                var task = model.DetectAsync(_image);
                while (!task.IsCompleted)
                    yield return null;
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.IsTrue(task.Result.Any(box => box.Label == "bus"));
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    Assert.Throws<OperationCanceledException>(() => model.Detect(_image, cancellationToken: cancellation.Token));
                }
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Detect(_image, scoreThreshold: float.NaN));
            }
            using (var reloaded = MNNYoloV8.Load(_directory, backendType: MNNBackendType.CPU))
                Assert.IsTrue(reloaded.Detect(_image).Any(box => box.Label == "bus"));
        }

        private static float IoU(MNNDetection box, float x, float y, float width, float height) =>
            MNNYoloV8.IntersectionOverUnion(box, new MNNDetection(0, "annotation", 1, x, y, width, height));
    }
}
