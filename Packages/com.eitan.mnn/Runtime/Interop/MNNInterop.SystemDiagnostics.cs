using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [StructLayout(LayoutKind.Sequential)]
        internal struct FileTime
        {
            public uint low, high;
            public ulong Value => ((ulong)high << 32) | low;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct MemoryStatus
        {
            public uint length, memoryLoad;
            public ulong totalPhysical, availablePhysical, totalPageFile, availablePageFile, totalVirtual, availableVirtual, availableExtendedVirtual;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private const string SystemLibrary = "/usr/lib/libSystem.B.dylib";
        [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint mach_host_self();
        [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int host_statistics(uint host, int flavor, [Out] uint[] info, ref uint count);
        [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int host_page_size(uint host, out uint size);
#endif
    }
}
