using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    public enum MNNModelRecommendationTask { Language, SpeechRecognition, SpeechSynthesis, Vision, Embedding, AudioProcessing, Other }
    [Flags] public enum MNNModelMemoryKind { Cpu = 1, Unified = 2, DiscreteGpu = 4 }
    public enum MNNModelRecommendationStatus { Suitable, Provisional, Rejected }

    /// <summary>One homogeneous attention/cache group. Fixed state is independent of context length.</summary>
    [Serializable] public sealed class MNNModelCacheProfile
    {
        public int Layers, KeyValueHeads, KeyHeadDimension, ValueHeadDimension;
        public int BytesPerElement = 2, WindowTokens, AllocationBlockTokens = 1;
        public long FixedBytesPerSequence;
        internal MNNModelCacheProfile Copy() => (MNNModelCacheProfile)MemberwiseClone();
    }

    /// <summary>A measurement applies only to this exact model export, runtime, device and workload.</summary>
    [Serializable] public sealed class MNNModelBenchmark
    {
        public string HardwareId, RuntimeId, ExecutionId, ExportIdentity, Evidence;
        public MNNModelMemoryKind MemoryKind;
        public int ContextTokens, PromptTokens, PrefillChunkTokens, ConcurrentSequences;
        public double InputUnits, ThroughputPerSecond, FirstResultSeconds;
        internal MNNModelBenchmark Copy() => (MNNModelBenchmark)MemberwiseClone();
    }

    /// <summary>Resource metadata for one runnable export, including its quantization and auxiliary graphs.</summary>
    [Serializable] public sealed class MNNModelResourceProfile
    {
        public string Id, ExportIdentity, Evidence;
        public MNNModelRecommendationTask Task;
        public bool RuntimeAvailable;
        public MNNModelMemoryKind SupportedMemoryKinds = MNNModelMemoryKind.Cpu;
        public long ParameterCount, ActiveParameterCount, WeightBytes, DownloadBytes, PeakWorkspaceBytes, HostStagingBytes;
        public int WeightBits = 4, QuantizationGroupSize = 64, QuantizationBytesPerGroup = 4, MaximumContextTokens;
        public double WorkspaceBytesPerInputUnit, PrefillBytesPerToken, OperationsPerUnit, WeightReadBytesPerUnit, QualityScore;
        public string[] Languages = Array.Empty<string>(), Capabilities = Array.Empty<string>();
        public MNNModelCacheProfile[] Cache = Array.Empty<MNNModelCacheProfile>();
        public MNNModelBenchmark[] Benchmarks = Array.Empty<MNNModelBenchmark>();
        internal MNNModelResourceProfile Copy()
        {
            var copy = (MNNModelResourceProfile)MemberwiseClone();
            copy.Languages = (string[])Languages.Clone(); copy.Capabilities = (string[])Capabilities.Clone();
            copy.Cache = Array.ConvertAll(Cache, item => item.Copy());
            copy.Benchmarks = Array.ConvertAll(Benchmarks, item => item.Copy());
            return copy;
        }
    }

    [Serializable] public sealed class MNNModelWorkload
    {
        public MNNModelRecommendationTask Task;
        /// <summary>Total retained prompt + generated tokens per sequence; never the model's advertised maximum.</summary>
        public int ContextTokens = 8192, PromptTokens = 512, PrefillChunkTokens = 256, ConcurrentSequences = 1;
        /// <summary>Seconds of audio, image count, etc.; units must match the profile.</summary>
        public double InputUnits = 1, MinimumThroughputPerSecond, MaximumFirstResultSeconds;
        public bool AllowEstimatedPerformance = true;
        public string[] RequiredLanguages = Array.Empty<string>(), RequiredCapabilities = Array.Empty<string>(), AllowedModelIds = Array.Empty<string>();
        internal MNNModelWorkload Copy()
        {
            var copy = (MNNModelWorkload)MemberwiseClone();
            copy.RequiredLanguages = (string[])RequiredLanguages.Clone(); copy.RequiredCapabilities = (string[])RequiredCapabilities.Clone();
            copy.AllowedModelIds = (string[])AllowedModelIds.Clone(); return copy;
        }
    }

    /// <summary>Policy values are conservative tuning knobs, not measured MNN performance claims.</summary>
    [Serializable] public sealed class MNNModelRecommendationPolicy
    {
        public long MinimumSystemReserveBytes = 2L << 30, ApplicationReserveBytes = 1L << 30, MinimumDeviceReserveBytes = 512L << 20;
        public double SystemReserveFraction = .20, DeviceReserveFraction = .20, SafetyMarginFraction = .15, BandwidthEfficiency = .55;
        public double QualityWeight = .70, ResponsivenessWeight = .20, HeadroomWeight = .10;
        public int MaximumPlanCandidates = 64, MaximumCandidatesPerTask = 16;
        internal MNNModelRecommendationPolicy Copy() => (MNNModelRecommendationPolicy)MemberwiseClone();
    }

    public sealed class MNNModelMemoryEstimate
    {
        public long WeightBytes { get; }
        public long CacheBytes { get; }
        public long WorkspaceBytes { get; }
        public long PeakBytes { get; }
        internal MNNModelMemoryEstimate(long weights, long cache, long workspace, long peak)
        { WeightBytes = weights; CacheBytes = cache; WorkspaceBytes = workspace; PeakBytes = peak; }
    }

    public sealed class MNNModelRecommendationResult
    {
        public string ModelId { get; }
        public string Evidence { get; }
        public MNNModelRecommendationTask Task { get; }
        public MNNModelRecommendationStatus Status { get; }
        public MNNModelMemoryEstimate Memory { get; }
        public long PeakSystemBytes { get; }
        public long PeakDeviceBytes { get; }
        public double ThroughputPerSecond { get; }
        public double FirstResultSeconds { get; }
        public bool HasMeasuredPerformance { get; }
        public double Score { get; }
        public IReadOnlyList<string> Reasons { get; }
        internal MNNModelRecommendationResult(MNNModelResourceProfile profile, MNNModelRecommendationStatus status,
            MNNModelMemoryEstimate memory, long system, long device, double throughput, double firstResult, bool measured, double score, List<string> reasons)
        {
            ModelId = profile.Id; Task = profile.Task; Evidence = profile.Evidence; Status = status; Memory = memory;
            PeakSystemBytes = system; PeakDeviceBytes = device; ThroughputPerSecond = throughput;
            FirstResultSeconds = firstResult; HasMeasuredPerformance = measured; Score = score; Reasons = reasons.AsReadOnly();
        }
    }

    public sealed class MNNModelRecommendationPlan
    {
        public bool IsComplete { get; }
        public IReadOnlyList<MNNModelRecommendationResult> Selections { get; }
        public IReadOnlyList<string> Warnings { get; }
        public long PeakSystemBytes { get; }
        public long PeakDeviceBytes { get; }
        internal MNNModelRecommendationPlan(bool complete, MNNModelRecommendationResult[] selections, long system, long device, string[] warnings)
        { IsComplete = complete; Selections = Array.AsReadOnly(selections); PeakSystemBytes = system; PeakDeviceBytes = device; Warnings = Array.AsReadOnly(warnings); }
    }
}
