using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        [Tooltip("StreamingAssets: 候选随包目录，是否加入构建由 Project Settings > MNN 决定；PersistentData: 设备可写目录")]
        public ModelStorageLocation storageLocation = ModelStorageLocation.StreamingAssets;
        [Tooltip("相对目录；StreamingAssets 模式对应 Assets/StreamingAssets，下行时会写入 Application.persistentDataPath")]
        public string customPath = "MNN/Models";
        [Header("Download Settings")]
        [HideInInspector]
        public bool includeInBuild = true;
        [Tooltip("运行时加载器在模型文件缺失且启用热更新时使用的下载地址；平台配置可覆盖此地址")]
        public string downloadUrl;
        [Tooltip("旧版单文件加载流程的下载开关。使用项目交付设置时，由 Project Settings > MNN 的清单与下载策略决定")]
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

        /// <summary>Prepares a declared build model for native file access; call from Unity's main thread.</summary>
        public async Task<string> PrepareAsync(Action<long, long> progress = null, CancellationToken token = default)
        {
            var location = GetEffectiveStorageLocation();
            if (customPath?.Replace('\\', '/').TrimEnd('/') != MNNModelDeliveryManifest.ModelsPath || location != ModelStorageLocation.StreamingAssets && location != ModelStorageLocation.PersistentData)
                throw new InvalidOperationException("Project model delivery requires MNN/Models with StreamingAssets or PersistentData storage.");
            var file = GetEffectiveModelFileName();
            var delivery = await MNNModelDelivery.LoadAsync(token);
            return await delivery.PrepareFileAsync(file, progress, token, location == ModelStorageLocation.PersistentData);
        }

        /// <summary>
        /// 获取模型的持久化存储路径
        /// </summary>
        public string GetPersistentPath()
        {
            return Path.Combine(Application.persistentDataPath, customPath, GetEffectiveModelFileName());
        }

        /// <summary>
        /// 获取StreamingAssets路径
        /// </summary>
        public string GetStreamingAssetsPath()
        {
            return Path.Combine(Application.streamingAssetsPath, customPath, GetEffectiveModelFileName());
        }

        public string GetEffectiveModelFileName()
        {
            var platformFileName = GetPlatformConfig()?.platformModelFileName;
            return string.IsNullOrEmpty(platformFileName) ? modelFileName : platformFileName;
        }

        public ModelStorageLocation GetEffectiveStorageLocation()
        {
            var platformConfig = GetPlatformConfig();
            return platformConfig != null ? platformConfig.platformStorageLocation : storageLocation;
        }

        public string GetEffectiveDownloadUrl()
        {
            var platformUrl = GetPlatformConfig()?.platformDownloadUrl;
            return string.IsNullOrEmpty(platformUrl) ? downloadUrl : platformUrl;
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
                if (config != null && config.platform == currentPlatform)
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
}
