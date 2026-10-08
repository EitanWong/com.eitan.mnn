using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// MNN模型资源配置
    /// 管理模型的存储位置、下载策略和加载路径
    /// </summary>
    [CreateAssetMenu(fileName = "MNNModelConfig", menuName = "MNN/Model Config", order = 1)]
    public class MNNModelConfig : ScriptableObject
    {
        [Header("Model Information")]
        [Tooltip("模型唯一标识")]
        public string modelId;

        [Tooltip("模型显示名称")]
        public string displayName;

        [Tooltip("模型文件名（包含.mnn扩展名）")]
        public string modelFileName;

        [Header("Storage Settings")]
        [Tooltip("模型存储策略")]
        public ModelStorageLocation storageLocation = ModelStorageLocation.StreamingAssets;

        [Tooltip("自定义存储路径（相对路径）")]
        public string customPath = "MNN/Models";

        [Header("Download Settings")]
        [Tooltip("是否在构建时包含模型")]
        public bool includeInBuild = true;

        [Tooltip("远程下载URL（用于热更新）")]
        public string downloadUrl;

        [Tooltip("是否启用热更新")]
        public bool enableHotUpdate = false;

        [Tooltip("模型版本号")]
        public string version = "1.0.0";

        [Header("Platform Specific")]
        [Tooltip("平台特定配置")]
        public PlatformModelConfig[] platformConfigs;

        /// <summary>
        /// 获取模型的完整加载路径
        /// </summary>
        public string GetModelPath()
        {
            return MNNModelPathResolver.GetModelPath(this);
        }

        /// <summary>
        /// 获取模型的持久化存储路径
        /// </summary>
        public string GetPersistentPath()
        {
            return Path.Combine(Application.persistentDataPath, customPath, modelFileName);
        }

        /// <summary>
        /// 获取StreamingAssets路径
        /// </summary>
        public string GetStreamingAssetsPath()
        {
            return Path.Combine(Application.streamingAssetsPath, customPath, modelFileName);
        }

        /// <summary>
        /// 检查模型是否存在
        /// </summary>
        public bool IsModelAvailable()
        {
            var path = GetModelPath();
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        /// <summary>
        /// 获取当前平台配置
        /// </summary>
        public PlatformModelConfig GetPlatformConfig()
        {
            if (platformConfigs == null || platformConfigs.Length == 0)
                return null;

            var currentPlatform = Application.platform;
            foreach (var config in platformConfigs)
            {
                if (config.platform == currentPlatform)
                    return config;
            }

            return null;
        }
    }

    /// <summary>
    /// 模型存储位置
    /// </summary>
    public enum ModelStorageLocation
    {
        /// <summary>
        /// StreamingAssets（随包）
        /// </summary>
        StreamingAssets,

        /// <summary>
        /// PersistentData（可写目录）
        /// </summary>
        PersistentData,

        /// <summary>
        /// Resources（资源文件夹，不推荐大文件）
        /// </summary>
        Resources,

        /// <summary>
        /// 自定义绝对路径
        /// </summary>
        Custom
    }

    /// <summary>
    /// 平台特定模型配置
    /// </summary>
    [Serializable]
    public class PlatformModelConfig
    {
        [Tooltip("目标平台")]
        public RuntimePlatform platform;

        [Tooltip("平台特定的模型文件名")]
        public string platformModelFileName;

        [Tooltip("平台特定的存储位置")]
        public ModelStorageLocation platformStorageLocation;

        [Tooltip("平台特定的下载URL")]
        public string platformDownloadUrl;

        [Tooltip("是否启用GPU加速")]
        public bool enableGPU = true;
    }
}
