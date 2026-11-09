using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MNN.Unity.Editor
{
    // A cache slot has one owner until native disposal finishes. Separate slots
    // let concurrent Studio windows tune without writing the same native files.
    internal sealed class MNNStudioLlmCache : IDisposable
    {
        internal string DirectoryPath { get; }
        private MNNLlmCache _lease;

        private MNNStudioLlmCache(MNNLlmCache lease)
        {
            DirectoryPath = lease.DirectoryPath;
            _lease = lease;
        }

        internal static MNNStudioLlmCache Acquire(string root, string model, string nativeLibrary, string device, int threads)
        {
            var identity = new StringBuilder("studio-llm-cache-v1|low|high|");
            identity.Append(threads).Append('|').Append(device).Append('|').Append(MNNPlatformSupport.AbiContract);
            using (var hash = SHA256.Create())
            using (var native = File.OpenRead(nativeLibrary))
                identity.Append('|').Append(BitConverter.ToString(hash.ComputeHash(native)));
            identity.Append('|').Append(Path.GetFullPath(model));
            foreach (string path in Directory.GetFiles(model, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
            {
                var file = new FileInfo(path);
                identity.Append('|').Append(path).Append(':').Append(file.Length).Append(':').Append(file.LastWriteTimeUtc.Ticks);
                if (file.Name.Equals("config.json", StringComparison.OrdinalIgnoreCase) ||
                    file.Name.Equals("llm_config.json", StringComparison.OrdinalIgnoreCase))
                    identity.Append(':').Append(File.ReadAllText(path));
            }
            MNNModelOptions.ReadLlmConfiguration(model, out string metadata);
            identity.Append('|').Append(metadata);
            if (File.Exists(metadata)) identity.Append(':').Append(File.ReadAllText(metadata));
            return new MNNStudioLlmCache(MNNLlmCache.AcquireIdentity(root, identity.ToString()));
        }

        public void Dispose()
        {
            _lease?.Dispose();
            _lease = null;
        }
    }
}
