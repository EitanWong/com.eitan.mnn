using System;

namespace MNN.Unity
{
    /// <summary>Page regions sorted by model-predicted reading order; no OCR or table-cell decoding.</summary>
    public sealed class MNNDocumentLayoutResult
    {
        private readonly MNNDocumentRegion[] _regions;
        public MNNDocumentRegion[] Regions => (MNNDocumentRegion[])_regions.Clone();
        public int Width { get; }
        public int Height { get; }
        internal MNNDocumentLayoutResult(MNNDocumentRegion[] regions, int width, int height)
        {
            _regions = (MNNDocumentRegion[])regions.Clone();
            Width = width;
            Height = height;
        }
    }
}
