using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Explicit("Requires an existing Qwen3.5-0.8B model and an artifact output directory; never downloads weights.")]
    public sealed class LlmPerformanceTests
    {
        [Serializable]
        private sealed class Sample
        {
            public string backend, precision, power, prompt, text;
            public int threads, repeat, promptTokens, tokens;
            public bool streaming;
            public double loadSeconds, wallSeconds, firstUpdateSeconds;
            public long prefillUs, decodeUs, sampleUs;
        }

        [Serializable]
        private sealed class Report
        {
            public string utc, device, cpu, gpu;
            public List<Sample> samples = new List<Sample>();
        }

        [Test]
        public void CompareNativeAndManagedLatencyWithCorrectnessGates()
        {
            string model = Environment.GetEnvironmentVariable("MNN_LLM_PERFORMANCE_MODEL");
            string output = Environment.GetEnvironmentVariable("MNN_LLM_PERFORMANCE_OUTPUT");
            Assert.IsTrue(File.Exists(Path.Combine(model ?? "", "config.json")), "Set MNN_LLM_PERFORMANCE_MODEL to existing Qwen3.5-0.8B weights.");
            Assert.IsNotEmpty(output, "Set MNN_LLM_PERFORMANCE_OUTPUT under project TestArtifacts~.");
            Directory.CreateDirectory(output);
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), device = SystemInfo.deviceModel,
                cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName };
            Task.Run(() =>
            {
                foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Auto })
                foreach (int threads in new[] { 2, 4 })
                {
                    var tuning = new MNNLlmPerformanceOptions { BackendType = backend, ThreadCount = threads };
                    string cache = Path.Combine(output, "cache-" + backend + "-" + threads);
                    var clock = Stopwatch.StartNew();
                    using (var llm = MNNLlm.Load(model, tuning, cacheDirectory: cache))
                    {
                        double load = clock.Elapsed.TotalSeconds;
                        string[] prompts =
                        {
                            "中国的首都是哪里？只回答城市名称。",
                            string.Concat(System.Linq.Enumerable.Repeat("记录：天空是蓝色的，草地是绿色的。\n", 32)) + "中国的首都是哪里？只回答城市名称。",
                            "Count from 1 to 20, separated by single spaces. Output only the numbers."
                        };
                        for (int index = 0; index < prompts.Length; ++index)
                        {
                            var turns = new[] { new MNNChatMessage(MNNChatRole.User, prompts[index]) };
                            string previous = null;
                            for (int repeat = 0; repeat < 4; ++repeat)
                            {
                                var row = new Sample { backend = llm.Backend.ToString(), precision = tuning.PrecisionMode.ToString(),
                                    power = tuning.PowerMode.ToString(), threads = threads, repeat = repeat, prompt = prompts[index],
                                    loadSeconds = load, streaming = repeat % 2 == 1 };
                                int updates = 0;
                                clock.Restart();
                                var result = row.streaming
                                    ? llm.GenerateConversationStreaming(turns, update =>
                                    {
                                        if (++updates == 1) row.firstUpdateSeconds = clock.Elapsed.TotalSeconds;
                                        Assert.AreEqual(-1, update.Text.IndexOf('\ufffd'));
                                    }, 96)
                                    : llm.GenerateConversation(turns, 96);
                                row.wallSeconds = clock.Elapsed.TotalSeconds;
                                row.text = result.Text; row.tokens = result.GeneratedTokens; row.promptTokens = result.PromptTokens;
                                row.prefillUs = result.PrefillMicroseconds; row.decodeUs = result.DecodeMicroseconds;
                                row.sampleUs = result.SampleMicroseconds;
                                report.samples.Add(row);
                                File.WriteAllText(Path.Combine(output, "managed-performance.json"), JsonUtility.ToJson(report, true));
                                StringAssert.Contains(index == 2 ? "20" : "北京", result.Text);
                                Assert.Greater(result.GeneratedTokens, 0);
                                Assert.LessOrEqual(result.GeneratedTokens, 96);
                                Assert.Greater(result.PrefillMicroseconds, 0);
                                if (row.streaming) Assert.Greater(updates, 0);
                                if (previous != null) Assert.AreEqual(previous, result.Text, "Streaming/ordinary greedy output must match within a configuration.");
                                previous = result.Text;
                            }
                        }
                        using (var cancel = new CancellationTokenSource())
                        {
                            var stopped = llm.GenerateConversationStreaming(new[] { new MNNChatMessage(MNNChatRole.User, "Explain gravity in detail.") },
                                update => cancel.Cancel(), 96, cancellationToken: cancel.Token);
                            Assert.IsTrue(stopped.Cancelled);
                            Assert.IsNotEmpty(stopped.Text);
                            StringAssert.Contains("2", llm.Generate("What is 1 + 1? Answer only the number.", 16).Text);
                        }
                    }
                }
            }).GetAwaiter().GetResult();
            TestContext.WriteLine("Saved " + report.samples.Count + " real managed/native measurements to " + output);
        }
    }
}
