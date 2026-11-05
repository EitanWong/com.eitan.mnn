using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;

namespace MNN.Unity
{
    /// <summary>
    /// Main-thread image ownership shared by chat views and previews. Cached entries have both an LRU count
    /// and an estimated native-byte budget. Active leases keep evicted images alive and can exceed that budget.
    /// Every transferred texture must be a newly owned runtime texture, never an imported or borrowed asset.
    /// </summary>
    public sealed class MNNMarkdownImageCache : IDisposable
    {
        private readonly int _thread = Thread.CurrentThread.ManagedThreadId;
        private readonly int _maximumImages;
        private readonly long _maximumEstimatedBytes;
        private readonly Dictionary<string, Entry> _cached = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<Texture2D, Entry> _owned = new Dictionary<Texture2D, Entry>(new TextureIdentity());
        private readonly LinkedList<Entry> _order = new LinkedList<Entry>();
        private long _cachedBytes, _residentBytes;
        private int _activeLeases;
        private bool _disposed;

        internal sealed class Entry
        {
            internal string Key;
            internal Texture2D Texture;
            internal long Bytes;
            internal int Leases;
            internal LinkedListNode<Entry> Node;
        }

        private sealed class TextureIdentity : IEqualityComparer<Texture2D>
        {
            public bool Equals(Texture2D x, Texture2D y) => ReferenceEquals(x, y);
            public int GetHashCode(Texture2D value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }

        /// <summary>One displayed binding. Dispose on the creating thread when hidden, rebound or destroyed.</summary>
        public sealed class Lease : IDisposable
        {
            private MNNMarkdownImageCache _owner;
            private Entry _entry;
            internal Lease(MNNMarkdownImageCache owner, Entry entry) { _owner = owner; _entry = entry; }
            public Texture2D Texture => _entry?.Texture;
            public void Dispose()
            {
                if (_owner == null) return;
                _owner.Release(_entry);
                _owner = null;
                _entry = null;
            }
        }

        public int CachedCount { get { CheckThread(); return _cached.Count; } }
        public long CachedEstimatedBytes { get { CheckThread(); return _cachedBytes; } }
        /// <summary>Estimated bytes still owned by cache or leases. Player destruction completes at Unity's frame boundary.</summary>
        public long ResidentEstimatedBytes { get { CheckThread(); return _residentBytes; } }
        public int ActiveLeaseCount { get { CheckThread(); return _activeLeases; } }

        public MNNMarkdownImageCache(int maximumImages = 32, long maximumEstimatedBytes = 64L * 1024 * 1024)
        {
            if (maximumImages < 1 || maximumImages > 65536) throw new ArgumentOutOfRangeException(nameof(maximumImages));
            if (maximumEstimatedBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumEstimatedBytes));
            _maximumImages = maximumImages;
            _maximumEstimatedBytes = maximumEstimatedBytes;
        }

        public bool TryAcquire(string key, out Lease lease)
        {
            CheckAvailable(key);
            lease = null;
            if (!_cached.TryGetValue(key, out var entry)) return false;
            if (entry.Texture == null) { RemoveCached(entry); return false; }
            _order.Remove(entry.Node);
            _order.AddLast(entry.Node);
            lease = Acquire(entry);
            return true;
        }

        /// <summary>
        /// Transfers ownership after argument validation, including on rejection. A duplicate key reuses the
        /// existing image and destroys the redundant texture. A texture exceeding the byte budget is destroyed.
        /// Byte estimates use Unity's reported native allocation with an RGBA mip estimate as a floor; they are
        /// platform-dependent accounting, not an exact GPU-memory limit or a bound on active displayed images.
        /// </summary>
        public bool TryStore(string key, Texture2D texture, out Lease lease)
        {
            CheckAvailable(key);
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (_owned.ContainsKey(texture)) throw new ArgumentException("This texture already belongs to the cache; acquire a lease instead.", nameof(texture));
            bool retained = false;
            lease = null;
            try
            {
                if (TryAcquire(key, out lease)) return true;
                long bytes = EstimateBytes(texture);
                if (bytes > _maximumEstimatedBytes) return false;
                while (_cached.Count >= _maximumImages || bytes > _maximumEstimatedBytes - _cachedBytes)
                    RemoveCached(_order.First.Value);
                var entry = new Entry { Key = key, Texture = texture, Bytes = bytes };
                entry.Node = _order.AddLast(entry);
                _cached.Add(key, entry);
                _owned.Add(texture, entry);
                _cachedBytes += bytes;
                _residentBytes += bytes;
                retained = true;
                lease = Acquire(entry);
                return true;
            }
            finally { if (!retained) DestroyTexture(texture); }
        }

        /// <summary>Releases cached ownership. Displayed images survive until their last lease is released.</summary>
        public void Clear()
        {
            CheckThread();
            while (_order.First != null) RemoveCached(_order.First.Value);
        }

        public void Dispose()
        {
            CheckThread();
            if (_disposed) return;
            _disposed = true;
            Clear();
        }

        private Lease Acquire(Entry entry)
        {
            ++entry.Leases;
            ++_activeLeases;
            return new Lease(this, entry);
        }

        private void Release(Entry entry)
        {
            CheckThread();
            --entry.Leases;
            --_activeLeases;
            ReleaseUnreferenced(entry);
        }

        private void RemoveCached(Entry entry)
        {
            _cached.Remove(entry.Key);
            _order.Remove(entry.Node);
            entry.Node = null;
            _cachedBytes -= entry.Bytes;
            ReleaseUnreferenced(entry);
        }

        private void ReleaseUnreferenced(Entry entry)
        {
            if (entry.Node != null || entry.Leases != 0) return;
            _owned.Remove(entry.Texture);
            _residentBytes -= entry.Bytes;
            DestroyTexture(entry.Texture);
            entry.Texture = null;
        }

        private static long EstimateBytes(Texture2D texture)
        {
            long rgba = 0;
            int width = texture.width, height = texture.height;
            for (int mip = 0; mip < texture.mipmapCount; ++mip)
            {
                rgba += (long)width * height * 4;
                width = Math.Max(1, width / 2);
                height = Math.Max(1, height / 2);
            }
            return Math.Max(rgba, Profiler.GetRuntimeMemorySizeLong(texture));
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }

        private void CheckAvailable(string key)
        {
            CheckThread();
            if (_disposed) throw new ObjectDisposedException(nameof(MNNMarkdownImageCache));
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Supply an image cache key.", nameof(key));
        }

        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _thread)
                throw new InvalidOperationException("Image cache ownership must stay on its creating Unity main thread.");
        }
    }
}
