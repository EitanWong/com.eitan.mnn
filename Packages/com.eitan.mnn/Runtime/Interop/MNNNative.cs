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
}
