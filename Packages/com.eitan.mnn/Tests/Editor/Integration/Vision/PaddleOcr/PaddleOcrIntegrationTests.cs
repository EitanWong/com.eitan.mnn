using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Explicit("Requires the pinned local PP-OCRv5 detector, five recognizers and known page fixtures; no downloads.")]
    public class PaddleOcrIntegrationTests
    {
        private string _root, _directory;
        [OneTimeSetUp]
        public void Prepare()
        {
            _root = Environment.GetEnvironmentVariable("MNN_TEST_VISION_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_VISION_ROOT.");
            _directory = Path.Combine(_root, "huggingface", MNNPaddleTextModel.Repository);
            MNNPaddleTextModel.Prepare(_directory);
        }

        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void ReadsMultipleLinesRotatedPagesAndBlankWithSourceCoordinates(MNNBackendType backend)
        {
            using (var ocr = MNNPaddleOcr.Load(_directory, backendType: backend))
            {
                foreach (string name in new[] {"english", "rotated", "blank", "english"})
                {
                    var image = Read(name);
                    var result = ocr.Recognize(image);
                    string[] expected = name == "blank" ? Array.Empty<string>() : new[] {"Hello MNN 2026", "TOTAL $123.45", "Unity inference works"};
                    Verify(result, expected, image);
                    if (name == "english")
                    {
                        Assert.That(result.Regions[0].Box.Corners[0].x, Is.InRange(50, 80));
                        Assert.That(result.Regions[0].Box.Corners[0].y, Is.InRange(40, 65));
                    }
                    if (name == "rotated")
                        Assert.Greater(Math.Abs(result.Regions[0].Box.Corners[1].y - result.Regions[0].Box.Corners[0].y), 30);
                }
                Assert.AreEqual(MNNBackendType.CPU, ocr.DetectionBackend);
                TestContext.WriteLine("Actual page OCR detection=" + ocr.DetectionBackend + "; recognition=" + ocr.RecognitionBackend);
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    Assert.Throws<OperationCanceledException>(() => ocr.Recognize(Read("english"), cancellation.Token));
                }
                ocr.Dispose(); ocr.Dispose();
                Assert.Throws<ObjectDisposedException>(() => ocr.Recognize(Read("english")));
            }
        }

        [TestCase("chinese", "你好世界 2026\n欢迎使用图像文字识别", MNNBackendType.CPU)]
        [TestCase("chinese", "你好世界 2026\n欢迎使用图像文字识别", MNNBackendType.Auto)]
        [TestCase("latin", "Bonjour été 2026\nGrüße aus München", MNNBackendType.CPU)]
        [TestCase("latin", "Bonjour été 2026\nGrüße aus München", MNNBackendType.Auto)]
        [TestCase("cyrillic", "Привет мир 2026\nHello MNN 2026", MNNBackendType.CPU)]
        [TestCase("cyrillic", "Привет мир 2026\nHello MNN 2026", MNNBackendType.Auto)]
        [TestCase("greek", "Καλημέρα κόσμε 2026\nHello MNN 2026", MNNBackendType.CPU)]
        [TestCase("greek", "Καλημέρα κόσμε 2026\nHello MNN 2026", MNNBackendType.Auto)]
        public void AllMatchedLanguageProfilesReadKnownPages(string profile, string expected, MNNBackendType backend)
        {
            var image = Read(profile);
            using (var ocr = MNNPaddleOcr.Load(_directory, "recognizers/" + profile, backendType: backend))
                Verify(ocr.Recognize(image), expected.Split('\n'), image);
        }

        [UnityTest]
        public IEnumerator StudioDiscoversFivePageProfilesAndReturnsTextWithAnAnnotatedImage()
        {
            var errors = new List<string>();
            var models = MNNStudioModel.Discover(new[] {_directory}, errors);
            Assert.IsEmpty(errors);
            Assert.AreEqual(1 + MNNPaddleTextModel.Profiles.Length, models.Count);
            foreach (var model in models.Where(item => item.OcrRecognizerProfile == "." || new[] {"chinese", "latin", "cyrillic", "greek"}.Contains(Path.GetFileName(item.Directory))))
            {
                Assert.True(model.IsPaddlePage);
                Assert.True(model.HasVisionPipeline);
                string profile = model.OcrRecognizerProfile == "." ? "english" : Path.GetFileName(model.Directory);
                string expected = profile == "english" ? "Hello MNN 2026\nTOTAL $123.45\nUnity inference works"
                    : profile == "chinese" ? "你好世界 2026\n欢迎使用图像文字识别"
                    : profile == "latin" ? "Bonjour été 2026\nGrüße aus München"
                    : profile == "cyrillic" ? "Привет мир 2026\nHello MNN 2026" : "Καλημέρα κόσμε 2026\nHello MNN 2026";
                var image = Read(profile);
                using (var session = new MNNStudioSession(MNNStudioNativeBackend.Load(model, 2, false)))
                {
                    session.Send(new MNNStudioRequest {Prompt = "Read the page.", VisionImage = image});
                    var deadline = DateTime.UtcNow.AddSeconds(60);
                    while (session.Busy && DateTime.UtcNow < deadline) { MNNStudioJobs.Update(); yield return null; }
                    Assert.False(session.Busy);
                    Assert.IsNull(session.Error);
                    Assert.AreEqual(expected, session.Result.Text);
                    Verify(session.Result.Ocr, expected.Split('\n'), image);
                    Assert.AreEqual(image.Width, session.Result.GeneratedImage.Width);
                    Assert.AreEqual(image.Height, session.Result.GeneratedImage.Height);
                    Assert.False(image.Pixels.SequenceEqual(session.Result.GeneratedImage.Rgb));
                    StringAssert.Contains("page OCR", session.Result.BackendDescription);
                    var preview = MNNStudioImage.CreateTexture(session.Result.GeneratedImage);
                    try { File.WriteAllBytes(Path.Combine(_root, "OCR", "Pages", "studio-" + profile + ".png"), preview.EncodeToPNG()); }
                    finally { UnityEngine.Object.DestroyImmediate(preview); }
                }
            }
        }

        private MNNRgbImage Read(string name) => MNNStudioImage.ReadRgb(Path.Combine(_root, "OCR", "Pages", name + ".png"));
        private static void Verify(MNNOcrResult result, string[] expected, MNNRgbImage image)
        {
            Assert.AreEqual(string.Join("\n", expected), result.Text);
            CollectionAssert.AreEqual(expected, result.Regions.Select(region => region.Text));
            foreach (var region in result.Regions)
            {
                Assert.Greater(region.Confidence, .8f);
                Assert.GreaterOrEqual(region.Box.Confidence, .6f);
                Assert.AreEqual(4, region.Box.Corners.Count);
                Assert.True(region.Box.Corners.All(p => p.x >= 0 && p.y >= 0 && p.x < image.Width && p.y < image.Height));
            }
            if (expected.Length == 0)
                Assert.AreEqual(0, result.Confidence);
        }
    }
}
