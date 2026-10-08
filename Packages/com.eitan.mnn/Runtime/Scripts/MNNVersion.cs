using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// MNN 版本信息和基础功能
    /// </summary>
    public static class MNNVersion
    {
        #if UNITY_IOS && !UNITY_EDITOR
            private const string DllName = "__Internal";
        #else
            private const string DllName = "MNN";
        #endif

        /// <summary>
        /// 获取 MNN 引擎版本
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr MNN_GetVersion();

        /// <summary>
        /// 获取 MNN 版本字符串
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
                Debug.LogError($"Failed to get MNN version: {e.Message}");
                return "Error";
            }
        }

        /// <summary>
        /// 检查 MNN 是否正常加载
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
        /// 打印 MNN 信息到控制台
        /// </summary>
        public static void LogInfo()
        {
            string version = GetVersion();
            Debug.Log($"[MNN] Version: {version}");
            Debug.Log($"[MNN] Platform: {Application.platform}");
            Debug.Log($"[MNN] Unity Version: {Application.unityVersion}");
            
            #if UNITY_IOS
                Debug.Log("[MNN] Backend: Metal (iOS)");
            #elif UNITY_ANDROID
                Debug.Log("[MNN] Backend: OpenCL (Android)");
            #elif UNITY_STANDALONE_OSX
                Debug.Log("[MNN] Backend: Metal (macOS)");
            #elif UNITY_STANDALONE_WIN
                Debug.Log("[MNN] Backend: CPU/CUDA (Windows)");
            #elif UNITY_STANDALONE_LINUX
                Debug.Log("[MNN] Backend: CPU/CUDA (Linux)");
            #elif UNITY_WEBGL
                Debug.Log("[MNN] Backend: WebAssembly (WebGL)");
            #else
                Debug.Log("[MNN] Backend: CPU (Generic)");
            #endif
        }
    }
}
