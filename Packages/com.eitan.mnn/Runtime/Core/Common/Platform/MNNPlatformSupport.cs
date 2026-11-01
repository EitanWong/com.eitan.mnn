using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// Availability of the implemented Interpreter/Session/Tensor C++ ABI binding, not certification of
    /// a model, native binary, graphics backend or device.
    /// </summary>
    public static class MNNPlatformSupport
    {
        /// <summary>The compiler/runtime ABI implemented by this package, not a portable MNN C# ABI.</summary>
        public static string AbiContract
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return $"MNN {MNNVersion.RequiredNativeVersion} / Android NDK r25c libc++ (__ndk1) / ARM64 IL2CPP";
#elif UNITY_IOS && !UNITY_EDITOR
                return $"MNN {MNNVersion.RequiredNativeVersion} / Apple libc++ ABI v1 / ARM64 IL2CPP";
#elif UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
                return $"MNN {MNNVersion.RequiredNativeVersion} / Apple libc++ ABI v1 / macOS 64-bit Mono";
#else
                return $"MNN {MNNVersion.RequiredNativeVersion} / unsupported platform ABI";
#endif
            }
        }

        public static bool IsSupported => UnsupportedReason == null;
        public static string UnsupportedReason => GetUnsupportedReason(Application.platform, UsesIl2Cpp, IntPtr.Size, BitConverter.IsLittleEndian, RuntimeInformation.ProcessArchitecture);
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

        internal static string GetUnsupportedReason(RuntimePlatform platform, bool il2cpp, int pointerSize, bool littleEndian, Architecture architecture = Architecture.Arm64)
        {
            if (pointerSize != 8 || !littleEndian)
                return "Direct MNN C++ binding requires a 64-bit little-endian process.";
            if (platform == RuntimePlatform.Android || platform == RuntimePlatform.IPhonePlayer)
            {
                if (architecture != Architecture.Arm64)
                    return "Direct mobile MNN C++ binding requires ARM64.";
                if (!il2cpp)
                    return "Direct mobile MNN C++ binding requires IL2CPP.";
                return null;
            }
            if (platform != RuntimePlatform.OSXEditor && platform != RuntimePlatform.OSXPlayer)
                return $"Direct MNN C++ binding is not implemented for {platform}.";
            if (il2cpp)
                return "Direct macOS MNN C++ binding requires Mono.";
            if (architecture != Architecture.Arm64 && architecture != Architecture.X64)
                return "Direct macOS MNN C++ binding requires ARM64 or x86_64.";
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
