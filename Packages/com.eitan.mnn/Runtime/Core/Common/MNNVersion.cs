using System;
using System.Runtime.InteropServices;

namespace MNN.Unity
{
    /// <summary>
    /// MNN版本信息和基础功能
    /// </summary>
    public static class MNNVersion
    {
        /// <summary>
        /// 获取MNN版本字符串
        /// </summary>
        public static string GetVersion()
        {
            // Unsupported platforms must fail before resolving any C++ symbol.
            MNNPlatformSupport.RequireSupported();
            try
            {
                IntPtr versionPtr = Interop.MNNInterop.MNN_GetVersion();
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
            if (!MNNPlatformSupport.IsSupported)
                return false;
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
            UnityEngine.Debug.Log("[MNN] C++ ABI: Apple libc++ v1, 64-bit Mono. Backend is selected per session.");
        }
    }
}
