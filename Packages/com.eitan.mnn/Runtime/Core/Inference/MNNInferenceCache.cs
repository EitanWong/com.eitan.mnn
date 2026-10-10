using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNInferenceCache
    {
        internal static string PathFor(string graphPath, MNNBackendType backend, MNNPrecisionMode precision)
        {
            string root = Environment.GetEnvironmentVariable("MNN_INFERENCE_CACHE_DIRECTORY");
            if (string.IsNullOrEmpty(root))
                root = Path.Combine(Application.persistentDataPath, "MNN", "GpuCache");
            Directory.CreateDirectory(root);
            var graph = new FileInfo(graphPath);
            var weights = new FileInfo(graphPath + ".weight");
            string key = string.Join("|", Path.GetFullPath(graphPath), graph.Length, graph.LastWriteTimeUtc.Ticks, weights.Exists ? weights.Length : 0, weights.Exists ? weights.LastWriteTimeUtc.Ticks : 0, "MNN-3.6.1-AppleAbi-v1", System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture, backend, precision, SystemInfo.graphicsDeviceName, SystemInfo.graphicsDeviceVersion);
            using (var hash = SHA256.Create())
                return Path.Combine(root, BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").ToLowerInvariant() + ".cache");
        }
    }
}
