namespace MNN.Unity
{
    /// <summary>One immutable detection in source-image pixels, with top-left origin.</summary>
    public sealed class MNNDetection
    {
        public int ClassIndex { get; }
        public string Label { get; }
        public float Score { get; }
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        internal MNNDetection(int classIndex, string label, float score, float x, float y, float width, float height)
        {
            ClassIndex = classIndex;
            Label = label;
            Score = score;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
