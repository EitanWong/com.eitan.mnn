using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class ModelRecommendationTests
    {
        private const long GiB = 1L << 30;
        private static MNNModelHardware Hardware(int ram = 24, double bandwidth = 153) => new MNNModelHardware {
            SystemMemoryBytes = ram * GiB, ProcessorCount = 10, MemoryKind = MNNModelMemoryKind.Unified,
            MemoryBandwidthBytesPerSecond = bandwidth * 1e9, HardwareId = "fixture", RuntimeId = "runtime", ExecutionId = "cpu/4" };
        private static MNNModelResourceProfile Model(string id = "fixture", long weights = GiB) => new MNNModelResourceProfile {
            Id = id, ExportIdentity = "fixture-export", WeightBytes = weights, RuntimeAvailable = true,
            Cache = new[] { new MNNModelCacheProfile { FixedBytesPerSequence = 1 } },
            SupportedMemoryKinds = MNNModelMemoryKind.Cpu | MNNModelMemoryKind.Unified | MNNModelMemoryKind.DiscreteGpu };
        private static MNNModelWorkload Work() => new MNNModelWorkload { MinimumThroughputPerSecond = 12 };
        private static MNNModelRecommendationPolicy Exact() => new MNNModelRecommendationPolicy { SafetyMarginFraction = 0 };

        [Test] public void WeightEstimateIncludesQuantizationScaleAndZeroOverhead()
        {
            var p = Model(); p.WeightBytes = 0; p.ParameterCount = 128; p.QuantizationGroupSize = 64;
            Assert.AreEqual(72, MNNModelRecommendation.Estimate(p, Work(), Exact()).WeightBytes);
            p.WeightBytes = 63;
            Assert.AreEqual(63, MNNModelRecommendation.Estimate(p, Work(), Exact()).WeightBytes, "Exact export bytes take precedence over nominal parameter arithmetic.");
        }

        [TestCase(8)] [TestCase(2)] [TestCase(1)]
        public void AttentionUsesKvHeadsNotQueryHeads(int heads)
        {
            var p = Model(); p.Cache = new[] { new MNNModelCacheProfile { Layers = 3, KeyValueHeads = heads, KeyHeadDimension = 64 } };
            var w = Work(); w.ContextTokens = 1024; w.PromptTokens = 512; w.ConcurrentSequences = 2;
            Assert.AreEqual(2L * 3 * heads * 64 * 2 * 1024 * 2, MNNModelRecommendation.Estimate(p, w, Exact()).CacheBytes);
        }

        [Test] public void SlidingCacheRoundsBlocksAndFixedStateDoesNotGrowWithContext()
        {
            var p = Model(); p.Cache = new[] { new MNNModelCacheProfile { Layers = 1, KeyValueHeads = 2, KeyHeadDimension = 8,
                ValueHeadDimension = 16, WindowTokens = 257, AllocationBlockTokens = 16, FixedBytesPerSequence = 4096 } };
            long expected = 2L * (8 + 16) * 2 * 272 + 4096;
            Assert.AreEqual(expected, MNNModelRecommendation.Estimate(p, Work(), Exact()).CacheBytes);
            var longer = Work(); longer.ContextTokens = 32768;
            Assert.AreEqual(expected, MNNModelRecommendation.Estimate(p, longer, Exact()).CacheBytes);
        }

        [Test] public void QwenNineBIncludesIndependentEmbeddingsAndOnlyEightKvLayers()
        {
            var p = MNNModelRecommendationProfiles.CreateDefault().Single(item => item.Id == MNNModelRecommendationProfiles.Qwen9Id);
            var estimate = MNNModelRecommendation.Estimate(p, Work(), Exact());
            Assert.Greater(estimate.WeightBytes, 7L * 1000 * 1000 * 1000);
            Assert.AreEqual(8, p.Cache.Single().Layers);
            Assert.AreEqual(268435456L + 53477376L, estimate.CacheBytes);
        }

        [Test] public void MoeKeepsAllExpertWeightsResidentButUsesActiveParametersForCompute()
        {
            var p = Model(); p.WeightBytes = 0; p.ParameterCount = 30000000000L; p.ActiveParameterCount = 3000000000L;
            Assert.Greater(MNNModelRecommendation.Estimate(p, Work()).WeightBytes, 15e9);
            var h = Hardware(64); h.MemoryBandwidthBytesPerSecond = 0; h.OperationsPerSecond = 120e9;
            var result = MNNModelRecommendation.Recommend(h, Work(), new[] { p });
            Assert.IsNotNull(result); Assert.AreEqual(20, result.ThroughputPerSecond, .001);
        }

        [Test] public void UnifiedMemoryIsNotAddedToReportedGraphicsMemory()
        {
            var h = Hardware(8); h.DeviceMemoryBytes = 24 * GiB;
            var result = MNNModelRecommendation.Rank(h, Work(), new[] { Model(weights: 8 * GiB) }).Single();
            Assert.AreEqual(MNNModelRecommendationStatus.Rejected, result.Status); Assert.Contains("SystemMemoryExceeded", result.Reasons.ToArray());
        }

        [Test] public void DedicatedGpuChecksVramAndHostStagingSeparately()
        {
            var h = Hardware(); h.MemoryKind = MNNModelMemoryKind.DiscreteGpu; h.DeviceMemoryBytes = 4 * GiB;
            var large = MNNModelRecommendation.Rank(h, Work(), new[] { Model(weights: 4 * GiB) }).Single();
            Assert.Contains("DeviceMemoryExceeded", large.Reasons.ToArray());
            h.DeviceMemoryBytes = 24 * GiB; h.AvailableSystemMemoryBytes = 2 * GiB;
            var staging = MNNModelRecommendation.Rank(h, Work(), new[] { Model(weights: 3 * GiB) }).Single();
            Assert.Contains("SystemMemoryExceeded", staging.Reasons.ToArray());
        }

        [Test] public void CurrentAvailableMemoryAndExternalResidentsConstrainTheBudget()
        {
            var h = Hardware(); h.AvailableSystemMemoryBytes = 3 * GiB; h.ReservedSystemBytes = GiB;
            Assert.IsNull(MNNModelRecommendation.Recommend(h, Work(), new[] { Model(weights: 2 * GiB) }));
        }

        [Test] public void JointPlanDownsizesLanguageInsteadOfOversubscribingSpeechMemory()
        {
            var h = Hardware(8); var large = Model("large", 3 * GiB); large.QualityScore = 1;
            var small = Model("small"); small.QualityScore = .5;
            var speech = Model("speech", 2 * GiB); speech.Task = MNNModelRecommendationTask.SpeechSynthesis;
            var plan = MNNModelRecommendation.RecommendPlan(h, new[] { Work(), new MNNModelWorkload { Task = speech.Task } }, new[] { large, small, speech });
            Assert.IsTrue(plan.IsComplete); Assert.AreEqual("small", plan.Selections[0].ModelId); Assert.Less(plan.PeakSystemBytes, 5 * GiB);
        }

        [Test] public void InfeasiblePlanReturnsNoPartialSelections()
        {
            var h = Hardware(4);
            var plan = MNNModelRecommendation.RecommendPlan(h, new[] { Work() }, new[] { Model(weights: 2 * GiB) });
            Assert.IsFalse(plan.IsComplete); Assert.IsEmpty(plan.Selections); Assert.IsNotEmpty(plan.Warnings);
        }

        [TestCase(MNNModelRecommendationTask.SpeechRecognition)] [TestCase(MNNModelRecommendationTask.SpeechSynthesis)]
        [TestCase(MNNModelRecommendationTask.Vision)] [TestCase(MNNModelRecommendationTask.Embedding)]
        [TestCase(MNNModelRecommendationTask.AudioProcessing)] [TestCase(MNNModelRecommendationTask.Other)]
        public void EveryTaskUsesItsOwnInputShapeAndExplicitComputeCost(MNNModelRecommendationTask task)
        {
            var p = Model(); p.Task = task; p.WorkspaceBytesPerInputUnit = 1024; p.OperationsPerUnit = 10e9;
            var w = new MNNModelWorkload { Task = task, InputUnits = 30, MinimumThroughputPerSecond = 2 };
            var h = Hardware(); h.OperationsPerSecond = 30e9;
            var result = MNNModelRecommendation.Recommend(h, w, new[] { p });
            Assert.AreEqual(30 * 1024, result.Memory.WorkspaceBytes); Assert.AreEqual(3, result.ThroughputPerSecond, .001);
        }

        [Test] public void LanguagesCapabilitiesRuntimeAndContextAreHardConstraints()
        {
            var p = Model(); p.RuntimeAvailable = false; p.MaximumContextTokens = 1024;
            var w = Work(); w.RequiredLanguages = new[] { "zh" }; w.RequiredCapabilities = new[] { "image" };
            var result = MNNModelRecommendation.Rank(Hardware(), w, new[] { p }).Single();
            CollectionAssert.IsSubsetOf(new[] { "RuntimeUnavailable", "LanguageUnsupported", "CapabilityUnsupported", "ContextUnsupported" }, result.Reasons);
            p.RuntimeAvailable = true; p.MaximumContextTokens = 32768; p.Languages = new[] { "zh" }; p.Capabilities = new[] { "image" };
            w.RequiredLanguages = new[] { "zh-CN" };
            Assert.IsNotNull(MNNModelRecommendation.Recommend(Hardware(), w, new[] { p }));
        }

        private static MNNModelBenchmark Benchmark() => new MNNModelBenchmark { HardwareId = "fixture", RuntimeId = "runtime", ExecutionId = "cpu/4", ExportIdentity = "fixture-export", Evidence = "fixture observation",
            MemoryKind = MNNModelMemoryKind.Unified, ContextTokens = 8192, PromptTokens = 512, PrefillChunkTokens = 256,
            ConcurrentSequences = 1, InputUnits = 1, ThroughputPerSecond = 5, FirstResultSeconds = 3 };

        [Test] public void ExactMeasurementOverridesAnOptimisticBandwidthEstimate()
        {
            var p = Model(); p.Benchmarks = new[] { Benchmark() };
            var result = MNNModelRecommendation.Rank(Hardware(), Work(), new[] { p }).Single();
            Assert.IsTrue(result.HasMeasuredPerformance); Assert.AreEqual(5, result.ThroughputPerSecond);
            Assert.Contains("ThroughputBelowTarget", result.Reasons.ToArray()); Assert.AreEqual(MNNModelRecommendationStatus.Rejected, result.Status);
        }

        [TestCase("runtime")] [TestCase("context")] [TestCase("device")] [TestCase("batch")] [TestCase("memory")] [TestCase("backend")] [TestCase("export")]
        public void IncompatibleBenchmarkCannotBeReused(string mismatch)
        {
            var p = Model(); var b = Benchmark(); p.Benchmarks = new[] { b };
            if (mismatch == "runtime") b.RuntimeId = "old-runtime";
            if (mismatch == "context") b.ContextTokens *= 2;
            if (mismatch == "device") b.HardwareId = "other-device";
            if (mismatch == "batch") b.ConcurrentSequences = 2;
            if (mismatch == "memory") b.MemoryKind = MNNModelMemoryKind.Cpu;
            if (mismatch == "backend") b.ExecutionId = "metal/4";
            if (mismatch == "export") b.ExportIdentity = "different-revision";
            var result = MNNModelRecommendation.Recommend(Hardware(), Work(), new[] { p });
            Assert.IsNotNull(result); Assert.IsFalse(result.HasMeasuredPerformance);
            Assert.Contains("PerformanceEstimated", result.Reasons.ToArray());
        }

        [Test] public void MeasuredFirstResultLatencyCanRejectAnOtherwiseFastModel()
        {
            var p = Model(); var b = Benchmark(); b.ThroughputPerSecond = 100; p.Benchmarks = new[] { b };
            var w = Work(); w.MaximumFirstResultSeconds = 1;
            Assert.Contains("FirstResultLatencyExceeded", MNNModelRecommendation.Rank(Hardware(), w, new[] { p }).Single().Reasons.ToArray());
        }

        [Test] public void StrictWorkloadRequiresMeasurementAndUnknownHardwareNeverInventsSpeed()
        {
            var w = Work(); w.AllowEstimatedPerformance = false;
            Assert.IsNull(MNNModelRecommendation.Recommend(Hardware(), w, new[] { Model() }));
            var h = Hardware(); h.MemoryBandwidthBytesPerSecond = 0;
            var ranked = MNNModelRecommendation.Rank(h, Work(), new[] { Model() }).Single();
            Assert.AreEqual(0, ranked.ThroughputPerSecond); Assert.Contains("PerformanceUnknown", ranked.Reasons.ToArray());
        }

        [Test] public void DeterministicTiesDoNotDependOnCatalogOrderOrMutateInputs()
        {
            var a = Model("a"); var b = Model("b"); var w = Work(); var h = Hardware();
            Assert.AreEqual("a", MNNModelRecommendation.Recommend(h, w, new[] { b, a }).ModelId);
            Assert.AreEqual("a", MNNModelRecommendation.Recommend(h, w, new[] { a, b }).ModelId);
            Assert.AreEqual(GiB, a.WeightBytes); Assert.AreEqual(24 * GiB, h.SystemMemoryBytes); Assert.AreEqual(8192, w.ContextTokens);
        }

        [Test] public void InvalidMetadataAndOverflowAreRejectedAtTheBoundary()
        {
            var p = Model(); p.WeightReadBytesPerUnit = double.NaN;
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNModelRecommendation.Recommend(Hardware(), Work(), new[] { p }));
            p = Model(); p.ParameterCount = 10; p.ActiveParameterCount = 11;
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNModelRecommendation.Estimate(p, Work()));
            p = Model(weights: long.MaxValue);
            Assert.Throws<OverflowException>(() => MNNModelRecommendation.Estimate(p, Work()));
        }

        [Test] public void MissingLanguageCacheMetadataCannotPretendToFit()
        {
            var p = Model(); p.Cache = Array.Empty<MNNModelCacheProfile>();
            var result = MNNModelRecommendation.Rank(Hardware(), Work(), new[] { p }).Single();
            Assert.AreEqual(MNNModelRecommendationStatus.Rejected, result.Status);
            Assert.Contains("CacheMetadataMissing", result.Reasons.ToArray());
        }

        [Test] public void CaptureReportsThisMachineAndAnOfflineRecommendation()
        {
            var h = MNNModelHardware.Capture();
            var result = MNNModelRecommendation.Recommend(h, Work(), MNNModelRecommendationProfiles.CreateDefault());
            TestContext.WriteLine("Hardware=" + JsonUtility.ToJson(h));
            TestContext.WriteLine("Recommended=" + result?.ModelId + "; estimatedTokensPerSecond=" + result?.ThroughputPerSecond);
            Assert.Greater(h.ProcessorCount, 0); Assert.Greater(h.SystemMemoryBytes, 0);
            if (h.DeviceName.Contains("Apple M5") && h.SystemMemoryBytes == 24 * GiB) Assert.AreEqual(MNNModelRecommendationProfiles.Qwen9Id, result?.ModelId);
        }

        [TestCase("Apple M5", 153)] [TestCase("Apple M5 Pro", 307)] [TestCase("Apple M5 Max", 460)]
        [TestCase("Apple M5 Ultra", 1200)] [TestCase("Apple M6", 153)] [TestCase("Unknown M8 GPU", 0)]
        public void PublishedHardwareProfilesUseSkuFloorsAndRejectUnknownNames(string name, double gbPerSecond)
        {
            var method = typeof(MNNModelHardware).GetMethod("AppleBandwidth", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.AreEqual(gbPerSecond * 1e9, (double)method.Invoke(null, new object[] { name }));
        }
    }
}
