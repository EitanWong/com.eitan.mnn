using System;
using System.Collections.Generic;
using MNN.Unity.Interop;

namespace MNN.Unity
{
    /// <summary>Device/runtime selection for the implemented native ABI.
    /// Availability is not a guarantee that every operation runs on the device.
    /// Auto prefers the platform GPU, then CPU on Android; NNAPI is explicit because graph compatibility must be verified.</summary>
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
                return IsBackendAvailable(Resolve(MNNBackendType.Auto));
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
            foreach (var candidate in AutoCandidates)
                if (IsBackendAvailable(candidate)) return candidate;
            return MNNBackendType.CPU;
        }

        // Platform constants avoid querying Unity APIs from inference workers.
        private static readonly MNNBackendType[] AutoCandidates =
#if (UNITY_ANDROID && !UNITY_EDITOR) || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR)
            { MNNBackendType.OpenCL, MNNBackendType.Vulkan, MNNBackendType.CPU };
#else
            { MNNBackendType.Metal, MNNBackendType.NN, MNNBackendType.CPU };
#endif

        private static readonly Lazy<IReadOnlyList<MNNNeuralDevice>> Devices =
            new Lazy<IReadOnlyList<MNNNeuralDevice>>(MNNInterop.ReadNeuralDevices);
        /// <summary>Read Android NNAPI drivers through documented Android C APIs.
        /// A reported accelerator is not proof that a given MNN graph is placed on it.</summary>
        public static IReadOnlyList<MNNNeuralDevice> NeuralDevices => Devices.Value;

        internal static void Validate(MNNBackendType backend)
        {
            if (!Enum.IsDefined(typeof(MNNBackendType), backend))
                throw new ArgumentOutOfRangeException(nameof(backend));
        }

        internal static int NativeThreads(MNNSessionConfig config)
        {
            // Official Schedule copies this field into Backend::Info's union.
            // Metal, OpenCL and Vulkan consume it as GPU mode, not CPU threads.
            if (config.BackendType == MNNBackendType.OpenCL)
                return (int)config.GpuTuningMode | (int)config.GpuMemoryMode;
            if (config.BackendType == MNNBackendType.Vulkan || config.BackendType == MNNBackendType.Metal)
                return (int)config.GpuTuningMode;
            return config.ThreadCount;
        }

        internal static string ConfigName(MNNBackendType backend)
        {
            if (backend == MNNBackendType.NN)
                return "npu";
            return backend.ToString().ToLowerInvariant();
        }

        internal static MNNBackendType ForDynamicGraph(MNNBackendType requested)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // MNN 3.6.2 NNAPI cannot safely resize these dynamic graphs: some
            // unsupported conversions terminate the native process, bypassing
            // managed exception/fallback handling. Static Interpreter sessions
            // retain the explicit NNAPI API for verified model/device profiles.
            if (requested == MNNBackendType.NN)
            {
                UnityEngine.Debug.Log("[MNN] Dynamic graph uses CPU: NNAPI graph/device compatibility is not verified.");
                return MNNBackendType.CPU;
            }
#endif
            return requested;
        }

        internal static bool CanRetryOnCpu(Exception error) => error is MNNException || error is System.IO.InvalidDataException;
    }
}
