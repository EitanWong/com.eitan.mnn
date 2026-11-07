#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR)
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // Borrowed, read-only inputs for the exact bundled MSVC x64 / GCC 13 ABI.
        // Always use external storage: no movable managed SSO self-pointer and no
        // native allocator ownership. Only synchronous const APIs may receive this.
        // This is a project-specific compiler contract, not an official MNN C# API.
        private static CppString CreateDesktopInputString(string text, out IntPtr storage)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? throw new ArgumentNullException(nameof(text)));
            int capacity = Math.Max(16, bytes.Length);
            storage = Marshal.AllocHGlobal(checked(capacity + 1));
            try
            {
                Marshal.Copy(bytes, 0, storage, bytes.Length);
                Marshal.WriteByte(storage, bytes.Length, 0);
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
                return new CppString { Data = unchecked((ulong)storage.ToInt64()), Capacity = (ulong)bytes.Length, Extra = (ulong)capacity };
#else
                return new CppString { Data = unchecked((ulong)storage.ToInt64()), Length = (ulong)bytes.Length, Capacity = (ulong)capacity };
#endif
            }
            catch
            {
                Marshal.FreeHGlobal(storage);
                storage = IntPtr.Zero;
                throw;
            }
        }

        private static int CopyDesktopInputStringBytes(ref CppString value, int start, ref byte[] buffer)
        {
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            ulong size = value.Capacity;
#else
            ulong size = value.Length;
#endif
            if (size > 64 * 1024 * 1024 || start < 0 || (ulong)start > size)
                throw new InvalidDataException("Invalid borrowed desktop input string range.");
            int count = (int)size - start;
            if (buffer == null || buffer.Length < count)
                buffer = new byte[count];
            if (count != 0)
                Marshal.Copy(IntPtr.Add(new IntPtr(unchecked((long)value.Data)), start), buffer, 0, count);
            return count;
        }
    }
}
#endif
