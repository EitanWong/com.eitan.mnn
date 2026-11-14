using System;
using System.Linq;

namespace MNN.Unity
{
    public static partial class MNNModelRecommendation
    {
        private static void Range(double value, double minimum, double maximum, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(name);
        }
        private static void Strings(string[] values, string name)
        {
            if (values == null || values.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Use nonempty metadata strings.", name);
        }
        private static void ValidateProfile(MNNModelResourceProfile p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (string.IsNullOrWhiteSpace(p.Id) || !Enum.IsDefined(typeof(MNNModelRecommendationTask), p.Task)) throw new ArgumentException("Invalid profile identity/task.");
            Range((int)p.SupportedMemoryKinds, 1, 7, nameof(p.SupportedMemoryKinds));
            foreach (long value in new[] { p.ParameterCount, p.ActiveParameterCount, p.WeightBytes, p.DownloadBytes, p.PeakWorkspaceBytes, p.HostStagingBytes }) Range(value, 0, long.MaxValue, "profile bytes/parameters");
            if (p.WeightBytes == 0 && p.ParameterCount == 0) throw new ArgumentException("Provide measured weight bytes or total parameters.");
            Range(p.ActiveParameterCount, 0, p.ParameterCount, nameof(p.ActiveParameterCount));
            Range(p.WeightBits, 1, 64, nameof(p.WeightBits)); Range(p.QuantizationGroupSize, 0, int.MaxValue, nameof(p.QuantizationGroupSize));
            Range(p.QuantizationBytesPerGroup, 0, 1024, nameof(p.QuantizationBytesPerGroup)); Range(p.MaximumContextTokens, 0, int.MaxValue, nameof(p.MaximumContextTokens));
            foreach (double value in new[] { p.WorkspaceBytesPerInputUnit, p.PrefillBytesPerToken, p.OperationsPerUnit, p.WeightReadBytesPerUnit }) Range(value, 0, double.MaxValue, "profile resources");
            Range(p.QualityScore, 0, 1, nameof(p.QualityScore)); Strings(p.Languages, nameof(p.Languages)); Strings(p.Capabilities, nameof(p.Capabilities));
            if (p.Cache == null || p.Benchmarks == null) throw new ArgumentException("Cache and benchmark collections cannot be null.");
            foreach (var group in p.Cache) ValidateCache(group);
            foreach (var benchmark in p.Benchmarks) ValidateBenchmark(benchmark);
        }
        private static void ValidateCache(MNNModelCacheProfile p)
        {
            if (p == null) throw new ArgumentException("Cache group cannot be null.");
            foreach (int value in new[] { p.Layers, p.KeyValueHeads, p.KeyHeadDimension, p.ValueHeadDimension, p.WindowTokens }) Range(value, 0, int.MaxValue, "cache dimensions");
            if (p.Layers > 0 && (p.KeyValueHeads == 0 || p.KeyHeadDimension == 0)) throw new ArgumentException("Attention requires KV heads and head dimensions.");
            Range(p.FixedBytesPerSequence, 0, long.MaxValue, nameof(p.FixedBytesPerSequence));
            Range(p.BytesPerElement, 1, 8, nameof(p.BytesPerElement)); Range(p.AllocationBlockTokens, 1, int.MaxValue, nameof(p.AllocationBlockTokens));
        }
        private static void ValidateWorkload(MNNModelWorkload w)
        {
            if (w == null) throw new ArgumentNullException(nameof(w));
            if (!Enum.IsDefined(typeof(MNNModelRecommendationTask), w.Task)) throw new ArgumentException("Unknown task.");
            Range(w.ContextTokens, 1, int.MaxValue, nameof(w.ContextTokens)); Range(w.PromptTokens, 0, w.ContextTokens, nameof(w.PromptTokens));
            Range(w.PrefillChunkTokens, 1, int.MaxValue, nameof(w.PrefillChunkTokens)); Range(w.ConcurrentSequences, 1, 65536, nameof(w.ConcurrentSequences));
            Range(w.InputUnits, 0, double.MaxValue, nameof(w.InputUnits));
            Range(w.MinimumThroughputPerSecond, 0, double.MaxValue, nameof(w.MinimumThroughputPerSecond));
            Range(w.MaximumFirstResultSeconds, 0, double.MaxValue, nameof(w.MaximumFirstResultSeconds));
            Strings(w.RequiredLanguages, nameof(w.RequiredLanguages)); Strings(w.RequiredCapabilities, nameof(w.RequiredCapabilities)); Strings(w.AllowedModelIds, nameof(w.AllowedModelIds));
        }
        private static void ValidateBenchmark(MNNModelBenchmark b)
        {
            if (b == null || string.IsNullOrWhiteSpace(b.HardwareId) || string.IsNullOrWhiteSpace(b.RuntimeId) || string.IsNullOrWhiteSpace(b.Evidence)) throw new ArgumentException("Benchmark provenance is required.");
            if (string.IsNullOrWhiteSpace(b.ExecutionId) || string.IsNullOrWhiteSpace(b.ExportIdentity)) throw new ArgumentException("Benchmark backend/options and export identity are required.");
            if (!Enum.IsDefined(typeof(MNNModelMemoryKind), b.MemoryKind)) throw new ArgumentException("Unknown measured memory kind.");
            Range(b.ThroughputPerSecond, double.Epsilon, double.MaxValue, nameof(b.ThroughputPerSecond));
            Range(b.FirstResultSeconds, 0, double.MaxValue, nameof(b.FirstResultSeconds));
            ValidateWorkload(new MNNModelWorkload { ContextTokens = b.ContextTokens, PromptTokens = b.PromptTokens, PrefillChunkTokens = b.PrefillChunkTokens, ConcurrentSequences = b.ConcurrentSequences, InputUnits = b.InputUnits });
        }
        private static void ValidateHardware(MNNModelHardware h)
        {
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (!Enum.IsDefined(typeof(MNNModelMemoryKind), h.MemoryKind)) throw new ArgumentException("Choose exactly one execution memory kind.");
            foreach (long value in new[] { h.SystemMemoryBytes, h.AvailableSystemMemoryBytes, h.DeviceMemoryBytes, h.AvailableDeviceMemoryBytes, h.ReservedSystemBytes, h.ReservedDeviceBytes }) Range(value, 0, long.MaxValue, "hardware bytes");
            Range(h.ProcessorCount, 1, int.MaxValue, nameof(h.ProcessorCount));
            Range(h.MemoryBandwidthBytesPerSecond, 0, double.MaxValue, nameof(h.MemoryBandwidthBytesPerSecond));
            Range(h.OperationsPerSecond, 0, double.MaxValue, nameof(h.OperationsPerSecond)); Range(h.SustainedPerformanceFactor, double.Epsilon, 1, nameof(h.SustainedPerformanceFactor));
        }
        private static void ValidatePolicy(MNNModelRecommendationPolicy p)
        {
            foreach (long value in new[] { p.MinimumSystemReserveBytes, p.ApplicationReserveBytes, p.MinimumDeviceReserveBytes }) Range(value, 0, long.MaxValue, "policy reserve bytes");
            foreach (double value in new[] { p.SystemReserveFraction, p.DeviceReserveFraction }) Range(value, 0, .99, "policy reserve fraction");
            Range(p.SafetyMarginFraction, 0, 10, nameof(p.SafetyMarginFraction)); Range(p.BandwidthEfficiency, double.Epsilon, 1, nameof(p.BandwidthEfficiency));
            foreach (double value in new[] { p.QualityWeight, p.ResponsivenessWeight, p.HeadroomWeight }) Range(value, 0, 1, "policy ranking weights");
            if (p.QualityWeight + p.ResponsivenessWeight + p.HeadroomWeight == 0) throw new ArgumentException("At least one ranking weight is required.");
            Range(p.MaximumPlanCandidates, 1, 4096, nameof(p.MaximumPlanCandidates)); Range(p.MaximumCandidatesPerTask, 1, 1024, nameof(p.MaximumCandidatesPerTask));
        }
    }
}
