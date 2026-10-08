# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.6.1] - 2024-10-08

### Added
- 🎉 Initial release of MNN for Unity
- ✅ Full platform support: iOS, Android, macOS, Windows, Linux, WebGL
- 🚀 Optimized native libraries (79% size reduction vs official builds)
- 📦 XCFramework for iOS (device + simulator)
- ⚡ Hardware acceleration support:
  - Metal (iOS/macOS)
  - OpenCL (Android)
  - CUDA (Windows/Linux)
  - WebGL (WebAssembly)
- 🔧 Pre-configured Unity .meta files for all platforms
- 📚 Complete documentation and examples

### Features
- Based on MNN v3.6.1 from Alibaba
- CNN / Transformer / LLM / Diffusion model support
- Lightweight inference engine (~60 MB total)
- Production-ready builds with strip optimization
- Android: armeabi-v7a (4.1 MB) + arm64-v8a (5.4 MB)
- iOS: Universal XCFramework (15.7 MB)
- macOS: Universal Binary x86_64 + arm64 (8.3 MB)
- Windows: x86_64 DLL (5.1 MB)
- Linux: x86_64 SO (4.0 MB)
- WebGL: WebAssembly static library (6.0 MB)

### Optimizations
- Strip symbols from all native libraries (-50+ MB)
- Disabled training functionality (-15+ MB)
- Disabled benchmark tools (-20+ MB)
- Inference-only configuration
- Low memory mode enabled

### Platform Details

#### iOS
- XCFramework with arm64 device + simulator slices
- Metal GPU acceleration
- CoreML integration support
- Minimum deployment target: iOS 11.0

#### Android
- Multi-ABI support (v7a + v8a)
- OpenCL GPU acceleration
- Minimum API level: 21 (Android 5.0)

#### macOS
- Universal binary (Intel + Apple Silicon)
- Metal GPU acceleration
- Minimum target: macOS 10.13

#### Windows
- x86_64 DLL
- AVX/AVX2/AVX512 support
- CUDA GPU acceleration support

#### Linux
- x86_64 shared library
- AVX/AVX2/AVX512 support
- CUDA GPU acceleration support

#### WebGL
- WebAssembly static library
- Single-threaded mode
- SIMD optimization

### Documentation
- Comprehensive README with quick start guide
- Platform-specific build notes
- Performance comparison with official builds
- API usage examples

### Known Limitations
- Inference only (no training support)
- No Python bindings
- No benchmark tools included

---

## Roadmap

### Future Versions

#### [3.7.0] - Planned
- Additional model format support
- Enhanced C# API wrapper
- More code examples
- Performance profiling tools

#### [3.8.0] - Planned
- Unity Editor integration tools
- Visual model inspector
- Drag-and-drop model import
- Runtime model loading utilities

---

For detailed MNN engine changes, see [MNN Official Releases](https://github.com/alibaba/MNN/releases)
