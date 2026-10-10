#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_APPLE_CPP_ABI
#endif
using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZN3MNN10getVersionEv")]
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
