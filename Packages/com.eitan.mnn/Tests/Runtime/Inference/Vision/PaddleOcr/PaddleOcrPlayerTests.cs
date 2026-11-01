using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("PaddleOcrPlayer")]
    public class PaddleOcrPlayerTests
    {
        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void ReadsFullPagesRotationAndBlankInActualPlayer(MNNBackendType backend)
        {
            string root = Root();
            using (var model = MNNPaddleOcr.Load(Path.Combine(root, "huggingface", "qefro", "pp-OCRv5-mnn"), backendType: backend))
            {
                foreach (string name in new[] {"english", "rotated", "blank", "english"})
                {
                    var image = VisionPlayerData.Read(Path.Combine(root, "OCR", "Pages", name + ".png"));
                    var result = model.Recognize(image);
                    Assert.AreEqual(name == "blank" ? "" : "Hello MNN 2026\nTOTAL $123.45\nUnity inference works", result.Text);
                    Assert.AreEqual(name == "blank" ? 0 : 3, result.Regions.Count);
                    Verify(result, image);
                }
                Assert.AreEqual(MNNBackendType.CPU, model.DetectionBackend);
                Assert.AreEqual(MNNBackendType.CPU, model.RecognitionBackend);
                TestContext.WriteLine("Actual page OCR detection=" + model.DetectionBackend + ";recognition=" + model.RecognitionBackend);
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
        public void ReadsAllFivePageLanguagesInActualPlayer(string profile, string expected, MNNBackendType backend)
        {
            string root = Root();
            using (var model = MNNPaddleOcr.Load(Path.Combine(root, "huggingface", "qefro", "pp-OCRv5-mnn"),
                "recognizers/" + profile, backendType: backend))
            {
                var image = VisionPlayerData.Read(Path.Combine(root, "OCR", "Pages", profile + ".png"));
                var result = model.Recognize(image);
                Assert.AreEqual(expected, result.Text);
                Assert.AreEqual(2, result.Regions.Count);
                Verify(result, image);
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
        public void AdditionalScriptsReadKnownPagesInActualPlayer(string profile, string expected, MNNBackendType backend)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_VISION_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Provide --vision-root with additional-script profiles and shaped fixtures.");
            string directory = Path.Combine(root, "huggingface", "qefro", "pp-OCRv5-mnn");
            using (var model = MNNPaddleOcr.Load(directory, "recognizers/" + profile, backendType: backend))
            {
                var image = VisionPlayerData.Read(Path.Combine(root, "OCR", "AdditionalScripts", profile + "-page.png"));
                var value = model.Recognize(image);
                Assert.AreEqual(expected + "\nHello MNN 2026", value.Text);
                Assert.AreEqual(2, value.Regions.Count);
                Verify(value, image);
            }
        }

        [TestCase("chinese-v4", MNNBackendType.CPU)]
        [TestCase("chinese-v4", MNNBackendType.Auto)]
        [TestCase("chinese-fp16", MNNBackendType.CPU)]
        [TestCase("chinese-fp16", MNNBackendType.Auto)]
        public void V4AndFp16VariantsReadKnownPagesInActualPlayer(string profile, MNNBackendType backend)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_VISION_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Provide --vision-root with prepared v4/fp16 OCR variants.");
            string directory = Path.Combine(root, "huggingface", "qefro", "pp-OCRv5-mnn");
            using (var model = MNNPaddleOcr.Load(directory, "recognizers/" + profile, backendType: backend))
            foreach (string name in new[] {"english", "rotated", "blank"})
            {
                var image = VisionPlayerData.Read(Path.Combine(root, "OCR", "Pages", name + ".png"));
                var result = model.Recognize(image);
                Assert.AreEqual(name == "blank" ? "" : "Hello MNN 2026\nTOTAL $123.45\nUnity inference works", result.Text);
                Assert.AreEqual(name == "blank" ? 0 : 3, result.Regions.Count);
                Verify(result, image);
            }
        }

        private static string Root()
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_VISION_ROOT");
            if (string.IsNullOrWhiteSpace(root))
                Assert.Ignore("Provide --vision-root with the prepared detector, multilingual recognizers and known page fixtures.");
            return root;
        }

        private static void Verify(MNNOcrResult result, MNNRgbImage image)
        {
            foreach (var region in result.Regions)
            {
                Assert.Greater(region.Confidence, .8f);
                Assert.GreaterOrEqual(region.Box.Confidence, .6f);
                Assert.True(region.Box.Corners.All(point => point.x >= 0 && point.y >= 0 && point.x < image.Width && point.y < image.Height));
            }
        }
    }
}
