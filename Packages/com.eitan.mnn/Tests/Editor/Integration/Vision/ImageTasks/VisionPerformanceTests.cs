using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Explicit("Benchmarks existing local detection and OCR models on CPU/Auto at 1/2/4/8 threads, with correctness gates; no downloads.")]
    public class VisionPerformanceTests
    {
        [Serializable]
        private sealed class Measurement
        {
            public string model, requestedBackend, actualBackend;
            public int threads, samples;
            public double loadMs, firstRunMs, medianMs, p95Ms, minimumMs;
        }
        [Serializable]
        private sealed class Report
        {
            public string utc, unity, device, cpu, gpu;
            public string scope = "One local machine, cached kernels, sequential corpus, end-to-end managed+native latency. Not a global optimum or other-device guarantee.";
            public List<Measurement> measurements = new List<Measurement>();
        }

        [UnityTest]
        public IEnumerator CompareCorrectnessQualifiedCpuAndMetalConfigurations()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_VISION_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_VISION_ROOT.");
            string yolo = Path.Combine(root, "modelscope", MNNYoloV8Model.Repository);
            string ocr = Path.Combine(root, "huggingface", MNNPaddleTextModel.Repository);
            MNNYoloV8Model.Prepare(yolo);
            MNNPaddleTextModel.Prepare(ocr);
            var bus = MNNStudioImage.ReadRgb(Path.Combine(root, "bus.jpg"));
            var images = new[] {"hello", "receipt", "long"}.Select(name => MNNStudioImage.ReadRgb(Path.Combine(root, "OCR", name + ".png"))).ToArray();
            string[] expected = {"Hello MNN 2026", "TOTAL $123.45", "Unity inference works with dynamic width 2026."};
            var report = new Report {utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,device=SystemInfo.deviceModel,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName};
            var task = Task.Run(() =>
            {
                foreach (var backend in new[] {MNNBackendType.CPU, MNNBackendType.Auto})
                foreach (int threads in new[] {1,2,4,8})
                {
                    var clock=Stopwatch.StartNew();
                    using (var detector = MNNYoloV8.Load(yolo, threadCount:threads, backendType:backend))
                    {
                        double load=clock.Elapsed.TotalMilliseconds;
                        var measurement=Measure("YOLOv8-N",backend,threads,load, () =>
                        {
                            var boxes=detector.Detect(bus);
                            var box=boxes.Single(item => item.Label=="bus" && item.Score>.6f);
                            Assert.Greater(MNNYoloV8.IntersectionOverUnion(box,new MNNDetection(0,"reference",1,10,225,800,550)),.7f);
                        });
                        measurement.actualBackend=detector.Backend.ToString();report.measurements.Add(measurement);
                    }
                    clock.Restart();
                    using (var recognizer = MNNPaddleTextRecognizer.Load(ocr, threadCount:threads, backendType:backend))
                    {
                        double load=clock.Elapsed.TotalMilliseconds;
                        int index=0;
                        var measurement=Measure("PP-OCRv5-English",backend,threads,load, () =>
                        {
                            int selected=index++%images.Length;
                            var text=recognizer.Recognize(images[selected]);
                            Assert.AreEqual(expected[selected],text.Text);
                            Assert.Greater(text.Confidence,.8f);
                        });
                        measurement.actualBackend=recognizer.Backend.ToString();report.measurements.Add(measurement);
                    }
                }
                File.WriteAllText(Path.Combine(root,"vision-performance.json"),JsonUtility.ToJson(report,true));
            });
            while (!task.IsCompleted) yield return null;
            Assert.False(task.IsFaulted,task.Exception?.ToString());
            foreach(var row in report.measurements)
                TestContext.WriteLine(row.model+" "+row.actualBackend+" threads="+row.threads+" median="+row.medianMs.ToString("F2")+"ms p95="+row.p95Ms.ToString("F2")+"ms");
        }

        private static Measurement Measure(string model, MNNBackendType backend, int threads, double load, Action operation)
        {
            var clock=Stopwatch.StartNew();operation();double first=clock.Elapsed.TotalMilliseconds;
            operation();operation(); // Warm-up is excluded from steady-state samples.
            var samples=new double[21];
            for(int i=0;i<samples.Length;++i){clock.Restart();operation();samples[i]=clock.Elapsed.TotalMilliseconds;}
            Array.Sort(samples);
            return new Measurement {model=model,requestedBackend=backend.ToString(),threads=threads,loadMs=load,firstRunMs=first,
                samples=samples.Length,medianMs=samples[samples.Length/2],p95Ms=samples[(int)Math.Ceiling(samples.Length*.95)-1],minimumMs=samples[0]};
        }

        [UnityTest]
        public IEnumerator CompareV4AndFp16VariantsWithExactTextQualityGate()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_VISION_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_VISION_ROOT.");
            string directory = Path.Combine(root, "huggingface", MNNPaddleTextModel.Repository);
            MNNPaddleTextModel.Prepare(directory);
            var line = MNNStudioImage.ReadRgb(Path.Combine(root, "OCR", "Multilingual", "chinese2.png"));
            var page = MNNStudioImage.ReadRgb(Path.Combine(root, "OCR", "Pages", "rotated.png"));
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, device = SystemInfo.deviceModel, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName };
            var task = Task.Run(() =>
            {
                foreach (string profile in new[] {"chinese-v4", "chinese-fp16"})
                foreach (var backend in new[] {MNNBackendType.CPU, MNNBackendType.Auto})
                foreach (int threads in new[] {1, 2, 4, 8})
                {
                    var clock = Stopwatch.StartNew();
                    using (var recognizer = MNNPaddleTextRecognizer.Load(Path.Combine(directory, "recognizers", profile), threadCount: threads, backendType: backend))
                    {
                        var row = Measure("PP-OCR-line-" + profile, backend, threads, clock.Elapsed.TotalMilliseconds, () =>
                        {
                            var value = recognizer.Recognize(line);
                            Assert.AreEqual("欢迎使用图像文字识别", value.Text);
                            Assert.Greater(value.Confidence, .8f);
                        });
                        row.actualBackend = recognizer.Backend.ToString(); report.measurements.Add(row);
                    }
                    clock.Restart();
                    using (var ocr = MNNPaddleOcr.Load(directory, "recognizers/" + profile, threadCount: threads, backendType: backend))
                    {
                        var row = Measure("PP-OCR-page-" + profile, backend, threads, clock.Elapsed.TotalMilliseconds, () =>
                        {
                            var value = ocr.Recognize(page);
                            Assert.AreEqual("Hello MNN 2026\nTOTAL $123.45\nUnity inference works", value.Text);
                            Assert.AreEqual(3, value.Regions.Count);
                            Assert.True(value.Regions.All(region => region.Confidence > .8f));
                        });
                        row.actualBackend = "detection=" + ocr.DetectionBackend + ";recognition=" + ocr.RecognitionBackend;
                        report.measurements.Add(row);
                    }
                    File.WriteAllText(Path.Combine(root, "OCR", "Variants", "performance.json"), JsonUtility.ToJson(report, true));
                }
            });
            while (!task.IsCompleted) yield return null;
            Assert.False(task.IsFaulted, task.Exception?.ToString());
            Assert.AreEqual(32, report.measurements.Count);
        }

        [UnityTest]
        public IEnumerator CompareAdditionalScriptLineAndPageConfigurationsWithExactTextQualityGate()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_VISION_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_VISION_ROOT.");
            string directory = Path.Combine(root, "huggingface", MNNPaddleTextModel.Repository);
            MNNPaddleTextModel.Prepare(directory);
            string[] profiles = {"arabic", "devanagari", "eslav", "korean", "tamil", "telugu", "thai"};
            string[] expected = {"مرحبا بالعالم", "नमस्ते दुनिया", "Привіт світ 2026", "안녕하세요 세계", "வணக்கம் உலகம்", "నమస్కారం ప్రపంచం", "สวัสดีชาวโลก"};
            var lines = profiles.Select(name => MNNStudioImage.ReadRgb(Path.Combine(root, "OCR", "AdditionalScripts", (name == "korean" ? "korean-tight" : name) + ".png"))).ToArray();
            var pages = profiles.Select(name => MNNStudioImage.ReadRgb(Path.Combine(root, "OCR", "AdditionalScripts", name + "-page.png"))).ToArray();
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, device = SystemInfo.deviceModel, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName };
            var task = Task.Run(() =>
            {
                for (int i = 0; i < profiles.Length; ++i)
                foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Auto })
                foreach (int threads in new[] { 1, 2, 4, 8 })
                {
                    var clock = Stopwatch.StartNew();
                    using (var recognizer = MNNPaddleTextRecognizer.Load(Path.Combine(directory, "recognizers", profiles[i]), threadCount: threads, backendType: backend))
                    {
                        var row = Measure("PP-OCRv5-line-" + profiles[i], backend, threads, clock.Elapsed.TotalMilliseconds, () =>
                        {
                            var result = recognizer.Recognize(lines[i]);
                            Assert.AreEqual(expected[i], result.Text);
                            Assert.Greater(result.Confidence, .8f);
                        });
                        row.actualBackend = recognizer.Backend.ToString(); report.measurements.Add(row);
                    }
                    clock.Restart();
                    using (var ocr = MNNPaddleOcr.Load(directory, "recognizers/" + profiles[i], threadCount: threads, backendType: backend))
                    {
                        var row = Measure("PP-OCRv5-page-" + profiles[i], backend, threads, clock.Elapsed.TotalMilliseconds, () =>
                        {
                            var result = ocr.Recognize(pages[i]);
                            Assert.AreEqual(expected[i] + "\nHello MNN 2026", result.Text);
                            Assert.AreEqual(2, result.Regions.Count);
                            Assert.True(result.Regions.All(region => region.Confidence > .8f));
                        });
                        row.actualBackend = "detection=" + ocr.DetectionBackend + ";recognition=" + ocr.RecognitionBackend;
                        report.measurements.Add(row);
                    }
                    File.WriteAllText(Path.Combine(root, "OCR", "AdditionalScripts", "performance.json"), JsonUtility.ToJson(report, true));
                }
            });
            while (!task.IsCompleted) yield return null;
            Assert.False(task.IsFaulted, task.Exception?.ToString());
            Assert.AreEqual(112, report.measurements.Count);
        }

        [UnityTest]
        public IEnumerator CompareMultilingualRecognizerConfigurationsWithExactTextQualityGate()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_VISION_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_VISION_ROOT.");
            string directory = Path.Combine(root, "huggingface", MNNPaddleTextModel.Repository);
            MNNPaddleTextModel.Prepare(directory);
            string[][] names = { new[] { "chinese", "chinese2" }, new[] { "latin", "german" }, new[] { "russian", "ascii" }, new[] { "greek", "ascii" } };
            string[][] expected = { new[] { "你好世界 2026", "欢迎使用图像文字识别" }, new[] { "Bonjour été 2026", "Grüße aus München" }, new[] { "Привет мир 2026", "Hello MNN 2026" }, new[] { "Καλημέρα κόσμε 2026", "Hello MNN 2026" } };
            var images = names.Select(pair => pair.Select(name => MNNStudioImage.ReadRgb(Path.Combine(root, "OCR", "Multilingual", name + ".png"))).ToArray()).ToArray();
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, device = SystemInfo.deviceModel, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName };
            var task = Task.Run(() =>
            {
                for (int profile = 0; profile < names.Length; ++profile)
                foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Auto })
                foreach (int threads in new[] { 1, 2, 4, 8 })
                {
                    string name = new[] { "chinese", "latin", "cyrillic", "greek" }[profile];
                    var clock = Stopwatch.StartNew();
                    using (var recognizer = MNNPaddleTextRecognizer.Load(Path.Combine(directory, "recognizers", name), threadCount: threads, backendType: backend))
                    {
                        int index = 0;
                        var measurement = Measure("PP-OCRv5-" + name, backend, threads, clock.Elapsed.TotalMilliseconds, () =>
                        {
                            int selected = index++ % images[profile].Length;
                            var result = recognizer.Recognize(images[profile][selected]);
                            Assert.AreEqual(expected[profile][selected], result.Text);
                            Assert.Greater(result.Confidence, .8f);
                        });
                        measurement.actualBackend = recognizer.Backend.ToString();
                        report.measurements.Add(measurement);
                    }
                }
                File.WriteAllText(Path.Combine(root, "OCR", "Multilingual", "performance.json"), JsonUtility.ToJson(report, true));
            });
            while (!task.IsCompleted)
                yield return null;
            Assert.False(task.IsFaulted, task.Exception?.ToString());
            foreach (var row in report.measurements)
                TestContext.WriteLine(row.model + " " + row.actualBackend + " threads=" + row.threads + " median=" + row.medianMs.ToString("F2") + "ms p95=" + row.p95Ms.ToString("F2") + "ms");
        }
        [UnityTest]
        public IEnumerator CompareFullPageOcrConfigurationsWithExactTextQualityGate()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_VISION_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_VISION_ROOT.");
            string directory = Path.Combine(root, "huggingface", MNNPaddleTextModel.Repository);
            MNNPaddleTextModel.Prepare(directory);
            var english = new[] {"english", "rotated"}.Select(name => MNNStudioImage.ReadRgb(Path.Combine(root, "OCR", "Pages", name + ".png"))).ToArray();
            var chinese = MNNStudioImage.ReadRgb(Path.Combine(root, "OCR", "Pages", "chinese.png"));
            var report = new Report {utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, device = SystemInfo.deviceModel, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName};
            var task = Task.Run(() =>
            {
                foreach (string profile in new[] {".", "recognizers/chinese"})
                foreach (var backend in new[] {MNNBackendType.CPU, MNNBackendType.Auto})
                foreach (int threads in new[] {1, 2, 4, 8})
                {
                    var clock = Stopwatch.StartNew();
                    using (var ocr = MNNPaddleOcr.Load(directory, profile, threadCount: threads, backendType: backend))
                    {
                        int index = 0;
                        var measurement = Measure("PP-OCRv5-page-" + (profile == "." ? "english" : "chinese"), backend, threads, clock.Elapsed.TotalMilliseconds, () =>
                        {
                            bool en = profile == ".";
                            var result = ocr.Recognize(en ? english[index++ % english.Length] : chinese);
                            Assert.AreEqual(en ? "Hello MNN 2026\nTOTAL $123.45\nUnity inference works" : "你好世界 2026\n欢迎使用图像文字识别", result.Text);
                            Assert.True(result.Regions.All(region => region.Confidence > .8f));
                        });
                        measurement.actualBackend = "detection=" + ocr.DetectionBackend + ";recognition=" + ocr.RecognitionBackend;
                        report.measurements.Add(measurement);
                    }
                }
                File.WriteAllText(Path.Combine(root, "OCR", "Pages", "performance.json"), JsonUtility.ToJson(report, true));
            });
            while (!task.IsCompleted) yield return null;
            Assert.False(task.IsFaulted, task.Exception?.ToString());
            foreach (var row in report.measurements)
                TestContext.WriteLine(row.model + " " + row.actualBackend + " threads=" + row.threads + " median=" + row.medianMs.ToString("F2") + "ms p95=" + row.p95Ms.ToString("F2") + "ms");
        }

    }
}
