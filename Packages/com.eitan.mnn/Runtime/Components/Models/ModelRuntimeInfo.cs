using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// 模型运行时信息
    /// </summary>
    [Serializable]
    public class ModelRuntimeInfo
    {
        public MNNModelConfig config;
        public bool isAvailable;
        public bool isLoaded;
        public string localPath;
        public DateTime registeredTime;
        public DateTime loadedTime;
        public long fileSize;
        public string GetStatusString()
        {
            if (isLoaded)
                return "Loaded";
            if (isAvailable)
                return "Available";
            return "Not Available";
        }
    }
}
