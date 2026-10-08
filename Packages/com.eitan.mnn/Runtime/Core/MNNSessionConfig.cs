using System;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// MNN会话配置
    /// </summary>
    public sealed class MNNSessionConfig
    {
        /// <summary>
        /// 后端类型
        /// </summary>
        public MNNBackendType BackendType { get; set; } = MNNBackendType.Auto;

        /// <summary>
        /// CPU线程数 (CPU后端) 或 GPU调优模式 (GPU后端)
        /// </summary>
        public int ThreadCount { get; set; } = 4;

        /// <summary>
        /// 备用后端类型（当主后端不支持某些操作时使用）
        /// </summary>
        public MNNBackendType BackupBackendType { get; set; } = MNNBackendType.CPU;

        /// <summary>
        /// 内存模式
        /// </summary>
        public MNNMemoryMode MemoryMode { get; set; } = MNNMemoryMode.Normal;

        /// <summary>
        /// 功耗模式
        /// </summary>
        public MNNPowerMode PowerMode { get; set; } = MNNPowerMode.Normal;

        /// <summary>
        /// 精度模式
        /// </summary>
        public MNNPrecisionMode PrecisionMode { get; set; } = MNNPrecisionMode.Normal;

        /// <summary>
        /// GPU调优模式（仅GPU后端有效）
        /// </summary>
        public MNNGpuTuningMode GpuTuningMode { get; set; } = MNNGpuTuningMode.Wide;

        /// <summary>
        /// GPU内存模式（仅OpenCL后端有效）
        /// </summary>
        public MNNGpuMemoryMode GpuMemoryMode { get; set; } = MNNGpuMemoryMode.Image;

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public MNNSessionConfig()
        {
        }

        /// <summary>
        /// 为当前平台创建优化配置
        /// </summary>
        public static MNNSessionConfig CreateForCurrentPlatform()
        {
            var config = new MNNSessionConfig();

#if UNITY_IOS && !UNITY_EDITOR
            // iOS: 优先使用Metal后端
            config.BackendType = MNNBackendType.Metal;
            config.PrecisionMode = MNNPrecisionMode.Low; // 使用FP16获得更好的性能
#elif UNITY_ANDROID && !UNITY_EDITOR
            // Android: 优先使用OpenCL后端
            config.BackendType = MNNBackendType.OpenCL;
            config.GpuTuningMode = MNNGpuTuningMode.Wide;
            config.GpuMemoryMode = MNNGpuMemoryMode.Image;
            config.PrecisionMode = MNNPrecisionMode.Low;
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // macOS: 优先使用Metal后端
            config.BackendType = MNNBackendType.Metal;
            config.PrecisionMode = MNNPrecisionMode.Normal;
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Windows: 尝试CUDA，回退到CPU
            config.BackendType = MNNBackendType.CUDA;
            config.BackupBackendType = MNNBackendType.CPU;
            config.ThreadCount = Mathf.Max(1, SystemInfo.processorCount - 1);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            // Linux: 尝试CUDA，回退到CPU
            config.BackendType = MNNBackendType.CUDA;
            config.BackupBackendType = MNNBackendType.CPU;
            config.ThreadCount = Mathf.Max(1, SystemInfo.processorCount - 1);
#elif UNITY_WEBGL
            // WebGL: 只支持CPU
            config.BackendType = MNNBackendType.CPU;
            config.ThreadCount = 1; // WebGL单线程
#else
            // 其他平台: 使用CPU
            config.BackendType = MNNBackendType.CPU;
            config.ThreadCount = 4;
#endif

            return config;
        }

        /// <summary>
        /// 创建高性能配置（牺牲精度）
        /// </summary>
        public static MNNSessionConfig CreateHighPerformance()
        {
            var config = CreateForCurrentPlatform();
            config.PowerMode = MNNPowerMode.High;
            config.PrecisionMode = MNNPrecisionMode.Low;
            config.MemoryMode = MNNMemoryMode.Normal;
            return config;
        }

        /// <summary>
        /// 创建低功耗配置
        /// </summary>
        public static MNNSessionConfig CreateLowPower()
        {
            var config = CreateForCurrentPlatform();
            config.PowerMode = MNNPowerMode.Low;
            config.PrecisionMode = MNNPrecisionMode.Normal;
            config.MemoryMode = MNNMemoryMode.Low;
            config.ThreadCount = Mathf.Min(2, config.ThreadCount);
            return config;
        }

        /// <summary>
        /// 创建高精度配置（牺牲性能）
        /// </summary>
        public static MNNSessionConfig CreateHighPrecision()
        {
            var config = CreateForCurrentPlatform();
            config.PrecisionMode = MNNPrecisionMode.High;
            config.PowerMode = MNNPowerMode.Normal;
            return config;
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public MNNSessionConfig Clone()
        {
            return new MNNSessionConfig
            {
                BackendType = BackendType,
                ThreadCount = ThreadCount,
                BackupBackendType = BackupBackendType,
                MemoryMode = MemoryMode,
                PowerMode = PowerMode,
                PrecisionMode = PrecisionMode,
                GpuTuningMode = GpuTuningMode,
                GpuMemoryMode = GpuMemoryMode
            };
        }

        /// <summary>
        /// 输出配置信息
        /// </summary>
        public override string ToString()
        {
            return $"MNNSessionConfig {{ Backend: {BackendType}, Threads: {ThreadCount}, " +
                   $"Memory: {MemoryMode}, Power: {PowerMode}, Precision: {PrecisionMode} }}";
        }
    }
}
