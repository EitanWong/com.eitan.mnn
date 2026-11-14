using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class LlmPerformanceOptionsTests
    {
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(8)]
        [TestCase(16)]
        public void OpenClCpuThreadCountDoesNotSelectGpuTuning(int threads)
        {
            if (!MNNAcceleration.IsBackendAvailable(MNNBackendType.OpenCL))
                Assert.Ignore("Requires the OpenCL runtime.");
            WithQwenConfiguration(directory =>
            {
                var performance = new MNNLlmPerformanceOptions { BackendType = MNNBackendType.OpenCL, ThreadCount = threads };
                var options = MNNModelOptions.PrepareLlm(directory, performance, false, Path.Combine(directory, "cache"), true, 0f);
                Assert.AreEqual(MNNBackendType.OpenCL, options.Backend);
                var configuration = MNNModelData.Object(MNNModelData.Parse(options.Json));
                Assert.AreEqual((int)MNNGpuTuningMode.Wide, MNNModelData.Integer(configuration["thread_num"]));
                options.UseCpuForCompatibility("test transition");
                configuration = MNNModelData.Object(MNNModelData.Parse(options.Json));
                Assert.AreEqual(threads, MNNModelData.Integer(configuration["thread_num"]));
                Assert.AreEqual(threads, performance.ThreadCount);
            });
        }

        [TestCase("qwen3_5", 1024, 24, MNNPrecisionMode.Normal)]
        [TestCase("qwen3_5", 2048, 24, MNNPrecisionMode.Low)]
        [TestCase("qwen3", 1024, 24, MNNPrecisionMode.Low)]
        [TestCase("qwen3_5", 1024, 32, MNNPrecisionMode.Low)]
        public void RenamedModelUsesQualifiedArchitectureInsteadOfDirectoryLabel(string type, int hidden,
            int layers, MNNPrecisionMode expected)
        {
            string directory = Path.GetFullPath("TestArtifacts~/LlmDeviceIdentity/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string configuration = "{\"model_type\":\"" + type + "\",\"hidden_size\":" + hidden + ",\"layer_nums\":" + layers + "}";
            File.WriteAllText(Path.Combine(directory, "llm_config.json"), configuration);
            try
            {
                var automatic = new MNNLlmPerformanceOptions { BackendType = MNNBackendType.CPU }.Resolve(directory);
                Assert.AreEqual(expected, automatic.PrecisionMode);
                var explicitLow = new MNNLlmPerformanceOptions { BackendType = MNNBackendType.CPU,
                    PrecisionMode = MNNPrecisionMode.Low }.Resolve(directory);
                Assert.AreEqual(MNNPrecisionMode.Low, explicitLow.PrecisionMode);
                Assert.AreEqual(configuration, File.ReadAllText(Path.Combine(directory, "llm_config.json")));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void AdvancedOverridesUseTheEffectiveExportConfiguration()
        {
            string directory = Path.GetFullPath("TestArtifacts~/LlmEffectiveConfiguration/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            const string configuration = "{\"dynamic_option\":1,\"attention_mode\":1,\"llm_config\":\"model-info.json\"}";
            const string metadata = "{\"dynamic_option\":10,\"attention_mode\":14,\"chunk_limits\":[128,1]}";
            File.WriteAllText(Path.Combine(directory, "config.json"), configuration);
            File.WriteAllText(Path.Combine(directory, "model-info.json"), metadata);
            try
            {
                var options = new MNNLlmPerformanceOptions { EnableWeightReordering = false, FlashAttention = false };
                var result = MNNModelData.Object(MNNModelData.Parse(options.ToJson(directory)));
                Assert.AreEqual(2, MNNModelData.Integer(result["dynamic_option"]), "Retain the metadata's dynamic quantization bits.");
                Assert.AreEqual(6, MNNModelData.Integer(result["attention_mode"]), "Retain the metadata's KV mode.");
                options.PrefillChunkSize = 64;
                Assert.Throws<ArgumentException>(() => options.ToJson(directory), "Do not override an exported chunk_limits shape contract.");
                Assert.AreEqual(configuration, File.ReadAllText(Path.Combine(directory, "config.json")));
                Assert.AreEqual(metadata, File.ReadAllText(Path.Combine(directory, "model-info.json")));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void QualifiedIdentityUsesTheConfiguredMetadataFile()
        {
            string directory = Path.GetFullPath("TestArtifacts~/LlmEffectiveConfiguration/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "config.json"), "{\"llm_config\":\"model-info.json\"}");
            File.WriteAllText(Path.Combine(directory, "model-info.json"), "{\"model_type\":\"qwen3_5\",\"hidden_size\":1024,\"layer_nums\":24}");
            try
            {
                var result = new MNNLlmPerformanceOptions { BackendType = MNNBackendType.CPU }.Resolve(directory);
                Assert.AreEqual(MNNPrecisionMode.Normal, result.PrecisionMode);
            }
            finally { Directory.Delete(directory, true); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnavailableAcceleratorUsesTheActualCpuProfile(bool explicitLow)
        {
            if (MNNAcceleration.IsBackendAvailable(MNNBackendType.Vulkan))
                Assert.Ignore("This gate requires an unavailable Vulkan backend.");
            WithQwenConfiguration(directory =>
            {
                var performance = new MNNLlmPerformanceOptions { BackendType = MNNBackendType.Vulkan };
                if (explicitLow) performance.PrecisionMode = MNNPrecisionMode.Low;
                var options = MNNModelOptions.PrepareLlm(directory, performance, false, Path.Combine(directory, "cache"), true, 0f);
                Assert.AreEqual(MNNBackendType.CPU, options.Backend);
                AssertCpuConfiguration(options, explicitLow ? "low" : "normal");
            });
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void CpuFallbackReselectsDefaultsAndPreservesExplicitOverrides(bool compatibility, bool explicitLow)
        {
            if (!MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal))
                Assert.Ignore("This gate requires the Metal-to-CPU transition.");
            WithQwenConfiguration(directory =>
            {
                var performance = new MNNLlmPerformanceOptions { BackendType = MNNBackendType.Metal };
                if (explicitLow) performance.PrecisionMode = MNNPrecisionMode.Low;
                var options = MNNModelOptions.PrepareLlm(directory, performance, false, Path.Combine(directory, "cache"), true, 0f);
                Assert.AreEqual(MNNBackendType.Metal, options.Backend);
                StringAssert.Contains("\"precision\":\"low\"", options.Json);
                if (compatibility) options.UseCpuForCompatibility("test transition");
                else options.FallBackToCpu(new MNNException(MNNErrorCode.NoExecution, "test transition"));
                AssertCpuConfiguration(options, explicitLow ? "low" : "normal");
                Assert.AreEqual(MNNBackendType.Metal, performance.BackendType, "Resolution must not mutate the caller's overrides.");
                string once = options.Json;
                options.UseCpuForCompatibility("repeat");
                Assert.AreEqual(once, options.Json);
            });
        }

        private static void AssertCpuConfiguration(MNNModelOptions options, string precision)
        {
            Assert.AreEqual(MNNBackendType.CPU, options.Backend);
            Assert.AreEqual(MNNBackendType.CPU, options.MediaBackend);
            var json = MNNModelData.Object(MNNModelData.Parse(options.Json));
            Assert.AreEqual("cpu", json["backend_type"]);
            Assert.AreEqual(precision, json["precision"]);
            var media = MNNModelData.Object(json["mllm"]);
            Assert.AreEqual("cpu", media["backend_type"]);
            Assert.AreEqual(precision, media["precision"]);
            Assert.AreEqual("greedy", json["sampler_type"]);
            Assert.AreEqual(1.17, MNNModelData.Number(json["repetition_penalty"]), .000001);
            Assert.AreEqual(63, MNNModelData.Integer(json["penalty_window"]));
        }

        private static void WithQwenConfiguration(Action<string> action)
        {
            string directory = Path.GetFullPath("TestArtifacts~/LlmFallbackConfiguration/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "config.json"), "{\"repetition_penalty\":1.17,\"penalty_window\":63}");
            File.WriteAllText(Path.Combine(directory, "llm_config.json"), "{\"model_type\":\"qwen3_5\",\"hidden_size\":1024,\"layer_nums\":24}");
            try { action(directory); }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void AppleQwenCpu_DefaultAvoidsLowPrecisionArithmeticRegression()
        {
            var defaults = MNNLlmDeviceDefaults.Select("Qwen3.5-0.8B-MNN", MNNBackendType.CPU,
                false, "iPhone18,3", "Apple GPU", 6);
            Assert.AreEqual(MNNPrecisionMode.Normal, defaults.PrecisionMode);
            Assert.AreEqual(4, defaults.ThreadCount);
            var explicitLow = new MNNLlmPerformanceOptions { PrecisionMode = MNNPrecisionMode.Low };
            Assert.AreEqual(MNNPrecisionMode.Low, explicitLow.Resolve(defaults).PrecisionMode);
        }

        [Test]
        public void DefaultsInheritDynamicAttentionAndChunkSettings()
        {
            var options = MNNModelData.Object(MNNModelData.Parse(new MNNLlmPerformanceOptions()
                .ToJsonFromConfiguration("{\"dynamic_option\":10,\"attention_mode\":14,\"chunk\":128}")));
            Assert.IsFalse(options.ContainsKey("dynamic_option"));
            Assert.IsFalse(options.ContainsKey("attention_mode"));
            Assert.IsFalse(options.ContainsKey("chunk"));
            Assert.AreEqual("high", options["power"]);
        }

        [TestCase(1, true, 9)]
        [TestCase(2, true, 10)]
        [TestCase(10, false, 2)]
        public void WeightReorderingRetainsDynamicQuantization(int inherited, bool enabled, int expected)
        {
            var options = new MNNLlmPerformanceOptions { EnableWeightReordering = enabled };
            var json = MNNModelData.Object(MNNModelData.Parse(options.ToJsonFromConfiguration("{\"dynamic_option\":" + inherited + "}")));
            Assert.AreEqual(expected, MNNModelData.Integer(json["dynamic_option"]));
        }

        [Test]
        public void IndependentOverridesRetainUnrelatedBitsAndRejectExportedShapeConflicts()
        {
            var options = new MNNLlmPerformanceOptions { DynamicQuantization = MNNLlmDynamicQuantization.PerTensor,
                FlashAttention = false, PrefillChunkSize = 128 };
            var json = MNNModelData.Object(MNNModelData.Parse(options.ToJsonFromConfiguration("{\"dynamic_option\":10,\"attention_mode\":14}")));
            Assert.AreEqual(9, MNNModelData.Integer(json["dynamic_option"]));
            Assert.AreEqual(6, MNNModelData.Integer(json["attention_mode"]));
            Assert.AreEqual(128, MNNModelData.Integer(json["chunk"]));
            Assert.Throws<ArgumentException>(() => options.ToJsonFromConfiguration("{\"chunk_limits\":[128,1]}"));
            options.PrefillChunkSize = -1;
            Assert.Throws<ArgumentOutOfRangeException>(() => options.ToJson());
            options.PrefillChunkSize = null;
            options.DynamicQuantization = (MNNLlmDynamicQuantization)3;
            Assert.Throws<ArgumentOutOfRangeException>(() => options.ToJson());
        }

        [Test]
        public void FlashAttentionUsesLegacySettingAndNativeDefault()
        {
            var options = new MNNLlmPerformanceOptions { FlashAttention = true };
            StringAssert.Contains("\"attention_mode\":10", options.ToJsonFromConfiguration("{\"quant_qkv\":2}"));
            options.FlashAttention = false;
            StringAssert.Contains("\"attention_mode\":0", options.ToJson());
        }
    }
}
