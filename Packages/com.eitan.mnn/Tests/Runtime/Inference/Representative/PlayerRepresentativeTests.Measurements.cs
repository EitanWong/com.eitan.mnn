using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public partial class PlayerRepresentativeTests
    {
        [Serializable]
        private sealed class Plan
        {
            public string runId;
            public Case[] cases;
        }
        [Serializable]
        private sealed class Case
        {
            public string id, model, backend, expectedBackend, prompt, systemPrompt, expected, image, audio;
            public int limit = 32, repeats = 2;
            public bool streaming;
            public bool tuned, weightReordering;
            public int threads = 4;
            public string precision = "Normal", power = "Normal";
        }
        [Serializable]
        private sealed class Sample
        {
            public string runId, id, model, backend, prompt, expected, text, stage, error;
            public int repeat, tokens, promptTokens;
            public int threads;
            public string precision, power;
            public long prefillMicroseconds, decodeMicroseconds, sampleMicroseconds, firstTextMicroseconds, progressCallbackMicroseconds;
            public double loadSeconds, wallSeconds, firstUpdateSeconds, prefillTokensPerSecond, decodeTokensPerSecond;
            public long baselineResidentBytes, sampledPeakResidentBytes, processPeakResidentBytes, sampledPeakPhysicalFootprintBytes;
            public bool correct, completed;
        }
        [Serializable]
        private sealed class Report
        {
            public string runId, device, os, unity, graphicsDevice;
            public List<Sample> samples = new List<Sample>();
        }

        [UnityTest, Category("MobileRealLlm")] public IEnumerator SmolLm_RealWeights() => RunModel("SmolLM2-135M-Instruct-MNN");
        [UnityTest, Category("MobileRealLlm")] public IEnumerator Qwen_RealWeights() => RunModel("Qwen3.5-0.8B-MNN");
        [UnityTest, Category("MobileRealLlm")] public IEnumerator SmolVlm_RealImage() => RunModel("SmolVLM-256M-Instruct-MNN");
        [UnityTest, Category("MobileRealLlm")] public IEnumerator Lfm_RealAudio() => RunModel("LFM2.5-Audio-1.5B-MNN");

        private IEnumerator RunModel(string name)
        {
#if !((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
            Assert.Ignore("This suite measures ARM64 IL2CPP mobile Players only.");
#endif
            string root = MobileModelRoot();
            string planPath = Path.Combine(root, "plan.json");
            if (!File.Exists(planPath)) Assert.Ignore("Stage actual mobile LLM weights and plan.json in persistentDataPath/mobile-llm.");
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(planPath));
            Assert.IsNotNull(plan.cases);
            var report = new Report{runId = plan.runId, device = SystemInfo.deviceModel, os = SystemInfo.operatingSystem, unity = Application.unityVersion, graphicsDevice = MNNInferenceCache.GraphicsDevice};
            string reportPath = Path.Combine(root, "results-" + name + ".json");
            var errors = new List<string>();
            int matched = 0;
            foreach (var item in plan.cases)
            {
                if (item.model != name) continue;
                ++matched;
                MNNBackendType backend = (MNNBackendType)Enum.Parse(typeof(MNNBackendType), item.backend);
                var row = new Sample{runId = plan.runId, id = item.id, model = name, backend = item.backend, prompt = item.prompt, expected = item.expected, stage = "load", threads = item.threads, precision = item.precision, power = item.power};
                report.samples.Add(row);
                var baseline = PlayerMeasurements.Read();
                row.baselineResidentBytes = baseline.residentBytes;
                row.sampledPeakResidentBytes = baseline.residentBytes;
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                Exception failure = null;
                string previous = null;
                var clock = Stopwatch.StartNew();
                // All JSON, path and Unity state preparation occurs before the worker.
                string directory = Path.Combine(root, "models", name);
                string cache = Path.Combine(root, "cache", item.id);
                string image = string.IsNullOrEmpty(item.image) ? null : Path.Combine(root, item.image);
                string audio = string.IsNullOrEmpty(item.audio) ? null : Path.Combine(root, item.audio);
                MNNLlm model = null;
                var tuning = new MNNLlmPerformanceOptions
                {
                    ThreadCount = item.threads, BackendType = backend,
                    PrecisionMode = (MNNPrecisionMode)Enum.Parse(typeof(MNNPrecisionMode), item.precision),
                    PowerMode = (MNNPowerMode)Enum.Parse(typeof(MNNPowerMode), item.power),
                    EnableWeightReordering = item.weightReordering
                };
                var loading = Task.Run(() => item.tuned ? MNNLlm.Load(directory, tuning, cacheDirectory: cache) :
                    MNNLlm.Load(directory, cacheDirectory: cache, backendType: backend));
                while (!loading.IsCompleted) { Observe(row); yield return null; }
                if (loading.IsFaulted) failure = loading.Exception;
                else
                {
                    model = loading.Result;
                    row.backend = model.Backend.ToString();
                }
                row.loadSeconds = clock.Elapsed.TotalSeconds;
                if (failure == null)
                {
                    for (int repeat = 0; repeat < item.repeats; ++repeat)
                    {
                        if (repeat > 0)
                        {
                            row = new Sample{runId = plan.runId, id = item.id, model = name, backend = item.backend, prompt = item.prompt, expected = item.expected, repeat = repeat, loadSeconds = report.samples[report.samples.Count - 1].loadSeconds, baselineResidentBytes = PlayerMeasurements.Read().residentBytes, threads = item.threads, precision = item.precision, power = item.power};
                            report.samples.Add(row);
                        }
                        row.stage = "generate";
                        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                        clock.Restart();
                        if (item.streaming)
                        {
                            int updates = 0;
                            var messages = new List<MNNChatMessage>();
                            if (!string.IsNullOrEmpty(item.systemPrompt)) messages.Add(new MNNChatMessage(MNNChatRole.System, item.systemPrompt));
                            messages.Add(new MNNChatMessage(MNNChatRole.User, item.prompt));
                            var task = model.GenerateConversationStreamingAsync(messages, update =>
                            {
                                if (Interlocked.Increment(ref updates) == 1) row.firstUpdateSeconds = clock.Elapsed.TotalSeconds;
                            }, item.limit);
                            while (!task.IsCompleted) { Observe(row); yield return null; }
                            if (task.IsFaulted) { failure = task.Exception; break; }
                            row.text = task.Result.Text;
                            row.tokens = task.Result.GeneratedTokens;
                            row.promptTokens = task.Result.PromptTokens;
                            row.prefillMicroseconds = task.Result.PrefillMicroseconds;
                            row.decodeMicroseconds = task.Result.DecodeMicroseconds;
                            row.sampleMicroseconds = task.Result.SampleMicroseconds;
                            row.firstTextMicroseconds = task.Result.FirstTextMicroseconds;
                            row.progressCallbackMicroseconds = task.Result.ProgressCallbackMicroseconds;
                            if (updates == 0) { failure = new InvalidOperationException("No streaming updates."); break; }
                        }
                        else if (image == null && audio == null)
                        {
                            var task = model.GenerateAsync(item.prompt, item.limit);
                            while (!task.IsCompleted) { Observe(row); yield return null; }
                            if (task.IsFaulted) { failure = task.Exception; break; }
                            var result = task.Result;
                            row.text = result.Text;
                            row.tokens = result.GeneratedTokens;
                            row.promptTokens = result.PromptTokens;
                            row.prefillMicroseconds = result.PrefillMicroseconds;
                            row.decodeMicroseconds = result.DecodeMicroseconds;
                            row.sampleMicroseconds = result.SampleMicroseconds;
                            row.prefillTokensPerSecond = result.PrefillMicroseconds > 0 ? result.PromptTokens * 1000000.0 / result.PrefillMicroseconds : 0;
                            // First output token comes from prefill; decode covers the remaining tokens.
                            row.decodeTokensPerSecond = result.DecodeMicroseconds > 0 ? Math.Max(0, result.GeneratedTokens - 1) * 1000000.0 / result.DecodeMicroseconds : 0;
                        }
                        else
                        {
                            var task = model.GenerateMultimodalAsync(item.prompt, image, audio, item.limit);
                            while (!task.IsCompleted) { Observe(row); yield return null; }
                            if (task.IsFaulted) { failure = task.Exception; break; }
                            row.text = task.Result.Text;
                            row.tokens = task.Result.GeneratedTokens;
                        }
                        row.wallSeconds = clock.Elapsed.TotalSeconds;
                        row.backend = model.Backend.ToString();
                        int chineseTokens = model.CountTokens("你好，世界！");
                        int englishTokens = model.CountTokens("Hello world 😀");
                        row.correct = row.tokens > 0 && row.text.IndexOf(item.expected, StringComparison.OrdinalIgnoreCase) >= 0 && (previous == null || previous == row.text);
                        row.correct &= chineseTokens > 0 && englishTokens > 0 && row.tokens <= item.limit;
                        if (backend != MNNBackendType.Auto && model.Backend != backend)
                            row.correct = false;
                        if (!string.IsNullOrEmpty(item.expectedBackend) && model.Backend.ToString() != item.expectedBackend)
                            row.correct = false;
                        previous = row.text;
                        row.completed = true;
                        row.stage = "completed";
                        Observe(row);
                        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                        TestContext.WriteLine(JsonUtility.ToJson(row));
                        if (!row.correct) errors.Add(item.id + ": content/repeatability check failed: " + row.text);
                    }
                }
                // Finish disposal before proceeding to the next model/backend.
                var dispose = Task.Run(() => model?.Dispose());
                while (!dispose.IsCompleted) { Observe(row); yield return null; }
                if (dispose.IsFaulted && failure == null) failure = dispose.Exception;
                if (failure != null)
                {
                    row.error = failure.ToString();
                    row.stage = "failed";
                    errors.Add(item.id + ": " + failure);
                }
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                GC.Collect();
                yield return null;
            }
            if (matched == 0) Assert.Ignore("No plan entries for " + name);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        internal static string MobileModelRoot()
        {
            string root = Path.Combine(Application.persistentDataPath, "mobile-llm");
#if UNITY_ANDROID && !UNITY_EDITOR
            // Some Android 16 devices isolate adb-created external files from the
            // app. A debuggable runner can stage the same fixtures via run-as in
            // its own internal files directory without storage permissions.
            if (!File.Exists(Path.Combine(root, "plan.json")))
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var files = activity.Call<AndroidJavaObject>("getFilesDir"))
                    root = Path.Combine(files.Call<string>("getAbsolutePath"), "mobile-llm");
            }
#endif
            return root;
        }

        private static void Observe(Sample row)
        {
            var memory = PlayerMeasurements.Read();
            row.sampledPeakResidentBytes = Math.Max(row.sampledPeakResidentBytes, memory.residentBytes);
            row.processPeakResidentBytes = Math.Max(row.processPeakResidentBytes, memory.peakResidentBytes);
            row.sampledPeakPhysicalFootprintBytes = Math.Max(row.sampledPeakPhysicalFootprintBytes, memory.physicalFootprintBytes);
        }
    }
}
