using System;

namespace MNN.Unity
{
    public static partial class MNNModelRecommendation
    {
        /// <summary>Resident weights + grouped KV/fixed state + peak workspace + safety margin. No native calls.</summary>
        public static MNNModelMemoryEstimate Estimate(MNNModelResourceProfile profile, MNNModelWorkload workload, MNNModelRecommendationPolicy policy = null)
        {
            ValidateProfile(profile); ValidateWorkload(workload);
            policy = policy ?? new MNNModelRecommendationPolicy(); ValidatePolicy(policy);
            if (profile.Task != workload.Task) throw new ArgumentException("Profile and workload tasks differ.");
            double weights = profile.WeightBytes;
            if (weights == 0)
            {
                weights = profile.ParameterCount * (profile.WeightBits / 8.0);
                if (profile.QuantizationGroupSize > 0 && profile.WeightBits < 16)
                    weights += Math.Ceiling((double)profile.ParameterCount / profile.QuantizationGroupSize) * profile.QuantizationBytesPerGroup;
            }
            double cache = 0;
            foreach (var group in profile.Cache)
            {
                long tokens = group.WindowTokens > 0 ? Math.Min(workload.ContextTokens, group.WindowTokens) : workload.ContextTokens;
                tokens = checked(((tokens + group.AllocationBlockTokens - 1) / group.AllocationBlockTokens) * group.AllocationBlockTokens);
                int valueDimension = group.ValueHeadDimension == 0 ? group.KeyHeadDimension : group.ValueHeadDimension;
                cache += (double)group.Layers * group.KeyValueHeads * ((double)group.KeyHeadDimension + valueDimension) * group.BytesPerElement * tokens;
                cache += group.FixedBytesPerSequence;
            }
            cache *= workload.ConcurrentSequences;
            double workspace = (profile.PeakWorkspaceBytes + profile.WorkspaceBytesPerInputUnit * workload.InputUnits +
                profile.PrefillBytesPerToken * Math.Min(workload.PromptTokens, workload.PrefillChunkTokens)) * workload.ConcurrentSequences;
            return new MNNModelMemoryEstimate(Bytes(weights), Bytes(cache), Bytes(workspace),
                Bytes((weights + cache + workspace) * (1 + policy.SafetyMarginFraction)));
        }

        private static long Bytes(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value >= long.MaxValue || value < 0)
                throw new OverflowException("Resource estimate exceeds the supported byte range.");
            return (long)Math.Ceiling(value);
        }

        private static long SystemBudget(MNNModelHardware hardware, MNNModelRecommendationPolicy policy)
        {
            double budget = hardware.SystemMemoryBytes - Math.Max(policy.MinimumSystemReserveBytes, hardware.SystemMemoryBytes * policy.SystemReserveFraction) - policy.ApplicationReserveBytes;
            if (hardware.AvailableSystemMemoryBytes > 0) budget = Math.Min(budget, hardware.AvailableSystemMemoryBytes - policy.ApplicationReserveBytes);
            return Bytes(Math.Max(0, budget - hardware.ReservedSystemBytes));
        }

        private static long DeviceBudget(MNNModelHardware hardware, MNNModelRecommendationPolicy policy)
        {
            if (hardware.MemoryKind != MNNModelMemoryKind.DiscreteGpu) return 0;
            double budget = hardware.DeviceMemoryBytes - Math.Max(policy.MinimumDeviceReserveBytes, hardware.DeviceMemoryBytes * policy.DeviceReserveFraction);
            if (hardware.AvailableDeviceMemoryBytes > 0) budget = Math.Min(budget, hardware.AvailableDeviceMemoryBytes - policy.MinimumDeviceReserveBytes);
            return Bytes(Math.Max(0, budget - hardware.ReservedDeviceBytes));
        }
    }
}
