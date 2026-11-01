using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class PaddleOcrTests
    {
        [Test]
        public void DbComponentsKeepOuterRegionsAndScaleExpandedBoxesIntoSourceBounds()
        {
            var map = new float[64 * 64];
            for (int y = 10; y <= 20; ++y)
            for (int x = 12; x <= 42; ++x)
                map[y * 64 + x] = .9f;
            map[15 * 64 + 25] = 0; // An internal hole must not create a second text candidate.
            var boxes = MNNPaddleDbPostprocessor.Decode(map, new[] {1, 1, 64, 64}, 128, 96, new MNNPaddleOcrConfig());
            Assert.AreEqual(1, boxes.Length);
            Assert.Greater(boxes[0].Confidence, .85);
            var p = boxes[0].Corners;
            Assert.Less(p[0].x, 24);
            Assert.Less(p[0].y, 15);
            Assert.Greater(p[2].x, 84);
            Assert.Greater(p[2].y, 30);
            Assert.True(p.All(point => point.x >= 0 && point.x < 128 && point.y >= 0 && point.y < 96));
            Assert.AreEqual(0, MNNPaddleDbPostprocessor.Decode(new float[16], new[] {1, 1, 4, 4}, 4, 4, new MNNPaddleOcrConfig()).Length);
            map[0] = float.NaN;
            Assert.Throws<InvalidDataException>(() => MNNPaddleDbPostprocessor.Decode(map, new[] {1, 1, 64, 64}, 64, 64, new MNNPaddleOcrConfig()));
            Assert.Throws<InvalidDataException>(() => MNNPaddleDbPostprocessor.Decode(new float[4], new[] {1, 2, 1, 2}, 64, 64, new MNNPaddleOcrConfig()));
        }

        [Test]
        public void MinimumRectanglePreservesRotatedGeometry()
        {
            float angle = 23 * Mathf.Deg2Rad;
            var u = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var v = new Vector2(-u.y, u.x);
            var origin = new Vector2(30, 40);
            var points = new List<Vector2> {origin, origin + u * 40, origin + u * 40 + v * 10, origin + v * 10, origin + u * 12 + v * 3};
            var rectangle = MNNPaddleDbPostprocessor.MinimumRectangle(points);
            double area = Vector2.Distance(rectangle[0], rectangle[1]) * Vector2.Distance(rectangle[0], rectangle[3]);
            Assert.That(area, Is.EqualTo(400).Within(.01));
            Assert.Greater(Math.Abs(rectangle[1].y - rectangle[0].y) + Math.Abs(rectangle[3].y - rectangle[0].y), 10);
        }

        [Test]
        public void RectifiedCropPreservesTopLeftRgbCoordinatesAndRotatesTallText()
        {
            var pixels = new byte[16 * 16 * 3];
            for (int y = 0; y < 16; ++y)
            for (int x = 0; x < 16; ++x)
            {
                pixels[(y * 16 + x) * 3] = (byte)x;
                pixels[(y * 16 + x) * 3 + 1] = (byte)y;
            }
            var image = new MNNRgbImage(pixels, 16, 16);
            var crop = MNNPaddleDbPostprocessor.Crop(image, Box(2, 2, 10, 6));
            Assert.AreEqual(8, crop.Width);
            Assert.AreEqual(4, crop.Height);
            Assert.AreEqual(2, crop.Pixels[0]);
            Assert.AreEqual(2, crop.Pixels[1]);
            Assert.AreEqual(9, crop.Pixels[crop.Pixels.Length - 3]);
            Assert.AreEqual(5, crop.Pixels[crop.Pixels.Length - 2]);
            var vertical = MNNPaddleDbPostprocessor.Crop(image, Box(2, 2, 6, 10));
            Assert.AreEqual(8, vertical.Width);
            Assert.AreEqual(4, vertical.Height);
            Assert.AreEqual(5, vertical.Pixels[0]);
            Assert.AreEqual(2, vertical.Pixels[1]);
            Assert.AreEqual(2, vertical.Pixels[vertical.Pixels.Length - 3]);
            Assert.AreEqual(9, vertical.Pixels[vertical.Pixels.Length - 2]);
        }

        [Test]
        public void ReadingOrderUsesRowsThenColumnsAndCancellationIsHonored()
        {
            var first = Box(10, 10, 80, 30);
            var second = Box(100, 8, 150, 30);
            var third = Box(10, 60, 80, 80);
            var boxes = new List<MNNTextBox> {third, second, first};
            MNNPaddleDbPostprocessor.SortReadingOrder(boxes);
            CollectionAssert.AreEqual(new[] {first, second, third}, boxes);
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Assert.Throws<OperationCanceledException>(() => MNNPaddleDbPostprocessor.Decode(new float[4], new[] {1, 1, 2, 2}, 2, 2, new MNNPaddleOcrConfig(), cancel.Token));
                Assert.Throws<OperationCanceledException>(() => MNNPaddleDbPostprocessor.Crop(new MNNRgbImage(new byte[300], 10, 10), Box(0, 0, 8, 8), cancel.Token));
            }
        }

        [Test]
        public void DetectionPreprocessingUsesBgrNormalizationAndRoundsDimensionsTo32()
        {
            var pixels = new byte[3 * 96 * 64];
            for (int i = 0; i < pixels.Length; i += 3)
                pixels[i] = 255;
            var image = new MNNRgbImage(pixels, 96, 64);
            var input = MNNPaddleTextDetector.Preprocess(image, 64, out int width, out int height);
            Assert.AreEqual(64, width);
            Assert.AreEqual(32, height);
            Assert.That(input[0], Is.EqualTo(-.485f / .229f).Within(1e-6));
            Assert.That(input[width * height], Is.EqualTo(-.456f / .224f).Within(1e-6));
            Assert.That(input[2 * width * height], Is.EqualTo((1 - .406f) / .225f).Within(1e-6));
            var tiny = MNNPaddleTextDetector.Preprocess(new MNNRgbImage(new byte[] {255, 255, 255}, 1, 1), 960, out width, out height);
            Assert.AreEqual(32, width);
            Assert.AreEqual(32, height);
            Assert.Greater(tiny[0], 0);
            Assert.Less(tiny[31], 0); // Tiny images are padded with zero RGB before normalization.
        }

        [Test]
        public void ParallelPreprocessingAndReusedWorkspacesCannotLeakPreviousImages()
        {
            var bytes = new byte[967 * 641 * 3];
            new System.Random(42).NextBytes(bytes);
            var image = new MNNRgbImage(bytes, 967, 641);
            var expected = MNNPaddleTextDetector.Preprocess(image, 960, out int width, out int height);
            var buffer = Enumerable.Repeat(float.NaN, expected.Length).ToArray();
            var actual = MNNPaddleTextDetector.Preprocess(image, 960, out int w, out int h, threadCount: 4, destinationBuffer: buffer);
            Assert.AreSame(buffer, actual);
            Assert.AreEqual(width, w); Assert.AreEqual(height, h);
            CollectionAssert.AreEqual(expected, actual);
            Assert.False(actual.Any(float.IsNaN));
            var next = new MNNRgbImage(new byte[967 * 641 * 3], 967, 641);
            var blank = MNNPaddleTextDetector.Preprocess(next, 960, out _, out _);
            MNNPaddleTextDetector.Preprocess(next, 960, out _, out _, threadCount: 4, destinationBuffer: buffer);
            CollectionAssert.AreEqual(blank, buffer);
            Assert.Throws<ArgumentException>(() => MNNPaddleTextDetector.Preprocess(image, 960, out _, out _, destinationBuffer: new float[1]));
            var workspace = new MNNPaddleDbPostprocessor.Workspace();
            var map = new float[64 * 64];
            for (int y = 10; y < 24; ++y)
                for (int x = 10; x < 40; ++x)
                    map[y * 64 + x] = .9f;
            var config = new MNNPaddleOcrConfig();
            Assert.AreEqual(1, MNNPaddleDbPostprocessor.Decode(map, new[] {1, 1, 64, 64}, 64, 64, config, workspace: workspace).Length);
            Assert.AreEqual(0, MNNPaddleDbPostprocessor.Decode(new float[16], new[] {1, 1, 4, 4}, 4, 4, config, workspace: workspace).Length);
            Assert.AreEqual(0, MNNPaddleDbPostprocessor.Decode(new float[64 * 64], new[] {1, 1, 64, 64}, 64, 64, config, workspace: workspace).Length);
            Assert.AreEqual(1, MNNPaddleDbPostprocessor.Decode(map, new[] {1, 1, 64, 64}, 64, 64, config, workspace: workspace).Length);
        }

        [Test]
        public void PageProfilesRejectPathEscapesAndInvalidProtocolsBeforeNativeLoading()
        {
            string root = Path.GetFullPath("TestArtifacts~/PaddleOcrConfig/" + Guid.NewGuid().ToString("N"));
            string child = Path.Combine(root, "recognizers", "test");
            Directory.CreateDirectory(child);
            try
            {
                File.WriteAllText(Path.Combine(root, "PP-OCRv5_mobile_det.mnn"), "fixture only");
                File.WriteAllText(Path.Combine(child, "line.mnn"), "fixture only");
                File.WriteAllText(Path.Combine(child, "dict.txt"), "a");
                File.WriteAllText(Path.Combine(child, MNNPaddleTextConfig.FileName), JsonUtility.ToJson(new MNNPaddleTextConfig {graph = "line.mnn", dictionary = "dict.txt"}));
                var config = new MNNPaddleOcrConfig();
                config.Validate(root);
                Assert.AreEqual(child, MNNPaddleOcrConfig.RecognizerDirectory(root, "recognizers/test"));
                File.WriteAllText(Path.Combine(root, MNNPaddleOcrConfig.FileName), JsonUtility.ToJson(config));
                File.WriteAllText(Path.Combine(root, "variant.json"), JsonUtility.ToJson(new MNNPaddleOcrConfig {output = "different-output"}));
                Assert.AreEqual("fetch_name_0", MNNPaddleOcrConfig.ReadForProfile(root, "recognizers/test").output);
                var lineConfig = MNNPaddleTextConfig.Read(child);
                lineConfig.pageConfig = "variant.json";
                File.WriteAllText(Path.Combine(child, MNNPaddleTextConfig.FileName), JsonUtility.ToJson(lineConfig));
                Assert.AreEqual("different-output", MNNPaddleOcrConfig.ReadForProfile(root, "recognizers/test").output);
                foreach (string invalid in new[] {"../outside.json", "nested/page.json", "nested\\page.json", ""})
                {
                    lineConfig.pageConfig = invalid;
                    File.WriteAllText(Path.Combine(child, MNNPaddleTextConfig.FileName), JsonUtility.ToJson(lineConfig));
                    Assert.Throws<InvalidDataException>(() => MNNPaddleOcrConfig.ReadForProfile(root, "recognizers/test"));
                }
                Assert.Throws<FileNotFoundException>(() => MNNPaddleOcrConfig.RecognizerDirectory(root, "../outside"));
                Assert.Throws<InvalidDataException>(() => MNNPaddleOcrConfig.RecognizerDirectory(root, Path.GetPathRoot(root)));
                config.pixelThreshold = float.NaN;
                Assert.Throws<InvalidDataException>(() => config.Validate(root));
                config.pixelThreshold = .3f;
                config.preferredDetectionBackend = (MNNBackendType)999;
                Assert.Throws<InvalidDataException>(() => config.Validate(root));
                config.preferredDetectionBackend = MNNBackendType.CPU;
                config.maximumSide = 958;
                Assert.Throws<InvalidDataException>(() => config.Validate(root));
            }
            finally { Directory.Delete(root, true); }
        }

        private static MNNTextBox Box(float x, float y, float right, float bottom) => new MNNTextBox(new[]
        {new Vector2(x, y), new Vector2(right, y), new Vector2(right, bottom), new Vector2(x, bottom)}, .9f);
    }
}
