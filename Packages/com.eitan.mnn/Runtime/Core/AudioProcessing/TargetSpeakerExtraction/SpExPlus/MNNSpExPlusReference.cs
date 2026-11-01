namespace MNN.Unity
{
    /// <summary>Immutable, unnormalized SpEx+ conditioning, reusable across extractions.</summary>
    public sealed class MNNSpExPlusReference
    {
        internal readonly float[] Values;
        internal MNNSpExPlusReference(float[] values) => Values = (float[])values.Clone();
        public int Size => Values.Length;
        public float[] ToArray() => (float[])Values.Clone();
    }
}
