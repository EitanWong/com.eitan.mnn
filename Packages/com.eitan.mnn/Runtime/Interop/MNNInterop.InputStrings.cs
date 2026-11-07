#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR && !ENABLE_IL2CPP)
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // GNU strings are constructed and destroyed in stable native storage.
        // Force heap storage before exposing read-only header copies to vectors;
        // a copied SSO header would retain a pointer into the original object.
        private static CppString CreateDesktopInputString(string text, out IntPtr storage)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? throw new ArgumentNullException(nameof(text)));
            return CreateDesktopInputString(bytes, bytes.Length, out storage);
        }

        private static CppString CreateDesktopInputString(byte[] bytes, int length, out IntPtr storage)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (length < 0 || length > bytes.Length) throw new ArgumentOutOfRangeException(nameof(length));
            storage = Marshal.AllocHGlobal(Marshal.SizeOf<CppString>());
            bool constructed = false;
            try
            {
                byte allocator = 0;
                DesktopStringConstruct(storage, bytes, (UIntPtr)(uint)length, ref allocator);
                constructed = true;
                DesktopStringReserve(storage, (UIntPtr)16u);
                return Marshal.PtrToStructure<CppString>(storage);
            }
            catch
            {
                try { if (constructed) DesktopStringDestroy(storage); }
                finally { Marshal.FreeHGlobal(storage); storage = IntPtr.Zero; }
                throw;
            }
        }

        private static void DestroyDesktopInputString(ref CppString value, ref IntPtr storage)
        {
            IntPtr owner = storage;
            storage = IntPtr.Zero;
            value = default;
            if (owner == IntPtr.Zero) return;
            try { DesktopStringDestroy(owner); }
            finally { Marshal.FreeHGlobal(owner); }
        }

        private static unsafe int CopyDesktopInputStringBytes(ref CppString value, int start, ref byte[] buffer)
        {
            fixed (CppString* pointer = &value)
                return CopyDesktopStringBytes((IntPtr)pointer, start, ref buffer);
        }
    }
}
#endif
