#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_INFERENCE_CPP_ABI
#endif
using System.Runtime.InteropServices;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // Official Interpreter::setSessionHint(HintMode, int), using the package's
        // existing pinned Itanium C++ ABI contract. No native wrapper is involved.
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN11Interpreter14setSessionHintENS0_8HintModeEi")]
        private static extern void SetSessionHint(InterpreterHandle interpreter, int mode, int value);
#else
        private static void SetSessionHint(InterpreterHandle interpreter, int mode, int value) => throw UnsupportedAbi();
#endif
        internal static void MNN_Interpreter_setWinogradMemoryLevel(InterpreterHandle interpreter, int level)
        {
            RequireAbi();
            SetSessionHint(interpreter, 3, level);
        }
    }
}
