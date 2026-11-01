using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
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
