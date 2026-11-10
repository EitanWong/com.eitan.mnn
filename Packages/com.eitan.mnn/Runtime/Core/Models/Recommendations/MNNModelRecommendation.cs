using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>Deterministic, offline resource/experience recommendations. Does not load or download models.</summary>
    public static partial class MNNModelRecommendation
    {
        public static IReadOnlyList<MNNModelRecommendationResult> Rank(MNNModelHardware hardware, MNNModelWorkload workload,
            IEnumerable<MNNModelResourceProfile> profiles, MNNModelRecommendationPolicy policy = null)
        {
            ValidateHardware(hardware); ValidateWorkload(workload);
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            policy = policy ?? new MNNModelRecommendationPolicy(); ValidatePolicy(policy);
            var snapshot = profiles.ToArray();
            foreach (var profile in snapshot) ValidateProfile(profile);
            if (snapshot.GroupBy(p => p.Task + ":" + p.Id, StringComparer.Ordinal).Any(group => group.Count() > 1)) throw new ArgumentException("Duplicate model profile identity.");
            var h = hardware.Copy(); var w = workload.Copy(); var p = policy.Copy();
            return Array.AsReadOnly(snapshot.Where(item => item.Task == w.Task).Select(item => Evaluate(h, w, item.Copy(), p))
                .OrderBy(item => item.Status == MNNModelRecommendationStatus.Rejected).ThenByDescending(item => item.Score)
                .ThenBy(item => item.Memory.PeakBytes).ThenBy(item => item.ModelId, StringComparer.Ordinal).ToArray());
        }

        public static MNNModelRecommendationResult Recommend(MNNModelHardware hardware, MNNModelWorkload workload,
            IEnumerable<MNNModelResourceProfile> profiles, MNNModelRecommendationPolicy policy = null) =>
            Rank(hardware, workload, profiles, policy).FirstOrDefault(item => item.Status != MNNModelRecommendationStatus.Rejected);

        private static MNNModelRecommendationResult Evaluate(MNNModelHardware h, MNNModelWorkload w, MNNModelResourceProfile p, MNNModelRecommendationPolicy policy)
        {
            var reasons = new List<string>();
            var memory = Estimate(p, w, policy);
            bool gpu = h.MemoryKind == MNNModelMemoryKind.DiscreteGpu;
            long system = gpu ? Bytes(Math.Max(p.HostStagingBytes, memory.WeightBytes) * (1 + policy.SafetyMarginFraction)) : memory.PeakBytes;
            long device = gpu ? memory.PeakBytes : 0;
            bool rejected = RejectCompatibility(h, w, p, reasons);
            if (system > SystemBudget(h, policy)) { reasons.Add("SystemMemoryExceeded"); rejected = true; }
            if (device > DeviceBudget(h, policy)) { reasons.Add("DeviceMemoryExceeded"); rejected = true; }
            var measured = MatchBenchmark(h, w, p);
            double throughput = measured?.ThroughputPerSecond ?? EstimateThroughput(h, p, memory, w, policy);
            double firstResult = measured?.FirstResultSeconds ?? 0;
            rejected |= RejectPerformance(w, throughput, firstResult, measured != null, reasons);
            double score = Score(p, w, system, device, h, policy, throughput);
            return new MNNModelRecommendationResult(p, rejected ? MNNModelRecommendationStatus.Rejected :
                measured == null ? MNNModelRecommendationStatus.Provisional : MNNModelRecommendationStatus.Suitable,
                memory, system, device, throughput, firstResult, measured != null, score, reasons);
        }

        private static bool RejectCompatibility(MNNModelHardware h, MNNModelWorkload w, MNNModelResourceProfile p, List<string> reasons)
        {
            if (!p.RuntimeAvailable) reasons.Add("RuntimeUnavailable");
            if (p.Task == MNNModelRecommendationTask.Language && p.Cache.Length == 0) reasons.Add("CacheMetadataMissing");
            if ((p.SupportedMemoryKinds & h.MemoryKind) == 0) reasons.Add("MemoryKindUnsupported");
            if (w.AllowedModelIds.Length > 0 && !w.AllowedModelIds.Contains(p.Id, StringComparer.Ordinal)) reasons.Add("ModelNotAllowed");
            if (p.MaximumContextTokens > 0 && w.ContextTokens > p.MaximumContextTokens) reasons.Add("ContextUnsupported");
            if (w.RequiredCapabilities.Except(p.Capabilities, StringComparer.OrdinalIgnoreCase).Any()) reasons.Add("CapabilityUnsupported");
            if (w.RequiredLanguages.Any(required => !p.Languages.Any(language => LanguageMatches(language, required)))) reasons.Add("LanguageUnsupported");
            return reasons.Count > 0;
        }

        private static bool LanguageMatches(string supported, string required) => supported.Equals(required, StringComparison.OrdinalIgnoreCase) ||
            required.StartsWith(supported + "-", StringComparison.OrdinalIgnoreCase);

        private static MNNModelBenchmark MatchBenchmark(MNNModelHardware h, MNNModelWorkload w, MNNModelResourceProfile p) => p.Benchmarks.FirstOrDefault(b =>
            b.HardwareId == h.HardwareId && b.RuntimeId == h.RuntimeId && b.ExecutionId == h.ExecutionId && b.ExportIdentity == p.ExportIdentity && b.MemoryKind == h.MemoryKind &&
            b.ContextTokens == w.ContextTokens && b.PromptTokens == w.PromptTokens && b.PrefillChunkTokens == w.PrefillChunkTokens &&
            b.ConcurrentSequences == w.ConcurrentSequences && b.InputUnits == w.InputUnits);

        private static double EstimateThroughput(MNNModelHardware h, MNNModelResourceProfile p, MNNModelMemoryEstimate memory, MNNModelWorkload w, MNNModelRecommendationPolicy policy)
        {
            double reads = p.WeightReadBytesPerUnit;
            if (reads == 0 && p.Task == MNNModelRecommendationTask.Language)
                reads = memory.WeightBytes * (p.ActiveParameterCount > 0 ? (double)p.ActiveParameterCount / p.ParameterCount : 1);
            // Autoregressive batching shares the weights, while KV reads grow with the batch.
            reads = (p.Task == MNNModelRecommendationTask.Language ? reads : reads * w.ConcurrentSequences) + memory.CacheBytes;
            double bandwidthLimit = reads > 0 && h.MemoryBandwidthBytesPerSecond > 0
                ? h.MemoryBandwidthBytesPerSecond * policy.BandwidthEfficiency / reads : double.PositiveInfinity;
            double operations = p.OperationsPerUnit;
            if (operations == 0 && p.Task == MNNModelRecommendationTask.Language) operations = 2.0 * (p.ActiveParameterCount > 0 ? p.ActiveParameterCount : p.ParameterCount);
            double computeLimit = operations > 0 && h.OperationsPerSecond > 0 ? h.OperationsPerSecond / operations / w.ConcurrentSequences : double.PositiveInfinity;
            double limit = Math.Min(bandwidthLimit, computeLimit);
            return double.IsInfinity(limit) ? 0 : limit * h.SustainedPerformanceFactor;
        }

        private static bool RejectPerformance(MNNModelWorkload w, double throughput, double firstResult, bool measured, List<string> reasons)
        {
            reasons.Add(measured ? "PerformanceMeasured" : throughput > 0 ? "PerformanceEstimated" : "PerformanceUnknown");
            if (!measured && !w.AllowEstimatedPerformance) { reasons.Add("MeasurementRequired"); return true; }
            bool rejected = false;
            if (throughput == 0 && w.MinimumThroughputPerSecond > 0) { reasons.Add("TargetThroughputUnknown"); rejected = true; }
            if (throughput > 0 && throughput < w.MinimumThroughputPerSecond) { reasons.Add("ThroughputBelowTarget"); rejected = true; }
            if (w.MaximumFirstResultSeconds > 0)
            {
                if (firstResult == 0) { reasons.Add("FirstResultLatencyUnknown"); rejected |= !w.AllowEstimatedPerformance; }
                else if (firstResult > w.MaximumFirstResultSeconds) { reasons.Add("FirstResultLatencyExceeded"); rejected = true; }
            }
            return rejected;
        }

        private static double Score(MNNModelResourceProfile p, MNNModelWorkload w, long system, long device, MNNModelHardware h, MNNModelRecommendationPolicy policy, double throughput)
        {
            // Capacity is a fallback prior within one task, not a cross-model benchmark score.
            double quality = p.QualityScore > 0 ? p.QualityScore : Math.Min(1, Math.Log(1 + p.ParameterCount / 1e8) / Math.Log(1001));
            double responsiveness = throughput == 0 ? .35 : w.MinimumThroughputPerSecond > 0 ? Math.Min(1, throughput / w.MinimumThroughputPerSecond) : 1;
            double pressure = Math.Max((double)system / Math.Max(1, SystemBudget(h, policy)), (double)device / Math.Max(1, DeviceBudget(h, policy)));
            return policy.QualityWeight * quality + policy.ResponsivenessWeight * responsiveness + policy.HeadroomWeight * Math.Max(0, 1 - pressure);
        }
    }
}
