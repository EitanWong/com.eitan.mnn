using System;
using System.Runtime.InteropServices;
namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        // CoreAudio's public C ABI: AudioObjectPropertyAddress is three UInt32 fields.
        [StructLayout(LayoutKind.Sequential)]
        internal struct AudioDevicePropertyAddress
        {
            public uint selector, scope, element;
            public AudioDevicePropertyAddress(uint property, uint propertyScope) { selector = property; scope = propertyScope; element = 0; }
        }
        private const string CoreAudio = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";
        [DllImport(CoreAudio, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AudioObjectGetPropertyDataSize(uint target, ref AudioDevicePropertyAddress address, uint qualifierSize, IntPtr qualifier, out uint size);
        [DllImport(CoreAudio, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AudioObjectGetPropertyData(uint target, ref AudioDevicePropertyAddress address, uint qualifierSize, IntPtr qualifier, ref uint size, [Out] uint[] data);

#endif
#if UNITY_IOS && !UNITY_EDITOR
        // iOS arm64 Objective-C ABI, using public AVAudioSession/AVAudioSessionPortDescription
        // selectors. These are existing OS exports; no native plugin or custom C++ is required.
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr objc_getClass(string name);
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr sel_registerName(string name);
        [DllImport("__Internal", EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SendObject(IntPtr receiver, IntPtr selector);
        [DllImport("__Internal", EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong SendCount(IntPtr receiver, IntPtr selector);
        [DllImport("__Internal", EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SendIndex(IntPtr receiver, IntPtr selector, ulong index);

#endif
    }
}
