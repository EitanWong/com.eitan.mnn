using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    /// <summary>
    /// MNN原生库名称管理
    /// </summary>
    internal static class MNNNative
    {
        /// <summary>
        /// 获取平台特定的DLL名称
        /// </summary>
#if UNITY_IOS && !UNITY_EDITOR
        public const string LibraryName = "__Internal";
#else
        public const string LibraryName = "MNN";
#endif

        /// <summary>
        /// 默认调用约定
        /// </summary>
        public const CallingConvention Convention = CallingConvention.Cdecl;
    }

    /// <summary>
    /// MNN后端配置 (对应C++ BackendConfig结构)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct BackendConfig
    {
        public MNNMemoryMode memory;
        public MNNPowerMode power;
        public MNNPrecisionMode precision;
        public IntPtr sharedContext; // union with flags
    }

    /// <summary>
    /// MNN调度配置 (对应C++ ScheduleConfig结构)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ScheduleConfig
    {
        public MNNBackendType type;
        public int numThread; // union with mode for GPU
        public MNNBackendType backupType;
        public IntPtr backendConfig; // BackendConfig*
        // Note: saveTensors和path字段在C#中难以直接映射，暂不支持
    }
}
