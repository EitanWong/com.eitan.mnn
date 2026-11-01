using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class TokenCountCacheTests
    {
        [Test]
        public void ReusesExactTextAndEvictsLeastRecentlyUsedEntries()
        {
            var cache = new MNNTokenCountCache();
            int calls = 0;
            int Count(string text) { ++calls; return text.Length; }
            cache.Get("keep", Count);
            for (int i = 0; i < 127; ++i) cache.Get(i.ToString(), Count);
            Assert.AreEqual(128, calls);
            cache.Get("keep", Count);
            cache.Get("new", Count);
            cache.Get("keep", Count);
            Assert.AreEqual(129, calls);
            cache.Get("0", Count);
            Assert.AreEqual(130, calls);
            Assert.AreEqual(128, cache.Count);
            cache.Clear();
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.Characters);
            cache.Get("keep", Count);
            Assert.AreEqual(131, calls);
        }

        [Test]
        public void BoundsRetainedCharactersAndDoesNotCacheOversizedOrFailedCounts()
        {
            var cache = new MNNTokenCountCache();
            int calls = 0;
            int Count(string text) { ++calls; return text.Length; }
            for (int i = 0; i < 30; ++i) cache.Get(new string((char)('a' + i), 32768), Count);
            Assert.LessOrEqual(cache.Characters, 131072);
            Assert.AreEqual(4, cache.Count);
            cache.Get(new string('x', 32769), Count);
            cache.Get(new string('x', 32769), Count);
            Assert.AreEqual(32, calls);
            Assert.Throws<InvalidOperationException>(() => cache.Get("bad", _ => throw new InvalidOperationException()));
            Assert.AreEqual(3, cache.Get("bad", Count));
            Assert.AreEqual(33, calls);
        }
    }
}
