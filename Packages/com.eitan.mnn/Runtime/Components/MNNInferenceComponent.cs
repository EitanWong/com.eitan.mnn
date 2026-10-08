using System;
using System.Collections;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// MNN推理组件 - 快速上手版
    /// 封装了完整的推理流程，开发者无需关心细节
    /// </summary>
    [RequireComponent(typeof(MNNModelLoader))]
    public class MNNInferenceComponent : MonoBehaviour
    {
        [Header("Input/Output")]
        [Tooltip("输入Texture（自动转换）")]
        public Texture2D inputTexture;

        [Tooltip("输出Texture（自动转换）")]
        public Texture2D outputTexture;

        [Header("Session Settings")]
        [Tooltip("会话配置（留空使用默认）")]
        public MNNSessionConfig sessionConfig;

        [Tooltip("自动创建会话")]
        public bool autoCreateSession = true;

        [Header("Inference Settings")]
        [Tooltip("输入归一化")]
        public bool normalizeInput = true;

        [Tooltip("输出反归一化")]
        public bool denormalizeOutput = true;

        [Header("Events")]
        public InferenceEvent onInferenceStart;
        public InferenceCompleteEvent onInferenceComplete;
        public InferenceErrorEvent onInferenceError;

        // 内部状态
        private MNNModelLoader _modelLoader;
        private MNNSession _session;
        private bool _isReady;
        private bool _isInferring;

        /// <summary>
        /// 是否准备好推理
        /// </summary>
        public bool IsReady => _isReady;

        /// <summary>
        /// 是否正在推理
        /// </summary>
        public bool IsInferring => _isInferring;

        /// <summary>
        /// 当前会话
        /// </summary>
        public MNNSession Session => _session;

        private void Awake()
        {
            _modelLoader = GetComponent<MNNModelLoader>();

            // 监听模型加载完成
            _modelLoader.onLoadComplete.AddListener(OnModelLoaded);
            _modelLoader.onLoadError.AddListener(OnModelLoadError);
        }

        private void OnModelLoaded(string path, MNNInterpreter interpreter)
        {
            if (autoCreateSession)
            {
                CreateSession(interpreter);
            }
        }

        /// <summary>
        /// 创建推理会话
        /// </summary>
        private void CreateSession(MNNInterpreter interpreter)
        {
            try
            {
                // 使用配置或默认配置
                var config = sessionConfig ?? MNNSessionConfig.CreateForCurrentPlatform();
                _session = interpreter.CreateSession(config);
                _isReady = true;

                Debug.Log($"[MNN] Inference session created. Backend: {config.BackendType}");
            }
            catch (Exception e)
            {
                OnError($"Failed to create session: {e.Message}");
            }
        }

        /// <summary>
        /// 运行推理（同步）
        /// </summary>
        public void RunInference()
        {
            if (!_isReady)
            {
                OnError("Session not ready. Ensure model is loaded.");
                return;
            }

            if (_isInferring)
            {
                Debug.LogWarning("[MNN] Already inferring, skipping...");
                return;
            }

            if (inputTexture == null)
            {
                OnError("Input texture is null");
                return;
            }

            try
            {
                _isInferring = true;
                onInferenceStart?.Invoke();

                var startTime = Time.realtimeSinceStartup;

                // 1. 获取输入张量
                var input = _session.GetInput();

                // 2. 填充输入数据（使用扩展方法，自动优化）
                input.CopyFromTexture(inputTexture, normalizeInput);

                // 3. 运行推理
                _session.Run();

                // 4. 获取输出张量
                var output = _session.GetOutput();

                // 5. 转换输出（如果有输出纹理）
                if (outputTexture != null)
                {
                    output.CopyToTexture(outputTexture, denormalizeOutput);
                }

                var inferenceTime = (Time.realtimeSinceStartup - startTime) * 1000f;

                Debug.Log($"[MNN] Inference completed in {inferenceTime:F2}ms");
                onInferenceComplete?.Invoke(output, inferenceTime);
            }
            catch (Exception e)
            {
                OnError($"Inference failed: {e.Message}");
            }
            finally
            {
                _isInferring = false;
            }
        }

        /// <summary>
        /// 运行推理（异步）
        /// </summary>
        public IEnumerator RunInferenceAsync()
        {
            if (!_isReady)
            {
                OnError("Session not ready");
                yield break;
            }

            if (_isInferring)
            {
                yield break;
            }

            _isInferring = true;
            onInferenceStart?.Invoke();

            Exception inferenceException = null;
            MNNTensor output = null;

            // 异步执行
            var task = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var input = _session.GetInput();
                    input.CopyFromTexture(inputTexture, normalizeInput);
                    await _session.RunAsync();
                    output = _session.GetOutput();
                }
                catch (Exception e)
                {
                    inferenceException = e;
                }
            });

            // 等待完成
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (inferenceException != null)
            {
                OnError($"Async inference failed: {inferenceException.Message}");
            }
            else if (output != null)
            {
                if (outputTexture != null)
                {
                    output.CopyToTexture(outputTexture, denormalizeOutput);
                }
                onInferenceComplete?.Invoke(output, 0f);
            }

            _isInferring = false;
        }

        /// <summary>
        /// 手动设置输入
        /// </summary>
        public void SetInput(Texture2D texture)
        {
            inputTexture = texture;
        }

        /// <summary>
        /// 获取输入张量（专业用户）
        /// </summary>
        public MNNTensor GetInputTensor()
        {
            return _session?.GetInput();
        }

        /// <summary>
        /// 获取输出张量（专业用户）
        /// </summary>
        public MNNTensor GetOutputTensor()
        {
            return _session?.GetOutput();
        }

        private void OnModelLoadError(string error)
        {
            OnError($"Model load failed: {error}");
        }

        private void OnError(string message)
        {
            Debug.LogError($"[MNN] {message}");
            onInferenceError?.Invoke(message);
        }

        private void OnDestroy()
        {
            _session?.Dispose();
        }
    }

    #region Events

    [Serializable]
    public class InferenceEvent : UnityEngine.Events.UnityEvent { }

    [Serializable]
    public class InferenceCompleteEvent : UnityEngine.Events.UnityEvent<MNNTensor, float> { }

    [Serializable]
    public class InferenceErrorEvent : UnityEngine.Events.UnityEvent<string> { }

    #endregion
}
