using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    // Consumers own their recurrent/context buffers. Only the fixed inference resource
    // is reference counted here; no idle cache survives the last consumer.
    internal static class MNNSileroFramePool
    {
        private sealed class Entry
        {
            internal string Key;
            internal MNNSileroFrameRunner Runner;
            internal int References;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Entry> Models = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static readonly Dictionary<MNNSileroFrameRunner, Entry> Owners = new Dictionary<MNNSileroFrameRunner, Entry>();

        internal static MNNSileroFrameRunner Acquire(string directory, int threads, MNNBackendType backend)
        {
            if (backend == MNNBackendType.NN) throw new NotSupportedException("Silero NNAPI graph/device compatibility has not been verified.");
            backend = backend == MNNBackendType.Auto ? MNNBackendType.CPU : MNNAcceleration.Resolve(backend);
            directory = Path.GetFullPath(directory);
            if (directory.Length > Path.GetPathRoot(directory).Length)
                directory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            lock (Gate)
            {
                string key = Key(directory, threads, backend);
                if (!Models.TryGetValue(key, out var entry))
                {
                    // Publish only fully constructed resources. A failed load can be retried.
                    var runner = new MNNSileroFrameRunner(directory, threads, backend);
                    entry = new Entry { Key = key, Runner = runner };
                    Models.Add(key, entry);
                    Owners.Add(runner, entry);
                }
                ++entry.References;
                return entry.Runner;
            }
        }

        internal static void Release(MNNSileroFrameRunner runner)
        {
            lock (Gate)
            {
                if (!Owners.TryGetValue(runner, out var entry)) throw new InvalidOperationException("Silero resource has no owner.");
                if (--entry.References != 0) return;
                Models.Remove(entry.Key);
                Owners.Remove(runner);
                runner.Dispose();
            }
        }

        private static string Key(string directory, int threads, MNNBackendType backend)
        {
            var config = MNNSileroVadConfig.Read(directory);
            bool official = MNNSileroModel.IsOfficialProfile(config);
            if (!official && !MNNSileroModel.IsRepository(config.repository)) throw new InvalidDataException("Unknown Silero VAD profile.");
            string graph = official ? MNNSileroModel.OfficialGraph : MNNSileroModel.Graph;
            string identity = Fingerprint(Path.Combine(directory, MNNSileroVadConfig.FileName)) + Fingerprint(Path.Combine(directory, graph));
            string fp16 = Path.Combine(directory, MNNSileroModel.GraphFp16);
            if (!official && File.Exists(fp16)) identity += Fingerprint(fp16);
            if (Path.DirectorySeparatorChar == '\\') directory = directory.ToUpperInvariant();
            return directory.Length + ":" + directory + ":" + threads + ":" + (int)backend + ":" + identity;
        }

        private static string Fingerprint(string path)
        {
            // Verify content on acquisition, so replacing/corrupting a file cannot reuse a
            // stale loaded model. Installed model directories must be immutable while in use.
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(stream));
        }
    }
}
