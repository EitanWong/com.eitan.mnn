using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class PocketTtsStreamingPerformanceTests
    {
        [Test, Explicit("Measure actual incremental Pocket PCM delivery; writes evidence without granting realtime qualification.")]
        public void MeasureColdAndWarmChunkDelivery()
            => Measure(4, MNNPrecisionMode.Normal, "pocket-streaming-performance.json");

        [Test, Explicit("Measure opt-in low-precision main with the same PCM and cancellation checks.")]
        public void MeasureLowPrecisionChunkDelivery()
            => Measure(4, MNNPrecisionMode.Low, "pocket-streaming-low-threads-4.json");

        private static void Measure(int threads, MNNPrecisionMode mainPrecision, string filename)
        {
            string root = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_LOCAL_MODEL_ROOT"), "Pocket-TTS-MNN");
            var reference = MNNReferenceAudio.FromWaveFile(Path.Combine(root, "reference_sample.wav"));
            var report = new Report
            {
                utc = DateTime.UtcNow.ToString("O"),
                unity = Application.unityVersion,
                os = SystemInfo.operatingSystem,
                cpu = SystemInfo.processorType,
                logicalProcessors = SystemInfo.processorCount,
                threads = threads,
                mainPrecision = mainPrecision.ToString(),
                text = "The capital of France is Paris.",
                scope = "CPU, Unity Editor Mono; no playback device, AEC or concurrent Omni load. First chunk is callback delivery, not audible onset."
            };
            var load = Stopwatch.StartNew();
            using (var model = MNNPocketTts.Load(root, report.threads, mainPrecision))
            {
                report.loadSeconds = load.Elapsed.TotalSeconds;
                report.runs = new Run[4];
                for (int run = 0; run < report.runs.Length; ++run)
                {
                    var arrivals = new List<double>(375);
                    var counts = new List<int>(375);
                    var watch = Stopwatch.StartNew();
                    int samples = model.SynthesizeStreaming(report.text, reference, (_, __, count, ___) =>
                    {
                        arrivals.Add(watch.Elapsed.TotalSeconds);
                        counts.Add(count);
                    });
                    double elapsed = watch.Elapsed.TotalSeconds;
                    Assert.That(arrivals.Count, Is.GreaterThan(13), "At least one complete >1 s throughput window is needed.");
                    Assert.That(counts, Is.All.EqualTo(1920));
                    Assert.AreEqual(samples, counts.Sum());
                    double audioSeconds = (double)samples / model.SampleRate;
                    double[] times = arrivals.ToArray();
                    // Exclude startup from sustained throughput; preserve all callback times
                    // so separate startup and total-request costs remain visible.
                    var windows = new List<double>();
                    for (int end = 13; end < times.Length; ++end)
                        windows.Add((times[end] - times[end - 13]) / (13 * .08));
                    report.runs[run] = new Run
                    {
                        cold = run == 0,
                        firstChunkSeconds = times[0],
                        totalSeconds = elapsed,
                        audioSeconds = audioSeconds,
                        totalRtf = elapsed / audioSeconds,
                        sustainedRtf = (times[times.Length - 1] - times[0]) / ((times.Length - 1) * .08),
                        maxWindowRtf = windows.Max(),
                        maxChunkGapSeconds = times.Skip(1).Select((time, index) => time - times[index]).Max(),
                        chunkArrivalSeconds = times
                    };
                    TestContext.WriteLine(JsonUtility.ToJson(report.runs[run]));
                }

                using (var cancellation = new CancellationTokenSource())
                {
                    int delivered = 0;
                    double cancelledAt = 0;
                    var watch = Stopwatch.StartNew();
                    Assert.Throws<OperationCanceledException>(() => model.SynthesizeStreaming(report.text, reference,
                        (_, __, ___, ____) =>
                        {
                            ++delivered;
                            cancelledAt = watch.Elapsed.TotalSeconds;
                            cancellation.Cancel();
                        }, cancellationToken: cancellation.Token));
                    report.callbackCancelToReturnSeconds = watch.Elapsed.TotalSeconds - cancelledAt;
                    Assert.AreEqual(1, delivered);
                }
            }
            Directory.CreateDirectory(AdditionalTtsValidation.Artifacts);
            string path = Path.Combine(AdditionalTtsValidation.Artifacts, filename);
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            TestContext.WriteLine("Streaming measurement: " + path);
        }

        [Serializable]
        private sealed class Report
        {
            public string utc, unity, os, cpu, text, scope, mainPrecision;
            public int logicalProcessors, threads;
            public double loadSeconds, callbackCancelToReturnSeconds;
            public Run[] runs;
        }

        [Serializable]
        private sealed class Run
        {
            public bool cold;
            public double firstChunkSeconds, totalSeconds, audioSeconds, totalRtf, sustainedRtf, maxWindowRtf, maxChunkGapSeconds;
            public double[] chunkArrivalSeconds;
        }
    }
}
