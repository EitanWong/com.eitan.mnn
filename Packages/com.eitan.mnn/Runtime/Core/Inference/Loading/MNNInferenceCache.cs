using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNInferenceCache
    {
        private sealed class EnvironmentSnapshot
        {
            internal readonly string Root, Device, Model;
            internal readonly int Processors;
            internal EnvironmentSnapshot(string root, string device, string model, int processors)
            {
                Root = root;
                Device = device;
                Model = model;
                Processors = processors;
            }
        }

        private static EnvironmentSnapshot _environment;
        internal static string GraphicsDevice => System.Threading.Volatile.Read(ref _environment)?.Device ?? string.Empty;
        internal static string DeviceModel => System.Threading.Volatile.Read(ref _environment)?.Model ?? string.Empty;
        internal static int ProcessorCount => System.Threading.Volatile.Read(ref _environment)?.Processors ?? Environment.ProcessorCount;

        internal static bool IsAdreno740(string graphicsDevice)
        {
            string gpu = (graphicsDevice ?? string.Empty).Split('|')[0].Trim();
            return string.Equals(gpu, "Adreno (TM) 740", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(gpu, "Adreno (TM)740", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(gpu, "Adreno 740", StringComparison.OrdinalIgnoreCase);
        }

        // Unity invokes these callbacks on its main thread. PathFor is also used
        // by worker-thread precision retries, which must never query Unity APIs.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void CaptureEnvironment()
        {
            System.Threading.Volatile.Write(ref _environment, new EnvironmentSnapshot(
                Path.Combine(Application.persistentDataPath, "MNN"),
                SystemInfo.graphicsDeviceName + "|" + SystemInfo.graphicsDeviceVersion,
                SystemInfo.deviceModel, SystemInfo.processorCount));
        }

        internal static string DirectoryFor(string purpose)
        {
            string configured = Environment.GetEnvironmentVariable("MNN_INFERENCE_CACHE_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;
            var environment = System.Threading.Volatile.Read(ref _environment);
            if (environment == null)
                throw new IOException("MNN cache environment has not been initialized on the Unity main thread.");
            return Path.Combine(environment.Root, purpose);
        }

        internal static string PathFor(string graphPath, MNNBackendType backend, MNNPrecisionMode precision)
        {
            string root = DirectoryFor("GpuCache");
            Directory.CreateDirectory(root);
            var graph = new FileInfo(graphPath);
            var weights = new FileInfo(graphPath + ".weight");
            var environment = System.Threading.Volatile.Read(ref _environment);
            if (environment == null)
                throw new IOException("MNN GPU device information has not been initialized on the Unity main thread.");
            string key = string.Join("|", Path.GetFullPath(graphPath), graph.Length, graph.LastWriteTimeUtc.Ticks, weights.Exists ? weights.Length : 0, weights.Exists ? weights.LastWriteTimeUtc.Ticks : 0, MNNPlatformSupport.AbiContract, System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture, backend, precision, environment.Device);
            using (var hash = SHA256.Create())
                return Path.Combine(root, BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").ToLowerInvariant() + ".cache");
        }
    }
}
