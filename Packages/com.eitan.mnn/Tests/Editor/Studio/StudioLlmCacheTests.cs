using System;
using System.IO;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class StudioLlmCacheTests
    {
        [Test]
        public void ReusesReleasedSlotPreservesCacheAndIsolatesConcurrentOwners()
        {
            WithFixture((root, model, native) =>
            {
                string first;
                using (var one = MNNStudioLlmCache.Acquire(root, model, native, "gpu", 4))
                {
                    first = one.DirectoryPath;
                    File.WriteAllText(Path.Combine(first, "mnn_cachefile.bin"), "compiled kernels");
                    using (var two = MNNStudioLlmCache.Acquire(root, model, native, "gpu", 4))
                        Assert.AreNotEqual(first, two.DirectoryPath);
                }
                using (var again = MNNStudioLlmCache.Acquire(root, model, native, "gpu", 4))
                {
                    Assert.AreEqual(first, again.DirectoryPath);
                    Assert.AreEqual("compiled kernels", File.ReadAllText(Path.Combine(first, "mnn_cachefile.bin")));
                }
            });
        }

        [Test]
        public void InvalidatesForChangedNativeContentModelConfigurationDeviceAndThreads()
        {
            WithFixture((root, model, native) =>
            {
                string initial;
                using (var cache = MNNStudioLlmCache.Acquire(root, model, native, "gpu", 4)) initial = cache.DirectoryPath;
                using (var cache = MNNStudioLlmCache.Acquire(root, model, native, "other gpu", 4)) Assert.AreNotEqual(initial, cache.DirectoryPath);
                using (var cache = MNNStudioLlmCache.Acquire(root, model, native, "gpu", 2)) Assert.AreNotEqual(initial, cache.DirectoryPath);
                var timestamp = File.GetLastWriteTimeUtc(native);
                File.WriteAllText(native, "two");
                File.SetLastWriteTimeUtc(native, timestamp);
                using (var cache = MNNStudioLlmCache.Acquire(root, model, native, "gpu", 4)) Assert.AreNotEqual(initial, cache.DirectoryPath);
                File.WriteAllText(native, "one");
                File.WriteAllText(Path.Combine(model, "config.json"), "{\"changed\":true}");
                using (var cache = MNNStudioLlmCache.Acquire(root, model, native, "gpu", 4)) Assert.AreNotEqual(initial, cache.DirectoryPath);
            });
        }

        private static void WithFixture(Action<string, string, string> test)
        {
            string root = Path.GetFullPath(Path.Combine("TestArtifacts~", "StudioLlmCache", Guid.NewGuid().ToString("N")));
            string model = Path.Combine(root, "model"), native = Path.Combine(root, "native");
            Directory.CreateDirectory(model);
            File.WriteAllText(Path.Combine(model, "config.json"), "{}");
            File.WriteAllText(native, "one");
            try { test(Path.Combine(root, "cache"), model, native); }
            finally { Directory.Delete(root, true); }
        }
    }
}
