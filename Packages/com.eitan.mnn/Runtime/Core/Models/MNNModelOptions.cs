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
        internal MNNBackendType Backend;
        internal MNNBackendType MediaBackend;
        internal MNNBackendType RequestedBackend;
        private string _mediaJson;
        private bool _speechRuntimePrepared;
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
        private sealed class StudioOptions
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

        internal static MNNModelOptions Prepare(string directory, int threads, bool thinking, string cacheDirectory, MNNPrecisionMode precision, bool chatSampling = false, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException(nameof(directory));
            if (threads < 1 || threads > 64)
                throw new ArgumentOutOfRangeException(nameof(threads));
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
            var backend = MNNAcceleration.Resolve(backendType);
            string backendName = MNNAcceleration.ConfigName(backend);
            var media = new MediaOptions{backend_type = backendName, thread_num = threads, precision = precisionName};
            string cacheRoot = Environment.GetEnvironmentVariable("MNN_INFERENCE_CACHE_DIRECTORY");
            if (string.IsNullOrWhiteSpace(cacheRoot))
                cacheRoot = Path.Combine(Application.persistentDataPath, "MNN", "Cache");
            string cache = Path.GetFullPath(cacheDirectory ?? Path.Combine(cacheRoot, "Tasks", Guid.NewGuid().ToString("N")));
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

                var studio = new StudioOptions{backend_type = backendName, mllm = media, thread_num = threads, sampler_type = sampler, mixed_samplers = sampler == "mixed" ? mixed : null, precision = precisionName, tmp_path = cache + Path.DirectorySeparatorChar};
                studio.jinja.context.enable_thinking = thinking;
                json = JsonUtility.ToJson(studio);
            }
            else
            {
                var options = new Options{backend_type = backendName, mllm = media, thread_num = threads, precision = precisionName, tmp_path = cache + Path.DirectorySeparatorChar};
                options.jinja.context.enable_thinking = thinking;
                json = JsonUtility.ToJson(options);
            }

            return new MNNModelOptions{ConfigPath = config, Json = json, OwnedCache = cacheDirectory == null ? cache : null, Backend = backend, MediaBackend = backend, RequestedBackend = backendType, _mediaJson = JsonUtility.ToJson(media)};
        }

        internal bool PrepareGpuSpeech()
        {
            if (_speechRuntimePrepared)
                return false;
            // Official Omni uses the main runtime for Thinker/Talker and mllm
            // for PreDiT/DiT/BigVGAN. Metal waveform processing failed ASR
            // regression even with High precision and a shared runtime.
            string cpuMedia = _mediaJson.Replace("\"backend_type\":\"metal\"", "\"backend_type\":\"cpu\"");
            Json = Json.Replace("\"mllm\":" + _mediaJson, "\"mllm\":" + cpuMedia).Replace("\"precision\":\"normal\"", "\"precision\":\"high\"").Replace("\"precision\":\"low\"", "\"precision\":\"high\"");
            MediaBackend = MNNBackendType.CPU;
            _speechRuntimePrepared = true;
            Debug.Log("[MNN] " + ConfigPath + ": using High precision Metal Thinker/Talker with CPU speech processing.");
            return true;
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
