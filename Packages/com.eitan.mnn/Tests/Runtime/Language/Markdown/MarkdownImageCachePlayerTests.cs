using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class MarkdownImageCachePlayerTests
    {
        [UnityTest]
        public IEnumerator EvictionAndLastBindingReleaseRespectPlayerFrameBoundary()
        {
            Assert.True(Application.isPlaying, "This resource lifecycle check requires PlayMode or Player.");
            var cache = new MNNMarkdownImageCache(maximumImages: 1);
            var first = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var second = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            MNNMarkdownImageCache.Lease firstBinding = null, sharedBinding = null, secondBinding = null;
            try
            {
                Assert.True(cache.TryStore("first", first, out firstBinding));
                Assert.True(cache.TryAcquire("first", out sharedBinding));
                Assert.True(cache.TryStore("second", second, out secondBinding));
                Assert.False(cache.TryAcquire("first", out _));
                yield return null;
                Assert.IsTrue(first != null, "Eviction must retain the texture while visible leases exist.");
                firstBinding.Dispose();
                yield return null;
                Assert.IsTrue(first != null, "Another visible binding still owns the image.");
                sharedBinding.Dispose();
                Assert.IsTrue(first != null, "Destroy is scheduled rather than immediate inside the Player frame.");
                yield return null;
                Assert.IsTrue(first == null, "The last evicted binding must release the native texture at the frame boundary.");
                cache.Dispose();
                cache.Dispose();
                Assert.IsTrue(second != null, "Cache disposal must retain a displayed texture.");
                secondBinding.Dispose();
                Assert.IsTrue(second != null);
                yield return null;
                Assert.IsTrue(second == null);
                Assert.AreEqual(0, cache.CachedCount);
                Assert.AreEqual(0, cache.ActiveLeaseCount);
                Assert.AreEqual(0, cache.ResidentEstimatedBytes);
            }
            finally
            {
                firstBinding?.Dispose();
                sharedBinding?.Dispose();
                secondBinding?.Dispose();
                cache.Dispose();
                if (first != null) UnityEngine.Object.Destroy(first);
                if (second != null) UnityEngine.Object.Destroy(second);
            }
        }

        [UnityTest]
        public IEnumerator DisposingUnboundCachedImageUsesDeferredPlayerDestruction()
        {
            Assert.True(Application.isPlaying, "This resource lifecycle check requires PlayMode or Player.");
            var cache = new MNNMarkdownImageCache();
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            MNNMarkdownImageCache.Lease binding = null;
            try
            {
                Assert.True(cache.TryStore("image", texture, out binding));
                binding.Dispose();
                cache.Dispose();
                Assert.IsTrue(texture != null, "Player destruction must not happen synchronously.");
                yield return null;
                Assert.IsTrue(texture == null);
                Assert.AreEqual(0, cache.ResidentEstimatedBytes);
            }
            finally
            {
                binding?.Dispose();
                cache.Dispose();
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
        }
    }
}
