# MNN for Unity - C# API Documentation

## Overview

本文档介绍 MNN Unity Package 的 C# API。当前接口直接调用官方 C++ API，
要求随包 MNN 3.6.1 / Apple libc++ ABI，CPU 已验证 macOS arm64 与 x86_64 Mono；Metal 加速验证范围见 [测试说明](Testing.md)。
六类代表模型的独立 Player 推理已在 arm64 和 Rosetta 下的 x86_64 验证；
未在 Intel 真机验证。其他平台、IL2CPP 暂不开放推理。

`MNNPlatformSupport.IsSupported` 和 `UnsupportedReason` 可查询当前绑定的 ABI 可用性；
该属性不代表具体模型或 GPU 后端已经通过测试。不支持的平台在原生调用前抛出
`PlatformNotSupportedException`，任务模型加载也会在创建缓存目录前拒绝。

## 文本、多模态与检索任务

`MNNLlm.Load(modelDirectory)` 加载包含配置、tokenizer 和权重的模型目录。
`Generate` / `GenerateAsync` 用于文本，`GenerateMultimodal` / `GenerateMultimodalAsync`
接受文本和单张图片、单个 WAV，使用 `Capabilities` 查询支持的模态。
经过验证的 Qwen2.5-Omni-3B 可通过 `generateSpeech: true` 返回托管 PCM 波形。

`MNNEmbedding.Load` 提供 `Dimension`、`Encode` / `EncodeAsync`；
`MNNReranker.Load` 提供 Qwen3 查询/文档评分 `Score` / `ScoreAsync`。
这三个任务入口在 Unity 主线程加载，默认 `Auto` 加速策略和 `MNNPrecisionMode.Normal`，
任务兼容例外（Embedding CPU、Omni 语音 GPU/CPU 分阶段配置等）见 [自动推理加速](Acceleration.md)，
可通过可选参数 `precisionMode` 选择 Normal、High、Low。每个实例串行执行推理与释放。
Omni 的首次 Metal 语音请求会自动升级为 High 精度并重载为 GPU/CPU 混合配置；
`Backend` 报告主模型 runtime，`MediaBackend` 报告图片/音频处理器 runtime。

示例、代表模型、实际测试结果和平台边界见 [真实推理与测试](Testing.md)。

## 独立语音合成与图片生成

这些托管入口使用官方 Interpreter/Tensor 和 Express/Module 图调用；
本轮仅验证 macOS arm64 Mono。它们不调用未导出的高层 TTS/Diffusion 类。
模型应保留下载仓库的配置、tokenizer、音色和外部权重目录结构。
Bert-VITS2 接入陈曦中文模型，Piper 接入仓库的 Amy/Kathleen/Ryan 英文 fp16 模型；
两者默认 `Auto` 加速后端，CPU 线程参数默认 1，实测 macOS arm64 Unity 2021.3.45f2 Mono。
这是特定编译器 ABI 的托管封装，并非 MNN 官方提供的跨平台 C# API。
Bert-VITS2 的条件分支通过公开 `Module::forward` 执行：每次请求将声调、语言和 BERT 特征
写入托管内存中的序列化图常量，再调用单输入 Module，原模型文件保持原样。
因此每次合成会重新加载约 50 MB 生成器，适合离线合成，尚未优化实时延迟。

```csharp
using MNN.Unity;

using (var tts = MNNSupertonic.Load(supertonicDirectory))
{
    MNNGeneratedAudio audio = tts.Synthesize(
        "The capital of France is Paris.", voice: "M1", steps: 5, speed: 1, seed: 42);
    // audio.Waveform: mono float PCM; audio.SampleRate: 44100.
}

using (var tts = MNNBertVits2.Load(bertVits2Directory))
{
    MNNGeneratedAudio audio = tts.Synthesize("你好，欢迎使用语音合成。"); // 44100 Hz
}

using (var tts = MNNPiper.Load(piperDirectory, espeakExecutable: espeakNgPath))
{
    // Voices contains installed voice names, e.g. en_US-amy-low.
    MNNGeneratedAudio audio = tts.Synthesize("The capital of France is Paris.", "en_US-amy-low"); // 16000 Hz
}

using (var diffusion = MNNStableDiffusion.Load(sdDirectory))
{
    MNNGeneratedImage image = diffusion.Generate(
        "A red bicycle beside a white wall", negativePrompt: "blurry",
        steps: 20, guidance: 7.5f, seed: 42);
    // image.Rgb: RGB24, top-left origin; image.Width/Height: 512.
}

using (var sana = MNNSana.Load(sanaDirectory))
{
    MNNGeneratedImage edited = sana.Edit(
        "Change the bicycle to blue", referenceRgb512, steps: 10, seed: 42, guidance: 4.5f);
}
```

Supertonic 当前验证英语 fp16，支持 M1/M2/F1/F2；输入限制 500 字符，
不支持的词表字符会在推理前拒绝。SD 1.5 支持仓库根目录或 `general` 目录，
输出固定 512×512；Sana 的参考输入必须为 512×512、从左上角开始的 RGB24。
Bert-VITS2 支持中文、常用数字/标点、词组拼音及常见变调，最多 300 字符；
当前前端未实现完整 Jieba 词性变调、中英混读与英文 BERT。英文输入会明确拒绝，
可改用 Piper/Supertonic。Piper 最多 1000 字符，需上游 eSpeak-NG 可执行程序与仓库
`espeak-ng-data`；可通过 `espeakExecutable`、`MNN_ESPEAK_NG_PATH` 或标准安装路径选择。
`espeak-ng-data` 自动按 config.json 的 asset_folder 和模型根目录默认位置查找；
可通过 `MNNPiper.Load(..., espeakDataDirectory: dataFolder)` 显式指定完整数据目录。
Studio 的 Browse 仅选择数据文件夹，工具另行自动发现。
工具路径会检查程序/脚本格式并排除 `espeak-ng-data` 内的数据文件；显式传入字典文件时在加载阶段拒绝，无效环境变量则跳过并继续自动查找。
Piper 的 phonemizer 是官方外部工具，不是桥接库。
Sana 已验证推理和提示词影响，但“红车改蓝车”的语义效果仍未达标。

`Synthesize`、`Generate` 和 `Edit` 均接受 `CancellationToken`，在模型图调用之间检查取消；
不能中断正在执行的原生 kernel。TTS/SD 另提供 `SynthesizeAsync` / `GenerateAsync`；
Sana 同步调用可交给工作线程。一个实例串行执行推理和释放，返回数组独立于模型生命周期。
图片转 Unity Texture2D 时须反转行顺序，并在 Unity 主线程创建和销毁纹理。
Studio 已提供 WAV 播放/保存与图片预览/PNG 保存。

## Quick Start

```csharp
using MNN.Unity;

// 1. 加载模型
var interpreter = MNNInterpreter.CreateFromFile("model.mnn");

// 2. 创建会话（Auto 优先可用加速器，保留 CPU 备用）
var session = interpreter.CreateSession();

// 3. 获取输入输出
var input = session.GetInput();
var output = session.GetOutput();

// 4. 设置映射后的输入数据
var inputSpan = input.MapForWrite<float>();
// ... 填充数据 ...
input.Unmap();

// 5. 运行推理
session.Run();

// 6. 读取映射后的输出数据
var outputSpan = output.MapForRead<float>();
// ... 处理结果 ...
output.Unmap();

// 7. 清理
session.Dispose();
interpreter.Dispose();
```

## Core API

### MNNInterpreter

模型解释器 - 负责加载模型和创建会话。

#### 工厂方法

```csharp
// 从文件加载
MNNInterpreter interpreter = MNNInterpreter.CreateFromFile(string modelPath);

// 从字节数组加载
MNNInterpreter interpreter = MNNInterpreter.CreateFromBuffer(byte[] buffer);
```

#### 会话管理

```csharp
// 创建会话
MNNSession session = interpreter.CreateSession(MNNSessionConfig config = null);

// 释放模型数据（节省内存）
interpreter.ReleaseModel();
```

#### GPU缓存

```csharp
// 设置缓存文件（加速后续启动）
interpreter.SetCacheFile(string cacheFilePath);
```

### MNNSession

推理会话 - 管理推理执行和张量访问。

#### 张量访问

```csharp
// 获取输入张量
MNNTensor input = session.GetInput(string name = null);

// 获取输出张量
MNNTensor output = session.GetOutput(string name = null);
```

#### 执行推理

```csharp
// 同步执行
session.Run();

// 异步执行
await session.RunAsync();
```

#### 动态形状

```csharp
// 调整输入大小
session.ResizeInput(string name, int[] shape);

// 更新GPU缓存
session.UpdateCache();
```

### MNNTensor

数据张量 - 多维数据容器。

#### 属性

```csharp
int[] Shape { get; }              // 张量形状
MNNDataType DataType { get; }     // 数据类型
MNNDimensionType DimensionType { get; } // 维度类型 (NCHW/NHWC)
int ElementCount { get; }         // 元素数量
int ByteSize { get; }            // 字节大小
int Dimensions { get; }          // 维度数量
```

#### 映射访问

```csharp
// 映射用于写入
Span<float> data = tensor.MapForWrite<float>();
// ... 填充数据 ...
tensor.Unmap();

// 映射用于读取
ReadOnlySpan<float> data = tensor.MapForRead<float>();
// ... 处理数据 ...
tensor.Unmap();
```

映射返回的 span 仅在解除映射前有效；GPU 映射仍可能包含设备/主机传输，不能保证端到端零拷贝。
用 `try/finally` 确保调用 `Unmap()`，不要跨 `await` 保存 span。

#### 数据拷贝

```csharp
// 拷贝到数组
float[] data = tensor.CopyToArray<float>();

// 从数组拷贝
tensor.CopyFromArray(float[] data);

// 从Span拷贝
tensor.CopyFromSpan(ReadOnlySpan<float> data);
```

#### Unity集成

```csharp
// 从Texture2D拷贝
tensor.CopyFromTexture(Texture2D texture, bool normalize = true);

// 拷贝到Texture2D
tensor.CopyToTexture(Texture2D texture, bool denormalize = true);
```

### MNNSessionConfig

会话配置 - 控制后端、性能和精度。

#### 创建配置

```csharp
// 默认配置
var config = new MNNSessionConfig();

// 自动选择最佳配置
var config = MNNSessionConfig.CreateForCurrentPlatform();

// 高性能配置
var config = MNNSessionConfig.CreateHighPerformance();

// 低功耗配置
var config = MNNSessionConfig.CreateLowPower();

// 高精度配置
var config = MNNSessionConfig.CreateHighPrecision();
```

#### 属性

```csharp
MNNBackendType BackendType { get; set; }      // 默认 Auto；枚举存在不代表 ABI 或 runtime 可用
int ThreadCount { get; set; }                  // CPU线程数
MNNBackendType BackupBackendType { get; set; } // 创建会话时固定为 CPU 备用
MNNMemoryMode MemoryMode { get; set; }        // Normal/High/Low
MNNPowerMode PowerMode { get; set; }          // Normal/High/Low
MNNPrecisionMode PrecisionMode { get; set; }  // Normal/High/Low/LowBF16
MNNGpuTuningMode GpuTuningMode { get; set; }  // OpenCL/Vulkan GPU调优模式
MNNGpuMemoryMode GpuMemoryMode { get; set; }  // GPU内存模式
```

## 枚举类型

### MNNBackendType

```csharp
CPU = 0      // CPU后端
Metal = 1    // Metal (iOS/macOS)
CUDA = 2     // CUDA (NVIDIA)
OpenCL = 3   // OpenCL (通用GPU)
Auto = 4     // 自动选择
NN = 5       // CoreML/NNAPI
OpenGL = 6   // OpenGL
Vulkan = 7   // Vulkan
```

### MNNDataType

```csharp
Float = 0    // float32
Double = 1   // float64
Int32 = 2    // int32
Int64 = 3    // int64
UInt8 = 4    // uint8
Int8 = 5     // int8
Half = 6     // float16
```

### MNNDimensionType

```csharp
TensorFlow = 0  // NHWC格式
Caffe = 1       // NCHW格式
CaffeC4 = 2     // NC4HW4格式
```

### MNNErrorCode

```csharp
NoError = 0           // 无错误
OutOfMemory = 1       // 内存不足
NotSupport = 2        // 不支持的操作
ComputeSizeError = 3  // 计算大小错误
NoExecution = 4       // 无执行器
InvalidValue = 5      // 无效值
InputDataError = 10   // 输入数据错误
CallBackStop = 11     // 回调停止
TensorNotSupport = 20 // 张量不支持
TensorNeedDivide = 21 // 张量需要分割
```

## 平台支持

### 自动后端选择

```csharp
var config = MNNSessionConfig.CreateForCurrentPlatform();
```

此方法默认 `Auto`，在已实现的 ABI 上探测 Metal → NN → CPU，设置适量 CPU 备用线程。
所有任务模型的 `Load` 同样支持可选 `backendType`。后端诊断、回退、缓存和 NPU/平台边界见
[自动推理加速](Acceleration.md)。

### 手动后端选择

```csharp
var config = new MNNSessionConfig
{
    BackendType = MNNBackendType.Metal,
    BackupBackendType = MNNBackendType.CPU
};
```

## 性能优化

### 1. 使用映射访问

```csharp
// 直接写入当前映射，无需另外创建托管输入数组
var span = tensor.MapForWrite<float>();
span[0] = 1.0f;
tensor.Unmap();

// 数组 API 适合已有托管数据的调用方
var array = new float[tensor.ElementCount];
array[0] = 1.0f;
tensor.CopyFromArray(array);
```

### 2. 释放模型数据

```csharp
var session = interpreter.CreateSession();
interpreter.ReleaseModel(); // 节省内存
```

### 3. 使用GPU缓存

```csharp
interpreter.SetCacheFile(System.IO.Path.Combine(
    UnityEngine.Application.persistentDataPath, "mnn-cache.bin"));
var session = interpreter.CreateSession();
// 首次运行后调用 session.UpdateCache() 保存缓存；速度由模型与设备决定
```

### 4. 选择合适的精度

```csharp
// 显式选择 Low；速度和输出质量须在已支持的设备/模型上验证
config.PrecisionMode = MNNPrecisionMode.Low;

// 默认 Normal；需要更高精度可选择 High
config.PrecisionMode = MNNPrecisionMode.Normal;
```

### 5. 异步执行

```csharp
await session.RunAsync(); // 不阻塞主线程
```

## LLM 流式对话

`MNNLlm.GenerateConversationStreaming` / `GenerateConversationStreamingAsync` 使用官方逐 token
decode；`MNNGenerationUpdate.Text` 是累计文本，`GeneratedTokens` 是累计生成 token 数。
消息使用 `MNNChatMessage` 的 System / User / Assistant 角色；图片与音频属于最后一条用户消息。
回调在推理线程同步执行，Unity UI 必须通过主线程消费队列或最新快照更新，不能直接调用 Repaint。

```csharp
var updates = new System.Collections.Concurrent.ConcurrentQueue<MNNGenerationUpdate>();
using (var stop = new System.Threading.CancellationTokenSource())
{
    var result = await model.GenerateConversationStreamingAsync(
        new[] { new MNNChatMessage(MNNChatRole.User, "解释彩虹是怎样形成的。") },
        update => updates.Enqueue(update), maxNewTokens: 128,
        cancellationToken: stop.Token);
    // 在 Update / EditorApplication.update 中消费 updates。
    // 停止按钮调用 stop.Cancel()；result.Cancelled 表示保留了部分输出。
}
```

取消在 token 边界及生成语音之前观察；预填充、媒体编码、原生波形生成不能在调用中途打断。
提前取消可能抛出 `OperationCanceledException`；开始生成后取消通常返回带 `Cancelled` 的部分结果。
这些 API 与其他任务接口一样，仅在当前 MNN 3.6.1 Apple libc++ 64 位 Mono ABI 契约下验证；
不能作为通用、官方支持的跨平台 C# API。

## 错误处理

原生执行错误抛出 `MNNException`；参数、文件、对象生命周期及平台错误分别使用对应的 .NET 异常：

```csharp
try
{
    var interpreter = MNNInterpreter.CreateFromFile("model.mnn");
    var session = interpreter.CreateSession();
    session.Run();
}
catch (MNNException ex)
{
    Debug.LogError($"MNN Error: {ex.ErrorCode} - {ex.Message}");
}
catch (FileNotFoundException ex)
{
    Debug.LogError($"Model file not found: {ex.Message}");
}
```

## 资源管理

所有MNN对象都实现了`IDisposable`，使用using语句或手动释放：

```csharp
// 方式1：using语句（推荐）
using (var interpreter = MNNInterpreter.CreateFromFile("model.mnn"))
using (var session = interpreter.CreateSession())
{
    session.Run();
}

// 方式2：手动释放
var interpreter = MNNInterpreter.CreateFromFile("model.mnn");
var session = interpreter.CreateSession();
try
{
    session.Run();
}
finally
{
    session.Dispose();
    interpreter.Dispose();
}
```

## 示例

完整示例请参见：
- [BasicExample](../Samples~/BasicExample/) - 基础推理示例
- [MNNInferenceExample.cs](../Samples~/BasicExample/Scripts/MNNInferenceExample.cs) - 详细代码

## API参考

完整API文档请参见：
- [API Reference](https://github.com/EitanWong/com.eitan.mnn/wiki) - 在线文档

## 版本历史

- v3.6.1 - 初始版本；可用接口与平台限制见本页。
