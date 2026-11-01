using AOT;
using Unity.Burst;
using UnityEngine;

namespace MNN.Unity.Performance
{
    [BurstCompile]
    internal static unsafe class MNNBurstFft
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        private static void Initialize()
        {
            // Cache Invoke once and compile before audio processing starts. Each dispatch
            // handles a complete FFT, amortizing function-pointer interop overhead.
            MNNFftPlan.BurstKernel = BurstCompiler.IsEnabled
                ? BurstCompiler.CompileFunctionPointer<MNNFftPlan.Kernel>(Transform).Invoke : null;
        }
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High)]
        [MonoPInvokeCallback(typeof(MNNFftPlan.Kernel))]
        private static unsafe void Transform(double* real, double* imaginary, int* reversed, double* cosine, double* sine, int length, int inverse)
        {
            MNNFftPlan.TransformKernel(real, imaginary, reversed, cosine, sine, length, inverse);
        }
    }
}
