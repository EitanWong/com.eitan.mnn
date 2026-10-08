# MNN for Unity - Architecture Design

## Overview

本文档描述了MNN Unity Package的整体架构设计，包括模块划分、API设计、内存管理、性能优化和测试策略。

## 设计目标

1. **易用性** - 提供符合C#习惯的高级API
2. **性能** - 最小化托管/非托管互操作开销
3. **安全性** - 自动资源管理，防止内存泄漏
4. **兼容性** - 支持Unity 2021.3+，所有平台，IL2CPP/Mono
5. **可测试性** - 80%+测试覆盖率，模块化设计

## 架构分层

```
┌─────────────────────────────────────────────┐
│         High-Level API (Public)             │
│  - MNNInterpreter (模型管理)                │
│  - MNNSession (会话管理)                     │
│  - MNNTensor (数据容器)                      │
│  - MNNConfig (配置)                          │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│      Core Implementation (Internal)         │
│  - 资源生命周期管理 (IDisposable)            │
│  - 错误处理与异常转换                        │
│  - 内存池与对象复用                          │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│     Interop Layer (P/Invoke + unsafe)       │
│  - MNNInterop (原始DllImport)               │
│  - 指针操作与内存拷贝                        │
│  - 平台特定调用约定                          │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│           Native Libraries                  │
│  iOS: MNN.framework (Metal, CoreML)         │
│  Android: libMNN.so (OpenCL, Vulkan)        │
│  macOS/Win/Linux: libMNN (CPU, GPU)         │
│  WebGL: libMNN.a (WASM)                     │
└─────────────────────────────────────────────┘
```

## 模块设计

### 1. 程序集结构

```
MNN.Unity (Runtime)
├── Core/               # 核心API
│   ├── MNNInterpreter.cs
│   ├── MNNSession.cs
│   ├── MNNTensor.cs
│   └── MNNConfig.cs
├── Interop/            # 底层互操作
│   ├── MNNInterop.cs
│   ├── MNNNative.cs
│   └── Handles/
│       ├── InterpreterHandle.cs
│       ├── SessionHandle.cs
│       └── TensorHandle.cs
├── Utils/              # 工具类
│   ├── MNNLogger.cs
│   ├── MNNException.cs
│   └── MNNMemoryPool.cs
└── Scripts/            # 现有代码
    └── MNNVersion.cs

MNN.Unity.Editor (Editor)
└── Tools/
    ├── MNNModelInspector.cs
    └── MNNSettingsProvider.cs

MNN.Unity.Tests (Tests)
├── Runtime/
│   ├── CoreTests.cs
│   ├── InteropTests.cs
│   └── IntegrationTests.cs
└── Editor/
    └── EditorTests.cs
```

### 2. 核心类设计

#### MNNInterpreter - 模型解释器

```csharp
public class MNNInterpreter : IDisposable
{
    // 工厂方法
    public static MNNInterpreter CreateFromFile(string modelPath);
    public static MNNInterpreter CreateFromBuffer(byte[] buffer);
    
    // 会话管理
    public MNNSession CreateSession(MNNSessionConfig config);
    
    // 模型信息
    public string[] GetInputNames();
    public string[] GetOutputNames();
    public MNNTensorInfo GetTensorInfo(string name);
    
    // 资源释放
    public void Dispose();
}
```

#### MNNSession - 执行会话

```csharp
public class MNNSession : IDisposable
{
    // 输入/输出管理
    public MNNTensor GetInput(string name);
    public MNNTensor GetOutput(string name);
    
    // 执行推理
    public void Run();
    public Task RunAsync(); // 异步执行
    
    // 批量处理
    public void RunBatch(int batchSize);
    
    // 资源管理
    public void Resize(string name, int[] shape);
    public void Dispose();
}
```

#### MNNTensor - 数据张量

```csharp
public class MNNTensor : IDisposable
{
    // 属性
    public int[] Shape { get; }
    public MNNDataType DataType { get; }
    public MNNDimensionType DimensionType { get; }
    public int ElementCount { get; }
    public int ByteSize { get; }
    
    // 数据访问 (零拷贝)
    public Span<T> GetData<T>() where T : unmanaged;
    public void SetData<T>(ReadOnlySpan<T> data) where T : unmanaged;
    
    // 数据拷贝 (安全模式)
    public T[] CopyToArray<T>() where T : unmanaged;
    public void CopyFromArray<T>(T[] data) where T : unmanaged;
    
    // Unity集成
    public void CopyFromTexture(Texture2D texture);
    public void CopyToTexture(Texture2D texture);
    
    public void Dispose();
}
```

#### MNNSessionConfig - 会话配置

```csharp
public class MNNSessionConfig
{
    // 后端配置
    public MNNBackendType BackendType { get; set; } = MNNBackendType.Auto;
    public int ThreadCount { get; set; } = 4;
    
    // 性能配置
    public MNNMemoryMode MemoryMode { get; set; } = MNNMemoryMode.Normal;
    public MNNPowerMode PowerMode { get; set; } = MNNPowerMode.Normal;
    public MNNPrecisionMode PrecisionMode { get; set; } = MNNPrecisionMode.Normal;
    
    // GPU配置
    public MNNGpuTuningMode GpuTuningMode { get; set; } = MNNGpuTuningMode.Wide;
    public MNNGpuMemoryMode GpuMemoryMode { get; set; } = MNNGpuMemoryMode.Image;
    
    // 平台特定优化
    public static MNNSessionConfig CreateForCurrentPlatform();
}
```

### 3. 枚举类型

```csharp
// 后端类型
public enum MNNBackendType
{
    CPU = 0,
    Auto = 4,
    Metal = 1,
    CUDA = 2,
    OpenCL = 3,
    Vulkan = 7,
    CoreML = 5  // MNN_FORWARD_NN on iOS
}

// 数据类型
public enum MNNDataType
{
    Float32,
    Int32,
    Int8,
    UInt8,
    Int64
}

// 维度类型
public enum MNNDimensionType
{
    TensorFlow = 0,  // NHWC
    Caffe = 1,       // NCHW
    Caffe_C4 = 2     // NC4HW4
}

// 错误码
public enum MNNErrorCode
{
    NoError = 0,
    OutOfMemory = 1,
    NotSupport = 2,
    ComputeSizeError = 3,
    NoExecution = 4,
    InvalidValue = 5,
    InputDataError = 10,
    CallBackStop = 11
}
```

### 4. Interop层设计

```csharp
internal static class MNNInterop
{
    #if UNITY_IOS && !UNITY_EDITOR
        private const string DllName = "__Internal";
    #else
        private const string DllName = "MNN";
    #endif
    
    // Interpreter
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr MNN_Interpreter_createFromFile(
        [MarshalAs(UnmanagedType.LPStr)] string file);
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr MNN_Interpreter_createFromBuffer(
        IntPtr buffer, long size);
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void MNN_Interpreter_destroy(IntPtr interpreter);
    
    // Session
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr MNN_Interpreter_createSession(
        IntPtr interpreter, ref MNNScheduleConfig config);
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int MNN_Interpreter_runSession(
        IntPtr interpreter, IntPtr session);
    
    // Tensor
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr MNN_Interpreter_getSessionInput(
        IntPtr interpreter, IntPtr session, 
        [MarshalAs(UnmanagedType.LPStr)] string name);
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr MNN_Tensor_getHost(IntPtr tensor);
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void MNN_Tensor_getDimensions(
        IntPtr tensor, IntPtr dims, ref int size);
}
```

## 内存管理策略

### 1. 资源生命周期

- **RAII模式**: 所有包含非托管资源的类实现IDisposable
- **SafeHandle**: 使用CriticalFinalizerObject保护关键资源
- **弱引用**: Session持有Interpreter的弱引用，避免循环引用

### 2. 内存优化

- **对象池**: 复用Tensor和Config对象
- **零拷贝**: 使用Span<T>直接操作非托管内存
- **延迟分配**: 按需创建和分配大对象

### 3. 错误处理

```csharp
public class MNNException : Exception
{
    public MNNErrorCode ErrorCode { get; }
    
    public MNNException(MNNErrorCode code, string message) 
        : base($"[MNN Error {code}] {message}")
    {
        ErrorCode = code;
    }
}
```

## 性能优化

### 1. 互操作优化

- 减少P/Invoke调用次数
- 使用blittable类型避免marshalling
- 批量操作降低跨边界开销

### 2. 内存优化

- 使用stackalloc分配小型临时缓冲区
- ArrayPool复用数组对象
- Span<T>避免数组拷贝

### 3. 平台特定优化

```csharp
#if UNITY_IOS
    // iOS: 优先使用Metal后端
    config.BackendType = MNNBackendType.Metal;
#elif UNITY_ANDROID
    // Android: 优先使用OpenCL或Vulkan
    config.BackendType = MNNBackendType.OpenCL;
#elif UNITY_STANDALONE_OSX
    // macOS: 优先使用Metal
    config.BackendType = MNNBackendType.Metal;
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
    // Desktop: 优先使用CUDA，回退到CPU
    config.BackendType = MNNBackendType.CUDA;
#else
    config.BackendType = MNNBackendType.CPU;
#endif
```

## 测试策略

### 1. 单元测试

- 测试每个公共API方法
- 测试错误处理和边界条件
- 测试内存泄漏（反复创建/销毁对象）

### 2. 集成测试

- 端到端推理测试（加载模型→设置输入→执行→读取输出）
- 多线程并发测试
- 不同后端切换测试

### 3. 性能测试

- 推理延迟基准测试
- 内存使用监控
- GC压力测试

### 4. 平台测试

- iOS: 真机 + 模拟器
- Android: 不同架构 (armeabi-v7a, arm64-v8a)
- Desktop: Windows/macOS/Linux
- WebGL: 浏览器兼容性

## 兼容性矩阵

| Unity版本 | .NET Standard | IL2CPP | Mono | 状态 |
|-----------|---------------|--------|------|------|
| 2021.3 LTS | 2.1 | ✓ | ✓ | 支持 |
| 2022.3 LTS | 2.1 | ✓ | ✓ | 支持 |
| 2023.2+ | 2.1 | ✓ | ✓ | 支持 |
| 6000.0+ | 2.1 | ✓ | ✓ | 计划支持 |

| 平台 | 最低版本 | 后端 | 状态 |
|------|---------|------|------|
| iOS | 11.0 | Metal, CoreML | ✓ |
| Android | 5.0 (API 21) | OpenCL, Vulkan | ✓ |
| macOS | 10.13 | Metal | ✓ |
| Windows | 7 SP1 | CPU, CUDA | ✓ |
| Linux | Ubuntu 18.04+ | CPU, CUDA | ✓ |
| WebGL | ES 3.0 | WASM | ✓ |

## 实施计划

### Phase 1: 基础互操作层 (Week 1)
- [ ] MNNInterop - P/Invoke声明
- [ ] SafeHandle实现
- [ ] 基础错误处理
- [ ] 单元测试

### Phase 2: 核心API (Week 2)
- [ ] MNNInterpreter实现
- [ ] MNNSession实现
- [ ] MNNTensor实现
- [ ] 集成测试

### Phase 3: 高级功能 (Week 3)
- [ ] 异步执行
- [ ] 内存池
- [ ] Unity Texture集成
- [ ] 性能优化

### Phase 4: 测试与优化 (Week 4)
- [ ] 平台测试
- [ ] 性能基准测试
- [ ] 文档完善
- [ ] 示例代码

## 参考资料

- [MNN官方文档](https://mnn-docs.readthedocs.io/)
- [MNN C++ API参考](https://github.com/alibaba/MNN)
- [Unity Native Plugin开发指南](https://docs.unity3d.com/Manual/NativePlugins.html)
