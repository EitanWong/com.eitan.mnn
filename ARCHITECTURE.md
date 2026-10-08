# MNN Unity C# API Architecture Plan

## 1. Executive Summary

This architecture defines a complete C# wrapper for the MNN neural network inference engine, exposing MNN's C++ API to Unity developers through a three-layer design: a low-level P/Invoke interop layer, a mid-level managed wrapper, and a high-level Unity-friendly API. The design prioritizes performance (zero-copy where possible), safety (IDisposable pattern, exception handling), and Unity compatibility (IL2CPP, all platforms). Implementation follows a 5-phase approach starting with foundation layers and building up to Unity-specific features.

## 2. Module Structure

### 2.1 Core Module (`MNN.Unity.Core`)
**Purpose**: Primary API surface for model inference

**Components**:
- **MNNInterpreter**: Model loading and session management
  - Static factory methods: `CreateFromFile()`, `CreateFromBuffer()`
  - Session creation with configuration
  - Session lifecycle management
  - IDisposable implementation
  
- **MNNSession**: Execution session handle
  - Input/output tensor access
  - Run inference
  - Resize operations
  - Session info queries

- **MNNTensor**: Tensor data container
  - Shape and dimension management
  - Data type handling (float, int, etc.)
  - Host/device memory access
  - Copy operations (host ↔ device)
  - Factory methods for creation

- **MNNErrorCode**: Error enumeration
  - Maps C++ ErrorCode enum
  - Used for exception generation

### 2.2 Interop Module (`MNN.Unity.Interop`)
**Purpose**: P/Invoke bindings to native MNN library

**Components**:
- **NativeMethods**: Static class with DllImport declarations
  - Platform-specific library name resolution
  - All C API function signatures
  - Unsafe pointer operations
  
- **NativeStructs**: Native structure definitions
  - halide_type_t
  - halide_dimension_t
  - halide_buffer_t (matches Tensor internal structure)

- **NativeHandles**: SafeHandle implementations
  - InterpreterHandle (manages Interpreter* lifetime)
  - SessionHandle (manages Session* lifetime)
  - TensorHandle (manages Tensor* lifetime)

### 2.3 Configuration Module (`MNN.Unity.Configuration`)
**Purpose**: Backend and session configuration

**Components**:
- **ScheduleConfig**: Session scheduling configuration
  - ForwardType (CPU, GPU, Metal, OpenCL, etc.)
  - Thread count / GPU mode
  - Backup backend
  - Path configuration
  - BackendConfig reference

- **BackendConfig**: Backend-specific settings
  - Memory mode (Normal, High, Low)
  - Power mode (Normal, High, Low)
  - Precision mode (Normal, High, Low, Low_BF16)
  - Shared context / flags

- **ForwardType**: Backend enumeration
  - CPU, Metal, CUDA, OpenCL, Vulkan, etc.
  - Platform-specific defaults

- **GpuMode**: GPU tuning flags
  - Tuning modes (None, Heavy, Wide, Normal, Fast)
  - Memory modes (Buffer, Image)
  - Recording modes

### 2.4 Utilities Module (`MNN.Unity.Utilities`)
**Purpose**: Memory management and type conversions

**Components**:
- **TensorPool**: Object pooling for frequent tensor allocations
  - Pool per tensor shape/type
  - Rent/return pattern
  - Auto-cleanup on high memory pressure

- **MemoryManager**: Native memory tracking
  - Allocation statistics
  - Leak detection (debug builds)
  - Force cleanup API

- **TypeConverter**: Type mapping utilities
  - C# type ↔ halide_type_t
  - Dimension format conversions (NCHW ↔ NHWC)
  - Platform endianness handling

- **ShapeUtility**: Tensor shape helpers
  - Batch/Channel/Height/Width extraction
  - Stride calculations
  - Element count computation

### 2.5 Extensions Module (`MNN.Unity.Extensions`)
**Purpose**: Unity-specific integrations

**Components**:
- **Texture2DExtensions**: Texture ↔ Tensor conversion
  - `ToMNNTensor()` extension method
  - `FromMNNTensor()` reconstruction
  - Format conversion (RGB/RGBA)
  - GPU texture support

- **ComputeBufferExtensions**: ComputeBuffer integration
  - Direct GPU buffer → Tensor
  - Avoid CPU roundtrip where possible

- **AsyncExtensions**: Async/await wrappers
  - `RunSessionAsync()` for non-blocking inference
  - Unity coroutine support
  - Progress reporting

- **EditorUtilities**: Editor-only tools
  - Model inspector
  - Performance profiler
  - Debug visualization

## 3. File Structure

```
Packages/com.eitan.mnn/
├── Runtime/
│   ├── MNN.Unity.asmdef (allowUnsafeCode: true)
│   ├── Core/
│   │   ├── MNNInterpreter.cs
│   │   ├── MNNSession.cs
│   │   ├── MNNTensor.cs
│   │   └── MNNErrorCode.cs
│   ├── Interop/
│   │   ├── NativeMethods.cs
│   │   ├── NativeStructs.cs
│   │   ├── NativeHandles.cs
│   │   ├── InterpreterHandle.cs
│   │   ├── SessionHandle.cs
│   │   └── TensorHandle.cs
│   ├── Configuration/
│   │   ├── ScheduleConfig.cs
│   │   ├── BackendConfig.cs
│   │   ├── ForwardType.cs
│   │   ├── GpuMode.cs
│   │   └── SessionMode.cs
│   ├── Utilities/
│   │   ├── TensorPool.cs
│   │   ├── MemoryManager.cs
│   │   ├── TypeConverter.cs
│   │   └── ShapeUtility.cs
│   ├── Extensions/
│   │   ├── Texture2DExtensions.cs
│   │   ├── ComputeBufferExtensions.cs
│   │   └── AsyncExtensions.cs
│   ├── Exceptions/
│   │   ├── MNNException.cs
│   │   ├── MNNOutOfMemoryException.cs
│   │   ├── MNNNotSupportedException.cs
│   │   └── MNNInvalidValueException.cs
│   └── Scripts/
│       └── MNNVersion.cs (existing)
├── Editor/
│   ├── MNN.Unity.Editor.asmdef
│   ├── EditorUtilities/
│   │   ├── ModelImporter.cs
│   │   ├── ModelInspector.cs
│   │   └── PerformanceProfiler.cs
│   └── Scripts/
│       └── AssemblyInfo.cs (existing)
├── Tests/
│   ├── MNN.Unity.Tests.asmdef
│   ├── Runtime/
│   │   ├── InterpreterTests.cs
│   │   ├── SessionTests.cs
│   │   ├── TensorTests.cs
│   │   ├── ConfigurationTests.cs
│   │   └── MemoryTests.cs
│   ├── Integration/
│   │   ├── EndToEndInferenceTests.cs
│   │   ├── MultiSessionTests.cs
│   │   └── PlatformSpecificTests.cs
│   └── Performance/
│       ├── TensorPoolBenchmark.cs
│       └── ZeroCopyBenchmark.cs
├── Samples~/
│   ├── BasicInference/
│   │   ├── Scripts/
│   │   │   └── BasicInferenceExample.cs
│   │   └── README.md
│   ├── ImageClassification/
│   │   ├── Scripts/
│   │   │   └── ImageClassifier.cs
│   │   └── README.md
│   └── ObjectDetection/
│       ├── Scripts/
│       │   └── ObjectDetector.cs
│       └── README.md
└── Documentation~/
    ├── API.md
    ├── GettingStarted.md
    ├── AdvancedUsage.md
    └── PlatformNotes.md
```

## 4. API Design Layers

### 4.1 High-Level Managed API (Public)
**Target**: Unity developers
**Characteristics**: Safe, managed, idiomatic C#, IDisposable

```csharp
// Example: High-level usage
using (var interpreter = MNNInterpreter.CreateFromFile("model.mnn"))
{
    var config = new ScheduleConfig 
    { 
        ForwardType = ForwardType.Auto,
        NumThreads = 4
    };
    
    using (var session = interpreter.CreateSession(config))
    {
        var inputTensor = session.GetInput("data");
        inputTensor.CopyFromArray(inputData);
        
        session.Run();
        
        var outputTensor = session.GetOutput("prob");
        float[] results = outputTensor.ToArray<float>();
    }
}
```

### 4.2 Mid-Level Wrapper API (Internal)
**Target**: Internal implementation
**Characteristics**: Managed but closer to native semantics

```csharp
internal class MNNInterpreter : IDisposable
{
    private readonly InterpreterHandle _handle;
    
    internal static MNNInterpreter CreateFromFile(string path)
    {
        var handle = NativeMethods.MNN_Interpreter_createFromFile(path);
        if (handle.IsInvalid)
            throw new MNNException("Failed to create interpreter");
        return new MNNInterpreter(handle);
    }
    
    internal MNNSession CreateSession(ScheduleConfig config)
    {
        var nativeConfig = ConvertToNative(config);
        var sessionPtr = NativeMethods.MNN_Interpreter_createSession(
            _handle, ref nativeConfig);
        return new MNNSession(sessionPtr, this);
    }
}
```

### 4.3 Low-Level Interop API (Private/Unsafe)
**Target**: P/Invoke layer
**Characteristics**: Unsafe, direct C API mapping

```csharp
internal static class NativeMethods
{
    #if UNITY_IOS && !UNITY_EDITOR
        private const string DllName = "__Internal";
    #else
        private const string DllName = "MNN";
    #endif
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern InterpreterHandle MNN_Interpreter_createFromFile(
        [MarshalAs(UnmanagedType.LPStr)] string file);
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern SessionHandle MNN_Interpreter_createSession(
        InterpreterHandle interpreter, 
        ref NativeScheduleConfig config);
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern MNNErrorCode MNN_Interpreter_runSession(
        InterpreterHandle interpreter, 
        SessionHandle session);
}
```

## 5. Implementation Phases

### Phase 1: Foundation (Week 1-2)
**Goal**: Establish interop layer and error handling

**Deliverables**:
1. `NativeMethods.cs` - All P/Invoke declarations
2. `NativeStructs.cs` - Native type definitions
3. `NativeHandles.cs` - SafeHandle implementations
4. `MNNErrorCode.cs` - Error enumeration
5. `MNNException.cs` + derived exception types
6. Platform testing for library loading

**Tests**:
- Library loading succeeds on all platforms
- SafeHandles properly dispose native resources
- Error codes map correctly to exceptions

**Success Criteria**:
- ✅ Can call MNN_GetVersion() on all platforms
- ✅ SafeHandles prevent memory leaks
- ✅ All exception types tested

### Phase 2: Core APIs (Week 3-5)
**Goal**: Implement Interpreter, Session, Tensor

**Deliverables**:
1. `MNNInterpreter.cs` - Full implementation
2. `MNNSession.cs` - Full implementation
3. `MNNTensor.cs` - Full implementation

**Tests**:
- Load real .mnn model files
- Create sessions with different configs
- Resize tensors dynamically
- Run inference end-to-end
- Memory cleanup verified

**Success Criteria**:
- ✅ Load and run inference on MobileNet model
- ✅ Input/output tensors correctly shaped
- ✅ Results match expected outputs
- ✅ No memory leaks after 1000 iterations

### Phase 3: Configuration & Utilities (Week 6-7)
**Goal**: Backend configuration and memory optimization

**Deliverables**:
1. `ScheduleConfig.cs` - Full configuration API
2. `BackendConfig.cs` - Backend settings
3. `ForwardType.cs`, `GpuMode.cs` - Enumerations
4. `TensorPool.cs` - Object pooling
5. `MemoryManager.cs` - Memory tracking
6. `TypeConverter.cs` - Type utilities
7. `ShapeUtility.cs` - Shape helpers

**Tests**:
- Configure CPU/GPU backends
- Pool creates/reuses tensors
- Type conversions accurate
- Memory tracking correct

**Success Criteria**:
- ✅ Run inference on Metal (iOS/macOS)
- ✅ Run inference on OpenCL (Android)
- ✅ TensorPool reduces allocations by 80%+
- ✅ No allocations in hot path after warmup

### Phase 4: Unity Integration (Week 8-9)
**Goal**: Unity-specific features and extensions

**Deliverables**:
1. `Texture2DExtensions.cs` - Texture conversion
2. `ComputeBufferExtensions.cs` - GPU buffer support
3. `AsyncExtensions.cs` - Async/await wrappers
4. `ModelImporter.cs` - .mnn asset importer
5. `ModelInspector.cs` - Custom inspector
6. Sample scenes

**Tests**:
- Texture2D → Tensor → Texture2D roundtrip
- Async inference doesn't block main thread
- Editor tools functional

**Success Criteria**:
- ✅ Real-time image classification at 30 FPS
- ✅ Async inference works in Unity coroutines
- ✅ Model inspector shows layer info
- ✅ All samples run on target platforms

### Phase 5: Advanced Features (Week 10-11)
**Goal**: Performance optimization and advanced scenarios

**Deliverables**:
1. Zero-copy optimization paths
2. Multi-session support
3. Performance profiling tools
4. Platform-specific optimizations

**Tests**:
- Zero-copy benchmarks
- Multi-threaded stress tests
- Platform-specific feature tests

**Success Criteria**:
- ✅ Zero-copy 2x faster than copy path
- ✅ Multi-session scales linearly
- ✅ Profiler shows accurate timing
- ✅ All optimizations verified on hardware

## 6. Performance Strategy

### 6.1 Zero-Copy Data Transfer
Direct pointer access for advanced scenarios to eliminate Marshal.Copy overhead (~2x faster for large tensors).

### 6.2 Memory Pooling
Pool per tensor signature (shape + type) to eliminate allocations in steady state and reduce GC pressure.

### 6.3 Platform-Specific Optimizations
- **iOS/macOS**: Direct Metal texture GPU copy
- **Android**: GL/CL interop
- **WebGL**: Async to avoid blocking main thread

### 6.4 IL2CPP Considerations
- Use `AggressiveInlining` for hot paths
- Limit generic instantiations
- Use unsafe pointers instead of marshaling
- Profile with IL2CPP builds

## 7. Testing Strategy

### 7.1 Unit Tests
- AAA pattern (Arrange-Act-Assert)
- One test file per class
- 80%+ coverage target

### 7.2 Integration Tests
- End-to-end inference with real models
- MobileNetV2, SqueezeNet, TinyYOLOv3

### 7.3 Platform-Specific Tests
- Conditional compilation for platform features
- Test matrix for CPU/GPU on each platform

### 7.4 Performance Tests
- Throughput and latency benchmarks
- Allocation rate verification

## 8. Compatibility Matrix

### 8.1 Platform Support

| Platform | Unity Version | .NET | IL2CPP | Mono | Backend Options |
|----------|---------------|------|--------|------|-----------------|
| **iOS** | 2021.3+ | Standard 2.1 | ✅ | ❌ | CPU, Metal |
| **Android** | 2021.3+ | Standard 2.1 | ✅ | ✅ | CPU, OpenCL, Vulkan |
| **macOS** | 2021.3+ | Standard 2.1 | ✅ | ✅ | CPU, Metal |
| **Windows** | 2021.3+ | Standard 2.1 | ✅ | ✅ | CPU, CUDA |
| **Linux** | 2021.3+ | Standard 2.1 | ✅ | ✅ | CPU, CUDA, Vulkan |
| **WebGL** | 2021.3+ | Standard 2.1 | ✅ | ❌ | CPU (WASM) |

### 8.2 Backend Availability

| Backend | iOS | Android | macOS | Windows | Linux | WebGL |
|---------|-----|---------|-------|---------|-------|-------|
| **CPU** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Metal** | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |
| **OpenCL** | ❌ | ✅ | ⚠️ | ⚠️ | ✅ | ❌ |
| **Vulkan** | ❌ | ✅ | ⚠️ | ✅ | ✅ | ❌ |
| **CUDA** | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ |

## 9. Error Handling Strategy

### 9.1 Exception Hierarchy
- `MNNException` (base)
  - `MNNOutOfMemoryException`
  - `MNNNotSupportedException`
  - `MNNComputeException`
  - `MNNInvalidValueException`
  - `MNNInputDataException`
  - `MNNFileException` and derived types

### 9.2 Error Code Mapping
Convert MNN ErrorCode enum to typed C# exceptions with context.

### 9.3 Resource Cleanup Patterns
- SafeHandle for native resources
- IDisposable for managed wrappers
- Error-safe resource allocation

## 10. Key Design Decisions

1. **SafeHandle vs Manual P/Invoke**: Use SafeHandle for automatic cleanup and exception safety
2. **IDisposable Pattern**: All resource-owning classes implement IDisposable
3. **Exception-Based Error Handling**: Convert error codes to typed exceptions
4. **No Async by Default**: Synchronous API with async extensions opt-in
5. **Zero-Copy via Unsafe**: Safe API default, unsafe zero-copy opt-in
6. **Pooling Opt-In**: TensorPool is explicit, not automatic
7. **Platform-Specific Code**: Use conditional compilation (#if UNITY_IOS, etc.)
8. **Assembly Separation**: Runtime, Editor, Tests in separate assemblies
9. **Texture Conversion in Extensions**: Separation of concerns (Core vs Unity integration)
10. **BackendConfig as Class**: Reference semantics for configuration objects

## Summary

This architecture provides a complete, production-ready C# wrapper for MNN in Unity. The three-layer design (interop, wrapper, public API) balances performance, safety, and ease of use. Implementation is broken into 5 phases over 11 weeks.

**Key strengths**:
- **Safe**: IDisposable, SafeHandle, exception handling
- **Fast**: Zero-copy opt-in, pooling, platform-specific optimizations
- **Compatible**: IL2CPP, all platforms, .NET Standard 2.1
- **Tested**: 80%+ coverage, integration tests, platform matrix
- **Idiomatic**: C# conventions, Unity patterns, clear API
