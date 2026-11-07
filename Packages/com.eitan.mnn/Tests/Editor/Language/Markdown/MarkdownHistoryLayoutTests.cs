using System;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class MarkdownHistoryLayoutTests
    {
        private sealed class Entries : IMNNConversationTranscriptSource
        {
            internal MNNConversationTranscriptEntry[] Values;
            public int Count => Values.Length;
            public MNNConversationTranscriptEntry this[int index] => Values[index];
            internal Entries(params string[] text) { Values = text.Select(value => new MNNConversationTranscriptEntry(MNNChatRole.Assistant, value)).ToArray(); }
        }
        private static float Rebuild(MNNMarkdownHistoryLayout layout, Entries entries, int page = 0, int size = 10, int dirty = 0) =>
            layout.Rebuild(entries, page, size, dirty, 20, _ => new MNNMarkdownHistoryLayout.MarkdownMeasurement(10, 10));

        [Test]
        public void BubblePolicyIsParameterizedAndViewportLookupUsesMeasuredSliceEdges()
        {
            var layout = new MNNMarkdownHistoryLayout(chunkCharacters: 4, sliceSpacing: 3, verticalPadding: 2, firstSlicePadding: 5, minimumLineHeight: 0);
            Assert.AreEqual(32, Rebuild(layout, new Entries("abcdefgh")));
            Assert.AreEqual(2, layout.Slices.Count); Assert.AreEqual(17, layout.Slices[0].Height); Assert.AreEqual(20, layout.Slices[1].Top);
            Assert.AreEqual(0, layout.FirstVisible(17)); Assert.AreEqual(1, layout.FirstVisible(17.1f)); Assert.AreEqual(2, layout.FirstVisible(99));
            Assert.AreEqual(1, layout.FindSlice(0, 4)); Assert.AreEqual(-1, layout.FindSlice(9, 9));
        }
        [Test]
        public void FormattingAndImagesCrossSlicesOnceAndUnicodeStaysWhole()
        {
            var layout = new MNNMarkdownHistoryLayout(chunkCharacters: 16);
            Rebuild(layout, new Entries("**" + new string('中', 15) + "🎙" + new string('长', 17) + "** ![diagram](https://example.com/image.png)"));
            Assert.AreEqual(1, layout.Slices.Sum(slice => slice.RenderResult.ImageCount));
            foreach (var slice in layout.Slices)
            {
                string text = System.Text.RegularExpressions.Regex.Replace(slice.Text, "<[^>]+>", "");
                if (text.Length > 0) { Assert.False(char.IsLowSurrogate(text[0])); Assert.False(char.IsHighSurrogate(text[text.Length - 1])); }
            }
        }
        [Test]
        public void PageSwitchAndSourceShrinkCannotRetainOldVisibleEntries()
        {
            var entries = new Entries("one", "two", "three"); var layout = new MNNMarkdownHistoryLayout();
            Rebuild(layout, entries, size: 1); Assert.AreEqual(0, layout.Slices[0].Entry);
            Rebuild(layout, entries, page: 2, size: 1, dirty: int.MaxValue); Assert.AreEqual(2, layout.Slices[0].Entry);
            Rebuild(layout, entries, size: 3); entries.Values = new[] { entries.Values[0] };
            Rebuild(layout, entries, size: 3, dirty: int.MaxValue); Assert.AreEqual(1, layout.Slices.Count);
        }
        [Test]
        public void UnchangedRenderResultsAreReusedWithinABoundedCache()
        {
            var entries = new Entries("one", "two", "three"); var layout = new MNNMarkdownHistoryLayout(maximumRenderCacheEntries: 2);
            Rebuild(layout, entries, size: 1); var first = layout.Slices[0].RenderResult;
            Rebuild(layout, entries, size: 1); Assert.AreSame(first, layout.Slices[0].RenderResult);
            Rebuild(layout, entries, page: 1, size: 1); Rebuild(layout, entries, page: 2, size: 1);
            Assert.LessOrEqual(layout.CachedRenderCount, 2);
            layout.Clear(); Assert.AreEqual(0, layout.Slices.Count); Assert.AreEqual(0, layout.CachedRenderCount);
        }
        [Test]
        public void RenderCacheBoundsTextPayloadAsWellAsEntryCount()
        {
            var layout = new MNNMarkdownHistoryLayout(maximumRenderCacheCharacters: 32);
            var entries = new Entries("small", new string('x', 100));
            Rebuild(layout, entries, size: 1); Assert.Greater(layout.CachedRenderCharacters, 0);
            Rebuild(layout, entries, page: 1, size: 1);
            Assert.LessOrEqual(layout.CachedRenderCharacters, 32); Assert.AreEqual(100, layout.Slices[0].RenderResult.RichText.Length);
        }
        [Test]
        public void InvalidMeasurementAndChunkArgumentsCannotCreateUnsearchableLayout()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNMarkdownHistoryLayout.MarkdownMeasurement(10, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNMarkdownHistoryLayout(sliceSpacing: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNMarkdownHistoryLayout.ChunkLength("text", 0, 1));
            var layout = new MNNMarkdownHistoryLayout(); Assert.Throws<ArgumentOutOfRangeException>(() => layout.FirstVisible(float.NaN));
            string unicode = "中🎙\r\ntext"; int offset = 0;
            while (offset < unicode.Length) { int length = MNNMarkdownHistoryLayout.ChunkLength(unicode, offset, 2); Assert.Greater(length, 0); offset += length; }
            Assert.AreEqual(unicode.Length, offset);
        }
    }
}
