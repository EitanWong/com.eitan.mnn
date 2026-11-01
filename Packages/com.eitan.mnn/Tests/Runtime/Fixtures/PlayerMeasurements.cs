using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MNN.Unity.Tests
{
    // Process memory includes Unity, native MNN allocations and mapped weights.
    // iOS uses public Darwin resource APIs; Android reads the kernel status file.
    internal static class PlayerMeasurements
    {
        internal struct Memory
        {
            public long residentBytes, peakResidentBytes, physicalFootprintBytes;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)]
        private struct RusageInfo
        {
            internal ulong uuid0, uuid1, userTime, systemTime, idleWakeups, interrupts,
                pageins, wired, resident, footprint, start, exit;
        }
        [DllImport("__Internal")] private static extern int getpid();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ProcUsage(int pid, int flavor, ref RusageInfo info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GetUsage(int who, IntPtr usage);
        [DllImport("__Internal", EntryPoint = "dlsym")]
        private static extern IntPtr Symbol(IntPtr handle, string name);
        // Resolve public OS APIs dynamically to avoid IL2CPP redeclaring their
        // typed C struct pointers as conflicting generated C++ pointer types.
        private static readonly ProcUsage ProcessUsage = Marshal.GetDelegateForFunctionPointer<ProcUsage>(Symbol(new IntPtr(-2), "proc_pid_rusage"));
        private static readonly GetUsage ResourceUsage = Marshal.GetDelegateForFunctionPointer<GetUsage>(Symbol(new IntPtr(-2), "getrusage"));
#endif

        internal static Memory Read()
        {
            var memory = new Memory();
#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (string line in File.ReadAllLines("/proc/self/status"))
            {
                if (!line.StartsWith("VmRSS:") && !line.StartsWith("VmHWM:")) continue;
                string[] fields = line.Split(new[]{' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
                long bytes = checked(long.Parse(fields[1]) * 1024);
                if (line.StartsWith("VmRSS:")) memory.residentBytes = bytes;
                else memory.peakResidentBytes = bytes;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            var info = new RusageInfo();
            if (ProcessUsage(getpid(), 0, ref info) != 0)
                throw new IOException("proc_pid_rusage failed.");
            memory.residentBytes = checked((long)info.resident);
            memory.physicalFootprintBytes = checked((long)info.footprint);
            IntPtr usage = Marshal.AllocHGlobal(144); // Darwin arm64 public struct rusage.
            try
            {
                if (ResourceUsage(0, usage) != 0) throw new IOException("getrusage failed.");
                memory.peakResidentBytes = Marshal.ReadInt64(usage, 32); // ru_maxrss is bytes on Darwin.
            }
            finally { Marshal.FreeHGlobal(usage); }
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                memory.residentBytes = process.WorkingSet64;
                memory.peakResidentBytes = process.PeakWorkingSet64;
            }
#endif
            return memory;
        }
    }
}
