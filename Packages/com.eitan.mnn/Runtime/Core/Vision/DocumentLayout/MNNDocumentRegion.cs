namespace MNN.Unity
{
    /// <summary>A page region in source-image pixels, with top-left origin.</summary>
    public sealed class MNNDocumentRegion
    {
        public MNNDetection Detection { get; }
        /// <summary>Model-predicted reading order. Gaps can remain after confidence filtering.</summary>
        public int ReadingOrder { get; }
        internal MNNDocumentRegion(MNNDetection detection, int readingOrder)
        {
            Detection = detection;
            ReadingOrder = readingOrder;
        }
    }
}
