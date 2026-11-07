using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>Text slicing and viewport lookup independent of uGUI objects and font measurement.</summary>
    public sealed class MNNMarkdownHistoryLayout
    {
        public const int DefaultChunkCharacters = 1024;
        private readonly int _chunkCharacters, _maximumRenderCacheEntries, _maximumRenderCacheCharacters;
        private int _cachedRenderCharacters;
        private readonly float _sliceSpacing, _verticalPadding, _firstSlicePadding, _minimumLineHeight;
        private readonly List<Slice> _slices = new List<Slice>();
        public IReadOnlyList<Slice> Slices => _slices;
        public int CachedRenderCount => _renderCache.Count;
        public int CachedRenderCharacters => _cachedRenderCharacters;
        private readonly Dictionary<int, RenderedEntry> _renderCache = new Dictionary<int, RenderedEntry>();

        private MNNMarkdownTheme _theme;
        private IMNNConversationTranscriptSource _source;
        private int _pageStart = -1, _pageSize, _fontSize;

        public MNNMarkdownHistoryLayout(int chunkCharacters = DefaultChunkCharacters, int maximumRenderCacheEntries = 256,
            float sliceSpacing = 0, float verticalPadding = 0, float firstSlicePadding = 0, float minimumLineHeight = 1.2f,
            int maximumRenderCacheCharacters = 1024 * 1024)
        {
            if (chunkCharacters < 2 || chunkCharacters > MNNConversationMailbox.MaximumTextCharacters) throw new ArgumentOutOfRangeException(nameof(chunkCharacters));
            if (maximumRenderCacheEntries < 1 || maximumRenderCacheEntries > 4096) throw new ArgumentOutOfRangeException(nameof(maximumRenderCacheEntries));
            if (maximumRenderCacheCharacters < 1 || maximumRenderCacheCharacters > 64 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maximumRenderCacheCharacters));
            if (!Finite(sliceSpacing) || sliceSpacing < 0 || !Finite(verticalPadding) || verticalPadding < 0 || !Finite(firstSlicePadding) || firstSlicePadding < 0)
                throw new ArgumentOutOfRangeException(nameof(sliceSpacing), "Layout padding and spacing must be finite and nonnegative.");
            if (!Finite(minimumLineHeight) || minimumLineHeight < 0) throw new ArgumentOutOfRangeException(nameof(minimumLineHeight));
            _maximumRenderCacheCharacters = maximumRenderCacheCharacters;
            _chunkCharacters = chunkCharacters; _maximumRenderCacheEntries = maximumRenderCacheEntries;
            _sliceSpacing = sliceSpacing; _verticalPadding = verticalPadding; _firstSlicePadding = firstSlicePadding; _minimumLineHeight = minimumLineHeight;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class RenderedEntry
        {
            internal string Source;
            internal int FontSize, Characters;
            internal MNNMarkdownTheme Theme;
            internal IReadOnlyList<MNNMarkdownRenderResult> Parts;
        }

        public sealed class Slice
        {
            internal Slice() { }
            public int Entry { get; internal set; }
            public int Start { get; internal set; }
            public MNNChatRole Role { get; internal set; }
            public string Text { get; internal set; }
            public MNNMarkdownRenderResult RenderResult { get; internal set; }
            public float BodyHeight { get; internal set; }
            public float Top { get; internal set; }
            public float Height { get; internal set; }
            public bool First { get; internal set; }
        }

        public readonly struct MarkdownMeasurement
        {
            public readonly float BodyHeight;
            public readonly float TotalHeight;

            public MarkdownMeasurement(float bodyHeight, float totalHeight)
            {
                if (!Finite(bodyHeight) || bodyHeight < 0 || !Finite(totalHeight) || totalHeight < bodyHeight)
                    throw new ArgumentOutOfRangeException(nameof(totalHeight), "Measurement must be finite, nonnegative and include its body.");
                BodyHeight = bodyHeight;
                TotalHeight = totalHeight;
            }
        }

        public float Rebuild(IMNNConversationTranscriptSource entries, int pageStart, int pageSize, int dirtyFrom,
            int fontSize, Func<MNNMarkdownRenderResult, MarkdownMeasurement> measure,
            MNNMarkdownTheme theme = MNNMarkdownTheme.Dark)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (pageStart < 0 || pageStart > entries.Count) throw new ArgumentOutOfRangeException(nameof(pageStart));
            if (pageSize < 1 || pageSize > 4096) throw new ArgumentOutOfRangeException(nameof(pageSize));
            if (dirtyFrom < 0) throw new ArgumentOutOfRangeException(nameof(dirtyFrom));
            if (fontSize < 1 || fontSize > 1024) throw new ArgumentOutOfRangeException(nameof(fontSize));
            if (measure == null) throw new ArgumentNullException(nameof(measure));
            if (_source != entries || _pageStart != pageStart || _pageSize != pageSize)
            {
                _slices.Clear(); dirtyFrom = pageStart; _source = entries; _pageStart = pageStart; _pageSize = pageSize;
            }
            if (_theme != theme || _fontSize != fontSize)
            {
                dirtyFrom = pageStart;
                _theme = theme; _fontSize = fontSize;
            }
            dirtyFrom = Math.Min(dirtyFrom, entries.Count);
            int keep = _slices.Count;
            while (keep > 0 && _slices[keep - 1].Entry >= dirtyFrom) --keep;
            float top = keep == 0 ? 0 : _slices[keep - 1].Top + _slices[keep - 1].Height + _sliceSpacing;
            _slices.RemoveRange(keep, _slices.Count - keep);
            int end = (int)Math.Min(entries.Count, (long)pageStart + pageSize);
            for (int index = Math.Max(pageStart, dirtyFrom); index < end; ++index)
            {
                var entry = entries[index];
                string text = entry.Text ?? "";
                if (text.Length > MNNConversationMailbox.MaximumTextCharacters) throw new ArgumentException("Transcript text exceeds the character limit.", nameof(entries));
                if (!_renderCache.TryGetValue(index, out RenderedEntry rendered) ||
                    rendered.Source != text || rendered.FontSize != fontSize || rendered.Theme != theme)
                {
                    if (rendered != null) { _cachedRenderCharacters -= rendered.Characters; _renderCache.Remove(index); }
                    rendered = new RenderedEntry { Source = text, FontSize = fontSize, Theme = theme,
                        Parts = MNNMarkdownRenderer.RenderSlices(text, _chunkCharacters, fontSize, theme) };
                    long characters = text.Length;
                    foreach (var part in rendered.Parts) characters += part.RichText.Length;
                    if (characters <= _maximumRenderCacheCharacters)
                    {
                        if (_renderCache.Count >= _maximumRenderCacheEntries || characters > _maximumRenderCacheCharacters - _cachedRenderCharacters)
                        { _renderCache.Clear(); _cachedRenderCharacters = 0; }
                        rendered.Characters = (int)characters; _renderCache[index] = rendered; _cachedRenderCharacters += rendered.Characters;
                    }
                }
                for (int part = 0; part < rendered.Parts.Count; ++part)
                {
                    MNNMarkdownRenderResult renderResult = rendered.Parts[part];
                    MarkdownMeasurement measurement = measure(renderResult);
                    bool first = part == 0;
                    var slice = new Slice
                    {
                        Entry = index, Start = renderResult.CharacterOffset, Role = entry.Role, Text = renderResult.RichText,
                        RenderResult = renderResult, BodyHeight = measurement.BodyHeight,
                        Top = top, First = first,
                        Height = (float)Math.Ceiling(Math.Max(fontSize * _minimumLineHeight, measurement.TotalHeight)) +
                            _verticalPadding + (first ? _firstSlicePadding : 0)
                    };
                    if (!Finite(top + slice.Height + _sliceSpacing)) throw new InvalidOperationException("Transcript layout height exceeds the supported range.");
                    _slices.Add(slice);
                    top += slice.Height + _sliceSpacing;
                }
            }
            return Math.Max(0, top - _sliceSpacing);
        }

        public void Clear()
        {
            _slices.Clear();
            _renderCache.Clear(); _cachedRenderCharacters = 0; _source = null; _pageStart = -1;
        }

        public static int ChunkLength(string text, int start, int chunkCharacters = DefaultChunkCharacters)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (start < 0 || start > text.Length) throw new ArgumentOutOfRangeException(nameof(start));
            if (chunkCharacters < 2 || chunkCharacters > MNNConversationMailbox.MaximumTextCharacters) throw new ArgumentOutOfRangeException(nameof(chunkCharacters));
            int end = (int)Math.Min(text.Length, (long)start + chunkCharacters);
            int lines = 0;
            for (int i = start; i < end; ++i)
                if (text[i] == '\n' && ++lines == 32) { end = i + 1; break; }
            if (end < text.Length && end > start && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end])) --end;
            if (end < text.Length && end > start && text[end - 1] == '\r' && text[end] == '\n') --end;
            return end - start;
        }

        public int FirstVisible(float top)
        {
            if (!Finite(top)) throw new ArgumentOutOfRangeException(nameof(top));
            int low = 0, high = _slices.Count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                var slice = _slices[middle];
                if (slice.Top + slice.Height < top) low = middle + 1;
                else high = middle;
            }
            return low;
        }
        public int FindSlice(int entry, int start)
        {
            for (int i = 0; i < _slices.Count; ++i) if (_slices[i].Entry == entry && _slices[i].Start == start) return i;
            return -1;
        }
    }
}
