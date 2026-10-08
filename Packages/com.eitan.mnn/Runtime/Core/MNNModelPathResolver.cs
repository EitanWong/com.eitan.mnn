using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// MNN模型路径解析器
    /// 根据平台和配置自动解析模型路径
    /// </summary>
    public static class MNNModelPathResolver
    {
        /// <summary>
        /// 解析模型路径
        /// </summary>
        public static string GetModelPath(MNNModelConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[MNN] Model config is null");
                return null;
            }

            // 检查平台特定配置
            var platformConfig = config.GetPlatformConfig();
            var storageLocation = platformConfig?.platformStorageLocation ?? config.storageLocation;
            var modelFileName = platformConfig?.platformModelFileName ?? config.modelFileName;

            string basePath = GetBasePath(storageLocation, config.customPath);
            string fullPath = Path.Combine(basePath, modelFileName);

            // 对于StreamingAssets，不同平台需要不同处理
            if (storageLocation == ModelStorageLocation.StreamingAssets)
            {
                return GetStreamingAssetsPath(config.customPath, modelFileName);
            }

            return fullPath;
        }

        /// <summary>
        /// 获取基础路径
        /// </summary>
        private static string GetBasePath(ModelStorageLocation location, string customPath)
        {
            return location switch
            {
                ModelStorageLocation.StreamingAssets =>
                    Path.Combine(Application.streamingAssetsPath, customPath),

                ModelStorageLocation.PersistentData =>
                    Path.Combine(Application.persistentDataPath, customPath),

                ModelStorageLocation.Resources =>
                    "", // Resources不需要完整路径

                ModelStorageLocation.Custom =>
                    customPath, // 自定义路径直接使用

                _ => Application.streamingAssetsPath
            };
        }

        /// <summary>
        /// 获取StreamingAssets路径（处理平台差异）
        /// </summary>
        private static string GetStreamingAssetsPath(string customPath, string fileName)
        {
            var relativePath = Path.Combine(customPath, fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android需要特殊处理，StreamingAssets在APK内部
            return Path.Combine(Application.streamingAssetsPath, relativePath);
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS的StreamingAssets在Data目录
            return Path.Combine(Application.streamingAssetsPath, relativePath);
#elif UNITY_WEBGL && !UNITY_EDITOR
            // WebGL的StreamingAssets通过HTTP访问
            return Path.Combine(Application.streamingAssetsPath, relativePath);
#else
            // Editor和其他平台
            return Path.Combine(Application.streamingAssetsPath, relativePath);
#endif
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        public static void EnsureDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// 获取推荐的存储位置
        /// </summary>
        public static ModelStorageLocation GetRecommendedStorageLocation()
        {
#if UNITY_ANDROID || UNITY_IOS
            // 移动平台推荐使用StreamingAssets（首次）+ PersistentData（热更新）
            return ModelStorageLocation.StreamingAssets;
#elif UNITY_WEBGL
            // WebGL只能使用StreamingAssets
            return ModelStorageLocation.StreamingAssets;
#else
            // PC平台可以使用任意位置
            return ModelStorageLocation.PersistentData;
#endif
        }
    }
}
