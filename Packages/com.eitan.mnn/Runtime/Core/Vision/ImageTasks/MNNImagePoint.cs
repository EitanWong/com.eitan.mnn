namespace MNN.Unity
{
    /// <summary>Image coordinates use source pixels and a top-left origin.</summary>
    public sealed class MNNImagePoint
    {
        public float X { get; }

        public float Y { get; }

        internal MNNImagePoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
