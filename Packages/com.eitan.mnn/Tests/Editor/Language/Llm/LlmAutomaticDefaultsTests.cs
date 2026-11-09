using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class LlmAutomaticDefaultsTests
    {
        [TestCase("Qwen3.5-0.8B-MNN")]
        [TestCase("Qwen2.5-0.5B-Instruct-MNN")]
        [TestCase("SmolLM2-135M-Instruct-MNN")]
        public void QualifiedPelmModelsUseMeasuredCpuProfile(string model)
        {
            foreach (var backend in new[] { MNNBackendType.Auto, MNNBackendType.CPU })
            {
                var result = Resolve(new MNNLlmPerformanceOptions { BackendType = backend }, model);
                Assert.AreEqual(MNNBackendType.CPU, result.BackendType);
                Assert.AreEqual(2, result.ThreadCount);
                Assert.AreEqual(MNNPrecisionMode.Low, result.PrecisionMode);
                Assert.AreEqual(MNNPowerMode.High, result.PowerMode);
            }
        }

        [Test]
        public void PjaRetainsItsOwnMeasuredProfile()
        {
            var result = new MNNLlmPerformanceOptions().Resolve(MNNLlmDeviceDefaults.Select(
                "Qwen3.5-0.8B-MNN", MNNBackendType.Auto, true, "OnePlus PJA110", "Adreno (TM) 740|driver", 8));
            Assert.AreEqual(MNNBackendType.CPU, result.BackendType);
            Assert.AreEqual(4, result.ThreadCount);
            Assert.AreEqual(MNNPrecisionMode.Normal, result.PrecisionMode);
            Assert.AreEqual(MNNPowerMode.Normal, result.PowerMode);
        }

        [TestCase("different-model", "OPPO PELM00", "Mali-G57 MC3", true)]
        [TestCase("Qwen3.5-0.8B-MNN", "OPPO PELM000", "Mali-G57 MC3", true)]
        [TestCase("Qwen3.5-0.8B-MNN", "different-phone", "Mali-G57 MC3", true)]
        [TestCase("Qwen3.5-0.8B-MNN", "OPPO PELM00", "Mali-G57 MC2", true)]
        [TestCase("Qwen3.5-0.8B-MNN", "OPPO PELM00", "Mali-G57 MC3", false)]
        [TestCase("Qwen3.5-0.8B-MNN", "", "", true)]
        public void UnqualifiedPairsDoNotInheritPelmCpuTuning(string model, string device, string gpu, bool android)
        {
            var result = MNNLlmDeviceDefaults.Select(model, MNNBackendType.Auto, android, device, gpu, 8);
            Assert.AreEqual(4, result.ThreadCount);
            Assert.AreEqual(MNNBackendType.Auto, result.BackendType);
        }

        [Test]
        public void EachExplicitPropertyOverridesOnlyItselfEvenWhenEqualToOldDefault()
        {
            var threads = Resolve(new MNNLlmPerformanceOptions { ThreadCount = 4 });
            Assert.AreEqual(4, threads.ThreadCount);
            Assert.AreEqual(MNNBackendType.CPU, threads.BackendType);
            Assert.AreEqual(MNNPrecisionMode.Low, threads.PrecisionMode);
            var precision = Resolve(new MNNLlmPerformanceOptions { PrecisionMode = MNNPrecisionMode.Normal });
            Assert.AreEqual(2, precision.ThreadCount);
            Assert.AreEqual(MNNPrecisionMode.Normal, precision.PrecisionMode);
            var power = Resolve(new MNNLlmPerformanceOptions { PowerMode = MNNPowerMode.Low });
            Assert.AreEqual(2, power.ThreadCount);
            Assert.AreEqual(MNNPowerMode.Low, power.PowerMode);
        }

        [TestCase(MNNBackendType.OpenCL)]
        [TestCase(MNNBackendType.Vulkan)]
        public void ExplicitAcceleratorDoesNotUseCpuOnlyProfile(MNNBackendType backend)
        {
            var result = Resolve(new MNNLlmPerformanceOptions { BackendType = backend });
            Assert.AreEqual(backend, result.BackendType);
            Assert.AreEqual(4, result.ThreadCount);
        }

        [Test]
        public void ResolutionDoesNotMutateInputsAndRetainsAdvancedOverrides()
        {
            var input = new MNNLlmPerformanceOptions { EnableWeightReordering = false,
                FlashAttention = false, PrefillChunkSize = 64, DynamicQuantization = MNNLlmDynamicQuantization.PerTensor };
            var first = Resolve(input);
            Assert.AreNotSame(input, first);
            Assert.AreEqual(4, input.ThreadCount);
            Assert.AreEqual(MNNBackendType.Auto, input.BackendType);
            var second = input.Resolve(MNNLlmDeviceDefaults.Select("unknown", MNNBackendType.Auto, false, "", "", 8));
            Assert.AreEqual(4, second.ThreadCount);
            Assert.AreEqual(MNNLlmDynamicQuantization.PerTensor, first.DynamicQuantization);
            Assert.AreEqual(64, first.PrefillChunkSize);
            Assert.AreEqual(false, first.FlashAttention);
            StringAssert.Contains("\"dynamic_option\":1", first.ToJsonFromConfiguration("{\"dynamic_option\":8}"));
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(8, 4)]
        public void GenericThreadBudgetFitsAvailableProcessors(int processors, int expected)
        {
            Assert.AreEqual(expected, MNNLlmDeviceDefaults.Select("unknown", MNNBackendType.Auto, false, "", "", processors).ThreadCount);
        }

        [Test]
        public void CacheIdentityUsesResolvedSettingsAndStillSeparatesExplicitOverrides()
        {
            string root = Path.GetFullPath("TestArtifacts~/LlmAutomaticDefaults/" + Guid.NewGuid().ToString("N"));
            string model = Path.Combine(root, "model"), cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(model);
            File.WriteAllText(Path.Combine(model, "config.json"), "{}");
            try
            {
                var automatic = new MNNLlmPerformanceOptions();
                var resolved = automatic.Resolve(model);
                string a, b, c;
                using (var lease = MNNLlmCache.Acquire(cache, model, "native", "device", automatic, "model")) a = lease.DirectoryPath;
                using (var lease = MNNLlmCache.Acquire(cache, model, "native", "device", resolved, "model")) b = lease.DirectoryPath;
                resolved.ThreadCount += 1;
                using (var lease = MNNLlmCache.Acquire(cache, model, "native", "device", resolved, "model")) c = lease.DirectoryPath;
                Assert.AreEqual(a, b);
                Assert.AreNotEqual(a, c);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void CacheIdentityIncludesCustomExternalMetadataWithVerifiedModelIdentity()
        {
            string directory = Path.GetFullPath("TestArtifacts~/LlmCacheMetadata/" + Guid.NewGuid().ToString("N"));
            string model = Path.Combine(directory, "model"), metadata = Path.Combine(directory, "metadata");
            Directory.CreateDirectory(model);
            Directory.CreateDirectory(metadata);
            File.WriteAllText(Path.Combine(model, "config.json"), "{\"base_dir\":\"" + metadata.Replace("\\", "/") + "/\",\"llm_config\":\"info.json\"}");
            string info = Path.Combine(metadata, "info.json"), cache = Path.Combine(directory, "cache");
            File.WriteAllText(info, "{\"temperature\":0.7}");
            try
            {
                var performance = new MNNLlmPerformanceOptions();
                string first, changed, unchanged;
                using (var lease = MNNLlmCache.Acquire(cache, model, "native", "device", performance, "verified-model")) first = lease.DirectoryPath;
                File.WriteAllText(info, "{\"temperature\":0.8}");
                using (var lease = MNNLlmCache.Acquire(cache, model, "native", "device", performance, "verified-model")) changed = lease.DirectoryPath;
                using (var lease = MNNLlmCache.Acquire(cache, model, "native", "device", performance, "verified-model")) unchanged = lease.DirectoryPath;
                Assert.AreNotEqual(first, changed, "Effective configuration is always part of a persistent cache key.");
                Assert.AreEqual(changed, unchanged);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void OriginalCompleteClrLoadSignaturesRemainAvailable()
        {
            var original = new[] { typeof(string), typeof(int), typeof(bool), typeof(string), typeof(MNNPrecisionMode), typeof(MNNBackendType) };
            Assert.NotNull(typeof(MNNLlm).GetMethod("Load", original));
            Assert.NotNull(typeof(MNNLlm).GetMethod("LoadForConversation", original));
            Assert.NotNull(typeof(MNNLlm).GetMethod("LoadForConversation", new[] {
                typeof(string), typeof(float), typeof(int), typeof(bool), typeof(string), typeof(MNNPrecisionMode), typeof(MNNBackendType) }));
        }

        private static MNNLlmPerformanceOptions Resolve(MNNLlmPerformanceOptions options, string model = "Qwen3.5-0.8B-MNN")
            => options.Resolve(MNNLlmDeviceDefaults.Select(model, options.BackendType, true, "OPPO PELM00", "Mali-G57 MC3|driver", 8));
    }
}
