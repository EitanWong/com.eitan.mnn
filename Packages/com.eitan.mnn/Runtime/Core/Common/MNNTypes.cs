using System;

namespace MNN.Unity
{
    /// <summary>
    /// MNN后端类型
    /// </summary>
    public enum MNNBackendType
    {
        /// <summary>CPU后端</summary>
        CPU = 0,
        /// <summary>Metal后端 (iOS/macOS)</summary>
        Metal = 1,
        /// <summary>CUDA后端 (NVIDIA GPU)</summary>
        CUDA = 2,
        /// <summary>OpenCL后端 (Android/Desktop GPU)</summary>
        OpenCL = 3,
        /// <summary>探测可用加速后端并保留 CPU 回退</summary>
        Auto = 4,
        /// <summary>CoreML/NNAPI后端 (iOS/Android)</summary>
        NN = 5,
        /// <summary>OpenGL后端</summary>
        OpenGL = 6,
        /// <summary>Vulkan后端</summary>
        Vulkan = 7
    }

    /// <summary>
    /// MNN数据类型
    /// </summary>
    public enum MNNDataType
    {
        /// <summary>32位浮点数</summary>
        Float = 0,
        /// <summary>64位浮点数</summary>
        Double = 1,
        /// <summary>32位整数</summary>
        Int32 = 2,
        /// <summary>64位整数</summary>
        Int64 = 3,
        /// <summary>8位无符号整数</summary>
        UInt8 = 4,
        /// <summary>8位有符号整数</summary>
        Int8 = 5,
        /// <summary>16位浮点数</summary>
        Half = 6
    }

    /// <summary>
    /// MNN张量维度类型
    /// </summary>
    public enum MNNDimensionType
    {
        /// <summary>TensorFlow格式 (NHWC)</summary>
        TensorFlow = 0,
        /// <summary>Caffe格式 (NCHW)</summary>
        Caffe = 1,
        /// <summary>Caffe C4格式 (NC4HW4)</summary>
        CaffeC4 = 2
    }

    /// <summary>
    /// MNN错误码
    /// </summary>
    public enum MNNErrorCode
    {
        /// <summary>无错误</summary>
        NoError = 0,
        /// <summary>内存不足</summary>
        OutOfMemory = 1,
        /// <summary>不支持的操作</summary>
        NotSupport = 2,
        /// <summary>计算大小错误</summary>
        ComputeSizeError = 3,
        /// <summary>无执行器</summary>
        NoExecution = 4,
        /// <summary>无效值</summary>
        InvalidValue = 5,
        /// <summary>输入数据错误</summary>
        InputDataError = 10,
        /// <summary>回调停止</summary>
        CallBackStop = 11,
        /// <summary>张量不支持</summary>
        TensorNotSupport = 20,
        /// <summary>张量需要分割</summary>
        TensorNeedDivide = 21
    }

    /// <summary>
    /// MNN内存模式
    /// </summary>
    public enum MNNMemoryMode
    {
        /// <summary>普通内存模式</summary>
        Normal = 0,
        /// <summary>高内存模式</summary>
        High = 1,
        /// <summary>低内存模式</summary>
        Low = 2
    }

    /// <summary>
    /// MNN功耗模式
    /// </summary>
    public enum MNNPowerMode
    {
        /// <summary>普通功耗</summary>
        Normal = 0,
        /// <summary>高功耗</summary>
        High = 1,
        /// <summary>低功耗</summary>
        Low = 2
    }

    /// <summary>
    /// MNN精度模式
    /// </summary>
    public enum MNNPrecisionMode
    {
        /// <summary>后端默认精度（Metal 可使用 FP16）</summary>
        Normal = 0,
        /// <summary>高精度（具体计算类型由后端实现决定）</summary>
        High = 1,
        /// <summary>低精度 (FP16)</summary>
        Low = 2,
        /// <summary>低精度 (BF16)</summary>
        LowBF16 = 3
    }

    /// <summary>
    /// GPU调优模式
    /// </summary>
    [Flags]
    public enum MNNGpuTuningMode
    {
        /// <summary>无调优</summary>
        None = 1 << 0,
        /// <summary>重度调优</summary>
        Heavy = 1 << 1,
        /// <summary>广泛调优 (推荐)</summary>
        Wide = 1 << 2,
        /// <summary>普通调优</summary>
        Normal = 1 << 3,
        /// <summary>快速调优</summary>
        Fast = 1 << 4
    }

    /// <summary>
    /// GPU内存模式
    /// </summary>
    [Flags]
    public enum MNNGpuMemoryMode
    {
        /// <summary>Buffer模式</summary>
        Buffer = 1 << 6,
        /// <summary>Image模式</summary>
        Image = 1 << 7
    }

    /// <summary>
    /// 张量映射类型
    /// </summary>
    public enum MNNTensorMapType
    {
        /// <summary>写入映射</summary>
        Write = 0,
        /// <summary>读取映射</summary>
        Read = 1
    }

    /// <summary>
    /// 会话模式
    /// </summary>
    public enum MNNSessionMode
    {
        /// <summary>调试模式 (支持回调)</summary>
        Debug = 0,
        /// <summary>发布模式 (不支持回调)</summary>
        Release = 1,
        /// <summary>输入在外部</summary>
        InputOutside = 2,
        /// <summary>输入在内部</summary>
        InputInside = 3,
        /// <summary>输出在外部</summary>
        OutputOutside = 4,
        /// <summary>输出在内部</summary>
        OutputInside = 5
    }
}
