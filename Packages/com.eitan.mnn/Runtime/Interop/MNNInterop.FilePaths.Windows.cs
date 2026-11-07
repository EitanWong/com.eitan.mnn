#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR && !ENABLE_IL2CPP)
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

        private static byte[] ToWindowsPathBytes(string path)
        {
            string full = Path.GetFullPath(path);
            Encoding encoding = Encoding.GetEncoding((int)GetACP(), EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            if (TryEncodeWindowsPath(full, encoding, out byte[] bytes))
                return bytes;
            string shortened = GetWindowsShortPath(full);
            if (shortened != null && TryEncodeWindowsPath(shortened, encoding, out bytes))
                return bytes;
            // Cache files need not exist yet; shorten their existing directory.
            shortened = GetWindowsShortPath(Path.GetDirectoryName(full));
            if (shortened != null && TryEncodeWindowsPath(Path.Combine(shortened, Path.GetFileName(full)), encoding, out bytes))
                return bytes;
            throw new NotSupportedException("The bundled MNN Windows FileLoader requires a path representable in the process ANSI code page or an available DOS short path. Move the model/cache to such a directory.");
        }

        private static bool TryEncodeWindowsPath(string path, Encoding encoding, out byte[] bytes)
        {
            bytes = null;
            if (path.IndexOf('\0') >= 0 || path.Length >= 1024)
                return false;
            try
            {
                bytes = encoding.GetBytes(path + "\0");
                return true;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
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
