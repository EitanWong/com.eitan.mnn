using System;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// Availability of the implemented C++ ABI binding, not certification of
    /// a model, native binary, graphics backend or device.
    /// </summary>
    public static class MNNPlatformSupport
    {
        public static bool IsSupported => UnsupportedReason == null;
        public static string UnsupportedReason => GetUnsupportedReason(Application.platform, UsesIl2Cpp, IntPtr.Size, BitConverter.IsLittleEndian);
        private static bool UsesIl2Cpp
        {
            get
            {
#if ENABLE_IL2CPP && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        internal static string GetUnsupportedReason(RuntimePlatform platform, bool il2cpp, int pointerSize, bool littleEndian)
        {
            if (platform != RuntimePlatform.OSXEditor && platform != RuntimePlatform.OSXPlayer)
                return $"Direct MNN C++ binding is not implemented for {platform}. " + "The current binding requires macOS Mono and Apple libc++ ABI v1.";
            if (il2cpp)
                return "Direct MNN C++ binding is not implemented for IL2CPP. Use macOS Mono.";
            if (pointerSize != 8 || !littleEndian)
                return "Direct MNN C++ binding requires a 64-bit little-endian process.";
            return null;
        }

        internal static void RequireSupported()
        {
            string reason = UnsupportedReason;
            if (reason != null)
                throw new PlatformNotSupportedException(reason);
        }
    }
}
