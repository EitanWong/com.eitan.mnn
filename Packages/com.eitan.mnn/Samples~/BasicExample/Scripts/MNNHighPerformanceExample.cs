using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using MNN.Unity;
using MNN.Unity.Performance;

/// <summary>
/// MNN高性能推理示例 - 使用Burst + Job System
/// </summary>
public class MNNHighPerformanceExample : MonoBehaviour
{
    [Header("Model Configuration")]
    [SerializeField] private string modelPath = "model.mnn";
    [SerializeField] private Texture2D inputTexture;
    [SerializeField] private Texture2D outputTexture;

    private MNNInterpreter _interpreter;
    private MNNSession _session;
    private NativeArray<float> _inputBuffer;
    private NativeArray<float> _outputBuffer;
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

            // 2. 创建高性能配置
            var config = MNNSessionConfig.CreateHighPerformance();
            _session = _interpreter.CreateSession(config);

            // 3. 分配原生缓冲区（复用，避免GC）
            var input = _session.GetInput();
            var output = _session.GetOutput();

            _inputBuffer = new NativeArray<float>(input.ElementCount, Allocator.Persistent);
            _outputBuffer = new NativeArray<float>(output.ElementCount, Allocator.Persistent);

            // 4. 释放模型数据
            _interpreter.ReleaseModel();

            _initialized = true;
            Debug.Log($"✓ High-performance MNN initialized with {config.BackendType} backend");
        }
        catch (MNNException ex)
        {
            Debug.LogError($"✗ Initialization failed: {ex.Message}");
        }
    }

    void Update()
    {
        if (_initialized && Input.GetKeyDown(KeyCode.Space))
        {
            RunInferenceAsync();
        }
    }

    async void RunInferenceAsync()
    {
        if (inputTexture == null)
        {
            Debug.LogWarning("Input texture not set");
            return;
        }

        var startTime = Time.realtimeSinceStartup;

        try
        {
            // 1. 预处理（Burst优化）- 在后台线程执行
            var input = _session.GetInput();
            JobHandle preprocessHandle;

            if (input.DimensionType == MNNDimensionType.Caffe)
            {
                // NCHW格式
                preprocessHandle = MNNTensorOps.TextureToNCHW(
                    inputTexture,
                    _inputBuffer,
                    normalize: true);
            }
            else
            {
                // NHWC格式
                preprocessHandle = MNNTensorOps.TextureToNHWC(
                    inputTexture,
                    _inputBuffer,
                    normalize: true);
            }

            // 2. 等待预处理完成
            preprocessHandle.Complete();
            var preprocessTime = (Time.realtimeSinceStartup - startTime) * 1000f;

            // 3. 拷贝到MNN张量（零拷贝视图）
            input.CopyFromNativeArray(_inputBuffer);

            // 4. 异步推理
            var inferenceStart = Time.realtimeSinceStartup;
            await _session.RunAsync();
            var inferenceTime = (Time.realtimeSinceStartup - inferenceStart) * 1000f;

            // 5. 读取输出
            var output = _session.GetOutput();
            output.CopyToNativeArray(_outputBuffer);

            // 6. 后处理（Burst优化）
            if (outputTexture != null)
            {
                var postprocessStart = Time.realtimeSinceStartup;
                JobHandle postprocessHandle;

                if (output.DimensionType == MNNDimensionType.Caffe)
                {
                    postprocessHandle = MNNTensorOps.NCHWToTexture(
                        _outputBuffer,
                        outputTexture,
                        denormalize: true);
                }
                else
                {
                    postprocessHandle = MNNTensorOps.NHWCToTexture(
                        _outputBuffer,
                        outputTexture,
                        denormalize: true);
                }

                postprocessHandle.Complete();
                outputTexture.Apply();
                var postprocessTime = (Time.realtimeSinceStartup - postprocessStart) * 1000f;

                var totalTime = (Time.realtimeSinceStartup - startTime) * 1000f;
                Debug.Log($"✓ Inference complete:\n" +
                         $"  Preprocess: {preprocessTime:F2}ms (Burst)\n" +
                         $"  Inference: {inferenceTime:F2}ms (MNN)\n" +
                         $"  Postprocess: {postprocessTime:F2}ms (Burst)\n" +
                         $"  Total: {totalTime:F2}ms");
            }
        }
        catch (MNNException ex)
        {
            Debug.LogError($"✗ Inference failed: {ex.Message}");
        }
    }

    void OnDestroy()
    {
        // 清理原生缓冲区
        if (_inputBuffer.IsCreated)
            _inputBuffer.Dispose();

        if (_outputBuffer.IsCreated)
            _outputBuffer.Dispose();

        // 清理MNN资源
        _session?.Dispose();
        _interpreter?.Dispose();

        Debug.Log("✓ Resources released");
    }

    void OnGUI()
    {
        if (!_initialized)
        {
            GUI.Label(new Rect(10, 10, 300, 30), "MNN not initialized");
            return;
        }

        GUI.Label(new Rect(10, 10, 400, 30), "Press SPACE for high-performance inference");
        GUI.Label(new Rect(10, 40, 400, 30), $"Backend: {_session.Config.BackendType}");
        GUI.Label(new Rect(10, 70, 400, 30), "Using Burst + Job System optimization");
    }
}
