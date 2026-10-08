using System;
using System.Runtime.InteropServices;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity.Interop
{
    /// <summary>
    /// MNN原生函数P/Invoke声明
    /// </summary>
    internal static class MNNInterop
    {
        private const string Lib = MNNNative.LibraryName;
        private const CallingConvention Conv = MNNNative.Convention;

        #region Version

        /// <summary>
        /// 获取MNN版本字符串
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern IntPtr MNN_GetVersion();

        #endregion

        #region Interpreter

        /// <summary>
        /// 从文件创建Interpreter
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv, CharSet = CharSet.Ansi)]
        internal static extern InterpreterHandle MNN_Interpreter_createFromFile(
            [MarshalAs(UnmanagedType.LPStr)] string file);

        /// <summary>
        /// 从内存缓冲区创建Interpreter
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern InterpreterHandle MNN_Interpreter_createFromBuffer(
            IntPtr buffer,
            long size);

        /// <summary>
        /// 销毁Interpreter
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern void MNN_Interpreter_destroy(IntPtr interpreter);

        /// <summary>
        /// 创建Session
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern IntPtr MNN_Interpreter_createSession(
            InterpreterHandle interpreter,
            ref ScheduleConfig config);

        /// <summary>
        /// 释放Session
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern void MNN_Interpreter_releaseSession(
            InterpreterHandle interpreter,
            IntPtr session);

        /// <summary>
        /// 运行Session
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern int MNN_Interpreter_runSession(
            InterpreterHandle interpreter,
            SessionHandle session);

        /// <summary>
        /// 释放模型数据
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern void MNN_Interpreter_releaseModel(
            InterpreterHandle interpreter);

        /// <summary>
        /// 获取Session输入Tensor
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv, CharSet = CharSet.Ansi)]
        internal static extern IntPtr MNN_Interpreter_getSessionInput(
            InterpreterHandle interpreter,
            SessionHandle session,
            [MarshalAs(UnmanagedType.LPStr)] string name);

        /// <summary>
        /// 获取Session输出Tensor
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv, CharSet = CharSet.Ansi)]
        internal static extern IntPtr MNN_Interpreter_getSessionOutput(
            InterpreterHandle interpreter,
            SessionHandle session,
            [MarshalAs(UnmanagedType.LPStr)] string name);

        /// <summary>
        /// 调整Tensor大小
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern void MNN_Interpreter_resizeTensor(
            InterpreterHandle interpreter,
            IntPtr tensor,
            IntPtr dims,
            int size);

        /// <summary>
        /// 调整Session大小
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern void MNN_Interpreter_resizeSession(
            InterpreterHandle interpreter,
            SessionHandle session);

        /// <summary>
        /// 设置缓存文件
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv, CharSet = CharSet.Ansi)]
        internal static extern void MNN_Interpreter_setCacheFile(
            InterpreterHandle interpreter,
            [MarshalAs(UnmanagedType.LPStr)] string cacheFile);

        /// <summary>
        /// 更新缓存文件
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern void MNN_Interpreter_updateCacheFile(
            InterpreterHandle interpreter,
            SessionHandle session);

        #endregion

        #region Tensor

        /// <summary>
        /// 获取Tensor的host指针
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern IntPtr MNN_Tensor_getHost(IntPtr tensor);

        /// <summary>
        /// 映射Tensor内存
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern IntPtr MNN_Tensor_map(
            IntPtr tensor,
            MNNTensorMapType mapType,
            MNNDimensionType dimType);

        /// <summary>
        /// 取消映射Tensor内存
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern void MNN_Tensor_unmap(
            IntPtr tensor,
            MNNTensorMapType mapType,
            MNNDimensionType dimType,
            IntPtr mappedPtr);

        /// <summary>
        /// 从Host Tensor拷贝
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern void MNN_Tensor_copyFromHostTensor(
            IntPtr dstTensor,
            IntPtr srcTensor);

        /// <summary>
        /// 拷贝到Host Tensor
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern void MNN_Tensor_copyToHostTensor(
            IntPtr srcTensor,
            IntPtr dstTensor);

        /// <summary>
        /// 获取Tensor维度
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern int MNN_Tensor_getDimensionType(IntPtr tensor);

        /// <summary>
        /// 获取Tensor数据类型
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern int MNN_Tensor_getDataType(IntPtr tensor);

        /// <summary>
        /// 获取Tensor维度数量
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern int MNN_Tensor_getDimensions(IntPtr tensor);

        /// <summary>
        /// 获取Tensor形状
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern void MNN_Tensor_getShape(
            IntPtr tensor,
            [Out] int[] shape,
            int maxSize);

        /// <summary>
        /// 获取Tensor元素数量
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern int MNN_Tensor_elementSize(IntPtr tensor);

        /// <summary>
        /// 获取Tensor字节大小
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern int MNN_Tensor_size(IntPtr tensor);

        /// <summary>
        /// 创建Host Tensor
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern IntPtr MNN_Tensor_create(
            int dimensions,
            IntPtr shape,
            int dataType,
            IntPtr data,
            MNNDimensionType dimType);

        /// <summary>
        /// 创建Device Tensor
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern IntPtr MNN_Tensor_createDevice(
            int dimensions,
            IntPtr shape,
            int dataType,
            MNNDimensionType dimType);

        /// <summary>
        /// 销毁Tensor
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern void MNN_Tensor_destroy(IntPtr tensor);

        /// <summary>
        /// 等待Tensor准备就绪
        /// </summary>
        [DllImport(Lib, CallingConvention = Conv)]
        internal static extern void MNN_Tensor_wait(
            IntPtr tensor,
            MNNTensorMapType mapType,
            [MarshalAs(UnmanagedType.I1)] bool finish);

        #endregion
    }
}
