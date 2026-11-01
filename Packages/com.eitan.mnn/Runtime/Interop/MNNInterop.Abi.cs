#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR && !ENABLE_IL2CPP) || ((UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP)
#define MNN_LLM_CPP_ABI
#endif
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && ENABLE_IL2CPP
#define MNN_MOBILE_LLM_CPP_ABI
#endif
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MNN.Unity.Interop
{
    // STL ownership: pinned MNN 3.6.2, macOS Mono or ARM64 mobile IL2CPP.
    // Apple libc++ / Android NDK r25c contracts are documented in MobileInterop.md.
    // These are compiler ABI representations, not a portable MNN C# interface.
    internal static partial class MNNInterop
    {
#if MNN_MOBILE_LLM_CPP_ABI
        private const string Cxx = Lib;
#else
        private const string Cxx = "/usr/lib/libc++.1.dylib";
#endif
        private static PlatformNotSupportedException UnsupportedAbi() => new PlatformNotSupportedException(MNNPlatformSupport.UnsupportedReason ?? "This entry is unavailable under the bundled platform ABI. Use the documented pinned compiler ABI for this inference task.");
        internal static void RequireAbi()
        {
            MNNPlatformSupport.RequireSupported();
            if (Marshal.PtrToStringUTF8(NativeGetVersion()) != MNNVersion.RequiredNativeVersion)
                throw new PlatformNotSupportedException($"Direct MNN C++ binding requires the platform-specific bundled MNN {MNNVersion.RequiredNativeVersion} binary and its documented compiler ABI.");
        }

#if MNN_MOBILE_LLM_CPP_ABI
#if UNITY_ANDROID
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNKSt6__ndk112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEE4dataEv")]
        private static extern IntPtr StringData(ref CppString value);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNKSt6__ndk112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEE4sizeEv")]
        private static extern UIntPtr StringSize(ref CppString value);
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNKSt3__112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEE4dataB9nqn220106Ev")]
        private static extern IntPtr StringData(ref CppString value);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNKSt3__112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEE4sizeB9nqn220106Ev")]
        private static extern UIntPtr StringSize(ref CppString value);
#endif

#endif

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

#if MNN_LLM_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNSt6__ndk112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEE6__initEPKcm")]
#else
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNSt3__112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEE6__initEPKcm")]
#endif
        private static extern void StringInit(ref CppString value, byte[] bytes, UIntPtr length);
#else
        private static void StringInit(ref CppString value, byte[] bytes, UIntPtr length) => throw UnsupportedAbi();
#endif
#if MNN_LLM_CPP_ABI
#if UNITY_IOS && !UNITY_EDITOR
        // IL2CPP's C-linkage declaration of this untagged symbol collides with
        // Xcode 27 libc++ inline basic_string instantiations in the same unit.
        // Resolve the actual system C++ destructor directly, without a wrapper.
        private const string AppleStringDestructorSymbol = "_ZNSt3__112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEED1Ev";
        [UnmanagedFunctionPointer(Conv)]
        private delegate void StringDestructor(ref CppString value);
        private static class AppleStringDestructor
        {
            internal static readonly StringDestructor Invoke = Resolve();
            private static StringDestructor Resolve()
            {
                IntPtr symbol = AppleDlsym(new IntPtr(-2), AppleStringDestructorSymbol); // Darwin RTLD_DEFAULT.
                if (symbol == IntPtr.Zero) throw new PlatformNotSupportedException("Pinned Apple libc++ string destructor is unavailable.");
                return Marshal.GetDelegateForFunctionPointer<StringDestructor>(symbol);
            }
        }
        private static void StringDestroy(ref CppString value) => AppleStringDestructor.Invoke(ref value);
#else
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNSt6__ndk112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEED1Ev")]
#else
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNSt3__112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEED1Ev")]
#endif
        private static extern void StringDestroy(ref CppString value);
#endif
#else
        private static void StringDestroy(ref CppString value) => throw UnsupportedAbi();
#endif
#if MNN_LLM_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNSt6__ndk16vectorIiNS_9allocatorIiEEED2Ev")]
#else
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNSt3__16vectorIiNS_9allocatorIiEEED1B9nqn220106Ev")]
#endif
        private static extern void IntVectorDestroy(ref CppVector value);
#else
        private static void IntVectorDestroy(ref CppVector value) => throw UnsupportedAbi();
#endif
#if MNN_LLM_CPP_ABI
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNSt6__ndk119__shared_weak_count16__release_sharedEv")]
#elif UNITY_IOS && !UNITY_EDITOR
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNSt3__119__shared_weak_count16__release_sharedB9nqn220106Ev")]
#else
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNSt3__119__shared_weak_count16__release_sharedEv")]
#endif
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

        internal static byte[] StringBytes(ref CppString value, int start = 0)
        {
            byte[] bytes = Array.Empty<byte>();
            CopyStringBytes(ref value, start, ref bytes);
            return bytes;
        }

        // Copy immediately while model storage is valid. Only the returned prefix of
        // the caller-owned buffer is initialized; no native pointer survives the call.
        internal static unsafe int CopyStringBytes(ref CppString value, int start, ref byte[] buffer)
        {
#if MNN_MOBILE_LLM_CPP_ABI
            // Use compiled libc++ members for SSO, long and embedded-NUL strings.
            // The 24-byte storage size is pinned to the audited compiler ABI.
            ulong size = StringSize(ref value).ToUInt64();
            if (size > 64 * 1024 * 1024)
                throw new InvalidDataException("Unexpected native string size.");
            if (start < 0 || (ulong)start > size)
                throw new InvalidDataException("Native string shrank during generation.");
            int length = (int)size - start;
            if (buffer == null || buffer.Length < length)
                buffer = new byte[length];
            if (length != 0)
                Marshal.Copy(IntPtr.Add(StringData(ref value), start), buffer, 0, length);
            return length;
#else
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
                if (start < 0 || start > length)
                    throw new InvalidDataException("Native string shrank during generation.");
                int count = length - start;
                if (buffer == null || buffer.Length < count)
                    buffer = new byte[count];
                IntPtr data = large ? new IntPtr(unchecked((long)(alternate ? value.Data : value.Capacity))) : IntPtr.Add((IntPtr)ptr, alternate ? 0 : 1);
                if (count != 0)
                    Marshal.Copy(IntPtr.Add(data, start), buffer, 0, count);
                return count;
            }
#endif
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

        // Copy borrowed model-owned vectors without transferring allocator ownership.
        internal static int[] CopyInts(CppVector value)
        {
            var values = new int[VectorCount(value, 4)];
            if (values.Length != 0)
                Marshal.Copy(value.Begin, values, 0, values.Length);
            return values;
        }

        internal static int[] TakeInts(CppVector value)
        {
            try
            {
                return CopyInts(value);
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

    }
}
