#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP)
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // Exact GNU libstdc++ C++11 release ABI for the bundled desktop builds.
        // Object storage is 32 bytes; all string operations call compiled members.
        // This contract is project-maintained, not an upstream portable C# API.
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        private const string DesktopStringConstructor = "_ZNSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEEC1EPKcyRKS3_";
        private const string DesktopStringReserveSymbol = "_ZNSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEE7reserveEy";
#else
        private const string DesktopStringConstructor = "_ZNSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEEC1EPKcmRKS3_";
        private const string DesktopStringReserveSymbol = "_ZNSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEE7reserveEm";
#endif
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = DesktopStringConstructor)]
        private static extern void DesktopStringConstruct(IntPtr value, byte[] bytes, UIntPtr length, ref byte allocator);
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = DesktopStringReserveSymbol)]
        private static extern void DesktopStringReserve(IntPtr value, UIntPtr capacity);
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEED1Ev")]
        private static extern void DesktopStringDestroy(IntPtr value);
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNKSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEE4dataEv")]
        private static extern IntPtr DesktopStringData(IntPtr value);
        [DllImport(Cxx, CallingConvention = Conv, EntryPoint = "_ZNKSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEE4sizeEv")]
        private static extern UIntPtr DesktopStringSize(IntPtr value);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNSt6vectorIiSaIiEED1Ev")]
        private static extern void DesktopIntVectorDestroy(ref CppVector value);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNSt6vectorIN3MNN7Express4VARPESaIS2_EED1Ev")]
        internal static extern void DesktopVariableVectorDestroy(ref CppVector value);
        [DllImport(Lib, CallingConvention = Conv, EntryPoint = "_ZNSt16_Sp_counted_baseILN9__gnu_cxx12_Lock_policyE2EE10_M_releaseEv")]
        private static extern void DesktopSharedRelease(IntPtr control);

        private static int CopyDesktopStringBytes(IntPtr value, int start, ref byte[] buffer)
        {
            ulong size = DesktopStringSize(value).ToUInt64();
            if (size > 64 * 1024 * 1024 || start < 0 || (ulong)start > size)
                throw new InvalidDataException("Invalid native GNU string range.");
            int count = (int)size - start;
            if (buffer == null || buffer.Length < count)
                buffer = new byte[count];
            if (count != 0)
                Marshal.Copy(IntPtr.Add(DesktopStringData(value), start), buffer, 0, count);
            return count;
        }

        // Nontrivial GNU string returns must use explicit native result storage.
        // The verified GNU SysV and MinGW x64 builds pass sret before this.
        // Callers declare explicit result storage and pass Pointer first.
        internal sealed class DesktopStringResult : IDisposable
        {
            internal IntPtr Pointer { get; private set; }
            private bool _constructed;

            internal DesktopStringResult()
            {
                Pointer = Marshal.AllocHGlobal(Marshal.SizeOf<CppString>());
            }

            internal void MarkConstructed()
            {
                if (Pointer == IntPtr.Zero) throw new ObjectDisposedException(nameof(DesktopStringResult));
                if (_constructed) throw new InvalidOperationException("The native result was already constructed.");
                _constructed = true;
            }

            internal string Read()
            {
                if (Pointer == IntPtr.Zero) throw new ObjectDisposedException(nameof(DesktopStringResult));
                if (!_constructed) throw new InvalidOperationException("The native result has not been constructed.");
                byte[] bytes = Array.Empty<byte>();
                CopyDesktopStringBytes(Pointer, 0, ref bytes);
                return Encoding.UTF8.GetString(bytes);
            }

            public void Dispose()
            {
                IntPtr owner = Pointer;
                Pointer = IntPtr.Zero;
                if (owner == IntPtr.Zero) return;
                try { if (_constructed) DesktopStringDestroy(owner); }
                finally { Marshal.FreeHGlobal(owner); }
            }
        }
    }
}
#endif
