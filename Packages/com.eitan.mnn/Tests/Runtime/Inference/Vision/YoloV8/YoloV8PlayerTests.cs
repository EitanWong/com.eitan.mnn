using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("YoloV8Player")]
    public class YoloV8PlayerTests
    {
        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void DetectsBusAndPeopleInActualPlayer(MNNBackendType backend)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_VISION_ROOT");
            if (string.IsNullOrWhiteSpace(root))
                Assert.Ignore("Provide --vision-root with the prepared YOLO graph and bus.jpg fixture.");
            var image = VisionPlayerData.Read(Path.Combine(root, "bus.jpg"));
            string directory = Path.Combine(root, "modelscope", "rap20171201", "YOLOv8-N-MNN");
            using (var detector = MNNYoloV8.Load(directory, backendType: backend))
            {
                var boxes = detector.Detect(image);
                TestContext.WriteLine("Actual detection backend=" + detector.Backend);
                var bus = boxes.Single(box => box.Label == "bus" && box.Score > .6);
                Assert.Greater(Overlap(bus, 10, 225, 800, 550), .7);
                Assert.True(boxes.Any(box => box.Label == "person" && Overlap(box, 50, 395, 195, 515) > .6));
                Assert.True(boxes.Any(box => box.Label == "person" && Overlap(box, 220, 400, 135, 460) > .6));
                Assert.IsEmpty(detector.Detect(new MNNRgbImage(Enumerable.Repeat((byte)255, 320 * 180 * 3).ToArray(), 320, 180)));
            }
        }

        private static float Overlap(MNNDetection box, float x, float y, float width, float height)
        {
            float intersection = Math.Max(0, Math.Min(box.X + box.Width, x + width) - Math.Max(box.X, x)) *
                Math.Max(0, Math.Min(box.Y + box.Height, y + height) - Math.Max(box.Y, y));
            return intersection / (box.Width * box.Height + width * height - intersection);
        }
    }
}
