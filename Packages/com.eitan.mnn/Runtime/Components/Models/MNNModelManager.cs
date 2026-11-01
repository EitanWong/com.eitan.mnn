using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// MNN模型管理器（运行时）
    /// 集中管理所有模型的生命周期、路径和状态
    /// </summary>
    [AddComponentMenu("MNN/Models/Model Manager")]
    public class MNNModelManager : MonoBehaviour
    {
        /// <summary>Fetch one catalog page without loading or downloading model weights.</summary>
        public static Task<MNNModelScopePage> FetchModelScopeCatalogPageAsync(int page, CancellationToken token = default) =>
            new MNNModelCatalog().FetchModelScopePageAsync(page, cancellationToken: token);

        /// <summary>Fetch one catalog page, following the previous page's cursor when provided.</summary>
        public static Task<MNNHuggingFacePage> FetchHuggingFaceCatalogPageAsync(string cursor = null, CancellationToken token = default) =>
            new MNNModelCatalog().FetchHuggingFacePageAsync(cursor, cancellationToken: token);

        public static Task<MNNModelScopePage> SearchModelScopeCatalogPageAsync(int stream, int page, string query = null, CancellationToken token = default) =>
            new MNNModelCatalog().FetchModelScopeSearchPageAsync(stream, page, query, cancellationToken: token);

        public static Task<MNNHuggingFacePage> SearchHuggingFaceCatalogPageAsync(int stream, string cursor, string query = null, CancellationToken token = default) =>
            new MNNModelCatalog().FetchHuggingFaceSearchPageAsync(stream, cursor, query, cancellationToken: token);

        private static MNNModelManager _instance;
        /// <summary>
        /// 单例实例
        /// </summary>
        public static MNNModelManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[MNN Model Manager]");
                    _instance = go.AddComponent<MNNModelManager>();
                    DontDestroyOnLoad(go);
                }

                return _instance;
            }
        }

        [Header("Settings")]
        [Tooltip("模型配置列表")]
        public List<MNNModelConfig> managedModels = new List<MNNModelConfig>();
        [Tooltip("启动时自动初始化")]
        public bool initializeOnAwake = true;
        [Tooltip("启用调试日志")]
        public bool enableDebugLog = true;
        // 运行时状态
        private Dictionary<string, ModelRuntimeInfo> _modelInfos = new Dictionary<string, ModelRuntimeInfo>();
        private Dictionary<string, MNNInterpreter> _loadedModels = new Dictionary<string, MNNInterpreter>();
        /// <summary>
        /// 全局事件
        /// </summary>
        public static event Action<string> OnModelRegistered;
        public static event Action<string> OnModelLoaded;
        public static event Action<string> OnModelUnloaded;
        // 移除未使用的事件
        // public static event Action<string, float> OnModelDownloadProgress;
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        /// <summary>
        /// 初始化管理器
        /// </summary>
        public void Initialize()
        {
            Log("Initializing MNN Model Manager...");
            // 注册所有配置的模型
            foreach (var config in managedModels)
            {
                if (config != null)
                {
                    RegisterModel(config);
                }
            }

            Log($"Registered {_modelInfos.Count} models");
        }

        /// <summary>
        /// 注册模型
        /// </summary>
        public void RegisterModel(MNNModelConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.modelId))
            {
                LogError("Invalid model config");
                return;
            }

            if (_modelInfos.ContainsKey(config.modelId))
            {
                LogWarning($"Model already registered: {config.modelId}");
                return;
            }

            var info = new ModelRuntimeInfo{config = config, isAvailable = config.IsModelAvailable(), localPath = config.GetModelPath(), registeredTime = DateTime.Now};
            _modelInfos[config.modelId] = info;
            Log($"Registered model: {config.displayName} ({config.modelId})");
            OnModelRegistered?.Invoke(config.modelId);
        }

        /// <summary>
        /// 加载模型（同步）
        /// </summary>
        public MNNInterpreter LoadModel(string modelId)
        {
            if (!_modelInfos.TryGetValue(modelId, out var info))
            {
                LogError($"Model not registered: {modelId}");
                return null;
            }

            // 检查是否已加载
            if (_loadedModels.TryGetValue(modelId, out var existingInterpreter))
            {
                Log($"Model already loaded: {modelId}");
                return existingInterpreter;
            }

            try
            {
                var path = info.config.GetModelPath();
                if (!File.Exists(path))
                {
                    LogError($"Model file not found: {path}");
                    return null;
                }

                var interpreter = MNNInterpreter.CreateFromFile(path);
                _loadedModels[modelId] = interpreter;
                info.isLoaded = true;
                info.loadedTime = DateTime.Now;
                Log($"Model loaded: {info.config.displayName}");
                OnModelLoaded?.Invoke(modelId);
                return interpreter;
            }
            catch (Exception e)
            {
                LogError($"Failed to load model {modelId}: {e.Message}");
                return null;
            }
        }

        /// <summary>Prepares a model using project delivery settings before native loading. Call on the Unity main thread.</summary>
        public async Task<MNNInterpreter> LoadModelAsync(string modelId, CancellationToken token = default)
        {
            if (!_modelInfos.TryGetValue(modelId, out var info))
                throw new ArgumentException("Model is not registered: " + modelId, nameof(modelId));
            if (_loadedModels.TryGetValue(modelId, out var existing))
                return existing;
            var path = await info.config.PrepareAsync(token: token);
            token.ThrowIfCancellationRequested();
            if (this == null)
                throw new ObjectDisposedException(nameof(MNNModelManager));
            if (_loadedModels.TryGetValue(modelId, out existing))
                return existing;
            var interpreter = MNNInterpreter.CreateFromFile(path);
            _loadedModels[modelId] = interpreter;
            info.localPath = path;
            info.isAvailable = true;
            info.isLoaded = true;
            info.loadedTime = DateTime.Now;
            OnModelLoaded?.Invoke(modelId);
            return interpreter;
        }

        /// <summary>
        /// 卸载模型
        /// </summary>
        public void UnloadModel(string modelId)
        {
            if (!_loadedModels.TryGetValue(modelId, out var interpreter))
            {
                LogWarning($"Model not loaded: {modelId}");
                return;
            }

            interpreter.Dispose();
            _loadedModels.Remove(modelId);
            if (_modelInfos.TryGetValue(modelId, out var info))
            {
                info.isLoaded = false;
            }

            Log($"Model unloaded: {modelId}");
            OnModelUnloaded?.Invoke(modelId);
        }

        /// <summary>
        /// 获取已加载的模型
        /// </summary>
        public MNNInterpreter GetLoadedModel(string modelId)
        {
            return _loadedModels.TryGetValue(modelId, out var interpreter) ? interpreter : null;
        }

        /// <summary>
        /// 检查模型是否已加载
        /// </summary>
        public bool IsModelLoaded(string modelId)
        {
            return _loadedModels.ContainsKey(modelId);
        }

        /// <summary>
        /// 获取模型信息
        /// </summary>
        public ModelRuntimeInfo GetModelInfo(string modelId)
        {
            return _modelInfos.TryGetValue(modelId, out var info) ? info : null;
        }

        /// <summary>
        /// 获取所有注册的模型ID
        /// </summary>
        public string[] GetRegisteredModelIds()
        {
            var ids = new string[_modelInfos.Count];
            _modelInfos.Keys.CopyTo(ids, 0);
            return ids;
        }

        /// <summary>
        /// 获取所有已加载的模型ID
        /// </summary>
        public string[] GetLoadedModelIds()
        {
            var ids = new string[_loadedModels.Count];
            _loadedModels.Keys.CopyTo(ids, 0);
            return ids;
        }

        /// <summary>
        /// 卸载所有模型
        /// </summary>
        public void UnloadAllModels()
        {
            Log("Unloading all models...");
            var modelIds = new List<string>(_loadedModels.Keys);
            foreach (var modelId in modelIds)
            {
                UnloadModel(modelId);
            }
        }

        /// <summary>
        /// 清理未使用的模型文件
        /// </summary>
        public void CleanupUnusedModels()
        {
            Log("Cleaning up unused models...");
            var persistentPath = Path.Combine(Application.persistentDataPath, "MNN/Models");
            if (!Directory.Exists(persistentPath))
                return;
            var files = Directory.GetFiles(persistentPath, "*.mnn");
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                bool isUsed = false;
                foreach (var info in _modelInfos.Values)
                {
                    if (info.config.modelFileName == fileName)
                    {
                        isUsed = true;
                        break;
                    }
                }

                if (!isUsed)
                {
                    try
                    {
                        File.Delete(file);
                        Log($"Deleted unused model: {fileName}");
                    }
                    catch (Exception e)
                    {
                        LogError($"Failed to delete {fileName}: {e.Message}");
                    }
                }
            }
        }

#region Logging
        private void Log(string message)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[MNN Manager] {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning($"[MNN Manager] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[MNN Manager] {message}");
        }

#endregion
        private void OnDestroy()
        {
            UnloadAllModels();
        }

        private void OnApplicationQuit()
        {
            UnloadAllModels();
        }
    }
}
