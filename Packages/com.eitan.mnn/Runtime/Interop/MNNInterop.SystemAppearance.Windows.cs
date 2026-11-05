#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // Public Windows Registry C ABI; RRF_RT_REG_DWORD controls the four-byte output.
        [DllImport("advapi32.dll", EntryPoint = "RegGetValueW", CharSet = CharSet.Unicode,
            ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        internal static extern int AppearanceRegistryValue(IntPtr key, string subkey, string value,
            uint flags, IntPtr type, out uint data, ref uint bytes);
    }
}
#endif
