using System;
using System.Collections;
using System.IO;
using UnityEngine;

#if UNITY_2018_1_OR_NEWER
using UnityEngine.Networking;
#endif

namespace MNN.Unity
{
    /// <summary>
    /// MNN模型加载器（运行时）
    /// 提供模型下载、缓存和加载功能
    /// 健壮性设计：即使缺少UnityWebRequest也能基本工作
    /// </summary>
    public class MNNModelLoader : MonoBehaviour
    {
        [Header("Model Configuration")]
        [Tooltip("模型配置资源")]
        public MNNModelConfig modelConfig;

        [Header("Loading Options")]
        [Tooltip("启动时自动加载")]
        public bool loadOnStart = true;

        [Tooltip("优先使用本地缓存")]
        public bool preferLocalCache = true;

        [Tooltip("下载超时时间（秒）")]
        public int downloadTimeout = 300;

        [Header("Events")]
        public ModelLoadEvent onLoadStart;
        public ModelLoadProgressEvent onLoadProgress;
        public ModelLoadCompleteEvent onLoadComplete;
        public ModelLoadErrorEvent onLoadError;

        // 状态
        private bool _isLoading;
        private bool _isLoaded;
        private string _loadedModelPath;
        private MNNInterpreter _interpreter;

        // 功能检测
        private static bool? _hasUnityWebRequest;

        /// <summary>
        /// 检测UnityWebRequest是否可用
        /// </summary>
        private static bool HasUnityWebRequest
        {
            get
            {
                if (!_hasUnityWebRequest.HasValue)
                {
#if UNITY_2018_1_OR_NEWER
                    _hasUnityWebRequest = true;
#else
                    _hasUnityWebRequest = false;
#endif
                }
                return _hasUnityWebRequest.Value;
            }
        }

        public bool IsLoaded => _isLoaded;
        public string LoadedModelPath => _loadedModelPath;
        public MNNInterpreter Interpreter => _interpreter;

        private void Start()
        {
            if (loadOnStart && modelConfig != null)
            {
                StartCoroutine(LoadModelAsync());
            }
        }

        /// <summary>
        /// 异步加载模型
        /// </summary>
        public IEnumerator LoadModelAsync()
        {
            if (_isLoading)
            {
                LogWarning("Model is already loading");
                yield break;
            }

            if (modelConfig == null)
            {
                OnError("Model config is null");
                yield break;
            }

            _isLoading = true;
            onLoadStart?.Invoke(modelConfig.displayName);

            // 1. 确定模型路径
            string modelPath = DetermineModelPath();

            // 2. 检查是否需要下载
            if (!File.Exists(modelPath))
            {
                bool downloadSuccess = false;

                // 尝试下载（如果支持）
                if (modelConfig.enableHotUpdate && !string.IsNullOrEmpty(modelConfig.downloadUrl))
                {
                    if (HasUnityWebRequest)
                    {
                        yield return DownloadModelAsync(modelConfig.downloadUrl, modelPath);
                        downloadSuccess = File.Exists(modelPath);
                    }
                    else
                    {
                        LogWarning("UnityWebRequest not available, cannot download model");
                    }
                }

                // 如果下载失败，尝试从StreamingAssets复制
                if (!downloadSuccess && modelConfig.storageLocation == ModelStorageLocation.StreamingAssets)
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    yield return CopyFromStreamingAssetsAsync(
                        modelConfig.GetStreamingAssetsPath(),
                        modelPath
                    );
#endif
                }

                // 最终检查
                if (!File.Exists(modelPath))
                {
                    OnError($"Model file not found and cannot be downloaded: {modelPath}");
                    _isLoading = false;
                    yield break;
                }
            }

            // 3. 加载模型
            yield return LoadModelFromFile(modelPath);

            _isLoading = false;
        }

        /// <summary>
        /// 确定模型路径
        /// </summary>
        private string DetermineModelPath()
        {
            // 优先使用缓存路径
            if (preferLocalCache)
            {
                var persistentPath = modelConfig.GetPersistentPath();
                if (File.Exists(persistentPath))
                {
                    Log($"Using cached model: {persistentPath}");
                    return persistentPath;
                }
            }

            // 使用配置的路径
            var configPath = modelConfig.GetModelPath();
            if (File.Exists(configPath))
            {
                return configPath;
            }

            // 如果启用热更新，返回持久化路径（准备下载）
            if (modelConfig.enableHotUpdate)
            {
                return modelConfig.GetPersistentPath();
            }

            // 默认返回配置路径
            return configPath;
        }

        /// <summary>
        /// 从文件加载模型
        /// </summary>
        private IEnumerator LoadModelFromFile(string path)
        {
            Log($"Loading model from: {path}");

            MNNInterpreter interpreter = null;
            Exception loadException = null;

            // 使用ThreadPool异步加载
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    interpreter = MNNInterpreter.CreateFromFile(path);
                }
                catch (Exception e)
                {
                    loadException = e;
                }
            });

            // 等待加载完成
            while (interpreter == null && loadException == null)
            {
                yield return null;
            }

            if (loadException != null)
            {
                OnError($"Failed to load model: {loadException.Message}");
                yield break;
            }

            _interpreter = interpreter;
            _loadedModelPath = path;
            _isLoaded = true;

            Log($"Model loaded successfully");
            onLoadComplete?.Invoke(path, _interpreter);
        }

        /// <summary>
        /// 下载模型（需要UnityWebRequest）
        /// </summary>
        private IEnumerator DownloadModelAsync(string url, string savePath)
        {
#if UNITY_2018_1_OR_NEWER
            Log($"Downloading model from: {url}");

            MNNModelPathResolver.EnsureDirectory(savePath);

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = downloadTimeout;
                request.downloadHandler = new DownloadHandlerFile(savePath);

                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    onLoadProgress?.Invoke(operation.progress);
                    yield return null;
                }

#if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    LogError($"Download failed: {request.error}");
                    yield break;
                }

                Log($"Model downloaded successfully");
            }
#else
            LogError("UnityWebRequest not available, cannot download");
            yield break;
#endif
        }

        /// <summary>
        /// 从StreamingAssets复制（Android/iOS）
        /// </summary>
        private IEnumerator CopyFromStreamingAssetsAsync(string sourcePath, string destPath)
        {
#if UNITY_2018_1_OR_NEWER
            Log($"Copying from StreamingAssets: {sourcePath}");

            MNNModelPathResolver.EnsureDirectory(destPath);

            using (var request = UnityWebRequest.Get(sourcePath))
            {
                yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    LogError($"Failed to copy: {request.error}");
                    yield break;
                }

                try
                {
                    File.WriteAllBytes(destPath, request.downloadHandler.data);
                    Log($"Copied successfully to: {destPath}");
                }
                catch (Exception e)
                {
                    LogError($"Failed to write file: {e.Message}");
                }
            }
#else
            // 降级方案：直接文件复制（仅在Editor和某些平台可用）
            try
            {
                if (File.Exists(sourcePath))
                {
                    MNNModelPathResolver.EnsureDirectory(destPath);
                    File.Copy(sourcePath, destPath, true);
                    Log($"Copied successfully (fallback)");
                }
                else
                {
                    LogError($"Source file not found: {sourcePath}");
                }
            }
            catch (Exception e)
            {
                LogError($"Failed to copy file: {e.Message}");
            }
            yield break;
#endif
        }

        /// <summary>
        /// 手动加载模型
        /// </summary>
        public void LoadModel()
        {
            StartCoroutine(LoadModelAsync());
        }

        /// <summary>
        /// 卸载模型
        /// </summary>
        public void UnloadModel()
        {
            if (_interpreter != null)
            {
                try
                {
                    _interpreter.Dispose();
                }
                catch (Exception e)
                {
                    LogError($"Error disposing interpreter: {e.Message}");
                }
                finally
                {
                    _interpreter = null;
                }
            }

            _isLoaded = false;
            _loadedModelPath = null;
        }

        #region Logging

        private void Log(string message)
        {
            Debug.Log($"[MNN Loader] {message}");
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[MNN Loader] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[MNN Loader] {message}");
        }

        private void OnError(string message)
        {
            LogError(message);
            onLoadError?.Invoke(message);
        }

        #endregion

        private void OnDestroy()
        {
            UnloadModel();
        }
    }

    #region Events

    [Serializable]
    public class ModelLoadEvent : UnityEngine.Events.UnityEvent<string> { }

    [Serializable]
    public class ModelLoadProgressEvent : UnityEngine.Events.UnityEvent<float> { }

    [Serializable]
    public class ModelLoadCompleteEvent : UnityEngine.Events.UnityEvent<string, MNNInterpreter> { }

    [Serializable]
    public class ModelLoadErrorEvent : UnityEngine.Events.UnityEvent<string> { }

    #endregion
}
