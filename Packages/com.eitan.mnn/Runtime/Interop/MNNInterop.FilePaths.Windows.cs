#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // Official MNN FileLoader converts char* with Windows CP_ACP, not UTF-8.
        // Use that exact encoding; existing Unicode paths can use the public DOS
        // short-path API when the process code page cannot represent them.
        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        private static extern uint GetACP();
        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint GetShortPathNameW(string path, StringBuilder result, uint length);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int WideCharToMultiByte(uint codePage, uint flags, string value, int length,
            byte[] bytes, int capacity, IntPtr defaultChar, out int usedDefaultChar);

        private static byte[] ToWindowsPathBytes(string path)
        {
            string full = Path.GetFullPath(path);
            uint codePage = GetACP();
            if (TryEncodeWindowsPath(full, codePage, out byte[] bytes))
                return bytes;
            string shortened = GetWindowsShortPath(full);
            if (shortened != null && TryEncodeWindowsPath(shortened, codePage, out bytes))
                return bytes;
            // Cache files need not exist yet; shorten their existing directory.
            shortened = GetWindowsShortPath(Path.GetDirectoryName(full));
            if (shortened != null && TryEncodeWindowsPath(Path.Combine(shortened, Path.GetFileName(full)), codePage, out bytes))
                return bytes;
            throw new NotSupportedException("The bundled MNN Windows FileLoader requires a path representable in the process ANSI code page or an available DOS short path. Move the model/cache to such a directory.");
        }

        private static bool TryEncodeWindowsPath(string path, uint codePage, out byte[] bytes)
        {
            bytes = null;
            if (path.IndexOf('\0') >= 0 || path.Length >= 1024)
                return false;
            // UTF-8 is built into the runtime; legacy ANSI pages need not be.
            // Windows performs its own exact ACP conversion without I18N assemblies.
            if (codePage == 65001)
            {
                try { bytes = new UTF8Encoding(false, true).GetBytes(path + "\0"); return true; }
                catch (EncoderFallbackException) { return false; }
            }
            string terminated = path + "\0";
            const uint noBestFitChars = 0x400;
            int size = WideCharToMultiByte(codePage, noBestFitChars, terminated, terminated.Length,
                null, 0, IntPtr.Zero, out int substituted);
            if (size == 0 || substituted != 0)
                return false;
            byte[] encoded = new byte[size];
            if (WideCharToMultiByte(codePage, noBestFitChars, terminated, terminated.Length,
                encoded, encoded.Length, IntPtr.Zero, out substituted) != size || substituted != 0)
                return false;
            bytes = encoded;
            return true;
        }

        private static string GetWindowsShortPath(string path)
        {
            var buffer = new StringBuilder(1024);
            uint size = GetShortPathNameW(path, buffer, (uint)buffer.Capacity);
            return size != 0 && size < buffer.Capacity ? buffer.ToString() : null;
        }
    }
}
#endif
