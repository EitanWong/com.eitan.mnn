using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MNN.Unity
{
    /// <summary>Exclusive persistent native cache directory for one live LLM instance.
    /// Dispose the model before releasing its lease. This is a shader/mmap cache,
    /// not a persisted conversation or KV snapshot.</summary>
    public sealed class MNNLlmCache : IDisposable
    {
        public string DirectoryPath { get; }
        private FileStream _lease;

        private MNNLlmCache(string directory, FileStream lease)
        {
            DirectoryPath = directory;
            _lease = lease;
        }

        /// <summary>Acquire a stable slot. nativeBuildIdentity must change when the native
        /// binary or its build options change (for example a build-manifest hash).
        /// deviceIdentity must include OS, GPU and driver identity. An optional verified
        /// modelIdentity from the delivery manifest avoids enumerating model file metadata;
        /// it must change for every model/tokenizer/template artifact update. Configuration
        /// text is always included. Cache root must be outside the model directory.</summary>
        public static MNNLlmCache Acquire(string root, string modelDirectory, string nativeBuildIdentity,
            string deviceIdentity, MNNLlmPerformanceOptions performance, string modelIdentity = null)
        {
            if (performance == null) throw new ArgumentNullException(nameof(performance));
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrWhiteSpace(modelDirectory)) throw new ArgumentNullException(nameof(modelDirectory));
            if (string.IsNullOrWhiteSpace(nativeBuildIdentity)) throw new ArgumentNullException(nameof(nativeBuildIdentity));
            if (string.IsNullOrWhiteSpace(deviceIdentity)) throw new ArgumentNullException(nameof(deviceIdentity));
            performance = performance.Resolve(modelDirectory);
            string model = Path.GetFullPath(modelDirectory).TrimEnd(Path.DirectorySeparatorChar);
            string cache = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            if (cache.Equals(model, StringComparison.OrdinalIgnoreCase) ||
                cache.StartsWith(model + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Keep the runtime cache outside the model directory.", nameof(root));
            var identity = new StringBuilder();
            Append(identity, "runtime-llm-cache-v1");
            Append(identity, model);
            Append(identity, nativeBuildIdentity);
            Append(identity, deviceIdentity);
            Append(identity, MNNPlatformSupport.AbiContract);
            Append(identity, System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString());
            Append(identity, performance.ThreadCount.ToString(CultureInfo.InvariantCulture));
            Append(identity, performance.BackendType.ToString());
            Append(identity, performance.PrecisionMode.ToString());
            Append(identity, performance.ToJson(model));
            Append(identity, File.ReadAllText(Path.Combine(model, "config.json")));
            MNNModelOptions.ReadLlmConfiguration(model, out string llmConfig);
            Append(identity, File.Exists(llmConfig) ? File.ReadAllText(llmConfig) : "");
            if (!string.IsNullOrWhiteSpace(modelIdentity)) Append(identity, modelIdentity);
            else
            {
                // Metadata fallback avoids reading gigabytes of weights on every load.
                // Same-size edits with preserved timestamps require an explicit artifact identity.
                foreach (string path in Directory.GetFiles(model, "*", SearchOption.AllDirectories)
                    .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.Ordinal))
                {
                    var file = new FileInfo(path);
                    Append(identity, path);
                    Append(identity, file.Length.ToString(CultureInfo.InvariantCulture));
                    Append(identity, file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
                }
            }
            return AcquireIdentity(cache, identity.ToString());
        }

        private static void Append(StringBuilder target, string value)
            => target.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);

        internal static MNNLlmCache AcquireIdentity(string root, string identity)
        {
            string key;
            using (var hash = SHA256.Create())
                key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();
            string parent = Path.Combine(root, key);
            Directory.CreateDirectory(parent);
            for (int slot = 0; slot < 16; ++slot)
            {
                string directory = Path.Combine(parent, slot.ToString(CultureInfo.InvariantCulture));
                Directory.CreateDirectory(directory);
                try
                {
                    return new MNNLlmCache(directory, new FileStream(Path.Combine(directory, ".lease"),
                        FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                }
                catch (IOException) when (slot < 15) { }
            }
            throw new IOException("No exclusive LLM cache slot is available.");
        }

        public void Dispose()
        {
            System.Threading.Interlocked.Exchange(ref _lease, null)?.Dispose();
        }
    }
}
