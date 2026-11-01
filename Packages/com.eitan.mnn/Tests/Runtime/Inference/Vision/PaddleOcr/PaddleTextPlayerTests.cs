using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("PaddleTextPlayer")]
    public class PaddleTextPlayerTests
    {
        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void ReadsKnownLinesAtDynamicWidthsAndBlankInActualPlayer(MNNBackendType backend)
        {
            string root=Environment.GetEnvironmentVariable("MNN_PLAYER_VISION_ROOT");
            if(string.IsNullOrWhiteSpace(root))
                Assert.Ignore("Provide --vision-root with the prepared English OCR profile and known text fixtures.");
            string directory=Path.Combine(root,"huggingface","qefro","pp-OCRv5-mnn");
            string[] files={"hello","receipt","long","blank"};
            string[] expected={"Hello MNN 2026","TOTAL $123.45","Unity inference works with dynamic width 2026.",""};
            using(var model=MNNPaddleTextRecognizer.Load(directory,backendType:backend))
            {
                for(int i=0;i<files.Length;++i)
                {
                    var text=model.Recognize(VisionPlayerData.Read(Path.Combine(root,"OCR",files[i]+".png")));
                    Assert.AreEqual(expected[i],text.Text);
                    if(expected[i].Length>0)Assert.Greater(text.Confidence,.8f);
                }
                TestContext.WriteLine("Actual OCR backend="+model.Backend);
            }
        }

        [TestCase("chinese", "chinese", "你好世界 2026", MNNBackendType.CPU)]
        [TestCase("chinese", "chinese2", "欢迎使用图像文字识别", MNNBackendType.Auto)]
        [TestCase("latin", "latin", "Bonjour été 2026", MNNBackendType.CPU)]
        [TestCase("latin", "german", "Grüße aus München", MNNBackendType.Auto)]
        [TestCase("cyrillic", "russian", "Привет мир 2026", MNNBackendType.CPU)]
        [TestCase("cyrillic", "russian", "Привет мир 2026", MNNBackendType.Auto)]
        [TestCase("greek", "greek", "Καλημέρα κόσμε 2026", MNNBackendType.CPU)]
        [TestCase("greek", "greek", "Καλημέρα κόσμε 2026", MNNBackendType.Auto)]
        public void MultilingualProfilesReadNonLatinAndAccentedTextInActualPlayer(string profile, string fixture, string expected, MNNBackendType backend)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_VISION_ROOT");
            if (string.IsNullOrWhiteSpace(root))
                Assert.Ignore("Provide --vision-root with prepared multilingual OCR profiles and known text fixtures.");
            string directory = Path.Combine(root, "huggingface", "qefro", "pp-OCRv5-mnn", "recognizers", profile);
            using (var model = MNNPaddleTextRecognizer.Load(directory, backendType: backend))
            {
                var text = model.Recognize(VisionPlayerData.Read(Path.Combine(root, "OCR", "Multilingual", fixture + ".png")));
                Assert.AreEqual(expected, text.Text);
                Assert.Greater(text.Confidence, .8f);
                TestContext.WriteLine("Actual OCR " + profile + " backend=" + model.Backend);
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
        public void AdditionalScriptsReadKnownLinesInActualPlayer(string profile, string expected, MNNBackendType backend)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_VISION_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Provide --vision-root with additional-script profiles and shaped fixtures.");
            string directory = Path.Combine(root, "huggingface", "qefro", "pp-OCRv5-mnn");
            using (var model = MNNPaddleTextRecognizer.Load(Path.Combine(directory, "recognizers", profile), backendType: backend))
            {
                var value = model.Recognize(VisionPlayerData.Read(Path.Combine(root, "OCR", "AdditionalScripts", (profile == "korean" ? "korean-tight" : profile) + ".png")));
                Assert.AreEqual(expected, value.Text);
                Assert.Greater(value.Confidence, .8f);
                if (profile == "korean") Assert.AreEqual(MNNBackendType.CPU, model.Backend);
            }
        }

        [TestCase("chinese-v4", MNNBackendType.CPU)]
        [TestCase("chinese-v4", MNNBackendType.Auto)]
        [TestCase("chinese-fp16", MNNBackendType.CPU)]
        [TestCase("chinese-fp16", MNNBackendType.Auto)]
        public void V4AndFp16VariantsReadKnownChineseLinesInActualPlayer(string profile, MNNBackendType backend)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_VISION_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Provide --vision-root with prepared v4/fp16 OCR variants.");
            string directory = Path.Combine(root, "huggingface", "qefro", "pp-OCRv5-mnn");
            using (var model = MNNPaddleTextRecognizer.Load(Path.Combine(directory, "recognizers", profile), backendType: backend))
            foreach (var pair in new[] {new[] {"chinese", "你好世界 2026"}, new[] {"chinese2", "欢迎使用图像文字识别"}})
            {
                var result = model.Recognize(VisionPlayerData.Read(Path.Combine(root, "OCR", "Multilingual", pair[0] + ".png")));
                Assert.AreEqual(pair[1], result.Text);
                Assert.Greater(result.Confidence, .8f);
            }
        }

    }
}
