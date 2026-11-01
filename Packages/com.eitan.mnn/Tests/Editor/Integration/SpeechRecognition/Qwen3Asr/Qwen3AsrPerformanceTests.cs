using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class Qwen3AsrPerformanceTests
    {
        [Serializable] private sealed class Row
        {
            public string requestedBackend, actualBackend, audioBackend, precision, error;
            public int threads;
            public bool qualityPassed;
            public double loadMs, medianMs;
            public double[] englishMs, bilingualMs;
            public string english, bilingual;
        }
        [Serializable] private sealed class Report { public string device, unity; public Row[] rows; }

        [Test, Explicit("Measures existing Qwen3-ASR weights with independent transcript gates; no downloads.")]
        public void CompareCpuThreadCountsAndMetalPrecisionWithQualityGates()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_ASR_ROOT") ?? throw new InvalidOperationException("Set MNN_TEST_QWEN3_ASR_ROOT.");
            string model = Path.Combine(root, MNNQwen3AsrModel.Repository.Replace("/", "--"));
            MNNQwen3AsrModel.Prepare(model);
            string audio = Path.Combine(root, "..", "..", "Zipformer", "Audio");
            string reference = File.ReadAllLines(Path.Combine(audio, "en-trans.txt"))[0].Substring(6).Trim();
            var rows = new List<Row>();
            foreach (var backend in new[] {MNNBackendType.CPU, MNNBackendType.Metal})
                foreach (var precision in backend == MNNBackendType.CPU ? new[] {MNNPrecisionMode.Normal} : new[] {MNNPrecisionMode.Normal, MNNPrecisionMode.High, MNNPrecisionMode.Low})
                    foreach (int threads in backend == MNNBackendType.CPU ? new[] {1, 2, 4, 8} : new[] {4})
                    {
                        var row = new Row {requestedBackend = backend.ToString(), precision = precision.ToString(), threads = threads};
                        rows.Add(row);
                        try
                        {
                            var watch = Stopwatch.StartNew();
                            using (var recognizer = MNNQwen3AsrRecognizer.Load(model, threads, Path.Combine(root, "PerformanceCache", backend + "-" + precision + "-" + threads), backend, precision))
                            {
                                row.loadMs = watch.Elapsed.TotalMilliseconds;
                                row.actualBackend = recognizer.Backend.ToString(); row.audioBackend = recognizer.MediaBackend.ToString();
                                row.englishMs = new double[5]; row.bilingualMs = new double[5];
                                for (int i = -1; i < 5; ++i)
                                {
                                    watch.Restart(); row.english = recognizer.RecognizeWaveFile(Path.Combine(audio, "en-0.wav")).Text;
                                    row.actualBackend = recognizer.Backend.ToString(); row.audioBackend = recognizer.MediaBackend.ToString();
                                    if (i >= 0) row.englishMs[i] = watch.Elapsed.TotalMilliseconds;
                                    if (System.Text.RegularExpressions.Regex.Replace(row.english.ToUpperInvariant(), "[^A-Z]+", " ").Trim() != reference)
                                        throw new InvalidDataException("English word sequence differs from the independent reference: " + row.english);
                                    watch.Restart(); row.bilingual = recognizer.RecognizeWaveFile(Path.Combine(audio, "zh-en-0.wav")).Text;
                                    if (i >= 0) row.bilingualMs[i] = watch.Elapsed.TotalMilliseconds;
                                    if (row.bilingual != "昨天是 Monday。Today is 礼拜二。The day after tomorrow 是星期三。")
                                        throw new InvalidDataException("Bilingual transcript differs from the verified CPU result: " + row.bilingual);
                                }
                                row.actualBackend = recognizer.Backend.ToString(); row.audioBackend = recognizer.MediaBackend.ToString();
                                row.qualityPassed = true;
                                row.medianMs = row.englishMs.OrderBy(value => value).ElementAt(2);
                            }
                        }
                        catch (Exception error) { row.error = error.ToString(); }
                        File.WriteAllText(Path.Combine(root, "performance.json"), JsonUtility.ToJson(new Report {device = SystemInfo.processorType, unity = Application.unityVersion, rows = rows.ToArray()}, true));
                        TestContext.WriteLine(backend + " " + precision + " threads=" + threads + " quality=" + row.qualityPassed + " median=" + row.medianMs + " " + row.error);
                    }
            Assert.True(rows.Where(row => row.requestedBackend == "CPU").All(row => row.qualityPassed), "Every baseline CPU configuration must pass.");
        }
    }
}
