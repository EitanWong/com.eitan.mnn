#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MNN.Unity;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class MNNWindowsLlmBenchmark
{
    [Serializable] public sealed class Request
    {
        public string model, output, backend = "CPU", precision = "Normal", power = "Normal";
        public string gpuTuning = "Wide", attention = "Default", cacheDirectory;
        public int threads = 4, repeats = 3, tokens = 64, promptCopies = 24, chunk = -1;
        public bool automatic, reorder;
    }
    [Serializable] public sealed class Sample
    {
        public int repeat, promptTokens, generatedTokens;
        public long firstTextUs, prefillUs, decodeUs, sampleUs;
        public double wallMs;
        public string text;
        public string startUtc, endUtc;
        public int[] tokenIds;
    }
    [Serializable] public sealed class Report
    {
        public string scriptingBackend, unity, model, requestedBackend, actualBackend, requestedConfiguration, capital, arithmetic, error;
        public bool passed;
        public int threads, repeats, unicodeTokens;
        public double loadMs;
        public string loadStartUtc, loadEndUtc;
        public long peakWorkingSetBytes;
        public Sample[] samples;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static async void Start()
    {
        string file = Environment.GetEnvironmentVariable("MNN_WINDOWS_BENCHMARK_REQUEST");
        if (string.IsNullOrEmpty(file)) { Debug.LogError("Missing MNN_WINDOWS_BENCHMARK_REQUEST"); Application.Quit(2); return; }
        Request request = null;
        try
        {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(file));
            Validate(request);
            var report = await Task.Run(() => Run(request));
            report.unity = Application.unityVersion;
            File.WriteAllText(request.output, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log("MNN_WINDOWS_BENCHMARK_RESULT=" + request.output);
            Application.Quit(report.passed ? 0 : 1);
        }
        catch (Exception error)
        {
            if (request != null && !string.IsNullOrEmpty(request.output))
                File.WriteAllText(request.output, JsonUtility.ToJson(new Report { passed = false, error = error.ToString() }, true));
            Debug.LogException(error);
            Application.Quit(1);
        }
    }

    private static void Validate(Request request)
    {
        if (request == null || !Directory.Exists(request.model) || !File.Exists(Path.Combine(request.model, "config.json")))
            throw new ArgumentException("Provide an existing MNN model installation.");
        if (request.threads < 1 || request.threads > 64 || request.repeats < 1 || request.repeats > 20 || request.tokens < 16 || request.tokens > 256)
            throw new ArgumentException("Invalid benchmark workload.");
        if (request.promptCopies < 1 || request.promptCopies > 512 || request.chunk < -1 ||
            (request.attention != "Default" && request.attention != "Enabled" && request.attention != "Disabled"))
            throw new ArgumentException("Invalid prompt, prefill chunk or attention setting.");
        if (request.automatic && (request.gpuTuning != "Wide" || request.attention != "Default" || request.chunk != -1 || request.reorder))
            throw new ArgumentException("Automatic load cannot apply explicit tuning overrides.");
        if (File.Exists(request.output)) throw new IOException("Refusing to overwrite a benchmark result.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.output)));
    }

    private static Report Run(Request request)
    {
#if ENABLE_IL2CPP
        const string scripting = "IL2CPP";
#else
        const string scripting = "Mono";
#endif
        var report = new Report { scriptingBackend = scripting, model = request.model, requestedBackend = request.backend,
            threads = request.threads, repeats = request.repeats, samples = new Sample[request.repeats] };
        var tuning = new MNNLlmPerformanceOptions {
            BackendType = (MNNBackendType)Enum.Parse(typeof(MNNBackendType), request.backend),
            ThreadCount = request.threads,
            PrecisionMode = (MNNPrecisionMode)Enum.Parse(typeof(MNNPrecisionMode), request.precision),
            PowerMode = (MNNPowerMode)Enum.Parse(typeof(MNNPowerMode), request.power),
            OpenClTuningMode = (MNNGpuTuningMode)Enum.Parse(typeof(MNNGpuTuningMode), request.gpuTuning),
            EnableWeightReordering = request.reorder };
        if (request.attention != "Default") tuning.FlashAttention = request.attention == "Enabled";
        if (request.chunk >= 0) tuning.PrefillChunkSize = request.chunk;
        report.loadStartUtc = DateTime.UtcNow.ToString("o");
        var timer = Stopwatch.StartNew();
        using (var model = request.automatic ? MNNLlm.Load(request.model, cacheDirectory: request.cacheDirectory) :
            MNNLlm.Load(request.model, tuning, cacheDirectory: request.cacheDirectory))
        {
            report.loadMs = timer.Elapsed.TotalMilliseconds;
            report.loadEndUtc = DateTime.UtcNow.ToString("o");
            report.actualBackend = model.Backend.ToString();
            report.requestedConfiguration = JsonUtility.ToJson(request);
            if (!request.automatic && tuning.BackendType != MNNBackendType.Auto && model.Backend != tuning.BackendType)
                throw new InvalidOperationException("Requested accelerator fell back to " + model.Backend);
            report.unicodeTokens = model.CountTokens("Unity 中文 😀");
            report.capital = Generate(model, "What is the capital of France? Answer only the city name.");
            report.arithmetic = Generate(model, "What is 2+2? Answer only the number.");
            if (report.unicodeTokens <= 1 || !report.capital.ToLowerInvariant().Contains("paris") || report.arithmetic.Trim() != "4")
                throw new InvalidOperationException("Correctness gate failed: capital=" + report.capital + "; arithmetic=" + report.arithmetic);
            var messages = new[] { new MNNChatMessage(MNNChatRole.System,
                string.Concat(Enumerable.Repeat("Follow the user's instructions. Be clear and accurate. ", request.promptCopies))),
                new MNNChatMessage(MNNChatRole.User, "Count from 1 to 40. Output only the numbers separated by commas.") };
            int promptTokens = model.CountConversationTokens(messages);
            int[] expected = null;
            for (int repeat = -1; repeat < request.repeats; ++repeat)
            {
                var text = new StringBuilder();
                string startUtc = DateTime.UtcNow.ToString("o");
                timer.Restart();
                var result = model.GenerateConversationStreamingDeltas(messages, d => text.Append(d.Text), request.tokens);
                double wallMs = timer.Elapsed.TotalMilliseconds;
                string endUtc = DateTime.UtcNow.ToString("o");
                if (result.Cancelled || result.GeneratedTokens < 16 || result.TotalPromptTokens != promptTokens || text.ToString() != result.Text)
                    throw new InvalidOperationException("Incomplete or inconsistent generation.");
                int[] ids = result.GeneratedTokenIds.ToArray();
                if (expected != null && !expected.SequenceEqual(ids)) throw new InvalidOperationException("Repeated greedy tokens differ.");
                expected = ids;
                if (repeat >= 0) report.samples[repeat] = new Sample { repeat = repeat, promptTokens = promptTokens,
                    generatedTokens = result.GeneratedTokens, firstTextUs = result.FirstTextMicroseconds,
                    prefillUs = result.PrefillMicroseconds, decodeUs = result.DecodeMicroseconds,
                    sampleUs = result.SampleMicroseconds, wallMs = wallMs, text = result.Text, tokenIds = ids,
                    startUtc = startUtc, endUtc = endUtc };
            }
        }
        var memory = new ProcessMemoryCounters { Size = (uint)Marshal.SizeOf<ProcessMemoryCounters>() };
        if (!GetProcessMemoryInfo(GetCurrentProcess(), ref memory, memory.Size))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        report.peakWorkingSetBytes = checked((long)memory.PeakWorkingSet.ToUInt64());
        report.passed = true;
        return report;
    }

    private static string Generate(MNNLlm model, string prompt)
        => model.GenerateConversation(new[] { new MNNChatMessage(MNNChatRole.User, prompt) }, 24).Text;

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCounters
    {
        public uint Size, PageFaults;
        public UIntPtr PeakWorkingSet, WorkingSet, PeakPagedPool, PagedPool,
            PeakNonPagedPool, NonPagedPool, Pagefile, PeakPagefile;
    }
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCounters counters, uint size);
}
#endif
