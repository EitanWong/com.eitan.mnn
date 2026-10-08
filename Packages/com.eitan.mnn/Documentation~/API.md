# MNN for Unity - C# API Documentation

## Overview

本文档介绍MNN Unity Package的完整C# API。

## Quick Start

```csharp
using MNN.Unity;

// 1. 加载模型
var interpreter = MNNInterpreter.CreateFromFile("model.mnn");

// 2. 创建会话（自动选择最佳后端）
var session = interpreter.CreateSession();

// 3. 获取输入输出
var input = session.GetInput();
var output = session.GetOutput();

// 4. 设置输入数据（零拷贝）
var inputSpan = input.MapForWrite<float>();
// ... 填充数据 ...
input.Unmap();

// 5. 运行推理
session.Run();

// 6. 读取输出（零拷贝）
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

#### 零拷贝访问（推荐）

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
MNNBackendType BackendType { get; set; }      // CPU/Metal/CUDA/OpenCL/Vulkan
int ThreadCount { get; set; }                  // CPU线程数
MNNBackendType BackupBackendType { get; set; } // 备用后端
MNNMemoryMode MemoryMode { get; set; }        // Normal/High/Low
MNNPowerMode PowerMode { get; set; }          // Normal/High/Low
MNNPrecisionMode PrecisionMode { get; set; }  // Normal/High/Low/LowBF16
MNNGpuTuningMode GpuTuningMode { get; set; }  // GPU调优模式
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

| 平台 | 默认后端 | 说明 |
|------|---------|------|
| iOS | Metal | 硬件加速 |
| Android | OpenCL | GPU加速 |
| macOS | Metal | 硬件加速 |
| Windows | CUDA → CPU | 优先CUDA |
| Linux | CUDA → CPU | 优先CUDA |
| WebGL | CPU | 单线程 |

### 手动后端选择

```csharp
var config = new MNNSessionConfig
{
    BackendType = MNNBackendType.Metal,
    BackupBackendType = MNNBackendType.CPU
};
```

## 性能优化

### 1. 使用零拷贝访问

```csharp
// ✓ 推荐：零拷贝
var span = tensor.MapForWrite<float>();
span[0] = 1.0f;
tensor.Unmap();

// ✗ 避免：数据拷贝
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
interpreter.SetCacheFile("cache.bin");
var session = interpreter.CreateSession();
// 首次运行后，后续启动更快
```

### 4. 选择合适的精度

```csharp
// 移动设备：使用FP16获得更好性能
config.PrecisionMode = MNNPrecisionMode.Low;

// 桌面设备：保持FP32精度
config.PrecisionMode = MNNPrecisionMode.Normal;
```

### 5. 异步执行

```csharp
await session.RunAsync(); // 不阻塞主线程
```

## 错误处理

所有MNN错误都会抛出`MNNException`：

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
- [BasicExample](Samples~/BasicExample/) - 基础推理示例
- [MNNInferenceExample.cs](Samples~/BasicExample/Scripts/MNNInferenceExample.cs) - 详细代码

## API参考

完整API文档请参见：
- [Architecture.md](Documentation~/Architecture.md) - 架构设计
- [API Reference](https://github.com/EitanWong/com.eitan.mnn/wiki) - 在线文档

## 版本历史

- v3.6.1 - 初始版本，包含完整C# API
