# Documentation

Start with the [package README](../README.md) for installation and the supported
desktop Mono core, Express, LLM and retrieval ABIs and the separate mobile ABI.
This is a project-maintained compiler-specific C# binding, not an
official portable MNN C# SDK. Existing plugin files and task entries do not imply
that every platform or model pipeline is implemented.

- [CosyVoice-300M-MNN 初代](CosyVoice300M.md): 22050 Hz 本地模型、C# 合成、Studio 与验证范围。
- [OmniVoice-MNN](OmniVoice.md): 三图转换、托管迭代合成、Unity API、RTF 与质量边界。
- [Chat Studio](ChatStudio.md): conversations, model preparation, media and task limits.
- [Managed Markdown](Markdown.md): shared C# Markdown parsing and Editor/Player rendering boundary.
- [共享声学处理](AcousticPipeline.md): 可独立组合的输入增强、回声、说话人、fade与波形API。
- [AI应用架构](ConversationApplicationArchitecture.md): 最小装配、共享能力入口、线程与资源所有权。
- [Harness background tasks](Harness.md): package-owned execution, persistence, frontend feedback and standalone Host entry points.
- [SiriAI复用审查](SiriAIReuseReview.md): 完整下沉单元、兼容迁移、回归与性能证据。
- [DTLN AEC 回声消除](DtlnAec.md): 三个规格、双音频输入、流式 API 与可选 Burst FFT。
- [Candy 风格迁移](StyleTransfer.md): 固定风格、模型转换和平台验证范围。
- [DeepLab 语义分割](Segmentation.md): 类别掩码、坐标约定和平台验证范围。
- [Depth Anything V2 深度估计](Depth.md): 相对逆深度、原始网格、PFM 和平台验证范围。
- [Model catalog](ModelCatalog.md): public ModelScope/Hugging Face discovery, community models and coverage limits.
- [模型打包与运行时准备](ModelDelivery.md): Project Settings、选择性打包、Android 提取与按需下载。
- [Automatic inference acceleration](Acceleration.md): backend selection, fallback and cache behavior.
- [LLM 性能配置与验证](LlmPerformance.md): 编译优化、逐设备调优、流式开销、缓存与实测限制。
- [Unity components and menus](Components.md): responsibilities, Add Component and Hierarchy commands.
- [Desktop Mono](DesktopMono.md): macOS/Windows/Linux core, Express, LLM and retrieval contracts, setup and observed evidence.
- [Android/iOS direct C++ inference](MobileInterop.md): ARM64 IL2CPP contracts, builds and limitations.
- [API reference](API.md): public C# API and inference workflow.
- [Project structure and conventions](ProjectStructure.md): directory ownership, assemblies and maintenance checks.
- [Repository maintenance](RepositoryManagement.md): tracked assets, ignored models/artifacts, snapshots and dev-to-main PRs.
- [Package tools](../Tools~/README.md): local validation and offline test runners.
- [Package README](../README.md): installation and native library check.
- [Basic sample](../Samples~/BasicExample/README.md): import and run the example.
- [Changelog](../CHANGELOG.md): package releases.

## Start here

- [Package overview and installation](../README.md)
- [Basic Inference sample](../Samples~/BasicExample/README.md)
- [Chat Studio](ChatStudio.md): conversations, media, model preparation and task limits.
- [简体中文 project overview](translations/README.zh-CN.md)

Paths under `TestArtifacts~/` in the test documentation refer to local evidence;
logs, model weights and generated media are excluded from Git. Historical test
results retain their original scope and do not replace validation of a new build.

- [中文唤醒词检测与 Studio 使用](KeywordSpotting.md)

## API and models

- [统一 API 层](PublicApiLayer.md)：领域入口、权限、线程、所有权和文档生成。
- [生成的公共 API 参考](PublicAPI.md)：实际公共符号、重载和 XML 说明。

- [API reference](API.md): public C# types and inference workflows.
- [Model recommendations](ModelRecommendations.md): hardware/workload budgets, KV caches and joint LLM/ASR/TTS selection.
- [Acceleration](Acceleration.md): backend selection, precision, fallback and caching.
- [OmniVoice](OmniVoice.md): conversion, synthesis, model licensing and measured limits.
- [Native build and ABI contract](../Native~/README.md): required compiler and library assumptions.

## Development

- [Contributing](development/Contributing.md): setup, checks and review expectations.
- [Project structure](ProjectStructure.md): directory ownership and assembly boundaries.
- [Repository maintenance](RepositoryManagement.md): source assets, local files and Git hygiene.
- [Branches and installation references](development/Branches.md)
- [CI and validation](development/ContinuousIntegration.md): automatic checks and optional Unity runs.
- [Release procedure](development/Releases.md): versioning, tags, package archives and draft releases.
- [Windows Mono/IL2CPP Players](WindowsPlayer.md): hardware tests, compiler ABI and performance boundaries.
- [Test evidence and reproduction](Testing.md): actual model results and platform limits.
- [Tool reference](../Tools~/README.md)

## Community and history

- [Support](community/Support.md)
- [Community guidelines](community/CodeOfConduct.md)
- [Security policy](community/Security.md)
- [Package changelog](../CHANGELOG.md)
- [Repository changelog](https://github.com/EitanWong/com.eitan.mnn/blob/dev/CHANGELOG.md)

## Documentation conventions

All project guides and translations live in `Documentation~`. The repository and
package READMEs remain short entry points; release history stays in the standard
CHANGELOG files. Topic guides currently retain their original language. New
navigation and maintainer guides use English; translations belong in `translations/`.
Keep executable examples, supported platforms and validation claims consistent
with the code included in the same commit.

Paths under `TestArtifacts~/` refer to local evidence excluded from Git. Historical
results describe the tested build and environment; they do not certify a later build.
