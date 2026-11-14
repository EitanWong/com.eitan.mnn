using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    // Configuration and cache identity checks only: these tests never load weights
    // or execute inference, so they can run while another GPU service is retained.
    public sealed class LlmGpuConfigurationPlayerTests
    {
        [TestCase(MNNGpuTuningMode.None, false)]
        [TestCase(MNNGpuTuningMode.Heavy, false)]
        [TestCase(MNNGpuTuningMode.Wide, false)]
        [TestCase(MNNGpuTuningMode.Normal, false)]
        [TestCase(MNNGpuTuningMode.Fast, false)]
        [TestCase(MNNGpuTuningMode.None, true)]
        [TestCase(MNNGpuTuningMode.Heavy, true)]
        [TestCase(MNNGpuTuningMode.Wide, true)]
        [TestCase(MNNGpuTuningMode.Normal, true)]
        [TestCase(MNNGpuTuningMode.Fast, true)]
        public void OpenClTuningKeepsCpuFallbackThreads(MNNGpuTuningMode mode, bool conversation)
        {
            if (!MNNAcceleration.IsBackendAvailable(MNNBackendType.OpenCL))
                Assert.Ignore("Requires an available OpenCL backend; no model is loaded.");
            WithConfiguration(directory =>
            {
                var performance = new MNNLlmPerformanceOptions { BackendType = MNNBackendType.OpenCL,
                    ThreadCount = 3, OpenClTuningMode = mode };
                var options = MNNModelOptions.PrepareLlm(directory, performance, false,
                    Path.Combine(Path.GetDirectoryName(directory), "cache"), conversation, 0f);
                Assert.AreEqual(MNNBackendType.OpenCL, options.Backend);
                AssertNativeThreads(options.Json, (int)mode);
                options.UseCpuForCompatibility("configuration regression");
                Assert.AreEqual(MNNBackendType.CPU, options.Backend);
                AssertNativeThreads(options.Json, 3);
                Assert.AreEqual(3, performance.ThreadCount);
                Assert.AreEqual(mode, performance.OpenClTuningMode);
                TestContext.WriteLine($"OpenCL mode={(int)mode}; CPU fallback threads=3; conversation={conversation}; inference=false");
            });
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(17)]
        [TestCase(64)]
        [TestCase(128)]
        [TestCase(-1)]
        public void RejectsCombinedOrNonTuningBits(int mode)
        {
            var performance = new MNNLlmPerformanceOptions { OpenClTuningMode = (MNNGpuTuningMode)mode };
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => performance.ToJson());
            Assert.AreEqual(nameof(MNNLlmPerformanceOptions.OpenClTuningMode), error.ParamName);
        }

        [TestCase(MNNBackendType.Auto)]
        [TestCase(MNNBackendType.OpenCL)]
        public void PersistentGpuCacheSeparatesTuningAndReusesReleasedMode(MNNBackendType backend)
        {
            WithConfiguration(directory =>
            {
                string root = Path.Combine(Path.GetDirectoryName(directory), "cache");
                var performance = new MNNLlmPerformanceOptions { BackendType = backend };
                string wide;
                using (var lease = MNNLlmCache.Acquire(root, directory, "native", "gpu-driver", performance, "fixture"))
                    wide = lease.DirectoryPath;
                performance.OpenClTuningMode = MNNGpuTuningMode.Heavy;
                using (var lease = MNNLlmCache.Acquire(root, directory, "native", "gpu-driver", performance, "fixture"))
                    Assert.AreNotEqual(wide, lease.DirectoryPath);
                performance.OpenClTuningMode = MNNGpuTuningMode.Wide;
                using (var lease = MNNLlmCache.Acquire(root, directory, "native", "gpu-driver", performance, "fixture"))
                    Assert.AreEqual(wide, lease.DirectoryPath);
            });
        }

        private static void AssertNativeThreads(string json, int expected)
        {
            var configuration = MNNModelData.Object(MNNModelData.Parse(json));
            Assert.AreEqual(expected, MNNModelData.Integer(configuration["thread_num"]));
            Assert.AreEqual(expected, MNNModelData.Integer(MNNModelData.Object(configuration["mllm"])["thread_num"]));
        }

        private static void WithConfiguration(Action<string> action)
        {
            string root = Environment.GetEnvironmentVariable("MNN_INFERENCE_CACHE_DIRECTORY") ?? Application.persistentDataPath;
            string directory = Path.Combine(root, "gpu-config-" + Guid.NewGuid().ToString("N"));
            string model = Path.Combine(directory, "model");
            Directory.CreateDirectory(model);
            File.WriteAllText(Path.Combine(model, "config.json"), "{}");
            try { action(model); }
            finally { Directory.Delete(directory, true); }
        }
    }
}
