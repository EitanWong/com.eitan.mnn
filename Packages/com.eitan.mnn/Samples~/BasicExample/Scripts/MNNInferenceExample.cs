using UnityEngine;
using MNN.Unity;

namespace MNN.Examples
{
    /// <summary>
    /// MNN推理示例
    /// </summary>
    public class MNNInferenceExample : MonoBehaviour
    {
        [Header("Model Configuration")]
        [SerializeField]
        private string modelPath = "model.mnn";
        [SerializeField]
        private Texture2D inputTexture;
        private MNNInterpreter _interpreter;
        private MNNSession _session;
        private bool _initialized;
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
                Debug.Log($"✓ Model loaded from {modelPath}");
                // 2. 创建会话（自动选择最佳后端）
                var config = MNNSessionConfig.CreateForCurrentPlatform();
                _session = _interpreter.CreateSession(config);
                Debug.Log($"✓ Session created with {config.BackendType} backend");
                // 3. 释放模型数据以节省内存
                _interpreter.ReleaseModel();
                _initialized = true;
                Debug.Log("✓ MNN initialization complete");
            }
            catch (MNNException ex)
            {
                Debug.LogError($"✗ MNN initialization failed: {ex.Message}");
                _initialized = false;
            }
        }

        void Update()
        {
            if (_initialized && Input.GetKeyDown(KeyCode.Space))
            {
                RunInference();
            }
        }

        void RunInference()
        {
            try
            {
                // 1. 获取输入张量
                var input = _session.GetInput();
                Debug.Log($"Input: {input}");
                // 2. 方法1：使用零拷贝写入（推荐）
                var inputSpan = input.MapForWrite<float>();
                for (int i = 0; i < inputSpan.Length; i++)
                {
                    inputSpan[i] = Random.Range(0f, 1f);
                }

                input.Unmap();
                // 方法2：从数组拷贝
                // var data = new float[input.ElementCount];
                // input.CopyFromArray(data);
                // 方法3：从Texture拷贝
                // if (inputTexture != null)
                // {
                //     input.CopyFromTexture(inputTexture, normalize: true);
                // }
                // 3. 运行推理
                var startTime = Time.realtimeSinceStartup;
                _session.Run();
                var inferenceTime = (Time.realtimeSinceStartup - startTime) * 1000f;
                // 4. 读取输出
                var output = _session.GetOutput();
                Debug.Log($"Output: {output}");
                // 方法1：使用零拷贝读取（推荐）
                var outputSpan = output.MapForRead<float>();
                Debug.Log($"First output value: {outputSpan[0]}");
                output.Unmap();
                // 方法2：拷贝到数组
                // var results = output.CopyToArray<float>();
                Debug.Log($"✓ Inference completed in {inferenceTime:F2}ms");
            }
            catch (MNNException ex)
            {
                Debug.LogError($"✗ Inference failed: {ex.Message}");
            }
        }

        void OnDestroy()
        {
            // 清理资源
            _session?.Dispose();
            _interpreter?.Dispose();
            Debug.Log("✓ MNN resources released");
        }

        void OnGUI()
        {
            if (!_initialized)
            {
                GUI.Label(new Rect(10, 10, 300, 30), "MNN not initialized");
                return;
            }

            GUI.Label(new Rect(10, 10, 300, 30), "Press SPACE to run inference");
            if (_session != null)
            {
                GUI.Label(new Rect(10, 40, 400, 30), $"Backend: {_session.Config.BackendType}");
            }
        }
    }
}
