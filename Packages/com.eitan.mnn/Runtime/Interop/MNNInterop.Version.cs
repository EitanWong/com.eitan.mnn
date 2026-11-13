#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN && !UNITY_EDITOR) || ((UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP)) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_INFERENCE_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_INFERENCE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = MNNCoreSymbols.Version)]
        private static extern IntPtr NativeGetVersion();
#else
        private static IntPtr NativeGetVersion() => throw UnsupportedAbi();
#endif
        internal static IntPtr MNN_GetVersion()
        {
            RequireAbi();
            return NativeGetVersion();
        }
    }
}
