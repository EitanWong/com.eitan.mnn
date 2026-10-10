using System;
using System.Collections.Generic;
using MNN.Unity.Interop;

namespace MNN.Unity
{
    /// <summary>Device/runtime selection for the implemented native ABI.
    /// Availability is not a guarantee that every operation runs on the device.
    /// Auto prefers the platform GPU, then a neural backend, then CPU.</summary>
    public static class MNNAcceleration
    {
        private static readonly object Gate = new object ();
        private static readonly Dictionary<MNNBackendType, bool> Availability = new Dictionary<MNNBackendType, bool>();
        public static MNNBackendType PreferredBackend => Resolve(MNNBackendType.Auto);
        public static bool IsBackendAvailable(MNNBackendType backend)
        {
            Validate(backend);
            if (!MNNPlatformSupport.IsSupported)
                return false;
            if (backend == MNNBackendType.Auto)
                return IsBackendAvailable(MNNBackendType.Metal) || IsBackendAvailable(MNNBackendType.NN) || IsBackendAvailable(MNNBackendType.CPU);
            lock (Gate)
            {
                if (!Availability.TryGetValue(backend, out bool available))
                {
                    try
                    {
                        available = MNNInterop.IsBackendAvailable(backend);
                    }
                    catch (MNNException error)
                    {
                        UnityEngine.Debug.Log($"[MNN] {backend} probe failed: {error.Message}");
                        available = false;
                    }

                    Availability.Add(backend, available);
                }

                return available;
            }
        }

        internal static MNNBackendType Resolve(MNNBackendType requested)
        {
            MNNPlatformSupport.RequireSupported();
            Validate(requested);
            if (requested != MNNBackendType.Auto)
                return IsBackendAvailable(requested) ? requested : MNNBackendType.CPU;
            // Other platform ABIs are explicitly gated by MNNPlatformSupport.
            // CoreML/NN availability depends on the bundled MNN build and device;
            // a CoreML runtime does not certify Apple Neural Engine placement.
            if (IsBackendAvailable(MNNBackendType.Metal))
                return MNNBackendType.Metal;
            if (IsBackendAvailable(MNNBackendType.NN))
                return MNNBackendType.NN;
            return MNNBackendType.CPU;
        }

        internal static void Validate(MNNBackendType backend)
        {
            if (!Enum.IsDefined(typeof(MNNBackendType), backend))
                throw new ArgumentOutOfRangeException(nameof(backend));
        }

        internal static int NativeThreads(MNNSessionConfig config)
        {
            // The GPU mode bitmask is documented for OpenCL/Vulkan only.
            // Metal/CUDA/NN keep the CPU fallback thread budget separate.
            if (config.BackendType == MNNBackendType.OpenCL)
                return (int)config.GpuTuningMode | (int)config.GpuMemoryMode;
            if (config.BackendType == MNNBackendType.Vulkan)
                return (int)config.GpuTuningMode;
            return config.ThreadCount;
        }

        internal static string ConfigName(MNNBackendType backend)
        {
            if (backend == MNNBackendType.NN)
                return "npu";
            return backend.ToString().ToLowerInvariant();
        }

        internal static bool CanRetryOnCpu(Exception error) => error is MNNException || error is System.IO.InvalidDataException;
    }
}
