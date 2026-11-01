using System;
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
        private sealed class ModelSampler
        {
            public string sampler_type;
            public string[] mixed_samplers;
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

        internal static MNNModelOptions Prepare(string directory, int threads, bool thinking, string cacheDirectory, MNNPrecisionMode precision, bool chatSampling = false, MNNBackendType backendType = MNNBackendType.Auto, bool speechRecognition = false, float? conversationTemperature = null)
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
            string backendName = MNNAcceleration.ConfigName(backend);
            var media = new MediaOptions{backend_type = backendName, thread_num = threads, precision = precisionName};
            string cache = Path.GetFullPath(cacheDirectory ?? Path.Combine(MNNInferenceCache.DirectoryFor("Cache"), "Tasks", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(cache);
            string json;
            if (chatSampling)
            {
                var source = JsonUtility.FromJson<ModelSampler>(File.ReadAllText(config)) ?? new ModelSampler();
                string sampler = string.IsNullOrEmpty(source.sampler_type) ? "greedy" : source.sampler_type;
                string[] mixed = source.mixed_samplers;
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
                studio.backend_type = backendName;
                studio.mllm = media;
                studio.thread_num = threads;
                studio.precision = precisionName;
                studio.tmp_path = cache + Path.DirectorySeparatorChar;
                studio.jinja.context.enable_thinking = thinking;
                json = JsonUtility.ToJson(studio);
            }
            else
            {
                var options = new Options{backend_type = backendName, mllm = media, thread_num = threads, precision = precisionName, tmp_path = cache + Path.DirectorySeparatorChar};
                if (speechRecognition) options.repetition_penalty = 1.0f;
                options.jinja.context.enable_thinking = thinking;
                json = JsonUtility.ToJson(options);
            }

            return new MNNModelOptions{ConfigPath = config, Json = json, CacheDirectory = cache, OwnedCache = cacheDirectory == null ? cache : null, Backend = backend, MediaBackend = backend, RequestedBackend = backendType};
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
            Json = Json.Replace("\"backend_type\":\"" + MNNAcceleration.ConfigName(Backend) + "\"", "\"backend_type\":\"cpu\"");
            Backend = MNNBackendType.CPU;
            MediaBackend = MNNBackendType.CPU;
        }

        internal void UseCpuForCompatibility(string reason)
        {
            if (Backend == MNNBackendType.CPU)
                return;
            Debug.Log("[MNN] " + ConfigPath + ": using CPU for compatibility. " + reason);
            Json = Json.Replace("\"backend_type\":\"" + MNNAcceleration.ConfigName(Backend) + "\"", "\"backend_type\":\"cpu\"");
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
