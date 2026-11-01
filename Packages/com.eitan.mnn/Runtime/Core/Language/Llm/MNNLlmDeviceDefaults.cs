using System;
using System.IO;

namespace MNN.Unity
{
    // Profiles qualify a model/device/backend pair, not every graph on that GPU.
    // Keep selection deterministic: MNN's process-wide CPU pools make an in-process
    // sweep of thread counts an unreliable benchmark and add work to first load.
    internal static class MNNLlmDeviceDefaults
    {
        internal static MNNLlmPerformanceOptions Select(string directory, MNNBackendType requested)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const bool android = true;
#else
            const bool android = false;
#endif
            string model = Path.GetFileName(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return Select(model, requested, android, MNNInferenceCache.DeviceModel,
                MNNInferenceCache.GraphicsDevice, MNNInferenceCache.ProcessorCount);
        }

        internal static MNNLlmPerformanceOptions Select(string model, MNNBackendType requested,
            bool android, string device, string graphics, int processors)
        {
            int threads = Math.Min(4, Math.Max(1, processors));
            var defaults = new MNNLlmPerformanceOptions
            {
                ThreadCount = threads,
                BackendType = requested,
                PrecisionMode = MNNPrecisionMode.Low,
                PowerMode = MNNPowerMode.High
            };
            bool qwen08 = string.Equals(model, "Qwen3.5-0.8B-MNN", StringComparison.OrdinalIgnoreCase);
            // MNN 3.6.2 Apple CPU Low/4 threads answers the arithmetic gate
            // incorrectly. Normal passes at both 1 and 4 threads; Metal Low passes.
            if (!android && qwen08 && requested == MNNBackendType.CPU)
                defaults.PrecisionMode = MNNPrecisionMode.Normal;
            if (!android || (requested != MNNBackendType.Auto && requested != MNNBackendType.CPU))
                return defaults;

            bool compact = string.Equals(model, "Qwen2.5-0.5B-Instruct-MNN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(model, "SmolLM2-135M-Instruct-MNN", StringComparison.OrdinalIgnoreCase);
            string gpu = (graphics ?? string.Empty).Split('|')[0].Trim();
            if (IsDevice(device, "PELM00") && gpu.Equals("Mali-G57 MC3", StringComparison.OrdinalIgnoreCase) && (qwen08 || compact))
            {
                defaults.BackendType = MNNBackendType.CPU;
                defaults.ThreadCount = Math.Min(2, Math.Max(1, processors));
            }
            else if (qwen08 && IsDevice(device, "PJA110") && MNNInferenceCache.IsAdreno740(graphics))
            {
                defaults.BackendType = MNNBackendType.CPU;
                defaults.PrecisionMode = MNNPrecisionMode.Normal;
                defaults.PowerMode = MNNPowerMode.Normal;
            }
            return defaults;
        }

        private static bool IsDevice(string actual, string expected)
        {
            string value = (actual ?? string.Empty).Trim();
            return value.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(" " + expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
