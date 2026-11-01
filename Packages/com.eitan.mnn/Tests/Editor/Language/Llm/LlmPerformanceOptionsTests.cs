using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class LlmPerformanceOptionsTests
    {
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
