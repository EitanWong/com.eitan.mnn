using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || (UNITY_IOS && !UNITY_EDITOR)
#if UNITY_IOS && !UNITY_EDITOR
        private const string ObjectiveC = "__Internal";
#else
        private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
#endif
        [DllImport(ObjectiveC, EntryPoint = "objc_getClass", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr Appearanceobjc_getClass(string name);
        [DllImport(ObjectiveC, EntryPoint = "sel_registerName", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr Appearancesel_registerName(string name);
        [DllImport(ObjectiveC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr AppearanceSend(IntPtr receiver, IntPtr selector);
        [DllImport(ObjectiveC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr AppearanceSendString(IntPtr receiver, IntPtr selector, string value);
        [DllImport(ObjectiveC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr AppearanceSendObject(IntPtr receiver, IntPtr selector, IntPtr value);
        [DllImport(ObjectiveC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)] internal static extern long AppearanceSendInteger(IntPtr receiver, IntPtr selector);
        [DllImport(ObjectiveC, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)] internal static extern bool AppearanceResponds(IntPtr receiver, IntPtr selector, IntPtr value);
        internal static IntPtr AppearanceSelector(string name) => Appearancesel_registerName(name);
#endif
    }
}
