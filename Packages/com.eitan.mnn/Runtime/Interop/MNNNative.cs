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

namespace MNN.Unity
{
    /// <summary>
    /// MNN版本信息和基础功能
    /// </summary>
    public static class MNNVersion
    {
        private const string DllName = Interop.MNNNative.LibraryName;
        private const CallingConvention Conv = Interop.MNNNative.Convention;

        /// <summary>
        /// 获取MNN引擎版本
        /// </summary>
        [DllImport(DllName, CallingConvention = Conv)]
        private static extern IntPtr MNN_GetVersion();

        /// <summary>
        /// 获取MNN版本字符串
        /// </summary>
        public static string GetVersion()
        {
            try
            {
                IntPtr versionPtr = MNN_GetVersion();
                if (versionPtr != IntPtr.Zero)
                {
                    return Marshal.PtrToStringAnsi(versionPtr);
                }
                return "Unknown";
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to get MNN version: {e.Message}");
                return "Error";
            }
        }

        /// <summary>
        /// 检查MNN是否正常加载
        /// </summary>
        public static bool IsLoaded()
        {
            try
            {
                string version = GetVersion();
                return !string.IsNullOrEmpty(version) && version != "Unknown" && version != "Error";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 打印MNN信息到控制台
        /// </summary>
        public static void LogInfo()
        {
            string version = GetVersion();
            UnityEngine.Debug.Log($"[MNN] Version: {version}");
            UnityEngine.Debug.Log($"[MNN] Platform: {UnityEngine.Application.platform}");
            UnityEngine.Debug.Log($"[MNN] Unity Version: {UnityEngine.Application.unityVersion}");

            #if UNITY_IOS
                UnityEngine.Debug.Log("[MNN] Backend: Metal (iOS)");
            #elif UNITY_ANDROID
                UnityEngine.Debug.Log("[MNN] Backend: OpenCL (Android)");
            #elif UNITY_STANDALONE_OSX
                UnityEngine.Debug.Log("[MNN] Backend: Metal (macOS)");
            #elif UNITY_STANDALONE_WIN
                UnityEngine.Debug.Log("[MNN] Backend: CPU/CUDA (Windows)");
            #elif UNITY_STANDALONE_LINUX
                UnityEngine.Debug.Log("[MNN] Backend: CPU/CUDA (Linux)");
            #elif UNITY_WEBGL
                UnityEngine.Debug.Log("[MNN] Backend: WebAssembly (WebGL)");
            #else
                UnityEngine.Debug.Log("[MNN] Backend: CPU (Generic)");
            #endif
        }
    }
}
