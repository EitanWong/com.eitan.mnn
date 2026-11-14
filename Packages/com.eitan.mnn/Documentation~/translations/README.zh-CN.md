# MNN for Unity

基于 [MNN](https://github.com/alibaba/MNN) 3.6.2 的 Unity API 与编辑器工具，
覆盖张量推理、语言与多模态模型、检索、语音/图像生成、Model Manager 和 Chat Studio。
最低 Unity 2021.3；实际验证版本为 **2021.3.45f2**。

[English](../../README.md) · [文档索引](../README.md) · [更新日志](../../CHANGELOG.md)

## 当前平台支持

下表分别说明绑定实现与实际运行证据。`MNNPlatformSupport.IsSupported` 只检查
已实现的 ABI，不保证具体模型、GPU、设备、浏览器或部署配置已经验证。

| 平台 / 运行时 | 后端与任务范围 | 实际验证与限制 |
| --- | --- | --- |
| macOS 11+ ARM64，Mono Editor/Player | Core、Express、LLM 文本/聊天/流式、检索及媒体输入；按任务使用 CPU/Metal | 选定 Unity Player 核心/平台/Express 检查 120/120；模型管线分别验收。 |
| macOS 11+ x64，Mono | 同类编译器专用绑定 | Rosetta Player 选定检查 120/120；未验证 Intel 真机。 |
| Windows 10+ x64/UCRT，Mono 与 IL2CPP | Core、Express、LLM、检索及媒体输入；随包 CPU/OpenCL 库 | Windows 11 / i7-10700F / RTX 3060：每种 Player 后端各 157/157，覆盖 CPU 与 Auto；不扩展到所有 GPU/模型。 |
| Linux x64，Mono | Core、Express、LLM、检索及媒体输入；CPU | 容器 host Mono 79 项任务、22 项核心断言；Unity Player 未验证。要求 GLIBC 2.38+、GLIBCXX_3.4.32。 |
| Android ARM64，IL2CPP | Core 与 LLM/媒体输入；CPU、按任务选择加速 | 真机/模型证据见移动文档。更新后的 Express/检索只有编译与符号检查，真机推理待验。 |
| iOS 15+ ARM64，IL2CPP | Core 与 LLM/媒体输入；CPU/Metal | 已记录 iPhone 17 部分模型验收；更新后的 Express/检索真机推理待验。模拟器符号通过不等于真机通过。 |
| WebGL wasm32，IL2CPP，实验性 | CPU 单线程、Wasm SIMD；核心张量与四个文本模型适配器 | 仅 Unity 2021.3.45f2 / Emscripten 2.0.19 / Chrome 157。Pocket 0.3B、Qwen2.5-0.5B、Qwen3-0.6B、MiniMind-O 文本已实测；未认证 Worker/线程、WebGPU、媒体、语音或 Express。 |
| macOS/Linux IL2CPP、Windows/Linux ARM64、Android ARMv7及其他 ABI | 未实现推理契约 | 原生推理前拒绝；存在插件文件不代表支持。 |

完整范围见[桌面 Mono](../DesktopMono.md)、[Windows Player](../WindowsPlayer.md)、
[移动 ABI](../MobileInterop.md)、[WebGL 契约](../WebGLInterop.md)及[测试证据](../Testing.md)。
Android 固定 NDK r25c；iOS 固定 Apple libc++ 契约；Windows 使用随包 MinGW-w64 GCC 16.2 /
libstdc++ DLL，IL2CPP 同样受该契约约束。

C# 在本项目维护的指定编译器/STL ABI 下直接调用官方 C++ 符号，没有新增 C++ 桥或自定义 C 导出。
这**不是官方可移植 MNN C# SDK**，不能随意换成相同 MNN 版本的其他原生库。
原生 Omni 波形回调仍不可用；独立图式 TTS 按各自模型和平台证据使用。
没有宣称 CoreML/NPU 或所有模型 GPU 加速已通过。

## 安装

Unity Package Manager 中选择 **Add package from git URL**：

```text
https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#main
```

`main` 为已合并代码，`dev` 为开发分支；使用提交 SHA 可固定版本。
URL 的包子目录不能省略，旧 `upm` 分支不是当前快照。模型权重须另行获取并遵守各自许可。

导入 Samples 中的 **Basic Inference**，加载兼容模型前检查
`MNNPlatformSupport.UnsupportedReason` 与 `MNNVersion.IsLoaded()`。
库可加载不能代替模型正确性验证。

真实包集成后的独立 Chrome 复核为首回调2.92秒、wall3.22秒，同文本及token数；
详细文档保留集成前后全部观测，不据此扩大支持或帧预算结论。

## 文本推理与 WebGL

从[统一 API 层](../PublicApiLayer.md)选择任务，签名见[公共 API 参考](../PublicAPI.md)。
文本应用可用 `MNNModelLoading.LoadAsync` 加载，并等待 `GenerateAsync` 或
`GenerateStreamingAsync`；对话异步入口复用同一文本路径。

WebGL 必须从 Unity 主线程同步上下文调用。异步生成在**同步原生调用之间**让出帧，
不会创建 Worker，也不能中断正在执行的原生调用。等待完成或取消后才能再次使用或释放模型。
加载只在开始让出一帧，随后仍同步阻塞。完整模型需暂存到浏览器内存文件系统，
构建必须设置 [WebGL 链接参数](../WebGLInterop.md#unity-build-settings)。

MiniMind-O 的纯文本异步请求（`GenerateSpeech=false`）通过现有 prefill/decode 图消费完整 prompt，
然后才开始采样，保留原有 512-token 输入加预算边界。完整 KV 输出仍逐元素检查 NaN/无穷大，
固定托管数组并缓存扫描终点消除了已测得的主要校验开销。

在同一 119-token 完整输入、原模型/量化和 16-token 预算下，首文字回调从 14.24 秒降至 **3.07 秒**，
wall 从 16.44 秒降至 **3.38 秒**，最大生成帧间隔 **162.5 ms**。
切分前参照为首文字 2.20 秒、wall 4.40 秒、帧间隔 2175.1 ms，首答仍有取舍；加载仍约 679 ms。
这些结果不能宣称 50 ms 流畅性、稳定 RSS 降低、硬件呈现 TTFT、其他浏览器或全模型整体提速。
同步 `Generate` 保留整段 prefill，仍会阻塞主线程。

## 模型和编辑器工具

- **Window > MNN > Model Manager** 浏览共享目录并安装完整 ModelScope 导出，包含配置、tokenizer 和外部权重。
  运行时使用 `MNNModelCatalog`、`MNNModelDownloader`。
- **Window > MNN > Chat Studio** 提供对话、模型任务、图片/WAV 输入、检索和重排；各任务分别验收。
- 构建随包模型放 `Assets/StreamingAssets/MNN/Models`；运行时下载放
  `Application.persistentDataPath/MNN/Models`。浏览器文件暂存与内存限制另行处理。
- 权重、对话、缓存、生成媒体和日志不提交 Git。测试复用本地资产，不自动下载模型。

参阅[模型获取](../ModelAcquisition.md)、[模型推荐](../ModelRecommendations.md)、
[Chat Studio](../ChatStudio.md)、[加速策略](../Acceleration.md)和[目录规范](../ProjectStructure.md)。

## 贡献与许可

阅读[贡献指南](../development/Contributing.md)和[原生 ABI/构建契约](../../Native~/README.md)。
历史结果保留其原始范围，编译或静态检查不代替目标设备推理。
问题请提交 [GitHub Issues](https://github.com/EitanWong/com.eitan.mnn/issues)，
安全问题遵循[安全策略](../community/Security.md)。

项目采用 [Apache License 2.0](../../LICENSE.md)，模型与可选工具保留各自许可。
