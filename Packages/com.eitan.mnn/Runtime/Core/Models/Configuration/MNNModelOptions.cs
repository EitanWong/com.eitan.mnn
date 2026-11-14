using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    // Shared accelerator configuration and cache ownership for Module-based tasks.
    internal sealed class MNNModelOptions
    {
        internal string ConfigPath;
        internal string Json;
        internal string OwnedCache;
        internal string CacheDirectory;
        internal MNNBackendType Backend;
        internal MNNBackendType MediaBackend;
        internal MNNBackendType RequestedBackend;
        private string _cpuFallbackJson;
        [Serializable]
        private sealed class MediaOptions
        {
            public string backend_type;
            public int thread_num;
            public string precision;
        }

        [Serializable]
        private sealed class Context
        {
            public bool enable_thinking;
        }

        [Serializable]
        private sealed class Jinja
        {
            public Context context = new Context();
        }

        [Serializable]
        private sealed class Options
        {
            public string backend_type = "cpu";
            public int thread_num;
            public string sampler_type = "greedy";
            public float repetition_penalty = 1.1f;
            public string precision;
            public string tmp_path;
            public Jinja jinja = new Jinja();
            public MediaOptions mllm;
        }

        [Serializable]
        private class StudioOptions
        {
            public string backend_type = "cpu";
            public int thread_num;
            public string sampler_type;
            public string[] mixed_samplers;
            public float repetition_penalty = 1.1f;
            public int penalty_window = 128;
            public string precision;
            public string tmp_path;
            public Jinja jinja = new Jinja();
            public MediaOptions mllm;
        }

        [Serializable]
        private sealed class TemperatureOptions : StudioOptions
        {
            public float temperature;
            public int topK = 40;
            public float topP = .95f;
        }

        internal static MNNModelOptions PrepareLlm(string directory, MNNLlmPerformanceOptions performance,
            bool thinking, string cacheDirectory, bool conversation, float? temperature)
        {
            var tuning = performance.Resolve(directory);
            var options = PrepareResolvedLlm(directory, tuning, thinking, cacheDirectory, conversation, temperature);
            // Availability and dynamic-graph policy can select CPU even before load.
            // Resolve from the original overrides, so GPU defaults never become CPU overrides.
            if (options.Backend != tuning.BackendType)
            {
                tuning = performance.ResolveForBackend(directory, options.Backend);
                var actual = PrepareResolvedLlm(directory, tuning, thinking, options.CacheDirectory, conversation, temperature);
                options.Json = actual.Json;
            }
            if (options.Backend != MNNBackendType.CPU)
                options._cpuFallbackJson = PrepareResolvedLlm(directory,
                    performance.ResolveForBackend(directory, MNNBackendType.CPU), thinking,
                    options.CacheDirectory, conversation, temperature).Json;
            options.RequestedBackend = performance.BackendType;
            return options;
        }

        private static MNNModelOptions PrepareResolvedLlm(string directory, MNNLlmPerformanceOptions tuning,
            bool thinking, string cacheDirectory, bool conversation, float? temperature)
        {
            string extra = tuning.ToJson(directory);
            var options = Prepare(directory, tuning.ThreadCount, thinking, cacheDirectory, tuning.PrecisionMode,
                conversation, tuning.BackendType, conversationTemperature: temperature, openClTuningMode: tuning.OpenClTuningMode);
            options.Json = options.Json.Substring(0, options.Json.Length - 1) + "," + extra.Substring(1);
            return options;
        }

        internal static MNNModelOptions Prepare(string directory, int threads, bool thinking, string cacheDirectory, MNNPrecisionMode precision, bool chatSampling = false, MNNBackendType backendType = MNNBackendType.Auto, bool speechRecognition = false, float? conversationTemperature = null, MNNGpuTuningMode? openClTuningMode = null)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException(nameof(directory));
            if (threads < 1 || threads > 64)
                throw new ArgumentOutOfRangeException(nameof(threads));
            if (conversationTemperature.HasValue && (float.IsNaN(conversationTemperature.Value) ||
                float.IsInfinity(conversationTemperature.Value) || conversationTemperature.Value < 0 || conversationTemperature.Value > 2))
                throw new ArgumentOutOfRangeException(nameof(conversationTemperature));
            // MNN's LLM config supports these three strings; it has no BF16 config mapping.
            string precisionName;
            switch (precision)
            {
                case MNNPrecisionMode.Normal:
                    precisionName = "normal";
                    break;
                case MNNPrecisionMode.High:
                    precisionName = "high";
                    break;
                case MNNPrecisionMode.Low:
                    precisionName = "low";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(precision));
            }

            string config = Path.Combine(Path.GetFullPath(directory), "config.json");
            if (!File.Exists(config))
                throw new FileNotFoundException("MNN config is missing", config);
            var backendRequest = backendType;
#if UNITY_ANDROID && !UNITY_EDITOR
            backendRequest = ForAndroidDevice(backendType, directory, MNNInferenceCache.GraphicsDevice);
#endif
            var backend = MNNAcceleration.Resolve(MNNAcceleration.ForDynamicGraph(backendRequest));
            // Official OpenCL LLM interprets thread_num as GPU mode bits, not CPU
            // workers. Map only after availability resolution; CPU keeps its count.
            int nativeThreads = backend == MNNBackendType.OpenCL && openClTuningMode.HasValue ? (int)openClTuningMode.Value : threads;
            string backendName = MNNAcceleration.ConfigName(backend);
            var media = new MediaOptions{backend_type = backendName, thread_num = nativeThreads, precision = precisionName};
            string cache = Path.GetFullPath(cacheDirectory ?? Path.Combine(MNNInferenceCache.DirectoryFor("Cache"), "Tasks", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(cache);
            string json;
            if (chatSampling)
            {
                var source = ReadLlmConfiguration(directory);
                string sampler = source.TryGetValue("sampler_type", out var samplerValue) ? samplerValue as string : null;
                if (string.IsNullOrEmpty(sampler)) sampler = "greedy";
                string[] mixed = source.TryGetValue("mixed_samplers", out var mixedValue) && mixedValue != null ?
                    MNNModelData.Array(mixedValue).ConvertAll(value => (string)value).ToArray() : null;
                if (sampler == "mixed")
                {
                    mixed = mixed ?? new[]{"topK", "topP", "temperature"};
                    if (Array.IndexOf(mixed, "penalty") < 0)
                    {
                        var augmented = new string[mixed.Length + 1];
                        augmented[0] = "penalty";
                        Array.Copy(mixed, 0, augmented, 1, mixed.Length);
                        mixed = augmented;
                    }
                }
                else if (sampler != "penalty")
                {
                    mixed = new[]{sampler, "penalty"};
                    sampler = "mixed";
                }

                StudioOptions studio;
                if (conversationTemperature.HasValue)
                {
                    studio = new TemperatureOptions
                    {
                        sampler_type = conversationTemperature.Value == 0 ? "greedy" : "mixed",
                        mixed_samplers = conversationTemperature.Value == 0 ? Array.Empty<string>() :
                            new[] { "penalty", "topK", "topP", "temperature" },
                        temperature = conversationTemperature.Value
                    };
                }
                else studio = new StudioOptions { sampler_type = sampler, mixed_samplers = sampler == "mixed" ? mixed : null };
                // Keep the export's calibrated penalty (including the official legacy
                // alias). Only our unconfigured fallback uses a recent-token window:
                // penalizing every token in a growing prompt suppresses valid vocabulary.
                var sampling = source;
                if (sampling.TryGetValue("repetition_penalty", out var penalty) && MNNModelData.Number(penalty) >= 0)
                    studio.repetition_penalty = (float)MNNModelData.Number(penalty);
                else if (sampling.TryGetValue("penalty", out penalty))
                    studio.repetition_penalty = (float)MNNModelData.Number(penalty);
                if (sampling.TryGetValue("penalty_window", out var window))
                    studio.penalty_window = MNNModelData.Integer(window);
                studio.backend_type = backendName;
                studio.mllm = media;
                studio.thread_num = nativeThreads;
                studio.precision = precisionName;
                studio.tmp_path = cache + Path.DirectorySeparatorChar;
                studio.jinja.context.enable_thinking = thinking;
                json = JsonUtility.ToJson(studio);
            }
            else
            {
                var options = new Options{backend_type = backendName, mllm = media, thread_num = nativeThreads, precision = precisionName, tmp_path = cache + Path.DirectorySeparatorChar};
                if (speechRecognition) options.repetition_penalty = 1.0f;
                options.jinja.context.enable_thinking = thinking;
                json = JsonUtility.ToJson(options);
            }

            return new MNNModelOptions{ConfigPath = config, Json = json, CacheDirectory = cache, OwnedCache = cacheDirectory == null ? cache : null, Backend = backend, MediaBackend = backend, RequestedBackend = backendType};
        }

        // Match official LlmConfig: config.json selects base_dir/llm_config,
        // then model metadata recursively overrides it. Native paths concatenate
        // base_dir and the metadata name; a custom base_dir includes its separator.
        internal static Dictionary<string, object> ReadLlmConfiguration(string directory)
            => ReadLlmConfiguration(directory, out _);

        internal static Dictionary<string, object> ReadLlmConfiguration(string directory, out string metadata)
        {
            string root = Path.GetFullPath(directory);
            string config = Path.Combine(root, "config.json");
            var result = File.Exists(config) ? MNNModelData.Object(MNNModelData.Read(config)) : new Dictionary<string, object>();
            string baseDirectory = result.TryGetValue("base_dir", out var value) ? (string)value : root + Path.DirectorySeparatorChar;
            metadata = baseDirectory + (result.TryGetValue("llm_config", out value) ? (string)value : "llm_config.json");
            if (File.Exists(metadata)) MergeConfiguration(result, MNNModelData.Object(MNNModelData.Read(metadata)));
            return result;
        }

        private static void MergeConfiguration(Dictionary<string, object> target, Dictionary<string, object> source)
        {
            foreach (var entry in source)
            {
                if (target.TryGetValue(entry.Key, out var value) && value is Dictionary<string, object> nested &&
                    entry.Value is Dictionary<string, object> replacement)
                    MergeConfiguration(nested, replacement);
                else target[entry.Key] = entry.Value;
            }
        }

        internal static MNNBackendType ForAndroidDevice(MNNBackendType requested, string directory, string graphicsDevice)
        {
            if (requested != MNNBackendType.Auto ||
                !string.Equals(Path.GetFileName(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)),
                    "Qwen3.5-0.8B-MNN", StringComparison.OrdinalIgnoreCase))
                return requested;

            string gpu = (graphicsDevice ?? string.Empty).Split('|')[0].Trim();
            // Measured with the bundled MNN build: Mali-G57 decodes faster on CPU.
            // PJA110 / Adreno 740 also has slow cold OpenCL prefill and a native
            // ConvBufLowMemoryExecution kernel crash on the long chat workload.
            // Explicit backend requests remain available for diagnostics.
            if (gpu.StartsWith("Mali-G57", StringComparison.OrdinalIgnoreCase) ||
                MNNInferenceCache.IsAdreno740(gpu))
                return MNNBackendType.CPU;
            return requested;
        }

        internal T Load<T>(Func<string, string, T> create)
            where T : System.Runtime.InteropServices.SafeHandle
        {
            try
            {
                return CreateChecked(create);
            }
            catch (MNNException error)when (Backend != MNNBackendType.CPU)
            {
                FallBackToCpu(error);
                return CreateChecked(create);
            }
        }

        private T CreateChecked<T>(Func<string, string, T> create)
            where T : System.Runtime.InteropServices.SafeHandle
        {
            var handle = create(ConfigPath, Json);
            if (handle == null || handle.IsInvalid)
            {
                handle?.Dispose();
                throw new MNNException(MNNErrorCode.NoExecution, "Failed to load model: " + ConfigPath);
            }

            Debug.Log($"[MNN] Loaded {ConfigPath}: requested={RequestedBackend}, configured={Backend}");
            return handle;
        }

        internal void FallBackToCpu(Exception reason)
        {
            Debug.Log($"[MNN] {ConfigPath}: {Backend} failed; retrying on CPU. {reason.Message}");
            SelectCpuConfiguration();
        }

        internal void UseCpuForCompatibility(string reason)
        {
            if (Backend == MNNBackendType.CPU)
                return;
            Debug.Log("[MNN] " + ConfigPath + ": using CPU for compatibility. " + reason);
            SelectCpuConfiguration();
        }

        private void SelectCpuConfiguration()
        {
            Json = _cpuFallbackJson ?? Json.Replace("\"backend_type\":\"" + MNNAcceleration.ConfigName(Backend) + "\"", "\"backend_type\":\"cpu\"");
            Backend = MNNBackendType.CPU;
            MediaBackend = MNNBackendType.CPU;
        }

        internal static void RemoveOwnedCache(string path)
        {
            if (path == null)
                return;
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
