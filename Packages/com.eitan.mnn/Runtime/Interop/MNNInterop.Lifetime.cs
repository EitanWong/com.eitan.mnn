using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        internal static NativeCallScope BeginNativeCall() => new NativeCallScope(true);

        // Unity worker threads do not provide a UIKit event-loop autorelease
        // pool. Drain temporary Foundation/Metal objects on the calling thread
        // after a complete synchronous MNN operation has copied its results.
        internal struct NativeCallScope : IDisposable
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || (UNITY_IOS && !UNITY_EDITOR)
            private IntPtr _pool;
            private readonly SendMessage _send;
            private readonly IntPtr _drain;
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate IntPtr ResolveName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate IntPtr SendMessage(IntPtr receiver, IntPtr selector);
#endif
            internal NativeCallScope(bool enabled)
            {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || (UNITY_IOS && !UNITY_EDITOR)
                IntPtr library = AppleDlopen(null, 1);
                if (library == IntPtr.Zero) throw new PlatformNotSupportedException("Objective-C runtime unavailable.");
                try
                {
                    var getClass = Marshal.GetDelegateForFunctionPointer<ResolveName>(AppleDlsym(library, "objc_getClass"));
                    var selector = Marshal.GetDelegateForFunctionPointer<ResolveName>(AppleDlsym(library, "sel_registerName"));
                    _send = Marshal.GetDelegateForFunctionPointer<SendMessage>(AppleDlsym(library, "objc_msgSend"));
                    _drain = selector("drain");
                    _pool = _send(getClass("NSAutoreleasePool"), selector("new"));
                    if (_pool == IntPtr.Zero) throw new OutOfMemoryException("Could not create native autorelease pool.");
                }
                finally { AppleDlclose(library); }
#endif
            }

            public void Dispose()
            {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || (UNITY_IOS && !UNITY_EDITOR)
                if (_pool == IntPtr.Zero) return;
                _send(_pool, _drain);
                _pool = IntPtr.Zero;
#endif
            }
        }
    }
}
