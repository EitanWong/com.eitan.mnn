using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Explicit("Requires the pinned local PP-OCRv5 English graph/dictionary and known text fixtures; no downloads.")]
    public class PaddleTextIntegrationTests
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
        public void ReadsKnownTextPunctuationRepeatedLettersAndBlankAtDynamicWidths(MNNBackendType backend)
        {
            using (var recognizer = MNNPaddleTextRecognizer.Load(_directory, backendType: backend))
            {
                string[] files = {"hello", "receipt", "wide", "long", "blank"};
                string[] expected = {"Hello MNN 2026", "TOTAL $123.45", "Unity inference works", "Unity inference works with dynamic width 2026.", ""};
                for (int i = 0; i < files.Length; ++i)
                {
                    var image = MNNStudioImage.ReadRgb(Path.Combine(_root, "OCR", files[i] + ".png"));
                    var result = recognizer.Recognize(image);
                    TestContext.WriteLine(recognizer.Backend + ": " + files[i] + " = " + result.Text + " / " + result.Confidence);
                    Assert.AreEqual(expected[i], result.Text);
                    if (expected[i].Length != 0)
                        Assert.Greater(result.Confidence, .8f);
                    else
                        Assert.AreEqual(0, result.Confidence);
                }
                var repeated = recognizer.Recognize(MNNStudioImage.ReadRgb(Path.Combine(_root, "OCR", "hello.png")));
                Assert.AreEqual(expected[0], repeated.Text);
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.Throws<OperationCanceledException>(() => recognizer.Recognize(new MNNRgbImage(new byte[3], 1, 1), cancelled.Token));
                }
                recognizer.Dispose();
                recognizer.Dispose();
                Assert.Throws<ObjectDisposedException>(() => recognizer.Recognize(new MNNRgbImage(new byte[3], 1, 1)));
            }
        }

        [TestCase("chinese", "chinese", "你好世界 2026", MNNBackendType.CPU)]
        [TestCase("chinese", "chinese", "你好世界 2026", MNNBackendType.Auto)]
        [TestCase("chinese", "chinese2", "欢迎使用图像文字识别", MNNBackendType.CPU)]
        [TestCase("chinese", "chinese2", "欢迎使用图像文字识别", MNNBackendType.Auto)]
        [TestCase("latin", "latin", "Bonjour été 2026", MNNBackendType.CPU)]
        [TestCase("latin", "latin", "Bonjour été 2026", MNNBackendType.Auto)]
        [TestCase("latin", "german", "Grüße aus München", MNNBackendType.CPU)]
        [TestCase("latin", "german", "Grüße aus München", MNNBackendType.Auto)]
        [TestCase("cyrillic", "russian", "Привет мир 2026", MNNBackendType.CPU)]
        [TestCase("cyrillic", "russian", "Привет мир 2026", MNNBackendType.Auto)]
        [TestCase("greek", "greek", "Καλημέρα κόσμε 2026", MNNBackendType.CPU)]
        [TestCase("greek", "greek", "Καλημέρα κόσμε 2026", MNNBackendType.Auto)]
        public void IndependentLanguageProfilesReadKnownAccentedAndNonLatinText(string profile, string fixture, string expected, MNNBackendType backend)
        {
            string directory = Path.Combine(_directory, "recognizers", profile);
            using (var recognizer = MNNPaddleTextRecognizer.Load(directory, backendType: backend))
            {
                var result = recognizer.Recognize(MNNStudioImage.ReadRgb(Path.Combine(_root, "OCR", "Multilingual", fixture + ".png")));
                Assert.AreEqual(expected, result.Text);
                Assert.Greater(result.Confidence, .8f);
                var ascii = recognizer.Recognize(MNNStudioImage.ReadRgb(Path.Combine(_root, "OCR", "Multilingual", "ascii.png")));
                Assert.AreEqual("Hello MNN 2026", ascii.Text);
                Assert.Greater(ascii.Confidence, .8f);
            }
        }

        [UnityTest]
        public IEnumerator EveryPreparedRecognizerIsDiscoveredAndRunsThroughStudioWorker()
        {
            MNNPaddleTextModel.Prepare(_directory); // Preparation is idempotent after files move into variants.
            var errors = new List<string>();
            var models = MNNStudioModel.Discover(new[] { _directory }, errors);
            Assert.IsEmpty(errors);
            Assert.AreEqual(1 + MNNPaddleTextModel.Profiles.Length, models.Count);
            foreach (string profile in MNNPaddleTextModel.Profiles.Where(value => value != "chinese-v4" && value != "chinese-fp16"))
            {
                var model = models.Single(item => item.Directory == Path.Combine(_directory, "recognizers", profile));
                Assert.True(model.HasVisionPipeline);
                Assert.AreEqual(MNNStudioTask.OCR, model.Task);
                using (var session = new MNNStudioSession(MNNStudioNativeBackend.Load(model, 2, false)))
                {
                    session.Send(new MNNStudioRequest { OcrCroppedLine = true, Prompt = "Read this line.", VisionImage = MNNStudioImage.ReadRgb(Path.Combine(_root, "OCR", "Multilingual", "ascii.png")) });
                    var deadline = DateTime.UtcNow.AddSeconds(60);
                    while (session.Busy && DateTime.UtcNow < deadline)
                    {
                        MNNStudioJobs.Update();
                        yield return null;
                    }
                    Assert.False(session.Busy);
                    Assert.IsNull(session.Error);
                    Assert.AreEqual("Hello MNN 2026", session.Result.Text);
                    Assert.Greater(session.Result.RecognizedText.Confidence, .8f);
                }
            }
        }

        [UnityTest]
        public IEnumerator StudioWindowRunDecodesTheAttachmentForBothVisionPipelines()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (int mode in new[] {0, 1, 2})
            {
                bool ocr = mode != 0, cropped = mode == 1;
                var model = MNNStudioModel.Read(ocr ? _directory : Path.Combine(_root,"modelscope",MNNYoloV8Model.Repository));
                var window = ScriptableObject.CreateInstance<MNNChatStudio>();
                Action<string,object> set = (name,value)=>typeof(MNNChatStudio).GetField(name,flags).SetValue(window,value);
                Func<string,object> get = name=>typeof(MNNChatStudio).GetField(name,flags).GetValue(window);
                Func<string,object> invoke = name=>typeof(MNNChatStudio).GetMethod(name,flags).Invoke(window,null);
                try
                {
                    set("_autoTitle",false);
                    set("_ocrCroppedLine",cropped);
                    set("_available",new List<MNNStudioModel>{model});
                    set("_modelIndex",0);
                    set("_taskIndex",(int)model.Task);
                    set("_query",ocr ? "Read this line." : "Detect objects.");
                    set("_imagePath",null);
                    Assert.False((bool)invoke("CanRunTask"),"An image attachment is required.");
                    set("_imagePath",Path.Combine(_root,ocr ? cropped ? "OCR/hello.png" : "OCR/Pages/english.png" : "bus.jpg"));
                    Assert.True((bool)invoke("CanRunTask"));
                    invoke("RunTask");
                    Assert.IsNull(get("_error"));
                    var session=(MNNStudioSession)get("_session");
                    Assert.NotNull(session);
                    var deadline=DateTime.UtcNow.AddSeconds(60);
                    while(session.Busy&&DateTime.UtcNow<deadline){MNNStudioJobs.Update();yield return null;}
                    Assert.False(session.Busy);
                    Assert.IsNull(session.Error);
                    if(ocr)
                    {
                        Assert.AreEqual(cropped ? "Hello MNN 2026" : "Hello MNN 2026\nTOTAL $123.45\nUnity inference works",session.Result.Text);
                        if (!cropped) { Assert.NotNull(session.Result.Ocr); Assert.NotNull(session.Result.GeneratedImage); }
                    }
                    else Assert.True(session.Result.Detections.Any(box=>box.Label=="bus"));
                }
                finally { UnityEngine.Object.DestroyImmediate(window); }
            }
        }

        [UnityTest]
        public IEnumerator StudioUsesTheNativeLineRecognizerOnAWorker()
        {
            var model = MNNStudioModel.Read(_directory);
            Assert.AreEqual(MNNStudioTask.OCR, model.Task);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.OCR).CanRun(model));
            using (var session = new MNNStudioSession(MNNStudioNativeBackend.Load(model, 2, false)))
            {
                session.Send(new MNNStudioRequest {OcrCroppedLine = true, Prompt = "Read this text line.", VisionImage = MNNStudioImage.ReadRgb(Path.Combine(_root, "OCR", "receipt.png"))});
                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (session.Busy && DateTime.UtcNow < deadline)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }
                Assert.False(session.Busy);
                Assert.IsNull(session.Error);
                Assert.AreEqual("TOTAL $123.45", session.Result.Text);
                Assert.Greater(session.Result.RecognizedText.Confidence, .8);
                StringAssert.Contains("PP-OCRv5", session.Result.BackendDescription);
            }
        }
    }
}
