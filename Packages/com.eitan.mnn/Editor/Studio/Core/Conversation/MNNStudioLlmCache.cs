using System;
using System.IO;
using System.Security.Cryptography;

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
            string nativeIdentity;
            using (var hash = SHA256.Create())
            using (var native = File.OpenRead(nativeLibrary))
                nativeIdentity = BitConverter.ToString(hash.ComputeHash(native));
            return new MNNStudioLlmCache(MNNLlmCache.Acquire(root, model, nativeIdentity, device,
                new MNNLlmPerformanceOptions { ThreadCount = threads }));
        }

        public void Dispose()
        {
            _lease?.Dispose();
            _lease = null;
        }
    }
}
