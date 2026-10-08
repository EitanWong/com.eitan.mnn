# MNN Model Management System - 使用指南

## 📦 系统架构

### 三层API设计

```
┌─────────────────────────────────────────────────┐
│  高层API（新手友好）                              │
│  - MNNInferenceComponent（MonoBehaviour）        │
│  - 一键式推理，无需关心细节                        │
└─────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│  中层API（平衡）                                  │
│  - MNNModelLoader（模型加载）                     │
│  - MNNModelManager（模型管理）                    │
│  - 自动化流程 + 部分控制权                        │
└─────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│  底层API（专业控制）                              │
│  - MNNInterpreter                                │
│  - MNNSession                                    │
│  - MNNTensor                                     │
│  - 完全控制，最大灵活性                           │
└─────────────────────────────────────────────────┘
```

---

## 🚀 快速开始（新手）

### 方法1：使用InferenceComponent（最简单）

```csharp
// 1. 在GameObject上添加MNNInferenceComponent
// 2. 在Inspector中:
//    - 设置Model Config
//    - 拖入Input Texture
//    - 拖入Output Texture
// 3. 调用RunInference()

public class MyAI : MonoBehaviour
{
    public MNNInferenceComponent inference;
    public Texture2D input;
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            inference.SetInput(input);
            inference.RunInference();
        }
    }
}
```

**特点**：
- ✅ 零代码配置
- ✅ 自动模型加载
- ✅ 自动数据转换
- ✅ 适合快速原型

---

## 🎯 中级使用（有一定控制）

### 方法2：使用ModelLoader

```csharp
public class MyModelController : MonoBehaviour
{
    public MNNModelLoader modelLoader;
    private MNNSession _session;
    
    void Start()
    {
        // 监听加载完成
        modelLoader.onLoadComplete.AddListener(OnModelLoaded);
        
        // 手动加载（或设置loadOnStart=true自动加载）
        modelLoader.LoadModel();
    }
    
    void OnModelLoaded(string path, MNNInterpreter interpreter)
    {
        // 创建会话（可自定义配置）
        var config = MNNSessionConfig.CreateForCurrentPlatform();
        _session = interpreter.CreateSession(config);
        
        Debug.Log($"Model ready! Backend: {config.BackendType}");
    }
    
    void RunCustomInference(float[] inputData)
    {
        var input = _session.GetInput();
        
        // 方式1: 直接写入数据
        input.CopyFromArray(inputData);
        
        // 方式2: 零拷贝写入
        var span = input.MapForWrite<float>();
        for (int i = 0; i < span.Length; i++)
        {
            span[i] = inputData[i];
        }
        input.Unmap();
        
        _session.Run();
        
        var output = _session.GetOutput();
        var results = output.CopyToArray<float>();
    }
}
```

**特点**：
- ✅ 可自定义会话配置
- ✅ 可控制数据流
- ✅ 事件驱动
- ✅ 适合大多数场景

---

## 💎 专业使用（完全控制）

### 方法3：完全手动管理

```csharp
public class AdvancedMLController : MonoBehaviour
{
    private MNNInterpreter _interpreter;
    private MNNSession _session;
    private string _modelPath;
    
    void Start()
    {
        // 1. 完全自定义路径
        _modelPath = DetermineModelPath();
        
        // 2. 手动加载模型
        _interpreter = MNNInterpreter.CreateFromFile(_modelPath);
        
        // 3. 自定义会话配置
        var config = new MNNSessionConfig
        {
            BackendType = MNNBackendType.Metal,
            NumThreads = 4,
            MemoryMode = MNNMemoryMode.Normal,
            PowerMode = MNNPowerMode.High,
            PrecisionMode = MNNPrecisionMode.Low
        };
        
        _session = _interpreter.CreateSession(config);
        
        // 4. 释放模型数据（节省内存）
        _interpreter.ReleaseModel();
    }
    
    string DetermineModelPath()
    {
        // 完全自定义逻辑
#if UNITY_EDITOR
        return "Assets/Models/model.mnn";
#elif UNITY_ANDROID
        return Path.Combine(Application.persistentDataPath, "model.mnn");
#elif UNITY_IOS
        return Path.Combine(Application.streamingAssetsPath, "model.mnn");
#else
        return "model.mnn";
#endif
    }
    
    void CustomInference()
    {
        var input = _session.GetInput("custom_input_name");
        
        // 零拷贝 + 自定义处理
        unsafe
        {
            var span = input.MapForWrite<float>();
            
            // 使用Burst优化的处理
            ProcessData(span);
            
            input.Unmap();
        }
        
        // 异步推理
        var task = _session.RunAsync();
        task.ContinueWith(_ =>
        {
            var output = _session.GetOutput("custom_output_name");
            HandleResults(output);
        });
    }
}
```

**特点**：
- ✅ 完全控制所有细节
- ✅ 最大性能优化空间
- ✅ 适合特殊需求
- ✅ 适合专业开发者

---

## 📂 模型路径管理

### 配置模型（推荐方式）

```csharp
// 1. 创建ModelConfig资源
// 右键 → Create → MNN → Model Config

// 2. 配置存储位置
public enum ModelStorageLocation
{
    StreamingAssets,  // 随包（只读）
    PersistentData,   // 可写目录
    Resources,        // Resources文件夹
    Custom           // 自定义路径
}
```

### 平台特定配置

```csharp
// 在ModelConfig中设置Platform Configs
[Serializable]
public class PlatformModelConfig
{
    public RuntimePlatform platform;          // iOS, Android, etc.
    public string platformModelFileName;      // 平台特定的模型
    public ModelStorageLocation location;     // 平台特定的位置
    public string platformDownloadUrl;        // 平台特定的下载URL
    public bool enableGPU;                   // 是否启用GPU
}
```

### 路径解析示例

```csharp
// 自动解析路径（处理所有平台差异）
string path = MNNModelPathResolver.GetModelPath(config);

// 或者使用ModelConfig的方法
string path = modelConfig.GetModelPath();

// 检查模型是否可用
bool available = modelConfig.IsModelAvailable();

// 获取推荐的存储位置
var recommended = MNNModelPathResolver.GetRecommendedStorageLocation();
```

---

## 🌐 热更新和下载

### 启用热更新

```csharp
// 在ModelConfig中设置
config.enableHotUpdate = true;
config.downloadUrl = "https://your-server.com/model.mnn";
config.version = "1.0.1";

// ModelLoader会自动:
// 1. 检查本地是否有缓存
// 2. 如果没有，从URL下载
// 3. 保存到PersistentData
// 4. 加载模型
```

### 手动控制下载

```csharp
public class ModelDownloader : MonoBehaviour
{
    public MNNModelConfig config;
    
    IEnumerator DownloadAndUpdate()
    {
        string url = config.downloadUrl;
        string savePath = config.GetPersistentPath();
        
        using (var request = UnityWebRequest.Get(url))
        {
            request.downloadHandler = new DownloadHandlerFile(savePath);
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Model updated!");
                // 重新加载模型
            }
        }
    }
}
```

---

## 🎮 集中管理多个模型

### 使用ModelManager

```csharp
// 1. 添加MNNModelManager到场景（或自动创建）
var manager = MNNModelManager.Instance;

// 2. 注册模型
manager.RegisterModel(mobilenetConfig);
manager.RegisterModel(yoloConfig);

// 3. 使用模型
var mobilenet = manager.LoadModel("mobilenet-v2");
var yolo = manager.LoadModel("yolov5s");

// 4. 获取状态
bool isLoaded = manager.IsModelLoaded("mobilenet-v2");
var info = manager.GetModelInfo("mobilenet-v2");

// 5. 清理
manager.UnloadModel("mobilenet-v2");
manager.UnloadAllModels();
```

### 监听全局事件

```csharp
void OnEnable()
{
    MNNModelManager.OnModelLoaded += HandleModelLoaded;
    MNNModelManager.OnModelDownloadProgress += HandleProgress;
}

void HandleModelLoaded(string modelId)
{
    Debug.Log($"Model {modelId} is ready!");
}

void HandleProgress(string modelId, float progress)
{
    UpdateProgressBar(progress);
}
```

---

## 🔧 构建时处理

### 自动包含模型

```csharp
// 在ModelConfig中:
config.includeInBuild = true;  // 自动包含到StreamingAssets

// 构建时自动处理:
// 1. 如果includeInBuild=true，复制到StreamingAssets
// 2. 根据平台选择对应的模型文件
// 3. 生成清单文件
```

### 按需下载（减小包体）

```csharp
// 策略1: 首次启动下载
config.includeInBuild = false;
config.enableHotUpdate = true;

// 策略2: 混合模式
// - 小模型随包 (includeInBuild=true)
// - 大模型首次下载 (includeInBuild=false, enableHotUpdate=true)
```

---

## 📊 完整示例：图像分类应用

```csharp
using UnityEngine;
using UnityEngine.UI;
using MNN.Unity;

public class ImageClassifier : MonoBehaviour
{
    [Header("UI")]
    public RawImage inputImage;
    public Text resultText;
    public Button inferButton;
    
    [Header("MNN")]
    public MNNInferenceComponent inference;
    
    private Texture2D _currentInput;
    
    void Start()
    {
        // 监听推理完成
        inference.onInferenceComplete.AddListener(OnInferenceComplete);
        inference.onInferenceError.AddListener(OnInferenceError);
        
        // 绑定按钮
        inferButton.onClick.AddListener(RunClassification);
    }
    
    void RunClassification()
    {
        if (_currentInput == null)
        {
            resultText.text = "请先加载图片";
            return;
        }
        
        // 设置输入并运行（一行代码）
        inference.SetInput(_currentInput);
        inference.RunInference();
        
        resultText.text = "推理中...";
    }
    
    void OnInferenceComplete(MNNTensor output, float inferenceTime)
    {
        // 获取结果
        var scores = output.CopyToArray<float>();
        
        // 找到最高分类
        int maxIndex = 0;
        float maxScore = scores[0];
        
        for (int i = 1; i < scores.Length; i++)
        {
            if (scores[i] > maxScore)
            {
                maxScore = scores[i];
                maxIndex = i;
            }
        }
        
        // 显示结果
        string label = GetImageNetLabel(maxIndex);
        resultText.text = $"{label}\n置信度: {maxScore:P2}\n时间: {inferenceTime:F2}ms";
    }
    
    void OnInferenceError(string error)
    {
        resultText.text = $"错误: {error}";
    }
    
    // 加载图片
    public void LoadImage(string path)
    {
        _currentInput = LoadTextureFromFile(path);
        inputImage.texture = _currentInput;
    }
}
```

---

## 🎯 最佳实践

### 性能优化
1. ✅ 使用`ReleaseModel()`释放模型数据
2. ✅ 复用Session，避免重复创建
3. ✅ 使用零拷贝API (`MapForWrite/Read`)
4. ✅ 启用Burst优化（自动）
5. ✅ 选择合适的后端（Metal/OpenCL/CUDA）

### 内存管理
1. ✅ 及时`Dispose()`不用的对象
2. ✅ 使用`UnloadModel()`卸载模型
3. ✅ 定期调用`CleanupUnusedModels()`
4. ✅ 避免多个相同模型同时加载

### 路径管理
1. ✅ 使用`ModelConfig`统一管理
2. ✅ 让系统自动处理平台差异
3. ✅ 启用热更新前做好降级方案
4. ✅ 测试所有目标平台

---

## 🔍 故障排查

### 模型加载失败
```csharp
// 检查路径
Debug.Log($"Model path: {config.GetModelPath()}");
Debug.Log($"File exists: {File.Exists(config.GetModelPath())}");

// 检查权限（Android）
// 确保READ_EXTERNAL_STORAGE权限

// 检查文件完整性
var fileInfo = new FileInfo(path);
Debug.Log($"File size: {fileInfo.Length} bytes");
```

### 推理错误
```csharp
// 检查输入形状
var input = session.GetInput();
Debug.Log($"Expected shape: [{string.Join(", ", input.Shape)}]");
Debug.Log($"Your data length: {yourData.Length}");

// 检查数据类型
Debug.Log($"Expected type: {input.DataType}");
```

### 下载失败
```csharp
// 检查网络
// 检查URL是否可访问
// 检查磁盘空间
// 启用日志查看详细错误
modelLoader.enableDebugLog = true;
```

---

## 📚 API层次总结

| 层次 | 适用人群 | 复杂度 | 灵活性 | 推荐场景 |
|------|---------|--------|--------|---------|
| MNNInferenceComponent | 新手 | ⭐ | ⭐⭐ | 快速原型 |
| MNNModelLoader | 中级 | ⭐⭐ | ⭐⭐⭐ | 通用应用 |
| MNNInterpreter | 专业 | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 特殊需求 |

**选择原则**：
- 🎯 需要快速验证 → InferenceComponent
- 🎯 需要一定控制 → ModelLoader + ModelManager
- 🎯 需要完全控制 → 直接使用底层API

---

**设计理念**：
> "让简单的事情保持简单，让复杂的事情成为可能"

✨ 新手可以快速上手，专业开发者可以完全掌控！
