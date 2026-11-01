namespace MNN.Unity
{
    // Compatibility specialization; the verified single-precision butterflies are shared.
    internal sealed class MNNRealFft400
    {
        private readonly MNNSourceRealFft _plan = new MNNSourceRealFft(400);
        internal void Forward(float[] input, float[] real, float[] imaginary) => _plan.Forward(input, real, imaginary);
    }
}
