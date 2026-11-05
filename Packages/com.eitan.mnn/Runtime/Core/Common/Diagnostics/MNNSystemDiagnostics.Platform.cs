using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using static MNN.Unity.Interop.MNNInterop;

namespace MNN.Unity
{
    public sealed partial class MNNSystemDiagnostics
    {
        private ulong _cpuTotal, _cpuIdle;
        private bool _hasSystemCpu;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        // Acquire the OS right only when diagnostics is sampled. MNNInterop must have no
        // type initializer: a platform guard must never initialize native callback state.
        private static class AppleHost
        {
            internal static readonly uint Port = mach_host_self();
        }
#endif

        // Only documented OS C APIs and procfs counters. No model interop or native plugin is introduced.
        private string ReadSystemCpu()
        {
            try
            {
                ulong total, idle;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                if (!GetSystemTimes(out var idleTime, out var kernel, out var user)) return Unavailable;
                idle = idleTime.Value; total = kernel.Value + user.Value; // Kernel includes idle.
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                uint count = 4;
                var ticks = new uint[4]; // host_cpu_load_info_data_t: user, system, idle, nice.
                if (host_statistics(AppleHost.Port, 3, ticks, ref count) != 0 || count != 4) return Unavailable;
                idle = ticks[2]; total = (ulong)ticks[0] + ticks[1] + ticks[2] + ticks[3];
#elif UNITY_STANDALONE_LINUX || (UNITY_ANDROID && !UNITY_EDITOR)
                using (var reader = new StreamReader("/proc/stat"))
                {
                    var values = reader.ReadLine()?.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (values == null || values.Length < 5 || values[0] != "cpu") return Unavailable;
                    total = 0; idle = ulong.Parse(values[4], CultureInfo.InvariantCulture);
                    if (values.Length > 5) idle += ulong.Parse(values[5], CultureInfo.InvariantCulture); // iowait.
                    for (int i = 1; i < Math.Min(values.Length, 9); ++i) total += ulong.Parse(values[i], CultureInfo.InvariantCulture);
                }
#else
                return Unavailable;
#endif
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || (UNITY_ANDROID && !UNITY_EDITOR)
                string result = _hasSystemCpu && total > _cpuTotal && idle >= _cpuIdle
                    ? Percent(1.0 - (double)(idle - _cpuIdle) / (total - _cpuTotal)) + " · all logical cores"
                    : "Collecting system CPU delta…";
                _hasSystemCpu = true; _cpuTotal = total; _cpuIdle = idle;
                return result;
#endif
            }
            catch (Exception error) { return "Unavailable · OS counter denied / unsupported (" + error.GetType().Name + ")"; }
        }

        private static string ReadSystemMemory()
        {
            try
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                var status = new MemoryStatus { length = (uint)Marshal.SizeOf<MemoryStatus>() };
                if (!GlobalMemoryStatusEx(ref status)) return Unavailable;
                return "Available " + Bytes((long)status.availablePhysical) + " / " + Bytes((long)status.totalPhysical) +
                    " · load " + status.memoryLoad + "%";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                uint count = 15;
                var pages = new uint[15]; // vm_statistics_data_t (HOST_VM_INFO), 32-bit natural_t fields.
                if (host_statistics(AppleHost.Port, 2, pages, ref count) != 0 || count < 4 || host_page_size(AppleHost.Port, out uint pageSize) != 0)
                    return Unavailable;
                return "Free pages " + Bytes((long)pages[0] * pageSize) + " · active " + Bytes((long)pages[1] * pageSize) +
                    " · inactive " + Bytes((long)pages[2] * pageSize) + " · wired " + Bytes((long)pages[3] * pageSize) +
                    "\nFree pages are not total available RAM; reclaimable/compressed memory is not included.";
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var manager = activity.Call<AndroidJavaObject>("getSystemService", "activity"))
                using (var info = new AndroidJavaObject("android.app.ActivityManager$MemoryInfo"))
                {
                    manager.Call("getMemoryInfo", info);
                    return "Available " + Bytes(info.Get<long>("availMem")) + " / " + Bytes(info.Get<long>("totalMem")) +
                        " · low memory: " + info.Get<bool>("lowMemory");
                }
#elif UNITY_STANDALONE_LINUX
                long total = -1, available = -1;
                foreach (string line in File.ReadLines("/proc/meminfo"))
                {
                    if (!line.StartsWith("MemTotal:", StringComparison.Ordinal) && !line.StartsWith("MemAvailable:", StringComparison.Ordinal)) continue;
                    var fields = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    long bytes = long.Parse(fields[1], CultureInfo.InvariantCulture) * 1024;
                    if (fields[0] == "MemTotal:") total = bytes; else available = bytes;
                }
                return total >= 0 && available >= 0 ? "Available " + Bytes(available) + " / " + Bytes(total) : Unavailable;
#else
                return Unavailable;
#endif
            }
            catch (Exception error) { return "Unavailable · OS counter denied / unsupported (" + error.GetType().Name + ")"; }
        }

    }
}
