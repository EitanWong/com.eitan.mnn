using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class MarkdownImageCacheTests
    {
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private readonly List<MNNMarkdownImageCache.Lease> _leases = new List<MNNMarkdownImageCache.Lease>();
        private MNNMarkdownImageCache _cache;

        [TearDown]
        public void TearDown()
        {
            foreach (var lease in _leases) lease.Dispose();
            _leases.Clear();
            _cache?.Dispose();
            _cache = null;
            foreach (var texture in _textures)
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            _textures.Clear();
        }

        private Texture2D Texture(int size = 2)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.Apply(false, true);
            _textures.Add(texture);
            return texture;
        }

        private MNNMarkdownImageCache.Lease Store(string key, Texture2D texture)
        {
            Assert.True(_cache.TryStore(key, texture, out var lease));
            _leases.Add(lease);
            return lease;
        }

        private MNNMarkdownImageCache.Lease Acquire(string key)
        {
            Assert.True(_cache.TryAcquire(key, out var lease));
            _leases.Add(lease);
            return lease;
        }

        [Test]
        public void EvictionKeepsDisplayedImageUntilItsLastBindingReleases()
        {
            _cache = new MNNMarkdownImageCache(maximumImages: 1);
            var first = Texture();
            var initial = Store("first", first);
            var visible = Acquire("first");
            initial.Dispose();
            var second = Texture();
            Store("second", second);
            Assert.False(_cache.TryAcquire("first", out _));
            Assert.AreSame(first, visible.Texture);
            Assert.IsTrue(first != null, "Eviction must not invalidate another visible binding.");
            Assert.AreEqual(1, _cache.CachedCount);
            Assert.Greater(_cache.ResidentEstimatedBytes, _cache.CachedEstimatedBytes,
                "Evicted active bindings are resident even though they do not consume cached ownership.");
            visible.Dispose();
            visible.Dispose();
            Assert.IsTrue(first == null, "The last binding releases an evicted native texture.");
            Assert.IsTrue(second != null);
        }

        [Test]
        public void LruTouchPreservesRecentlyReadUnboundImage()
        {
            _cache = new MNNMarkdownImageCache(maximumImages: 2);
            var first = Texture();
            var second = Texture();
            Store("first", first).Dispose();
            Store("second", second).Dispose();
            Acquire("first").Dispose();
            Store("third", Texture());
            Assert.IsTrue(second == null);
            Assert.IsTrue(first != null);
            Assert.AreEqual(2, _cache.CachedCount);
        }

        [Test]
        public void ByteBudgetEvictsEvenWhenEntryCountHasRoom()
        {
            const long budget = 100000;
            _cache = new MNNMarkdownImageCache(maximumImages: 32, maximumEstimatedBytes: budget);
            var first = Texture(64);
            Store("0", first).Dispose();
            for (int i = 1; i < 10; ++i) Store(i.ToString(), Texture(64)).Dispose();
            Assert.Less(_cache.CachedCount, 10);
            Assert.LessOrEqual(_cache.CachedEstimatedBytes, budget);
            Assert.AreEqual(_cache.CachedEstimatedBytes, _cache.ResidentEstimatedBytes);
            Assert.IsTrue(first == null);
        }

        [Test]
        public void OversizedImageIsDestroyedWithoutEvictingAcceptedImage()
        {
            _cache = new MNNMarkdownImageCache(maximumEstimatedBytes: 65536);
            var accepted = Texture();
            Store("accepted", accepted).Dispose();
            var oversized = Texture(256);
            Assert.False(_cache.TryStore("oversized", oversized, out var lease));
            Assert.IsNull(lease);
            Assert.IsTrue(oversized == null);
            Assert.IsTrue(accepted != null);
            Assert.AreEqual(1, _cache.CachedCount);
            Acquire("accepted");
        }

        [Test]
        public void DuplicateDownloadReusesImageAndDestroysRedundantTexture()
        {
            _cache = new MNNMarkdownImageCache();
            var first = Texture();
            Store("same-url", first);
            var redundant = Texture();
            var reused = Store("same-url", redundant);
            Assert.AreSame(first, reused.Texture);
            Assert.IsTrue(redundant == null);
            Assert.AreEqual(1, _cache.CachedCount);
            Assert.AreEqual(2, _cache.ActiveLeaseCount);
        }

        [Test]
        public void ClearAndDisposeRetainActiveBindingsUntilRelease()
        {
            _cache = new MNNMarkdownImageCache();
            var image = Texture();
            var binding = Store("image", image);
            _cache.Clear();
            _cache.Dispose();
            _cache.Dispose();
            Assert.IsTrue(image != null);
            Assert.AreSame(image, binding.Texture);
            Assert.AreEqual(0, _cache.CachedCount);
            Assert.AreEqual(0, _cache.CachedEstimatedBytes);
            Assert.Greater(_cache.ResidentEstimatedBytes, 0);
            binding.Dispose();
            Assert.IsTrue(image == null);
            Assert.IsNull(binding.Texture);
            Assert.AreEqual(0, _cache.ActiveLeaseCount);
            Assert.AreEqual(0, _cache.ResidentEstimatedBytes);
        }

        [Test]
        public void ClearingAllowsNewImageForSameKeyWithoutDestroyingOldBinding()
        {
            _cache = new MNNMarkdownImageCache();
            var first = Texture();
            var oldBinding = Store("key", first);
            _cache.Clear();
            var replacement = Texture();
            var newBinding = Store("key", replacement);
            Assert.AreSame(first, oldBinding.Texture);
            Assert.AreSame(replacement, newBinding.Texture);
            oldBinding.Dispose();
            Assert.IsTrue(first == null);
            Assert.IsTrue(replacement != null);
        }

        [Test]
        public void AlreadyOwnedOrRejectedArgumentsDoNotDestroyBorrowedImage()
        {
            _cache = new MNNMarkdownImageCache();
            var image = Texture();
            Store("key", image);
            Assert.Throws<ArgumentException>(() => _cache.TryStore("another-key", image, out _));
            Assert.Throws<ArgumentException>(() => _cache.TryStore("", image, out _));
            Assert.IsTrue(image != null);
            _cache.Dispose();
            var callerOwned = Texture();
            Assert.Throws<ObjectDisposedException>(() => _cache.TryStore("new", callerOwned, out _));
            Assert.IsTrue(callerOwned != null, "Argument validation happens before ownership transfer.");
        }

        [Test]
        public void WorkerCannotReleaseMainThreadOwnedBinding()
        {
            _cache = new MNNMarkdownImageCache();
            var image = Texture();
            var binding = Store("key", image);
            _cache.Clear();
            var error = Task.Run(() => Assert.Throws<InvalidOperationException>(() => binding.Dispose())).GetAwaiter().GetResult();
            Assert.IsNotNull(error);
            Assert.IsTrue(image != null);
            Assert.AreEqual(1, _cache.ActiveLeaseCount);
            binding.Dispose();
            Assert.IsTrue(image == null);
        }
    }
}
