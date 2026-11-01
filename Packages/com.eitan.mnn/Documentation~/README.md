# MNN for Unity Documentation

Start with the [package README](../README.md) for installation and the supported
macOS Mono ABI and mobile Interpreter ABI. This is a project-maintained compiler-specific C# binding, not an
official portable MNN C# SDK. Existing plugin files and task entries do not imply
that every platform or model pipeline is implemented.

- [CosyVoice-300M-MNN 初代](CosyVoice300M.md): 22050 Hz 本地模型、C# 合成、Studio 与验证范围。
- [OmniVoice-MNN](OmniVoice.md): 三图转换、托管迭代合成、Unity API、RTF 与质量边界。
- [Chat Studio](ChatStudio.md): conversations, model preparation, media and task limits.
- [Managed Markdown](Markdown.md): shared C# Markdown parsing and Editor/Player rendering boundary.
- [DTLN AEC 回声消除](DtlnAec.md): 三个规格、双音频输入、流式 API 与可选 Burst FFT。
- [Candy 风格迁移](StyleTransfer.md): 固定风格、模型转换和平台验证范围。
- [DeepLab 语义分割](Segmentation.md): 类别掩码、坐标约定和平台验证范围。
- [Depth Anything V2 深度估计](Depth.md): 相对逆深度、原始网格、PFM 和平台验证范围。
- [Model catalog](ModelCatalog.md): public ModelScope/Hugging Face discovery, community models and coverage limits.
- [模型打包与运行时准备](ModelDelivery.md): Project Settings、选择性打包、Android 提取与按需下载。
- [Automatic inference acceleration](Acceleration.md): backend selection, fallback and cache behavior.
- [LLM 性能配置与验证](LlmPerformance.md): 编译优化、逐设备调优、流式开销、缓存与实测限制。
- [Unity components and menus](Components.md): responsibilities, Add Component and Hierarchy commands.
- [Android/iOS direct C++ inference](MobileInterop.md): ARM64 IL2CPP contracts, builds and limitations.
- [API reference](API.md): public C# API and inference workflow.
- [Project structure and conventions](ProjectStructure.md): directory ownership, assemblies and maintenance checks.
- [Repository maintenance](RepositoryManagement.md): tracked assets, ignored models/artifacts, snapshots and dev-to-main PRs.
- [Package tools](../Tools~/README.md): local validation and offline test runners.
- [Package README](../README.md): installation and native library check.
- [Basic sample](../Samples~/BasicExample/README.md): import and run the example.
- [Changelog](../CHANGELOG.md): package releases.

- [Real inference and tests](Testing.md): verified models, results and platform limits.
- [Native build](../Native~/README.md): direct C++ calls and compiler ABI contract.

Paths under `TestArtifacts~/` in the test documentation refer to local evidence;
logs, model weights and generated media are excluded from Git. Historical test
results retain their original scope and do not replace validation of a new build.

- [中文唤醒词检测与 Studio 使用](KeywordSpotting.md)
