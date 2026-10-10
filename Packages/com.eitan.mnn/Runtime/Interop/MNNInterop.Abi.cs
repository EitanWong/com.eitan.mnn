#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP)
#define MNN_APPLE_CPP_ABI
#endif
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MNN.Unity.Interop
{
    // Explicit contract: MNN 3.6.1, Apple clang/libc++ ABI v1, 64-bit macOS Mono.
    // These are compiler ABI representations, not a portable MNN C# interface.
    internal static partial class MNNInterop
    {
        private const string Cxx = "/usr/lib/libc++.1.dylib";
        // Unsupported targets contain managed throwing stubs, not Apple
        // P/Invokes. This also avoids unresolved static imports on iOS/WebGL.
        private static PlatformNotSupportedException UnsupportedAbi() => new PlatformNotSupportedException(MNNPlatformSupport.UnsupportedReason);
        internal static void RequireAbi()
        {
            MNNPlatformSupport.RequireSupported();
            if (Marshal.PtrToStringUTF8(NativeGetVersion()) != "3.6.1")
                throw new PlatformNotSupportedException("Direct MNN C++ binding requires the bundled MNN 3.6.1 / Apple libc++ 64-bit ABI.");
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CppString
        {
            internal ulong Data, Length, Capacity;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CppVector
        {
            internal IntPtr Begin, End, Capacity;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CppShared
        {
            internal IntPtr Object, Control;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HalideType
        {
            internal int Code;
            internal byte Bits;
            internal ushort Lanes;
        }

#if MNN_APPLE_CPP_ABI
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNSt3__112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEE6__initEPKcm")]
        private static extern void StringInit(ref CppString value, byte[] bytes, UIntPtr length);
#else
        private static void StringInit(ref CppString value, byte[] bytes, UIntPtr length) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNSt3__112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEED1Ev")]
        private static extern void StringDestroy(ref CppString value);
#else
        private static void StringDestroy(ref CppString value) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNSt3__16vectorIiNS_9allocatorIiEEED1B9nqn220106Ev")]
        private static extern void IntVectorDestroy(ref CppVector value);
#else
        private static void IntVectorDestroy(ref CppVector value) => throw UnsupportedAbi();
#endif
#if MNN_APPLE_CPP_ABI
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNSt3__119__shared_weak_count16__release_sharedEv")]
        private static extern void SharedRelease(IntPtr control);
#else
        private static void SharedRelease(IntPtr control) => throw UnsupportedAbi();
#endif
        internal sealed class NativeString : IDisposable
        {
            internal CppString Value;
            internal NativeString(string text)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                StringInit(ref Value, bytes, (UIntPtr)(uint)bytes.Length);
            }

            public void Dispose()
            {
                StringDestroy(ref Value);
                Value = default;
            }
        }

        internal static unsafe byte[] StringBytes(ref CppString value)
        {
            fixed (CppString* ptr = &value)
            {
                // Apple libc++ uses alternate layout on arm64, but its original
                // layout on x86_64. The same 24 bytes have different tag/data
                // positions; this also applies to strings inside LlmContext.
                bool alternate = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
                byte tag = ((byte*)ptr)[alternate ? 23 : 0];
                bool large = (tag & (alternate ? 128 : 1)) != 0;
                int length = large ? checked((int)value.Length) : (alternate ? tag : tag >> 1);
                if (length < 0 || length > 64 * 1024 * 1024 || (!large && length > 22))
                    throw new InvalidDataException("Unexpected libc++ string layout.");
                var bytes = new byte[length];
                IntPtr data = large ? new IntPtr(unchecked((long)(alternate ? value.Data : value.Capacity))) : IntPtr.Add((IntPtr)ptr, alternate ? 0 : 1);
                if (length != 0)
                    Marshal.Copy(data, bytes, 0, length);
                return bytes;
            }
        }

        internal static string TakeString(CppString value)
        {
            try
            {
                return Encoding.UTF8.GetString(StringBytes(ref value));
            }
            finally
            {
                StringDestroy(ref value);
            }
        }

        internal static int VectorCount(CppVector value, int elementSize)
        {
            long bytes = value.End.ToInt64() - value.Begin.ToInt64();
            if (bytes < 0 || bytes % elementSize != 0 || bytes > int.MaxValue)
                throw new InvalidDataException("Unexpected libc++ vector layout.");
            return checked((int)(bytes / elementSize));
        }

        internal static int[] TakeInts(CppVector value)
        {
            try
            {
                var values = new int[VectorCount(value, 4)];
                if (values.Length != 0)
                    Marshal.Copy(value.Begin, values, 0, values.Length);
                return values;
            }
            // Destroy through the STL instantiation in MNN so the allocator
            // matches native construction, including Unity Player overrides.
            finally
            {
                IntVectorDestroy(ref value);
            }
        }

        internal static void ReleaseShared(ref CppShared value)
        {
            if (value.Control != IntPtr.Zero)
                SharedRelease(value.Control);
            value = default;
        }

        internal sealed class IntVector : IDisposable
        {
            private GCHandle _pin;
            internal CppVector Value;
            // Borrowed const vector over pinned managed memory. Never pass to a mutating API
            // or run a C++ destructor on it; the GC owns this buffer.
            internal IntVector(int[] values)
            {
                _pin = GCHandle.Alloc(values, GCHandleType.Pinned);
                IntPtr begin = _pin.AddrOfPinnedObject(), end = IntPtr.Add(begin, checked(values.Length * 4));
                Value = new CppVector{Begin = begin, End = end, Capacity = end};
            }

            public void Dispose()
            {
                if (_pin.IsAllocated)
                    _pin.Free();
                Value = default;
            }
        }

        internal static T Virtual<T>(IntPtr instance, int slot)
            where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
        [UnmanagedFunctionPointer(Conv)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool LoadFunction(IntPtr instance);
        internal static bool LoadVirtual(IntPtr instance) => Virtual<LoadFunction>(instance, 2)(instance);
    }
}
