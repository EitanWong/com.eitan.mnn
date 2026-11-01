using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Explicit("Requires pinned additional-script OCR profiles and local shaped text fixtures; no downloads.")]
    public class AdditionalPaddleScriptIntegrationTests
    {
        private string _root, _directory;
        [OneTimeSetUp]
        public void Prepare()
        {
            _root = Environment.GetEnvironmentVariable("MNN_TEST_VISION_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_VISION_ROOT.");
            _directory = Path.Combine(_root, "huggingface", MNNPaddleTextModel.Repository);
            MNNPaddleTextModel.Prepare(_directory);
        }

        [TestCase("arabic", "مرحبا بالعالم", MNNBackendType.CPU)]
        [TestCase("arabic", "مرحبا بالعالم", MNNBackendType.Auto)]
        [TestCase("devanagari", "नमस्ते दुनिया", MNNBackendType.CPU)]
        [TestCase("devanagari", "नमस्ते दुनिया", MNNBackendType.Auto)]
        [TestCase("eslav", "Привіт світ 2026", MNNBackendType.CPU)]
        [TestCase("eslav", "Привіт світ 2026", MNNBackendType.Auto)]
        [TestCase("korean", "안녕하세요 세계", MNNBackendType.CPU)]
        [TestCase("korean", "안녕하세요 세계", MNNBackendType.Auto)]
        [TestCase("tamil", "வணக்கம் உலகம்", MNNBackendType.CPU)]
        [TestCase("tamil", "வணக்கம் உலகம்", MNNBackendType.Auto)]
        [TestCase("telugu", "నమస్కారం ప్రపంచం", MNNBackendType.CPU)]
        [TestCase("telugu", "నమస్కారం ప్రపంచం", MNNBackendType.Auto)]
        [TestCase("thai", "สวัสดีชาวโลก", MNNBackendType.CPU)]
        [TestCase("thai", "สวัสดีชาวโลก", MNNBackendType.Auto)]
        public void ReadsShapedScriptLinesAndBlankWithMatchedDictionary(string profile, string expected, MNNBackendType backend)
        {
            using (var model = MNNPaddleTextRecognizer.Load(Path.Combine(_directory, "recognizers", profile), backendType: backend))
            {
                var image = Read(profile == "korean" ? "korean-tight" : profile);
                for (int i = 0; i < 2; ++i)
                {
                    var value = model.Recognize(image);
                    Assert.AreEqual(expected, value.Text);
                    Assert.Greater(value.Confidence, .8f);
                }
                var blank = model.Recognize(new MNNRgbImage(Enumerable.Repeat((byte)255, image.Width * image.Height * 3).ToArray(), image.Width, image.Height));
                Assert.AreEqual("", blank.Text);
                Assert.AreEqual(0, blank.Confidence);
                if (profile == "korean") Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                TestContext.WriteLine(profile + ": " + model.Backend);
            }
        }

        [TestCase("arabic", "مرحبا بالعالم", MNNBackendType.CPU)]
        [TestCase("arabic", "مرحبا بالعالم", MNNBackendType.Auto)]
        [TestCase("devanagari", "नमस्ते दुनिया", MNNBackendType.CPU)]
        [TestCase("devanagari", "नमस्ते दुनिया", MNNBackendType.Auto)]
        [TestCase("eslav", "Привіт світ 2026", MNNBackendType.CPU)]
        [TestCase("eslav", "Привіт світ 2026", MNNBackendType.Auto)]
        [TestCase("korean", "안녕하세요 세계", MNNBackendType.CPU)]
        [TestCase("korean", "안녕하세요 세계", MNNBackendType.Auto)]
        [TestCase("tamil", "வணக்கம் உலகம்", MNNBackendType.CPU)]
        [TestCase("tamil", "வணக்கம் உலகம்", MNNBackendType.Auto)]
        [TestCase("telugu", "నమస్కారం ప్రపంచం", MNNBackendType.CPU)]
        [TestCase("telugu", "నమస్కారం ప్రపంచం", MNNBackendType.Auto)]
        [TestCase("thai", "สวัสดีชาวโลก", MNNBackendType.CPU)]
        [TestCase("thai", "สวัสดีชาวโลก", MNNBackendType.Auto)]
        public void ReadsShapedScriptPagesWithOriginalCoordinates(string profile, string expected, MNNBackendType backend)
        {
            var image = Read(profile + "-page");
            using (var model = MNNPaddleOcr.Load(_directory, "recognizers/" + profile, backendType: backend))
                Verify(model.Recognize(image), expected + "\nHello MNN 2026", image);
        }

        [UnityTest]
        public IEnumerator StudioRunsAllAdditionalProfilesWithNativePageAnnotations()
        {
            var errors = new List<string>();
            var models = MNNStudioModel.Discover(new[] {_directory}, errors);
            Assert.IsEmpty(errors);
            Assert.AreEqual(1 + MNNPaddleTextModel.Profiles.Length, models.Count);
            string[] profiles = {"arabic", "devanagari", "eslav", "korean", "tamil", "telugu", "thai"};
            string[] texts = {"مرحبا بالعالم", "नमस्ते दुनिया", "Привіт світ 2026", "안녕하세요 세계", "வணக்கம் உலகம்", "నమస్కారం ప్రపంచం", "สวัสดีชาวโลก"};
            for (int i = 0; i < profiles.Length; ++i)
            {
                string profile = profiles[i];
                var model = models.Single(item => item.Directory == Path.Combine(_directory, "recognizers", profile));
                Assert.AreEqual(MNNStudioTask.OCR, model.Task);
                Assert.True(model.IsPaddlePage);
                var image = Read(profile + "-page");
                using (var session = new MNNStudioSession(MNNStudioNativeBackend.Load(model, 2, false)))
                {
                    session.Send(new MNNStudioRequest {Prompt = "Read this page.", VisionImage = image});
                    var deadline = DateTime.UtcNow.AddSeconds(60);
                    while (session.Busy && DateTime.UtcNow < deadline) { MNNStudioJobs.Update(); yield return null; }
                    Assert.False(session.Busy);
                    Assert.IsNull(session.Error);
                    Verify(session.Result.Ocr, texts[i] + "\nHello MNN 2026", image);
                    Assert.AreEqual(session.Result.Ocr.Text, session.Result.Text);
                    Assert.False(image.Pixels.SequenceEqual(session.Result.GeneratedImage.Rgb));
                    var preview = MNNStudioImage.CreateTexture(session.Result.GeneratedImage);
                    try { File.WriteAllBytes(Path.Combine(_root, "OCR", "AdditionalScripts", "studio-" + profile + ".png"), preview.EncodeToPNG()); }
                    finally { UnityEngine.Object.DestroyImmediate(preview); }
                }
            }
        }

        private MNNRgbImage Read(string name) => MNNStudioImage.ReadRgb(Path.Combine(_root, "OCR", "AdditionalScripts", name + ".png"));
        private static void Verify(MNNOcrResult result, string expected, MNNRgbImage image)
        {
            Assert.AreEqual(expected, result.Text);
            Assert.AreEqual(2, result.Regions.Count);
            foreach (var region in result.Regions)
            {
                Assert.Greater(region.Confidence, .8f);
                Assert.GreaterOrEqual(region.Box.Confidence, .6f);
                Assert.True(region.Box.Corners.All(point => point.x >= 0 && point.y >= 0 && point.x < image.Width && point.y < image.Height));
            }
        }
    }
}
