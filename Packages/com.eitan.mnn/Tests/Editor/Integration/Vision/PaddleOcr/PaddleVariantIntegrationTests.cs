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
    [Explicit("Requires pinned local v4/fp16 graphs and known OCR fixtures; no downloads.")]
    public class PaddleVariantIntegrationTests
    {
        private string _root, _directory;
        [OneTimeSetUp]
        public void Prepare()
        {
            _root = Environment.GetEnvironmentVariable("MNN_TEST_VISION_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_VISION_ROOT.");
            _directory = Path.Combine(_root, "huggingface", MNNPaddleTextModel.Repository);
            MNNPaddleTextModel.Prepare(_directory);
        }

        [TestCase("chinese-v4", MNNBackendType.CPU)]
        [TestCase("chinese-v4", MNNBackendType.Auto)]
        [TestCase("chinese-fp16", MNNBackendType.CPU)]
        [TestCase("chinese-fp16", MNNBackendType.Auto)]
        public void MatchedVariantReadsChineseLinesAndEnglishRotatedPages(string profile, MNNBackendType backend)
        {
            using (var recognizer = MNNPaddleTextRecognizer.Load(Path.Combine(_directory, "recognizers", profile), backendType: backend))
            {
                foreach (var pair in new[] {new[] {"chinese", "你好世界 2026"}, new[] {"chinese2", "欢迎使用图像文字识别"}})
                {
                    var value = recognizer.Recognize(Read("Multilingual/" + pair[0]));
                    Assert.AreEqual(pair[1], value.Text);
                    Assert.Greater(value.Confidence, .8f);
                }
            }
            var pageConfig = MNNPaddleOcrConfig.ReadForProfile(_directory, "recognizers/" + profile);
            Assert.AreEqual(profile == "chinese-v4" ? "ch_PP-OCRv4_det_infer.mnn" : "PP-OCRv5_mobile_det_fp16.mnn", pageConfig.graph);
            using (var model = MNNPaddleOcr.Load(_directory, "recognizers/" + profile, backendType: backend))
            {
                foreach (string name in new[] {"english", "rotated", "blank", "english"})
                {
                    var image = Read("Pages/" + name);
                    var result = model.Recognize(image);
                    Verify(result, name == "blank" ? "" : "Hello MNN 2026\nTOTAL $123.45\nUnity inference works", image);
                }
                if (profile == "chinese-fp16")
                {
                    var image = Read("Pages/chinese");
                    Verify(model.Recognize(image), "你好世界 2026\n欢迎使用图像文字识别", image);
                }
            }
        }

        [UnityTest]
        public IEnumerator StudioSelectsVariantDetectorAndReturnsNativeTextAndAnnotations()
        {
            var errors = new List<string>();
            var models = MNNStudioModel.Discover(new[] {_directory}, errors);
            Assert.IsEmpty(errors);
            Assert.AreEqual(14, models.Count);
            foreach (string profile in new[] {"chinese-v4", "chinese-fp16"})
            {
                var model = models.Single(item => item.Directory == Path.Combine(_directory, "recognizers", profile));
                Assert.AreEqual(MNNStudioTask.OCR, model.Task);
                Assert.True(model.HasVisionPipeline);
                var image = Read("Pages/rotated");
                using (var session = new MNNStudioSession(MNNStudioNativeBackend.Load(model, 4, false)))
                {
                    session.Send(new MNNStudioRequest {Prompt = "Read this page.", VisionImage = image});
                    var deadline = DateTime.UtcNow.AddSeconds(60);
                    while (session.Busy && DateTime.UtcNow < deadline) { MNNStudioJobs.Update(); yield return null; }
                    Assert.False(session.Busy);
                    Assert.IsNull(session.Error);
                    Verify(session.Result.Ocr, "Hello MNN 2026\nTOTAL $123.45\nUnity inference works", image);
                    Assert.AreEqual(session.Result.Ocr.Text, session.Result.Text);
                    Assert.False(image.Pixels.SequenceEqual(session.Result.GeneratedImage.Rgb));
                    var preview = MNNStudioImage.CreateTexture(session.Result.GeneratedImage);
                    try { File.WriteAllBytes(Path.Combine(_root, "OCR", "Variants", "studio-" + profile + ".png"), preview.EncodeToPNG()); }
                    finally { UnityEngine.Object.DestroyImmediate(preview); }
                }
            }
        }

        [Test]
        public void PinnedInstallCoversEveryGraphAndDictionaryFromRepositoryManifest()
        {
            string metadata = File.ReadAllText(Path.Combine(_root, "OCR", "AdditionalScripts", "repository.json"));
            var manifest = JsonUtility.FromJson<Repository>(metadata);
            CollectionAssert.AreEquivalent(manifest.siblings.Where(item => item.rfilename.EndsWith(".mnn", StringComparison.Ordinal) || item.rfilename.EndsWith(".txt", StringComparison.Ordinal)).Select(item => item.rfilename), MNNPaddleTextModel.Files().Select(item => item.Path));
            Assert.AreEqual(30, MNNPaddleTextModel.Files().Length);
            MNNPaddleTextModel.Prepare(_directory); // Repeat after moving files and copying shared dictionary.
            Assert.True(File.Exists(Path.Combine(_directory, "recognizers/chinese-fp16/ppocr_keys_v5.txt")));
            Assert.True(File.Exists(Path.Combine(_directory, "recognizers/chinese/ppocr_keys_v5.txt")));
        }

        [Serializable] private sealed class Repository { public Entry[] siblings; }
        [Serializable] private sealed class Entry { public string rfilename; }
        private MNNRgbImage Read(string relative) => MNNStudioImage.ReadRgb(Path.Combine(_root, "OCR", relative + ".png"));
        private static void Verify(MNNOcrResult result, string expected, MNNRgbImage image)
        {
            Assert.AreEqual(expected, result.Text);
            Assert.AreEqual(expected.Length == 0 ? 0 : expected.Split('\n').Length, result.Regions.Count);
            foreach (var region in result.Regions)
            {
                Assert.Greater(region.Confidence, .8f);
                Assert.GreaterOrEqual(region.Box.Confidence, .6f);
                Assert.True(region.Box.Corners.All(p => p.x >= 0 && p.y >= 0 && p.x < image.Width && p.y < image.Height));
            }
        }
    }
}
