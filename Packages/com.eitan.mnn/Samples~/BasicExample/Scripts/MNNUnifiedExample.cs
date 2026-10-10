using UnityEngine;
using MNN.Unity;

namespace MNN.Examples
{
    /// <summary>
    /// MNN推理示例 - 统一API，自动优化
    /// 无需关心底层是否有Burst/Collections，自动选择最优实现
    /// </summary>
    public class MNNUnifiedExample : MonoBehaviour
    {
        [Header("Model Configuration")]
        [SerializeField]
        private string modelPath = "model.mnn";
        [SerializeField]
        private Texture2D inputTexture;
        [SerializeField]
        private Texture2D outputTexture;
        private MNNInterpreter _interpreter;
        private MNNSession _session;
        void Start()
        {
            InitializeModel();
        }

        void InitializeModel()
        {
            try
            {
                // 1. 加载模型
                _interpreter = MNNInterpreter.CreateFromFile(modelPath);
                // 2. 创建会话（自动选择最佳后端）
                _session = _interpreter.CreateSession();
                // 3. 释放模型数据节省内存
                _interpreter.ReleaseModel();
                Debug.Log("✓ MNN initialized successfully");
                LogPerformanceMode();
            }
            catch (MNNException ex)
            {
                Debug.LogError($"✗ Initialization failed: {ex.Message}");
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                RunInference();
            }
        }

        void RunInference()
        {
            if (inputTexture == null)
            {
                Debug.LogWarning("Input texture not set");
                return;
            }

            var startTime = Time.realtimeSinceStartup;
            try
            {
                var input = _session.GetInput();
                var output = _session.GetOutput();
                // 统一API - 自动选择最优实现
                // 如果有Burst: 使用Burst优化的Job
                // 如果没有: 使用高性能托管实现
                input.CopyFromTexture(inputTexture, normalize: true);
                // 可选：归一化（自动优化）
                // input.Normalize(mean: 0.5f, std: 0.5f);
                // 运行推理
                _session.Run();
                // 读取输出（自动优化）
                if (outputTexture != null)
                {
                    output.CopyToTexture(outputTexture, denormalize: true);
                }

                var totalTime = (Time.realtimeSinceStartup - startTime) * 1000f;
                Debug.Log($"✓ Inference completed in {totalTime:F2}ms");
            }
            catch (MNNException ex)
            {
                Debug.LogError($"✗ Inference failed: {ex.Message}");
            }
        }

        void LogPerformanceMode()
        {
#if UNITY_COLLECTIONS && UNITY_BURST
            Debug.Log("[MNN] Performance Mode: Burst + Collections (Optimized)");
#else
            Debug.Log("[MNN] Performance Mode: High-performance managed (No external dependencies)");
#endif
            Debug.Log($"[MNN] Backend: {_session.Config.BackendType}");
            Debug.Log("[MNN] API: Unified - automatically selects best implementation");
        }

        void OnDestroy()
        {
            _session?.Dispose();
            _interpreter?.Dispose();
        }

        void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 500, 30), "Press SPACE to run inference");
            if (_session != null)
            {
                GUI.Label(new Rect(10, 40, 500, 30), $"Backend: {_session.Config.BackendType}");
#if UNITY_COLLECTIONS && UNITY_BURST
                GUI.Label(new Rect(10, 70, 500, 30), "Mode: Burst Optimized");
#else
                GUI.Label(new Rect(10, 70, 500, 30), "Mode: Managed (No Burst)");
#endif
            }
        }
    }
}
