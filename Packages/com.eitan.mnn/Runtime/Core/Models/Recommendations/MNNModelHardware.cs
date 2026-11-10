using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Supply available memory and sustained measurements when the host can obtain them. Zero means unknown.</summary>
    [Serializable] public sealed class MNNModelHardware
    {
        public string DeviceName, HardwareId, RuntimeId, ExecutionId;
        public MNNModelMemoryKind MemoryKind = MNNModelMemoryKind.Cpu;
        public int ProcessorCount = 1;
        public long SystemMemoryBytes, AvailableSystemMemoryBytes, DeviceMemoryBytes, AvailableDeviceMemoryBytes;
        public long ReservedSystemBytes, ReservedDeviceBytes;
        public double MemoryBandwidthBytesPerSecond, OperationsPerSecond, SustainedPerformanceFactor = 1;
        public string PerformanceEvidence;
        internal MNNModelHardware Copy() => (MNNModelHardware)MemberwiseClone();

        /// <summary>Main-thread Unity snapshot. Does not allocate models, request permissions, benchmark, or access the network.</summary>
        public static MNNModelHardware Capture()
        {
            string processor = SystemInfo.processorType ?? string.Empty;
            string graphics = SystemInfo.graphicsDeviceName ?? string.Empty;
            bool apple = processor.IndexOf("Apple", StringComparison.OrdinalIgnoreCase) >= 0 || graphics.StartsWith("Apple M", StringComparison.OrdinalIgnoreCase);
            var result = new MNNModelHardware
            {
                DeviceName = processor, HardwareId = processor + "|" + graphics,
                RuntimeId = "Unity " + Application.unityVersion + "|build=" + Application.buildGUID,
                ProcessorCount = Math.Max(1, SystemInfo.processorCount),
                SystemMemoryBytes = Math.Max(0L, SystemInfo.systemMemorySize) << 20,
                // Shared GPU memory must never be added to physical system memory.
                MemoryKind = apple ? MNNModelMemoryKind.Unified : MNNModelMemoryKind.Cpu
            };
            result.MemoryBandwidthBytesPerSecond = AppleBandwidth(processor + " " + graphics);
            result.PerformanceEvidence = result.MemoryBandwidthBytesPerSecond > 0
                ? "Apple published base-chip bandwidth; conservative floor for Pro/Max/Ultra, not measured MNN throughput."
                : "Memory bandwidth/compute unknown; host measurements can refine this snapshot.";
            return result;
        }

        private static double AppleBandwidth(string name)
        {
            var match = Regex.Match(name, @"\bApple M([1-6])\b(?:\s+(Pro|Max|Ultra)\b)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success) return 0;
            if (match.Groups[1].Value == "5")
            {
                // Current MacBook Pro/Studio specs. Max has 460/614 GB/s SKUs; use the lower bound.
                switch (match.Groups[2].Value.ToLowerInvariant())
                {
                    case "pro": return 307e9;
                    case "max": return 460e9;
                    case "ultra": return 1200e9;
                }
            }
            // Apple product specifications; policy applies a separate sustained efficiency factor.
            double[] gbPerSecond = { 68.25, 100, 100, 120, 153, 153 };
            return gbPerSecond[match.Groups[1].Value[0] - '1'] * 1e9;
        }
    }
}
