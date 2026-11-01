# Android / iOS 直接 C++ 推理绑定

移动端使用 `MNNInterpreter`、`MNNSession`、`MNNTensor` 和 `MNNLlm`，直接调用随包
MNN 3.6.2 的官方 C++ 符号。没有新增 C++、修改官方源码、增加桥接库或自定义 C 导出。
这是本项目维护、限定编译器和二进制的 ABI 契约，不是 MNN 官方提供或保证的通用 C# 接口。

## 支持范围

| 平台 | 托管后端与架构 | 原生构建约定 | 推理配置 |
| --- | --- | --- | --- |
| Android | IL2CPP / ARM64 | NDK r25c / clang 14 / libc++ `std::__ndk1`，API 21 原生库 | CPU；按可用性探测 OpenCL/Vulkan |
| iOS | IL2CPP / ARM64 | Xcode 27.2 / Apple clang / libc++ `std::__1`，iOS 15+ | CPU、Metal |

已实现文件/内存加载、会话创建/运行/释放、输入输出访问、动态形状、张量映射、缓存与外部权重，
以及 LLM 加载、配置、tokenizer、文本生成、角色对话、增量流式解码、token 边界取消、图像/音频输入。
`MNNLlmResult` 提供 prompt token 数及原生 load/prefill/decode 微秒计时。
官方 `createLLM` 的合并配置决定调用 `Llm::load` 还是 `Omni::load`；移动端不通过虚表槽调用。
SmolLM2 的已知旧版 ChatML `prompt_template` 在托管层转换为对应 Jinja 模板，不改写模型文件，
已有 Jinja 配置不被覆盖。

Express executor/scope、VARP 结果所有权及原生 Embedding/Reranker 已接入共享 partial；
Android/iOS 的当前导入符号审计通过，但这些新路径尚未完成移动真机权重推理验证。
语音波形 `std::function` 回调尚无可用编组契约，所有平台均明确拒绝语音输出，
已移除旧的手工虚表/回调对象实现。图像/音频输入通过 Omni 实现。
下述历史真机证据仅覆盖四个具体模型，不能扩展为所有任务或当前修改均已验证。

`MNNPlatformSupport.IsSupported` 表示基础 ABI 已实现，不代表每个任务/加速后端已验证。
ARMv7、x86_64 移动 Player 和移动端 Mono 被拒绝；保留的历史 ARMv7 插件不属于此契约。
Android Auto 当前按 OpenCL→Vulkan→CPU 探测；模型可为正确性限定 CPU。
NNAPI 为显式选项，动态图在 Android 上限制为 CPU；报告 NNAPI 驱动不代表模型已放到 NPU。
每个模型／设备的精度与耗时仍需单独验证，见 [加速策略](Acceleration.md)。
iOS Metal 配置允许 MNN 按算子选择 CPU，不保证全图都在 GPU 执行。

iOS XCFramework 包含真机 ARM64 与模拟器 ARM64 静态 framework。Unity 2021.3.45f2 自带
x86_64 模拟器运行库，不能用它运行 ARM64 模拟器 Player；模拟器仅完成原生构建与符号审计。
Android 应用最低版本仍须满足 Unity 要求；测试配置使用 API 22。`.so` 的 ELF 段按 16 KB 对齐，
包中没有额外 STL `.so`。16 KB 对齐不等于已在 16 KB 页设备上验证。

## ABI 与资源所有权

使用 Itanium C++ 名称及 ARM64 调用约定，显式传入成员函数的 `this`。
`Tensor::length`、`dimensions` 和 `getType` 等官方 inline 定义通过编译选项保留。
不读取 Tensor 私有字段；移动端不依赖虚表槽。

`ScheduleConfig` 大小 104，嵌套 `Path` 大小 56；`backupType`、`backendConfig` 偏移 88、96。
`BackendConfig` 大小 24，`halide_type_t` 大小 8。这些公开结构按目标编译器核对。
LLM 使用官方公开 `LlmContext` 前缀，状态偏移 192；两平台 record-layout dump 位于
`TestArtifacts~/MobileLlmDirectCpp/{android-arm64,ios-arm64}/llm-layouts.txt`。

string/vector 存储各为 24 字节，`pair<string,string>` 为 48 字节；这些是本项目固定构建的
编译器 ABI 表示，不能作为跨编译器、跨 STL 的承诺。移动 string 使用 libc++ 官方初始化、
`data()`、`size()`、析构函数，不手写短字符串布局。非平凡聚合返回由 ARM64 IL2CPP 通过 x8
传递间接结果，已用真实 tokenizer 返回值验证。返回的 integer vector 由对应 libc++ 析构函数释放；
只读形状和 ChatMessages vector 借用托管固定/分配存储，不允许原生修改、保留或析构。
Interpreter/Session 由 SafeHandle 管理，tensor 视图归会话；LLM 串行化推理和释放。

Unity 工作线程不具备 UIKit 事件循环的 autorelease pool。iOS 托管作用域通过系统 Objective-C
运行时创建/排空 `NSAutoreleasePool`，覆盖完整同步加载、生成、读取和释放调用，及时释放
Foundation/Metal 临时对象。它不添加推理桥接或原生代码。不得将作用域跨线程释放。

## 重建

使用未修改的官方 MNN 3.6.2，当前提交 `7e3c6fb5225d3ccf22807ee641332669df34650d`。
工具不下载依赖、不生成 C++，默认启用 LLM/Omni、低内存和 Transformer fuse，关闭 HTTP 资源。
当前 Android CPU、iOS Metal 插件均通过 69 个绑定入口的官方 MNN/libc++ 符号审计，并拒绝 `MNN_*` 导出。
部分 Apple libc++ string 符号由 SDK 系统库提供，审计与 Player 链接共同核对。

```bash
python3 Packages/com.eitan.mnn/Native~/build-mobile.py \
  --source /path/to/clean/MNN --platform android --ndk /path/to/android-ndk-r25c --llm --install
python3 Packages/com.eitan.mnn/Native~/build-mobile.py \
  --source /path/to/clean/MNN --platform ios --llm --install
```

省略 `--install` 仅在 `TestArtifacts~/MobileLlmDirectCpp/` 构建和审计。
`--basic-only` 只用于历史 26 符号基础 ABI 审计，不能安装到当前包。
安装保留插件 `.meta` 和 GUID。不要用任意同版本 MNN 或不同 clang/libc++ 构建替换随包二进制。
升级 MNN、编译器、STL 或 Unity/IL2CPP 后必须重新审计并执行真机测试。

性能构建将禁用内联的范围缩小到两个必要的官方翻译单元，启用 MNN thread pool；
iOS 默认启用 ARM82/SME2，Android 默认启用 ARM82/OpenCL/Vulkan/NNAPI、关闭 SME2。
各设备使用的实际后端、线程数、精度与最新源码候选的质量回归见
[LLM 性能配置与验证](LlmPerformance.md)。编译了某个后端不代表该模型在该硬件上更快。

## 已验证范围

2026-10-25 整理后：13 个条件编译分支通过，macOS Mono 为 66 个 P/Invoke 入口，Android IL2CPP 为 69 个；iOS 为 68 个静态 P/Invoke
加 1 个直接解析的系统 libc++ string 析构符号（官方符号审计共 69 个）。
当前符号审计见 `TestArtifacts~/InteropCleanup/Symbols/`；macOS PlayMode 回归 115 通过、0 失败、
93 跳过；macOS Editor 专项 33 通过、0 失败，包含实际权重的 Embedding/Reranker 与 Omni 检查。
它们不是新移动真机推理证据。移动端 VARP vector 由其精确类型的官方析构函数释放；
executor/scope 与桌面共用实现，未建立 Mobile 专用 API 类型。

2026-10-24，Unity 2021.3.45f2 / ARM64 IL2CPP：OPPO PELM00 Android 13 CPU、
iPhone 17 iOS 27.0.1 CPU 与 Metal 各 116 通过、0 失败、89 可选跳过。
每组使用现有 SmolLM2-135M、Qwen3.5-0.8B、SmolVLM-256M、LFM2.5-Audio-1.5B 权重，
执行 9 个输入案例、17 次生成，并记录墙钟耗时、原生文本计时与进程内存。
5 个 libc++ string 边界/UTF-8/内嵌 NUL 检查也通过。该历史源码当时移动端各含 44 个入口；
此结果不覆盖后续 Express/检索所有权及回调清理修改。

逐案例输出、速度、内存、设备/二进制身份与限制见项目
`TestArtifacts~/MobileLlmDirectCpp/validation-report.md`、`validation-summary.json`，
以及 [测试说明](Testing.md)。测试不下载权重；已下载资产保留在
`Assets/StreamingAssets/MNN/Models/`，可继续用于 Studio。

Unity 2021 / iOS 27 测试宿主在隔离导出工程中使用 Swift SceneDelegate 适配启动生命周期，
文件只在 `TestArtifacts~/`，不属于 package。相同旧 Unity / 新 SDK 组合的应用仍需处理该生命周期。

本轮新增的图像任务、MobileCLIP 和 ERes2NetV2 声纹 API 使用与桌面完全相同的托管实现，
在 Android/iOS 只切换官方 MNN Interpreter/Session 的平台库；没有 `MobileXXX` 平行脚本。
桌面实际权重回归、独立 tokenizer/fbank 参考、耗时和 RSS 输出见
`TestArtifacts~/TaskIntegration/`。移动设备历史 LLM 真机结果仍只覆盖既有四个代表模型，
新增任务尚未写成设备通过记录，不能把条件编译或符号审计当作新增模型的真机推理证据。

PP-DocLayoutV3 使用同一 `MNNDocumentLayout` API，基于真实页面检查后将 Auto 限定为 CPU：
Metal Normal 输出无效边框，High 精度漏检。Android OPPO PELM00 的 CPU / Auto 实际权重用例均通过；完整设备结果 115 通过、0 失败、
0 跳过，已核对 XML build id 与安装 APK 中的 build id 一致。Editor 传输超时后从设备恢复证据。
iPhone 17 ARM64 IL2CPP：115 项通过、0 失败/跳过，两项 CPU/Auto→CPU 真实页面均得到 13 个区域；
加载约 1.1–1.34 秒、推理约 1.08–1.29 秒，进程峰值 RSS 约 1.87 GB，包含 Unity/缓存。最新记录见
`TestArtifacts~/TaskIntegration/DocumentLayout/validation-report.md`。

iOS Unity 2021 / Xcode 27 的 IL2CPP 编译不能直接声明未加 ABI tag 的 string 析构符号，
否则与同编译单元的 libc++ inline 实例重名。共享 `MNNInterop.Abi` partial 通过系统 `dlsym`
直接取得该析构函数，委托参数仍遵守固定 Apple ARM64 ABI；不增加 C++ 或自定义导出。
`NativeNew` 沿用共享 `Cxx` 库名，避免 IL2CPP 为 `libc++.1` 生成非法预处理宏。
符号审计同时检查动态析构符号；条件编译数量不代表设备验证。

2026-10-25 新增 Candy / DeepLab 共用图像 profile：Android ARM64 IL2CPP 合并回归
117 项通过、无失败或跳过，包含两类各 CPU/Auto 的独立参考实际权重检查。
Candy 使用 High 精度解决 Metal FP16 偏差；DeepLab 提供类别掩码与稳定 softmax。
对应 Studio 均有独立页面；没有增加移动端平行 API 或原生桥接。
范围和证据分别见 [风格迁移](StyleTransfer.md) 和 [语义分割](Segmentation.md)。
Candy/DeepLab 的 iPhone 17 ARM64 IL2CPP 回归 117 项通过、0 失败/跳过，包含四项 CPU/Auto→Metal 真实权重对照；
Metal 首次/复用 Candy 约 36 ms、DeepLab 约 22 ms，分割类别与独立参考一致。

最新合并验证记录：`TestArtifacts~/TaskIntegration/ImageTasks/validation-report.md` 和 `validation-summary.json`（开发仓库本地产物）。

MoveNet SinglePose Lightning 复用 `MNNImageTask.Pose.cs`：OPPO PELM00 Android
ARM64 IL2CPP 专项 117 项全部通过、无跳过，包含两个真实人物各 CPU/Auto 的
独立 ONNX 输入／关节对照、重复执行、取消和释放。最终源码首次 Process 约 47–58 ms、复用约 43–45 ms，
进程峰值 RSS 约 307 MB（包括 Unity，未控制冷缓存／温度）。这不扩展为所有姿态模型。
iPhone 17 同专项 117 项通过、无失败/跳过，四项 CPU/Metal 真实权重对照通过；Process 约 6–7 ms。精确 profile、参考差异和测量边界见 [姿态估计](Pose.md)。

English Laya structured decisions now use the shared `MNNTextDecision` API and Studio
NLP page. iPhone17 CPU/Auto→CPU ran six original-reference cases per backend with
probability error <=0.000430; the 845.9 MB profile reached 2.76 GB process RSS and
emitted memory warnings. GPU is intentionally refused for this CPU-only publisher
profile. INT8 and multilingual variants are not yet certified.

2026-10-26 Depth Anything V2 Small 使用共享 MNNImageTask.Depth partial：
iPhone 17 专项 117 项通过，Android OPPO PELM00 与 English Laya 合并专项 119 项通过，
均无失败／跳过，并对照原始 ONNX／PyTorch 参考。深度限制 CPU/High，Auto→CPU；
iPhone 约 1.5–2.2 秒、峰值 RSS 880 MB，Android 约 5.3–7.7 秒、峰值约 1.05 GB。
Laya Android 的 CPU/Auto 各六项参考通过，概率最大误差 0.000428，但约 13–16 秒／次、
加载 36–44 秒、峰值 RSS 3.07 GB；INT8 和多语言仍未验证。
数值、坐标、资源与性能边界见 [深度估计](Depth.md) 和本地 TaskIntegration/NLP 报告。
