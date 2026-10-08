# MNN Unity Package - Complete File Structure

This document details every file to be created for the MNN Unity C# API wrapper.

## Assembly Definitions

### Runtime/MNN.Unity.asmdef
```json
{
    "name": "MNN.Unity",
    "rootNamespace": "MNN.Unity",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": true,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### Editor/MNN.Unity.Editor.asmdef
```json
{
    "name": "MNN.Unity.Editor",
    "rootNamespace": "MNN.Unity.Editor",
    "references": ["MNN.Unity"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### Tests/MNN.Unity.Tests.asmdef
```json
{
    "name": "MNN.Unity.Tests",
    "rootNamespace": "MNN.Unity.Tests",
    "references": [
        "MNN.Unity",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": true,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

## Core Module Files

### Runtime/Core/MNNInterpreter.cs
**Purpose**: Model loading and session management
**Key APIs**:
- `static CreateFromFile(string path)`
- `static CreateFromBuffer(byte[] buffer)`
- `CreateSession(ScheduleConfig config)`
- `ReleaseSession(MNNSession session)`
- `ResizeSession(MNNSession session)`
- IDisposable implementation

### Runtime/Core/MNNSession.cs
**Purpose**: Execution session handle
**Key APIs**:
- `GetInput(string name = null)`
- `GetOutput(string name = null)`
- `GetAllInputs()`
- `GetAllOutputs()`
- `Run()`
- `GetSessionInfo(SessionInfoCode code)`
- IDisposable implementation

### Runtime/Core/MNNTensor.cs
**Purpose**: Tensor data container
**Key APIs**:
- `static Create(int[] shape, HalideType type)`
- `CopyFromArray<T>(T[] data)`
- `ToArray<T>()`
- `GetDataSpan<T>()` (unsafe)
- Shape properties: `Batch`, `Channel`, `Height`, `Width`
- `DimensionType` property
- IDisposable implementation

### Runtime/Core/MNNErrorCode.cs
**Purpose**: Error code enumeration
**Content**:
```csharp
namespace MNN.Unity
{
    public enum MNNErrorCode
    {
        NO_ERROR = 0,
        OUT_OF_MEMORY = 1,
        NOT_SUPPORT = 2,
        COMPUTE_SIZE_ERROR = 3,
        NO_EXECUTION = 4,
        INVALID_VALUE = 5,
        INPUT_DATA_ERROR = 10,
        CALL_BACK_STOP = 11,
        TENSOR_NOT_SUPPORT = 20,
        TENSOR_NEED_DIVIDE = 21,
        FILE_CREATE_FAILED = 30,
        FILE_REMOVE_FAILED = 31,
        FILE_OPEN_FAILED = 32,
        FILE_CLOSE_FAILED = 33,
        FILE_RESIZE_FAILED = 34,
        FILE_SEEK_FAILED = 35,
        FILE_NOT_EXIST = 36,
        FILE_UNMAP_FAILED = 37
    }
}
```

## Interop Module Files

### Runtime/Interop/NativeMethods.cs
**Purpose**: P/Invoke declarations for all MNN C API functions
**Key sections**:
- Interpreter functions (create, destroy, session management)
- Session functions (run, resize, get tensors)
- Tensor functions (create, copy, access)
- Version and info functions

### Runtime/Interop/NativeStructs.cs
**Purpose**: Native structure definitions
**Structures**:
- `halide_type_t` (code, bits, lanes)
- `halide_dimension_t` (min, extent, stride, flags)
- `NativeScheduleConfig` (matches C++ ScheduleConfig)
- `NativeBackendConfig` (matches C++ BackendConfig)

### Runtime/Interop/InterpreterHandle.cs
**Purpose**: SafeHandle for Interpreter*
**Content**:
```csharp
internal sealed class InterpreterHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public InterpreterHandle() : base(true) { }
    
    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            NativeMethods.MNN_Interpreter_destroy(handle);
            return true;
        }
        return false;
    }
}
```

### Runtime/Interop/SessionHandle.cs
**Purpose**: SafeHandle for Session*
**Note**: Sessions are released through Interpreter, not directly

### Runtime/Interop/TensorHandle.cs
**Purpose**: SafeHandle for Tensor*
**Note**: Device tensors owned by session; host tensors need explicit destroy

## Configuration Module Files

### Runtime/Configuration/ScheduleConfig.cs
**Purpose**: Session scheduling configuration
**Properties**:
- `ForwardType Type` (CPU, GPU, Metal, etc.)
- `int NumThreads` (for CPU) / `GpuMode Mode` (for GPU)
- `ForwardType BackupType`
- `BackendConfig BackendConfig`
- `PathConfig Path`

### Runtime/Configuration/BackendConfig.cs
**Purpose**: Backend-specific settings
**Properties**:
- `MemoryMode Memory` (Normal, High, Low)
- `PowerMode Power` (Normal, High, Low)
- `PrecisionMode Precision` (Normal, High, Low, Low_BF16)
- `IntPtr SharedContext` or `ulong Flags`

### Runtime/Configuration/ForwardType.cs
**Purpose**: Backend enumeration
**Values**: CPU, Metal, CUDA, OpenCL, Vulkan, NN, Auto, etc.

### Runtime/Configuration/GpuMode.cs
**Purpose**: GPU tuning flags
**Values**: TuningNone, TuningHeavy, TuningWide, TuningNormal, TuningFast, MemoryBuffer, MemoryImage, etc.

### Runtime/Configuration/SessionMode.cs
**Purpose**: Session behavior flags
**Values**: Debug, Release, InputInside, InputUser, OutputInside, OutputUser, etc.

## Utilities Module Files

### Runtime/Utilities/TensorPool.cs
**Purpose**: Object pooling for tensors
**Key APIs**:
- `static Shared` property (global instance)
- `Rent(int[] shape, HalideType type)`
- `Return(MNNTensor tensor)`
- `Clear()` (release all pooled tensors)

### Runtime/Utilities/MemoryManager.cs
**Purpose**: Native memory tracking and statistics
**Key APIs**:
- `static GetNativeMemoryUsage()`
- `static ForceCleanup()`
- `static EnableLeakDetection(bool enable)` (debug only)

### Runtime/Utilities/TypeConverter.cs
**Purpose**: Type mapping utilities
**Key APIs**:
- `static HalideType GetHalideType<T>()`
- `static Type GetManagedType(HalideType halideType)`
- `static int GetTypeSize(HalideType type)`

### Runtime/Utilities/ShapeUtility.cs
**Purpose**: Tensor shape helpers
**Key APIs**:
- `static int GetElementCount(int[] shape)`
- `static int[] NCHWToNHWC(int[] nchw)`
- `static int[] NHWCToNCHW(int[] nhwc)`
- `static void ValidateShape(int[] shape)`

## Extensions Module Files

### Runtime/Extensions/Texture2DExtensions.cs
**Purpose**: Texture ↔ Tensor conversion
**Key APIs**:
- `ToMNNTensor(this Texture2D texture, DimensionType format = NHWC)`
- `FromMNNTensor(this MNNTensor tensor, TextureFormat format = RGB24)`

### Runtime/Extensions/ComputeBufferExtensions.cs
**Purpose**: ComputeBuffer integration
**Key APIs**:
- `ToMNNTensor(this ComputeBuffer buffer, int[] shape, HalideType type)`
- `CopyToComputeBuffer(this MNNTensor tensor, ComputeBuffer buffer)`

### Runtime/Extensions/AsyncExtensions.cs
**Purpose**: Async/await wrappers
**Key APIs**:
- `Task<MNNErrorCode> RunAsync(this MNNSession session)`
- `IEnumerator RunCoroutine(this MNNSession session, Action<MNNErrorCode> callback)`

## Exception Module Files

### Runtime/Exceptions/MNNException.cs
**Purpose**: Base exception for all MNN errors
**Properties**:
- `MNNErrorCode ErrorCode`
- Constructor with error code and message

### Runtime/Exceptions/MNNOutOfMemoryException.cs
**Purpose**: OUT_OF_MEMORY error
**Inherits**: MNNException

### Runtime/Exceptions/MNNNotSupportedException.cs
**Purpose**: NOT_SUPPORT error
**Inherits**: MNNException

### Runtime/Exceptions/MNNInvalidValueException.cs
**Purpose**: INVALID_VALUE error
**Inherits**: MNNException

### Runtime/Exceptions/MNNFileException.cs
**Purpose**: Base for file-related errors
**Derived types**:
- `MNNFileNotFoundException` (FILE_NOT_EXIST)
- `MNNFileAccessException` (FILE_OPEN_FAILED)
- `MNNFileOperationException` (other file errors)

## Editor Module Files

### Editor/EditorUtilities/ModelImporter.cs
**Purpose**: Import .mnn files as Unity assets
**Features**:
- Custom asset importer for .mnn files
- Model metadata extraction
- Asset preview generation

### Editor/EditorUtilities/ModelInspector.cs
**Purpose**: Custom inspector for .mnn assets
**Features**:
- Display model info (inputs, outputs, layers)
- Show memory requirements
- Backend compatibility info

### Editor/EditorUtilities/PerformanceProfiler.cs
**Purpose**: Profiling tools for MNN inference
**Features**:
- Per-layer timing breakdown
- Memory usage tracking
- Unity Profiler integration

## Test Files

### Tests/Runtime/InterpreterTests.cs
**Test coverage**:
- CreateFromFile with valid/invalid paths
- CreateFromBuffer with valid/invalid data
- Session creation and management
- Resource disposal and leak prevention

### Tests/Runtime/SessionTests.cs
**Test coverage**:
- GetInput/GetOutput by name
- GetAllInputs/GetAllOutputs
- Run with various configs
- Session info queries

### Tests/Runtime/TensorTests.cs
**Test coverage**:
- Tensor creation with various shapes/types
- CopyFromArray/ToArray roundtrip
- Shape property access
- Dimension type conversions
- Memory management

### Tests/Runtime/ConfigurationTests.cs
**Test coverage**:
- ScheduleConfig validation
- BackendConfig options
- Platform-specific backend selection

### Tests/Runtime/MemoryTests.cs
**Test coverage**:
- TensorPool rent/return cycles
- No leaks after repeated inference
- Memory pressure handling

### Tests/Integration/EndToEndInferenceTests.cs
**Test coverage**:
- Load real models (MobileNet, SqueezeNet)
- Run inference with known inputs
- Verify outputs match expected results
- Multiple inference iterations

### Tests/Integration/PlatformSpecificTests.cs
**Test coverage**:
- Metal backend (iOS/macOS)
- OpenCL backend (Android)
- CUDA backend (Windows/Linux)
- WebGL CPU fallback

### Tests/Performance/TensorPoolBenchmark.cs
**Benchmarks**:
- Allocation rate with/without pooling
- Memory usage comparison
- Performance impact measurement

### Tests/Performance/ZeroCopyBenchmark.cs
**Benchmarks**:
- CopyFromArray vs GetDataSpan
- Large tensor throughput
- Memory bandwidth utilization

## Sample Files

### Samples~/BasicInference/Scripts/BasicInferenceExample.cs
**Purpose**: Minimal working example
**Demonstrates**:
- Load model
- Create session
- Prepare input
- Run inference
- Read output

### Samples~/ImageClassification/Scripts/ImageClassifier.cs
**Purpose**: Real-time image classification
**Demonstrates**:
- Texture2D to Tensor conversion
- TensorPool usage
- Result visualization
- Performance optimization

### Samples~/ObjectDetection/Scripts/ObjectDetector.cs
**Purpose**: Object detection example
**Demonstrates**:
- Multi-output models
- Bounding box visualization
- Non-maximum suppression
- Real-time detection

## Documentation Files

### Documentation~/API.md
**Content**:
- Complete API reference
- Class and method documentation
- Code examples for each API

### Documentation~/GettingStarted.md
**Content**:
- Installation instructions
- First inference tutorial
- Model conversion guide
- Troubleshooting

### Documentation~/AdvancedUsage.md
**Content**:
- Performance optimization tips
- Zero-copy data transfer
- Multi-session usage
- Custom backend configuration

### Documentation~/PlatformNotes.md
**Content**:
- Platform-specific setup
- Backend selection guide
- Known limitations
- Performance expectations

## Summary

Total files to create: **48 new files**
- Core: 4 files
- Interop: 6 files
- Configuration: 5 files
- Utilities: 4 files
- Extensions: 3 files
- Exceptions: 7 files
- Editor: 3 files
- Tests: 9 files
- Samples: 3 files
- Documentation: 4 files

Existing files to update: **2 files**
- Runtime/MNN.Unity.asmdef (enable unsafe code)
- Runtime/Scripts/MNNVersion.cs (keep as-is)
