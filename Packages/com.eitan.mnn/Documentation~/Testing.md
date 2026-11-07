# MNN Studio 编辑器工作区

入口：`Window > MNN > Chat Studio`（Cmd/Ctrl + Shift + M）。需要 Unity 2021.3 或更新版本。
Studio 高层任务的真实推理验证范围为 macOS 64 位 Mono，官方 MNN 3.6.2 与 Apple libc++ 的特定 ABI。
这是本项目限定版本的 C# 编组契约，不是官方提供的跨平台 C# API。Windows/Linux x64 Mono
已接入核心 Interpreter/Session/Tensor、LLM、Express、原生检索及媒体输入。Windows/Linux 的新任务
验证使用 Wine/容器 Mono 与真实原生库；尚无这两个平台的 Unity Player 结果。
WebGL 与桌面 IL2CPP 不具备相同推理支持。Android/iOS ARM64 IL2CPP 的基础推理及
LLM 文本/媒体输入的历史设备验证见下节；Express/原生检索已接入共享 partial，
当前修改尚无新的移动真机推理结果。Omni/Qwen3 原生波形回调在所有平台明确拒绝。
没有新增 C++、桥接库、自定义 C 导出或官方源码修改。

## Windows/Linux 桌面 Mono 高级任务（2026-11-07）

直接调用官方 C++，没有新增 C++、自定义导出或修改官方源码。Windows 更换为固定版本的
MinGW-w64 GCC 16.2 GNU DLL；Linux 沿用 GCC 13.3 GNU 库。不能替换回同版本 MSVC DLL。
完整 ABI、运行要求及限制见[桌面 Mono](DesktopMono.md)。

生产 API 的真实原生测试包括：Express 常量/Module/图执行、动态 shape、独立所有权与 GC；
SmolLM2-135M 文本/聊天/流式、取消恢复、分词与释放；BGE-small512 维归一化及检索；
Qwen3 yes/no 重排；SmolVLM 图片输入。Windows/Linux 各 79 项高级任务检查通过，基础推理各 22 项通过。Windows 使用 Wine 9/Mono9.4，
Linux 使用容器内 Mono 解释器，这些均不是 Windows/Linux Unity Player 结果。逐条命令、输入、stdout/stderr、
退出码和失败修复保存在 `TestArtifacts~/MonoSupport-2026-11-07/Advanced/Tests/`。
复现入口为 `Tools~/Testing/run_desktop_mono_tasks.py`，只复用本地模型，不下载资源。
本轮 macOS ARM64 和 x64（Rosetta）Mono Player 按 core/platform/Express filter 各 120/120 通过，
0 失败/跳过；聚焦 Editor 14/14 通过。13 种条件编译与基础 9 个包程序集编译通过，Burst 未安装。
未配置模型/独立参考根目录的全量 Player 尝试含 95 项 fixture 失败，保留日志；它不构成全量模型通过。

## 三平台桌面 Mono 核心绑定（2026-11-07）

核心范围为官方 C++ Interpreter/Session/Tensor，包括版本、会话配置、张量读写、resize、同步/异步
执行及释放；下表保留基础绑定阶段的历史结果，高级任务结果见上一节。编译器与二进制契约见[桌面 Mono](DesktopMono.md)。
本轮使用已提交的 584 字节 `2*x+1` 仿射图，没有下载模型，没有新增 C++、自定义 C 导出或修改官方源码。

| 环境 | 观察到的结果 | 证据范围 |
| --- | --- | --- |
| macOS ARM64 Unity 2021.3.45f2 Mono Player | 118/118 通过，0 失败/跳过，含 11 项原生测试；CPU、Auto→Metal 仿射图通过 | 实际 Unity Player；不代表所有模型 |
| Windows x64 Wine 9 / Wine Mono 9.4.0 JIT，Linux amd64 容器 | 生产 C# 核心 smoke 完成：CPU 1/4 线程、张量元数据、同步数值、UTF-8 saved tensor、resize、异步、释放、Unicode 文件路径及 Auto→CPU；exit 0 | Windows DLL 兼容执行，尚无 Windows Unity Player 结果 |
| Linux x64 host Mono `--interpreter`，Linux amd64 容器 | 同一生产 C# 核心数值 smoke 完成，exit 0；CPU_EXTENSION(13) 归一到托管 CPU | 容器内 Mono 解释器与真实 Linux `.so`，尚无 Linux Unity Player 结果 |

本轮前置 macOS Player 为 113/113；最终候选回归为上表 118/118。x64 macOS 沿用此前 Rosetta
Player 证据，本轮未新增 Intel 硬件结果。Linux 宿主 Mono JIT 在当前容器翻译环境中的早期尝试崩溃，
成功结果明确使用 `--interpreter`，不能记成 Linux JIT 或 Unity 验收。Windows 成功路径使用
`LANG=C.UTF-8` 与生产路径适配；码页无法表示且无 DOS 短路径的文件仍显式拒绝。

证据位于仓库 `TestArtifacts~/MonoSupport-2026-11-07/`：`CrossPlatform/mac-player.stdout`、
`CrossPlatform/Windows/utf8-locale-run.stdout`、`CrossPlatform/Linux/run-mono.stdout`、
`WindowsAudit/`、`WindowsRun/` 和 `LinuxBuild/`。早期失败和崩溃记录保留。Windows/Linux 核心
当前仅有 CPU 数值证据；OpenCL/Vulkan 的探测回退或编译成功不代表 GPU 推理通过。

## DTLN AEC 与可选 Burst（2026-10-25）

M5 Mac / Unity 2021.3.45f2 ARM64 Editor / 随包官方 MNN 3.6.2 CPU/High：
DTLN AEC 128、256、512 的模型目录发现、固定下载计划、安装准备、双音频 Studio
运行与真实流式/离线波形对照已通过。用法和限制见 [DTLN AEC](DtlnAec.md)。

| 配置 | 专项结果 | 证据（`TestArtifacts~/DTLNAecIntegration/`） |
| --- | --- | --- |
| 未安装 Burst | 16 通过、0 失败/跳过 | `managed-final-results.xml` |
| 安装 Burst 1.8.18 并要求实际启用 | 16 通过、0 失败/跳过 | `burst-packed-results.xml` |
| 已安装但关闭 Burst，未配置本地数据 | FFT 5 通过、模型 8 跳过、0 失败 | `burst-disabled-no-fixtures-results.xml` |
| 逐程序集编译 | 基础 8/8，可选配置 9/9 | `CompilationFinal/`、`CompilationBurst/` |

模型数据来自 AEC Challenge 同一设备的双讲、远端单讲和近端单讲，每段约 8.19 秒。
三规格均对照独立 Python MNN/NumPy FFT 完整波形，并检查非整帧尾部、重置、取消、
异步快照与释放。连续流 131072 个采样的最大误差为 4.18×10⁻⁷ 以下，预热后完整
`ProcessHop` 链路托管分配为 0。安装测试复用本地已有发布文件，覆盖 HTTP 处理和
哈希校验；不代表重新执行公网权重下载。缺少环境变量的模型测试跳过，不计为验证成功。

| 规格 | C# FFT 路径完整 ms/hop | Burst FFT 路径完整 ms/hop |
| --- | ---: | ---: |
| 128 | 0.125 | 0.106 |
| 256 | 0.235 | 0.225 |
| 512 | 0.689 | 0.684 |

每 hop 代表 8 ms 音频，计时包含两阶段推理、状态复制、FFT 与 overlap-add，不含模型加载。
这是各一次运行的平均值，没有控制温度和系统调度，不能据此对细小差异作统计结论。
单独 512 点复数 FFT 的预热测量为 C# 约 10.06 µs、Burst 约 1.95 µs；神经网络仍使用 CPU。
以上为 Unity Editor 实际权重证据，不替代 Android/iPhone 的 Unity Player 或完整音质测试。

准备本地 `Reference/manifest.json` 所列数据与三组模型后，在不打开主工程的隔离
Unity 工程中运行（不加 Burst 依赖可验证回退，加已有 Burst 1.8+ 可验证加速）：

```bash
export MNN_TEST_DTLN_AEC_ROOT="$PWD/TestArtifacts~/DTLNAecIntegration"
export MNN_TEST_DTLN_AEC_MODEL_ROOT="$PWD/Assets/StreamingAssets/MNN/Models"
# Burst 配置额外设置 MNN_TEST_REQUIRE_BURST=1，要求实际初始化。
<Unity> -batchmode -nographics -projectPath <isolated-project> \
  -runTests -testPlatform editmode \
  -testFilter 'MNN.Unity.Tests.FftPlanTests;MNN.Unity.Tests.DtlnAecIntegrationTests;MNN.Unity.Tests.DtlnAecCatalogTests;MNN.Unity.Tests.ModelDownloadTaskTests.DtlnAecManagerInstallsAllVariantsAndReusesVerifiedFiles' \
  -testResults "$MNN_TEST_DTLN_AEC_ROOT/results.xml" \
  -logFile "$MNN_TEST_DTLN_AEC_ROOT/unity.log"
```

## Package 工程化回归（2026-10-25）

模型源码按领域/模型归类，安装与真实模型测试归属 `MNN.Unity.Integration.Tests`，
共享 Editor 测试 helper 归属 `MNN.Unity.Tests.Fixtures`。原有 Runtime、Editor、Player
测试和示例程序集名称保留；测试类型的完整名称和 category 保留。
目录和依赖约束见 [项目结构](ProjectStructure.md)。

本轮证据位于仓库 `TestArtifacts~/PackageEngineering/`：

| 检查 | 结果 | 范围 |
| --- | --- | --- |
| 包结构检查 | 通过 | 8 个程序集、目录职责、依赖循环、partial 归属、meta/GUID、本地文档链接 |
| 逐程序集编译 | 8/8 | 使用各 asmdef 声明的包依赖，包含未导入的 BasicExample |
| 平台条件编译 | 13/13 | 本地 Unity 引用上的 C# 分支与导入数量检查；不是目标平台构建 |
| 常规 Editor 回归 | 995 通过、0 失败、36 跳过 | 显式或缺少本地模型条件的用例不计为通过 |
| 专项集成回归 | 43 通过、0 失败、0 跳过 | 内嵌图、资源生命周期、profile 准入与本地替身安装 |
| PlayMode 基础回归 | 115 通过、0 失败、0 跳过 | macOS Editor PlayMode 的仿射图、UTF-8/生命周期和平台拒绝策略 |
| 公共 API 对比 | 一致 | Runtime、Editor、两个示例程序集的公开类型/成员签名与类型继承关系 |
| 原资源 GUID 对比 | 603/603 保留 | 包括移动的源码及文件夹；新文件另分配 GUID |

本轮未重跑所有实际模型权重、独立内容质量检查或移动设备测试。
原生文件和声明未改动；以上编译与基础测试不扩展已有 ABI 的支持范围。
原始宽范围试跑中的缺资产失败保留在 `editmode-results.xml` 和 `regression-results.xml`，
最终三个专项报告分别为 `managed-results.xml`、`targeted-integration-results.xml`、
`playmode-results.xml`，没有把缺失资产或跳过改记为模型验证成功。

Unity 2021 自带测试框架对正向程序集筛选会显式选中 `[Explicit]` 用例。
仅运行常规 Editor 检查时，在隔离工程中使用负向程序集筛选，保留显式用例的跳过语义：

```bash
<Unity> -batchmode -projectPath <isolated-project> -runTests -testPlatform editmode \
  -assemblyNames '!MNN.Unity.Integration.Tests' \
  -testResults <repository>/TestArtifacts~/managed-results.xml \
  -logFile <repository>/TestArtifacts~/managed-unity.log
```

实际模型测试使用对应 fixture 的显式筛选和已准备的本地资产，不用上述常规回归替代。
两个异步文本通道测试通过共享 `AsyncTest.Await` 适配 Unity 的 IEnumerator runner；
目录并发测试跟随公开 curated 列表数量，轮次测试显式提供 VAD 能力，窗口键盘测试固定 Chat 任务。

## 共享实现整理回归（2026-10-25）

移动端测量 fixture 合入 `PlayerRepresentativeTests.Measurements.cs` partial，
辅助类型统一为 `PlayerMeasurements` / `PlayerResults`，移动文件保留原 `.meta` GUID。
平台特有 ABI 入口保留在各任务 `MNNInterop.*.cs`，不新增 Mobile 专用 API。
清除模型虚表槽加载及手工构造的 `std::function` 波形回调，改为直接加载符号与显式能力拒绝。

13 个条件编译分支通过，macOS Mono 66 个静态入口，Android IL2CPP 69 个；
iOS 为 68 个静态入口加 1 个直接解析的 libc++ 析构符号，官方符号审计仍为 69 个；
当前 Android/iOS 插件符号审计通过。macOS PlayMode 为 115 通过、0 失败、93 跳过。
Editor 专项为 33 通过、0 失败，包含实际 Qwen3 Embedding/Reranker 权重、Express 数值与资源释放、
Omni 语音请求拒绝后继续文本推理，以及 ABI/打包检查。移除无调用方的旧 GPU 语音配置逻辑。
记录见项目 `TestArtifacts~/InteropCleanup/`。这些回归不代表新增移动端权重推理已通过，
下列 2026-10-24 记录属于修改前源码，不能用于验证新的 Express/检索或回调路径。

## Android / iOS 实际大模型验证（2026-10-24，历史快照）

移动端扩展后的编译器契约与支持范围见 [MobileInterop](MobileInterop.md)。随包插件启用官方
LLM/Omni，各核对 44 个官方 MNN/libc++ 入口。Android 使用 NDK r25c，iOS 使用 Xcode 27.1，
源码是未修改的官方 MNN 3.6.2。以下是实际权重和输入的设备运行结果：

| 设备与配置 | 通过 | 失败 | 可选跳过 | 实际模型 / 生成次数 |
| --- | ---: | ---: | ---: | ---: |
| OPPO PELM00 / Android 13 / CPU | 116 | 0 | 89 | 4 / 17 |
| iPhone 17 / iOS 27.0.1 / CPU | 116 | 0 | 89 | 4 / 17 |
| iPhone 17 / iOS 27.0.1 / Metal | 116 | 0 | 89 | 4 / 17 |

四个模型为 SmolLM2-135M-Instruct、Qwen3.5-0.8B、SmolVLM-256M-Instruct、
LFM2.5-Audio-1.5B 的已下载 MNN 权重。每组 9 个输入案例：Paris 问答、中文北京、1+1、
374-token 长输入、较长解码、流式 Alice、实际红图/蓝图、实际 WAV 转写。除长输入外各执行两次，
检查预期内容、重复一致、token 上限、中文/英文 tokenizer 所有权和请求后端配置；
媒体入口还检查原生媒体编码计时，避免仅绕过媒体回答。5 个 string 边界/UTF-8/NUL 检查也通过。

预期内容使用固定答案锚点，不是完整语义评分；视觉回复的额外描述可能幻觉。Android 长解码
原始天空解释虽通过锚点和重复检查，内容含事实错误，因此仅作为解码稳定性/性能证据。
iPhone 长解码改为可精确检查的 1–20 数列；CPU 为逗号、Metal 为空格格式，不能据此计算同提示词加速比。
Metal 配置已通过，日志未见初始化回退；MNN 仍可能将部分算子放在 CPU。
89 项跳过不属于已验证模型。多轮上下文、取消、长时间压力、其他量化/输入与设备尚未获得本轮专项验证。
当时的移动端 Express 所有权、原生 Embedding/Reranker、波形回调未实现；不能把本结果描述为所有模型均支持。

每次生成记录 load/首轮/第二轮墙钟耗时、流式首次回调、原生文本 prefill/decode 速度及内存。
例如 Qwen 长解码第二次在 Android CPU 为 10.2 token/s，iPhone CPU 为 71.2 token/s，
iPhone Metal 为 109.2 token/s；三个提示词并不完全相同，只报告独立测量值。
墙钟计时包含任务调度与 Unity 帧轮询；首次 Metal 编译可能显著慢于第二次。
RSS 是整个进程采样值，包含 Unity 与保留缓存；iOS physical footprint 还反映 GPU 等系统计账，
不能用这些值当作单模型净分配。Android / iOS 指标也不能直接等价比较。

修复了 iOS 工作线程缺少 autorelease pool 导致 Foundation/Metal 临时对象延迟释放的问题。
修复前连续重载的 Qwen 组采样 footprint 升至约 3.2 GiB，随后进程 signal 9；没有取得对应
Jetsam 记录，不能确认系统终止原因。补入托管系统运行时 pool 作用域后，完整 Metal 组通过，
Qwen 组采样 footprint 最高约 1.2 GiB，第二次生成约 0.8–0.9 GiB；CPU 最终回归同样通过。

完整报告位于项目 `TestArtifacts~/MobileLlmDirectCpp/validation-report.md` 和
`validation-summary.json`；原始 NUnit XML 与逐模型 JSON 分别在 `AndroidVerified/`、
`IPhoneCpuVerified/`、`IPhoneMetalVerified/`。Android JSON 从最终 build-id 对应 XML 原样提取，
XML 为权威来源。模型和输入哈希见 `device-assets-manifest.json`；各构建 `symbols.json`
记录随包二进制身份。Android 最终通过后增加的 autorelease 作用域仅在 iOS 生效。

重跑先同步当前 package 到隔离 Unity 工程，使用现有资产准备计划：

```bash
python3 Packages/com.eitan.mnn/Tools~/Testing/prepare_mobile_llm.py \
  --models-root Assets/StreamingAssets/MNN/Models \
  --fixtures TestArtifacts~/MNNValidation/Multimodal/Fixtures \
  --run-id unique-device-run --backend CPU
```

通过 adb 或 devicectl 将生成的 `mobile-llm/` 复制到 `Application.persistentDataPath`。
Android runner 增加 `--require-mobile-llm`，要求四个实际模型检查通过；每次生成 build-id，拒绝
旧安装产出的 XML。iOS 先导出测试工程，再签名安装、启动和取回设备报告。Unity 2021 / iOS 27
仍使用隔离宿主 Swift SceneDelegate，未安装到 package。ARM64 模拟器仅通过构建/符号审计。

补充回归：13 个条件编译分支、8 个 Editor ABI/打包检查、114 个 macOS PlayMode 检查通过；
最终 macOS Qwen 真实中文问答专项 1 通过。结构校验不替代以上原生和设备运行。

## Android / iOS 基础直接 C++ 推理（2026-10-24，扩展 LLM 前历史记录）

移动端绑定范围与构建契约见 [MobileInterop](MobileInterop.md)，只调用官方 MNN 3.6.2
C++ 符号。原生源码来自未修改的官方提交 `7e3c6fb5225d3ccf22807ee641332669df34650d`，
使用 NDK r25c / Apple Xcode 27.1 重建，没有编写桥接 C++ 或新增 C ABI 导出。

- Android：OPPO PELM00，ARM64 / IL2CPP / CPU。测试报告 107 通过、0 失败、94 跳过。
  5 项基础原生推理检查全部通过：版本加载、CPU 数值结果、1/4 线程下的 resize、GC 后继续使用、
  异步执行、Auto 选择 CPU 与释放后拒绝访问。跳过项为没有提供权重的可选模型及移动端未实现的
  libc++ string ABI；不代表这些模型已在手机验证。
- iOS：ARM64 真机与 ARM64 模拟器原生静态库各通过全部 26 个绑定入口的符号审计。
  iPhone 17（iPhone18,3，iOS 27.0.1 / 24A446）运行 Unity 2021.3.45f2 / ARM64 / IL2CPP
  测试 Player，Xcode 27.1 签名构建。完整报告为 107 通过、0 失败、94 跳过。
  5 项基础检查全部通过：MNN 3.6.2 加载、CPU 数值/resize/GC/释放、异步数值推理及
  Auto 选择 Metal 后在工作线程推理。使用同一 584 字节 `2*x+1` 模型；跳过范围与 Android 相同。
  本机 Unity 2021 iOS 模块只有 x86_64 模拟器运行库，因此没有执行 ARM64 模拟器 Player。
- macOS 回归：114 项 PlayMode 检查和 8 项 Editor ABI/插件检查通过。
  修复 `ScheduleConfig::Path` 嵌套结构的尾部填充，配置大小改为编译器实测的 104 字节，
  `backupType` / `backendConfig` 偏移为 88 / 96；Android 与 iOS 官方头文件分别核对。
- 13 个条件 C# 编译分支全部通过，移动 IL2CPP 各含 26 个官方绑定入口；
  macOS Mono 保持 64 个入口。条件编译检查不作为真机推理证据。

证据位于项目 `TestArtifacts~/MobileDirectCpp/validation-summary.json`、
`playmode-results.xml`、`editor-results.xml`、`android-player-final.log`、`ios-link-final.log`、
`iphone17-results.xml`、`iphone17-player-scene-delegate.log`、`iphone17-build-scene-delegate.log`，
以及 `TestArtifacts~/MNNValidation/PlatformValidation/Android-IL2CPP-inference-mobile-direct-local-results/`
下的 `results.xml` 与 `summary.json`。各原生构建目录的 `symbols.json` 记录二进制 SHA-256。
Android 测试由设备端 callback 将完整 NUnit XML 写入应用数据目录，再通过 adb 读取，
避免多个 Editor 争用默认 USB 回传端口；报告中的 Editor 进程 `-15` 是读取完成后的主动结束，
不表示 Player 崩溃。未下载新模型。

iPhone 报告同样由设备端 callback 写入 Documents，再通过 `devicectl device copy from`
取回。NUnit 根节点因包含忽略项而标为 `Skipped`；201 个叶节点实际为 107 通过、94 跳过，
没有失败。初始未适配的 Unity 2021 宿主在 iOS 27 Scene 生命周期检查处触发启动断言，尚未调用 MNN；
隔离导出工程添加 Scene manifest 和 Swift SceneDelegate，转发到现有 Unity AppDelegate 后完成测试。
该 Swift 文件和准备脚本 `TestArtifacts~/MobileDirectCpp/prepare_iphone17_scene.py` 仅适配测试宿主，
没有调用 MNN、编写 C++ 或添加推理桥接；package 未安装这一生命周期适配。使用相同旧 Unity / 新 SDK
组合的应用仍需自行处理 Scene 生命周期。详细运行记录见 `TestArtifacts~/MobileDirectCpp/iphone17-validation.md`。

重跑时将当前 package 同步到 `TestArtifacts~/` 下的隔离 Unity 工程，再执行：

```bash
python3 Packages/com.eitan.mnn/Tools~/Testing/run_platform_tests.py \
  --unity /path/to/Unity --project /path/to/project/TestArtifacts~/isolated \
  --platform Android --backend IL2CPP
python3 Packages/com.eitan.mnn/Tools~/Testing/run_platform_tests.py \
  --unity /path/to/Unity --project /path/to/project/TestArtifacts~/isolated-ios \
  --platform iOS --backend IL2CPP --export-only
```

第二条仅导出包含测试的 Xcode 工程；它本身不能证明原生链接或设备推理通过。

## 对话与媒体

- Chat：真实多轮角色对话与逐 token 流式回复，显示耗时、token 数和停止/长度上限状态。
- 视觉与 Omni：选择或拖入 PNG/JPEG/BMP，每轮一张图；显示缩略图与可移除附件。
- 音频与 Omni：上传 WAV，或录制最长 30 秒麦克风音频；当前 Omni 原生波形回调未实现，不提供语音回复。
- `Spoken replies`：普通聊天完成后自动播放生成语音；回复下方也提供 Play、Stop、Save audio。
  播放状态来自 Unity 音频后端，播放进度驱动音量反馈；失败会显示错误，不用计时器模拟播放。
  当 Unity Editor 全局静音时，回复行会显示 `Enable & Play`；只有用户点击后才解除全局静音并播放。
- Embeddings：真实向量、余弦相似度、维度、耗时和 JSON 复制。
- Reranking：真实查询/文档相关性概率。
- 任务工作区：独立入口覆盖当前 Model Manager 的全部分类，按 Language、Multimodal、Audio、Vision、Generation、Retrieval 分组。
  左侧 `Tasks & models` 打开任务目录，显示本地模型数量、可运行或模型准备状态。
- Speech recognition：专用 WAV 上传/录音与 Transcribe，默认逐字转写；使用支持音频输入的 LLM，结果不追加到聊天历史。
- Image understanding、Audio understanding、Omni、OCR/document reading：专用指令与媒体选择，支持单次流式结果和复制。
  Omni 当前提供文本与媒体输入，语音输出选项不显示。独立 TTS 结果可播放、停止、保存 WAV。OCR 支持 PP-OCRv5 专用整页管线，也可使用视觉语言模型提示词读取文档。
- Code generation、Safety/moderation：按模型类型筛选并运行独立文本请求；审核须遵循模型卡的提示词与标签格式。
- Image generation：支持 SD 1.5 `general` 文生图（提示词、负面提示词、步数、guidance、seed、PNG 保存）；Sana Edit V2 参考图编辑管线可运行、显示并保存结果，但本轮语义颜色编辑效果尚未达标。
- Speech synthesis：支持 Supertonic 1/2/3（已安装音色、语种、语速、步数、seed、播放和 WAV 保存）。Bert-VITS2 陈曦中文、英文及中英混读（44.1 kHz）和 Piper 英文 Amy/Kathleen/Ryan（16 kHz）也支持合成、播放和 WAV 保存。
  FireRedTTS3 选择参考 WAV 后，在其下方的 `Reference audio transcript (required)` 多行输入框填写参考录音的准确原文；上方 `Instruction` 填写要新合成的内容。原文填写后才会启用 Run。
- 所有成功生成音频的结果（独立 TTS、Omni 和 Chat spoken replies）统一显示 `RTF`、音频秒数和生成秒数。
  `RTF = 生成耗时 / 音频时长`，小于 1 表示快于实时；例如 1 秒生成 4 秒音频，RTF 为 0.25。
  计时从后台任务实际开始计算，覆盖文本/参考音频处理、模型推理、GPU 回读和 PCM 输出，
  不含模型加载、后台排队、播放与 WAV 保存。语音聊天的耗时同时包含文本回复生成，不能当作独立声码器速度。
  首次生成包含该次推理的预热/编译开销；取消、失败或没有音频的结果不显示 RTF。
  对话历史仅保存时长等指标，不保存 PCM；旧记录缺少音频时长时不补算 RTF。
- Whisper、SenseVoiceSmall 与 Sherpa-MNN Zipformer 提供 ASR 分类、模型安装和目录发现。Whisper 的五个固定 profile 使用 CPU greedy 管线，均支持英文，small 可选中文；SenseVoiceSmall 使用已验证的 CPU CTC 管线，Zipformer 使用已验证的托管 greedy 管线。指定 PP-OCRv5/v4、YOLOv8 及固定图像任务 profile 已接通；新增 DeepLabV3 分割和 Candy 风格迁移各有独立页面。支持限于各 profile 明确的权重、输入输出协议和验证平台，其他导出仍需适配。

扫描 `Assets/StreamingAssets/MNN/Models`、`Application.persistentDataPath/MNN/Models` 和设置中的
额外目录。任务在顶部菜单切换，模型菜单随任务筛选。媒体能力读取 `llm_config.json`；
Embedding/Reranker 及专用 pipeline 按安装标记中的仓库 ID 或目录名识别。右上角更多菜单提供刷新、Model Manager 与 Markdown 导出。
刷新保留仍存在的模型与会话；模型缺失时保留文字并禁用发送。

## 按任务准备模型

1. 在顶部工作区菜单选择任务，或从左侧 `Tasks & models` 目录进入。
2. 无兼容模型时显示三步准备页面：推荐仓库/模型卡、所需完整文件、下载与选择方法。
   图片生成推荐 `MNN/stable-diffusion-v1-5-mnn`、`MNN/MNN-Sana-Edit-V2`；
   独立 TTS 推荐 `MNN/supertonic-tts-mnn`、`MNN/bert-vits2-MNN`、`MNN/piper-voices-MNN`；
   转写推荐 `MNN/LFM2.5-Audio-1.5B-MNN`；离线中文/英文可安装 `wo105090/SenseVoiceSmall-MNN`（CPU，85 ms–60 秒）或 Sherpa-MNN streaming Zipformer 可在 ASR 分类中准备：
   `MNN/sherpa-mnn-streaming-zipformer-bilingual-zh-en-2023-02-20`（中文/英文，int8）和
   `MNN/sherpa-mnn-streaming-zipformer-en-2023-02-21`（英文）。两者需要完整保留 `config.json`、
   `configuration.json`、encoder/decoder/joiner `.mnn` 和 `tokens.txt`；Zipformer 已接入 Studio 的托管 greedy 管线。
   视觉推荐 `MNN/SmolVLM-256M-Instruct-MNN`，
   Omni 推荐 `MNN/Qwen2.5-Omni-3B-MNN`。推荐不是对所有版本、量化或设备的兼容认证。
3. 点击 `Find / download models`，Model Manager 按任务筛选完整官方 MNN catalog，允许搜索模型名、标签与任务名。
   TTS、ASR 单独分类，不再混入音频语言模型；`Show all models` 可取消任务筛选，新任务与辅助资源始终可从全目录找到。
   用户在 Model Manager 查看模型信息并点击下载，不自动下载推荐权重。
4. 完整仓库安装到 `Assets/StreamingAssets/MNN/Models` 后 Studio 自动刷新可用模型；
   在模型菜单选择，或从 Model Manager 的 `Open in Studio` 回到原任务。
   也可 `Choose local folder…` 直接选择模型目录；Additional model folder 接受模型目录或其父目录。
   官方安装标记存在但没有 LLM config 的 TTS/Diffusion/ASR 仓库也会被发现，用于准备和定位文件。
5. 可运行任务首次请求时按需加载；尚未接入的专用 pipeline 显示准备状态，不进入 LLM 加载器。

## 会话管理与搜索

左侧提供 New chat、Search chats、Tasks & models、文件夹和会话列表；大部分侧栏面积用于会话。

- 首次完成回复后，空闲时使用同一已加载 LLM 生成简短标题，保持用户语言。
  命名是独立角色请求，不追加到消息历史；失败保留首条消息标题，菜单可手动重试。
  设置可关闭自动命名。手工重命名会保留，不再被自动命名覆盖。
- 会话和文件夹可以拖拽排序，蓝线提示插入位置；会话行上半部插到前面，下半部插到后面。
  拖到文件夹上将会话移入文件夹，拖到 Chats 区域移回未分类列表。
- 排序保存到项目内历史文件；发送消息、生成标题、重新打开窗口都不会重排已有会话。
- 右侧更多菜单或右键可重命名、移动、生成标题、删除。会话直接删除，没有回收站。
  删除文件夹保留其中会话，并移回 Chats。
- 点击 Search chats 或 Cmd/Ctrl + K 打开居中搜索弹窗，搜索标题以及双方消息正文。
  结果显示会话标题、说话方和匹配片段；点击或按 Return 打开会话并滚动到匹配消息，
  用短暂蓝色边线标出位置。↑/↓ 选择结果，Escape 关闭。最多显示前 100 个结果。

## 输入与快捷键

| 操作 | 快捷键 |
| --- | --- |
| 发送 | Enter（输入法组字时不发送） |
| 换行 | Shift + Enter |
| 新建会话 | Cmd/Ctrl + Shift + O，或 Cmd/Ctrl + N |
| 展开/收起侧栏 | Cmd/Ctrl + Shift + S，或 Cmd/Ctrl + B |
| 搜索会话及正文 | Cmd/Ctrl + K |
| 复制最后回复 | Cmd/Ctrl + Shift + C |
| 聚焦输入框 | Shift + Escape |
| 停止当前回复/取消录音 | Escape |
| 语音面板静音切换 | Space |
| 结束语音对话 | Escape（语音面板内） |

输入区 ＋ 菜单只显示当前模型支持的附件类型；没有提示词时图片/音频使用默认提示。
右上角设置弹窗提供线程数、Thinking、token 上限、自动命名、停顿后发送和减少动态效果。
回复支持标题、段落与围栏代码块、代码复制，其他 Markdown 保留为文字，不执行模型内容。
向上滚动停止跟随输出，点击 `↓ Latest` 回到底部。

## 流式输出与停止

使用官方示例的 `response(..., 0)`、`generate(1)`、`stoped()` 流程；仅在同步原生调用之间由
模型拥有线程读取快照。没有自定义 ostream、并发读取原生上下文或模拟打字动画。
UTF-8 分片不会在汉字/Emoji 尚未完成时显示替换字符。
后台更新以最新快照合并，主线程最多 20 Hz 更新文本布局。停止保留已生成内容，
在下一个 token 边界结束；预填充、媒体编码和语音波形生成不能在同步原生调用中途打断。
停止发生于波形生成期间时，返回后标记取消并抑制播放。
关闭窗口请求停止，并在原生调用退出后释放模型；不会在推理过程中销毁模型句柄。

## 语音对话

音频输入与语音输出均受支持时，Voice 菜单提供语音对话面板：动态小球、真实 RMS 音量反馈、
Listening / Thinking / Speaking / Muted / Error 状态、静音、发送本轮、打断、结束、返回聊天。
减少动态效果可关闭持续形变。该面板需要后端提供音频输入及语音输出；当前原生 Omni 不上报 SpeechOutput，因而不启用。

流程为录音 → 停顿检测或手动发送 → 流式文本推理 → 生成并播放音频 → 再次收听。
本地停顿检测要求检测到声音后静音约 1.15 秒，属于音量阈值判断，不是神经网络 VAD。
这是逐轮语音对话，不是 ChatGPT Live 的全双工音频流；播放期间麦克风暂停。
静音、打断与结束会正确管理下一轮录音调度。设备权限、扬声器实际听感与环境噪音须在目标机器验证；
自动测试不申请麦克风权限、不录制环境声音。

## 布局、性能与存储

采用 ChatGPT/Codex 风格侧栏、轻量顶部、居中阅读区、独立底部输入框及 Apple HIG 的留白与层级。
侧栏宽 208 GUI points，阅读区最大 760，最小窗口 760×540；同时验证 1180×800 布局。
浅/深主题采用语义颜色，文字对比度检查达到 4.5:1。语音面板动画最多 30 Hz，支持减少动态效果。
样式、Markdown 解析与图片预览缓存复用；图片最长边缩为 320 像素。小球使用程序化绘制，无图片下载。
VoiceOver、Dynamic Type、系统 Increase Contrast 尚未全面验证，不能视为完整原生 macOS 无障碍实现。

模型按需加载，每个 Studio 窗口只保留当前模型的一个原生实例；同模型会话会复用它。
切换模型或任务、刷新后模型不可用、修改模型设置、手动卸载或闲置 5 分钟会释放实例。
首次加载仍在主线程执行，可能短暂阻塞；推理在工作线程串行执行。
完整文字记录保留到本地历史文件 32 MiB 的容量限制，不再按消息条数裁掉开头。每条会话保存
摘要及其覆盖的消息位置；后续压缩只处理新增的旧轮次。压缩按时间顺序分批进行，不会从过长历史中
静默跳过开头；只有整条消息成功压缩后才推进覆盖位置。每次成功的摘要检查点立即写入会话，回复取消
或失败时仍可继续使用。历史媒体只保留附件路径与附件标记，不会假称摘要记住了图片/音频内容。

上下文使用当前模型的官方 tokenizer 计数，并预留聊天模板、输出 token 和当前媒体预算。Studio 默认
上下文预算为 8,192 token，可在 Model settings 调整到 2,048–32,768；不同模型的可用上下文窗口不同，
用户应设在模型实际支持范围内。多模态预留是保守估值，视觉裁剪等预填充开销无法仅从文本 tokenizer
精确推导；超预算时会明确报错，不会默默截断。上下文超过约 12,000 字符或 token 预算不足时自动压缩，
同时保留最近约 8 条消息。摘要最多 2,400 字符，模型仍可能遗漏事实，重要决策建议由用户确认摘要。
对“我的名字/偏好/必须/记住”等明确陈述以及常见中文对应表述，压缩还会保留有界的用户原句片段，
降低小模型摘要时漏掉关键身份与偏好的概率；这不能代替用户检查摘要。单条输入仍受 16,000 字符上限约束。

Studio 使用模型加载时的轻度重复惩罚采样，普通 Runtime API 的默认 greedy 行为不变。流式解码还会
检测连续三次以上、较长且完全相同的文本段，在 token 边界停止并保留第一份内容。该保护针对明显的
机械循环，不判断语义质量，也不能保证模型永不重复。
发送失败或取消后遗留的连续用户轮次会在构造官方角色历史时合并，避免把非法/歧义的相邻角色消息传给模型。

历史、录音、模型缓存均位于当前工程 `Library/MNN/Studio`。`history.json` 原子保存标题、
文件夹、顺序、角色、草稿、附件路径和结果指标；没有自动淘汰旧会话，文件大小限制为 32 MiB。
不复制图片、不序列化原生会话或生成波形。附件依赖原文件；生成语音需在离开会话前 Save audio。
关闭窗口清理自动录音和缓存，活跃原生任务延后清理。Markdown/WAV 导出使用开发者选择的路径。
没有额外下载模型或依赖；测试日志、构建和诊断全部在项目 `TestArtifacts~/`。

## 复用模块与测试

`Editor` 按编辑器功能和职责组织，程序集定义与 `AssemblyInfo.cs` 留在根目录：

```text
Editor/
├── Localization/       编辑器本地化
├── ModelManagement/    模型仓库、下载任务和模型管理窗口
├── Studio/
│   ├── Core/           会话、历史、上下文、推理任务、语音与搜索逻辑
│   ├── UI/             Studio 专用绘制组件、弹窗、Markdown 和语音球
│   └── Window/         MNNChatStudio 窗口及 Workspace、Library、Search 等 partial
└── UI/Common/          可被其他编辑器窗口复用的通用控件
```

Studio 核心将 `MNNStudioSession`、`MNNStudioHistory`、`MNNStudioContext`、`MNNStudioNaming`、
`MNNStudioSearch`、`MNNStudioModels` 和语音逻辑分开维护；`MNNStudioUI`、`MNNStudioMarkdown`、
`MNNStudioSearchWindow`、`MNNStudioNamePopover` 与 `MNNStudioVoiceOrb` 负责可复用视图组件。
运行时新增 `MNNLlm.Streaming.cs` 与 `MNNInterop.Streaming.cs`，按推理职责扩展 C# partial。

测试覆盖真实 IMGUI 布局、鼠标拖拽及落点、持久化、自动命名隔离、内容搜索与消息定位、
快捷键、语音状态布局、静音端点、主线程进度、取消与关闭、UTF-8，以及真实文本/VL/音频/Omni 流式推理。
音频测试检查实际生成波形的非静音能量、Unity 后端播放状态、播放位置推进和停止，
普通聊天的 Spoken replies 自动播放有替身后端 UI 测试；旧 Omni 语音/Player 波形记录属于历史实现，
移除手工回调后不再代表当前能力。角色历史、流式回调和停止后再次推理继续按各自用例验证。
拖拽测试发送 IMGUI 鼠标与拖放事件；隔离系统拖拽启动调用，避免 macOS 模态拖拽阻塞批量测试。

此前聊天工作区验证结果见 `TestArtifacts~/MNNValidation/Studio/live-summary.json` 与 `live-final-results.xml`；
平台编译/Player 报告在 `TestArtifacts~/MNNValidation/PlatformValidation/`。
条件编译成功不能作为其他平台真实推理支持或所有模型兼容的证据。

此前完整 EditMode：154 通过、0 失败、2 项 Explicit 联网测试跳过（总计 156）；
Studio/流式相关 55/55，最终 UI 回归 9/9。macOS arm64 与 x86_64 Mono Player 各 115/115；
x86_64 使用 Apple Silicon + Rosetta。13 组平台条件编译检查全部通过。

本次任务目录扩展 EditMode：48 通过、0 失败、0 跳过。验证见 `TestArtifacts~/StudioTasksValidation/results.xml` 与 `unity.log`：
覆盖模型分类与下载筛选、专用模型隔离、完整文件夹发现、各任务在紧凑/宽窗口的真实 IMGUI 布局，
以及通过专用工作区运行已有视觉、音频转写和 Omni 模型。不将模型准备页测试作为对应 pipeline 已接入的证据。

### 运行已验证的生成模型

在 `Speech synthesis` 中选择 Supertonic 完整模型文件夹，输入英语文本；选择音色、步数和语速后运行，结果可播放、停止或保存 WAV。默认优先使用已安装的 fp16，否则使用完整 INT8/fp32 图组；语速默认 1.05。Supertonic 2/3 可选择十种音色与版本对应的语种标签，目前十种音色仅完成英语样本内容验证。每次最多接受 500 字符。长文本优先按句末、其次按空格拆成不超过 160 字符的片段（补句号时最多 161 字符），保留小数与完整单词；每段预测时长上限 30 秒。片段之间添加 80 ms 停顿和 5 ms 边缘淡化，避免拼接爆音。超过 160 字符的连续单词或数字会明确拒绝。点击 `Use example` 会替换当前输入并结束旧的文本编辑状态，随后可继续修改。数字、小数、金额和百分比先展开为英文读法，再按展开后的长度分段。换行、常见不可见控制符和 emoji 会规范化，纯标点、纯 emoji 和超出词表的文字会在推理前明确拒绝。当前随包 macOS Metal 图在长文本、数字和密集标点下会产生错误时长、丢失语音和声码器尖峰，因此 Metal/Auto 下 Supertonic 的四个阶段均使用 CPU，合成速度会相应降低。异常音频不会通过硬限幅作为正常结果返回。

在 `Image generation` 中选择 SD 1.5 的仓库根目录（含 `general`）或 `general` 本身，输入提示词后运行。默认 20 步、guidance 7.5、seed 42，输出固定 512×512，可预览并保存 PNG；默认自动选择可用的 Metal GPU，失败时按阶段回退 CPU，生成耗时取决于机器；Stop 在当前模型阶段结束后生效。模型需要三个 `.mnn` 及对应 `.mnn.weight`，并保留 CLIP 的 `vocab.json` / `merges.txt`。

Sana Edit V2 请选择完整仓库根目录，再选择参考图片并输入编辑指令。参考图会居中填充并缩放至 512×512；默认 10 步、guidance 4.5、seed 42。结果可预览并保存 PNG。已验证管线和资源生命周期，但本轮“红车改蓝车”的结果仍主要为红色，颜色编辑效果尚未达标。

在同一个 `Speech synthesis` 工作区中也可选择：

- `MNN/bert-vits2-MNN`：下载 `config.json`、`tts_generator_w_bert_chenxi_0310_int8.mnn`、`common/mnn_models/chinese_bert.mnn` 及其 `.weight`，以及 `common/text_processing_jsons`。支持陈曦音色的中文、英文和中英混读，例如“你好，Hello world，欢迎使用语音合成。”，最多 300 字符。英文使用 `eng_dict.bin` 发音词典和重音；词典外单词按字母拼读，数字按英文读法展开。此生成器与上游一样使用零英文 BERT 特征，无需加载 `english_bert.mnn`。
- `MNN/piper-voices-MNN`：下载 `config.json`、所需的 `en_US-*-low_fp16_public.mnn` 和 `espeak-ng-data`。`espeak-ng-data` 默认自动从模型文件夹读取，无需选择；**Browse…** 打开目录选择器，手动选择数据文件夹时检查其完整性并按模型记住。eSpeak-NG 工具从模型目录、环境变量、常见安装位置、PATH 和项目已有测试依赖自动查找；缺失时安装上游 eSpeak-NG（macOS 可用 `brew install espeak-ng`），或设置 `MNN_ESPEAK_NG_PATH`。选择 Amy、Kathleen 或 Ryan，输入英文，最多 1000 字符。文件缺失或工具启动失败会给出错误提示。

本项目的测试依赖位于 `TestArtifacts~/GenerationImplementation/Dependencies`，Studio 会自动发现其中的
`espeak-ng/run-espeak-ng.sh`，该脚本为下载的上游可执行程序设置动态库搜索路径；不需要手动选择或重复安装。
Browse 选择的是数据目录，不是此脚本或可执行文件；选择/取消目录均保留当前输入文本。
旧版若误将 `af_dict` 等字典文件保存为工具路径，Studio 会自动清除该设置并重新发现工具；数据文件不会被当作程序启动。环境变量中的无效工具路径也会跳过。
运行时根据 `config.json` 判断架构，模型文件夹改名仍能正确识别。播放/保存输出与 Supertonic 共用。

本次验证模型保存在项目 `TestArtifacts~/GenerationImplementation/Models`，可用 Choose local folder 直接选择，无需重复下载到 Assets。

## 新增 TTS 格式与验证边界

`Speech synthesis` 已识别 Kokoro 的原始音色 bin、`voices.mnn` 常量音色及 Sherpa v1.0
三种布局，显示已安装音色；输入需被配套中文/US English 词典覆盖。
结果为 24 kHz，可播放、保存。混合运行的偶发错误尚在定位，当前不应视为稳定可用。
KindBrave Kokoro v1.1 的外部权重已接入；其他语言前端尚未实现。

`developerabu/vits-tts-mnn` 进入相同工作区后显示 20 个已标注说话人与 14 个已标注情绪。
`Use example` 按所选说话人填入对应文字，输入最多 300 字符；不显示该图没有的语速、
步数和 seed 控件。真实后端、播放和 WAV 保存已经检查，输出 24 kHz。
所有说话人/情绪仅完成非静音波形检查；Tamil 内容做过独立 ASR 抽查，存在部分错误。
其他语言质量仍需分别验证。目录显示为 TTS 并不表示已经实现对应运行时；
Qwen3-TTS 的其他导出及其他 VITS 等仍需专用适配与真实结果测试；Pocket/IndicF5 的后续验证见下文。DhVaani 与 Qwen3 显式 KV 导出的最新验证见下文。

## CosyVoice-MNN

在 Model Manager 搜索 `Cosy-Voice-MNN`（Hugging Face 仓库
[VicenTrent/Cosy-Voice-MNN](https://huggingface.co/VicenTrent/Cosy-Voice-MNN)），点击下载。
默认仅下载完整合成包及说明文件，不下载 enrollment 包或 APK。完整 ZIP 约 1.4 GB，
解压阶段会额外占用约 1.4 GB；需要为 ZIP、解压文件及已有安装预留空间。
下载服务可通过 Hugging Face 镜像池重试；内容固定到已验证 release，安装前校验 SHA-256。

下载完成后选择 **Speech synthesis** 工作区和该模型，输入中文，点击生成即可试听及保存 WAV。
也可使用 `Choose model folder` 选择手工解压后的完整目录。安装根目录改名不影响识别。
CosyVoice 输出为 24 kHz 单声道，提供 `Default` 内置音色和随机种子；每次最多 200 字符。
英文和中文长输入会按句子、分句和完整单词拆段，缺少句末标点时补齐；段间增加 40 ms 停顿，
全部完成后才返回音频。某段达到 token 上限会报告错误，不播放前半段作为成功结果。
分段可能改变韵律；需要检查特定文本的读法时，可保存 WAV 后试听。
缺少资源或文件大小不匹配时不会启用生成。使用范围及取消语义见 [API](API.md)。

## IndexTTS-2.5 / FireRedTTS3

在 Model Manager 搜索 `IndexTTS-2.5-mnn` 或 `fireredtts3-mnn`，下载完整模型配置。
Index 约 3.6 GB，FireRed 约 5.4 GB；下载失败可恢复，文件通过 SHA-256 检查后才发布安装。
FireRed 的 tokenizer 会自动从其上游仓库补齐，无需手动拼目录。
下载复用 Hugging Face 镜像池；这两个仓库发布的是独立文件，无需解压。

打开 **Speech synthesis**，顶部模型菜单可在已安装的 TTS 模型间快速切换；
选择 IndexTTS 或 FireRed 后，可直接录制参考声音，或选取已有的 **0.5–15 秒 WAV**。
录音会在 15 秒自动停止，也可以手动停止或取消。两个参考音频模型间切换会保留录音。
选择 Chinese / English 后输入待合成文本。FireRed 还必须填写录音中实际说出的参考原文；
它与待合成文本是两个独立输入。缺少录音或 FireRed 原文时不能开始生成。

Index 输出 22.05 kHz，FireRed 输出 24 kHz，均可在 Studio 播放及保存 WAV。
也可通过 `Choose model folder` 选择手动准备的完整目录，目录改名不影响识别；
缺权重或 tokenizer 时不会启用生成。文本、录音格式、平台范围和模型许可见 [API](API.md#indextts-25-与-fireredtts3)。

## YOLOv8 目标检测

在模型管理器下载 ModelScope `rap20171201/YOLOv8-N-MNN` 后，安装器校验图并生成
`mnn_yolov8.json`。在 Studio 选择 Object detection，加载模型、附加 PNG/JPEG，点击 Run。
结果列出类别、置信度及原图 `[x, y, width, height]`，并提供标框预览及 PNG 导出。
图片在主线程解码为独立 RGB 副本，后台 worker 使用同一 `MNNYoloV8` API。
缺少或无效的 profile 会阻止运行；其他 YOLO 导出不能仅凭文件名当成已支持。

## PP-OCRv5 页面与文本行

下载 `qefro/pp-OCRv5-mnn` 后，在 OCR / document reading 工作区选择英文根模型，
或对应语言的 `recognizers/` 子模型。附加 PNG/JPEG 后点击 Run，
默认先检测文字区域、校正倾斜 crop，再识别多行文字。结果包含换行文本和原图四角标注预览，
可复制文字或保存标注图；所有原生计算在 worker 上执行。

如果图片已经裁剪为正向单行，勾选 **Cropped text line** 直接识别，避免再次检测和裁剪。
请按语言选择 profile；未安装检测图的旧安装继续使用原有单行管线，重新安装该仓库可补齐检测器。
十二组页面 profile 共用一份检测图，各自使用经 SHA 校验的识别图和字典。
`chinese-v4` 和 `chinese-fp16` 另外关联配套检测图，安装后共有十四组可选 profile。
v4 的部分空格准确率较标准 v5 低，模型输出保持原样；默认仍是标准 v5。
新增阿拉伯文、天城文、Eastern Slavic、韩文、泰米尔文、泰卢固文和泰文可从子模型列表选择。
阿拉伯文输出恢复逻辑字符顺序；韩文单行 Auto 使用 CPU，且输入留白可能影响词间空格。

当前整页 Auto 采用通过逐字校验的 CPU 配置。受支持运行时上的 Metal 检测与部分整页 crop
未通过质量门槛；独立文本行曾通过 Metal 的结果不能证明整页 GPU 正确。
当前不含 0/180 度方向分类、复杂版面排序、弯曲文字矫正或其余未验证语言。
同一工作区中的视觉语言模型仍可按原有方式读取文档。

### Standalone Zipformer speech recognition

Choose **Speech recognition**, install either official 2023-02 English or
Chinese/English Zipformer with Model Manager, then choose or record a WAV and
click **Transcribe**. Installation verifies the graph/token hashes and creates
`mnn_zipformer.json`. Earlier raw downloads need to be reinstalled to prepare this
profile. The standalone recognizer uses CPU by default and returns a transcript;
it does not use the instruction field to translate or follow prompts. Audio-language
models remain available for prompt-driven audio tasks. The supported recording
formats are mono/stereo PCM16/24/32 or float32 WAV at 8–96 kHz. Transcription
can contain recognition errors, particularly code-switching in the bilingual model.

### Whisper and SenseVoice transcription

In **Speech recognition**, Model Manager supports the five pinned Whisper
profiles listed in [API](API.md#whisper-speech-recognition), each accepting
25 ms–29.5 seconds. All default to English; the small profile shows a
**Recognition language** selector for English or Chinese. Its bilingual fixture
retains key Chinese content with mixed Traditional Chinese and English output.
You can also install `wo105090/SenseVoiceSmall-MNN` or
`xinliu/sensevoice_mnn` (85 ms–60 seconds). Installation verifies pinned assets
and prepares the matching profile. Choose/record a WAV and click **Transcribe**.
These standalone ASR models transcribe audio; instructions do not enable
translation or change their decoding tasks. Whisper has no token timestamps.
Old raw installations need profile preparation through Model Manager.

SenseVoice uses the FunASR training frontend: seven centered fbank frames every
six frames, replicating both edges and retaining the final partial window before
fixed CMVN. Paraformer's streaming export retains its separate causal window
contract. The ModelScope SenseVoice runner appends the public MNN `ArgMax` op to
an in-memory copy of its verified graph, returning int32 IDs instead of the full
25,055-value vocabulary per frame. The installed model and weights are unchanged;
the Hugging Face profile retains its Express logits route. This reduces managed
output transfer, not the model's internal vocabulary projection or native tensor storage.

For independent frontend references, run
`Tools~/Testing/generate_sensevoice_reference.py --root <existing-ASR-Additional-root>`
with locally installed numpy and kaldi-native-fbank. It writes
`en-sensevoice-training.f32` and `zh-en-sensevoice-training.f32` beside the existing
models. Set `MNN_TEST_SENSEVOICE_ROOT` for `SenseVoiceIntegrationTests`, including
full-logit versus reduced-output parity across changing shapes. No assets or
dependencies are downloaded. These checks exercise the existing supported MNN ABI;
they do not establish a portable official C# API or qualify other platforms.

### Qwen3-ASR transcription

In **Speech recognition**, install the verified Qwen3-ASR 0.6B INT8 profile from
`hb-dev` on Hugging Face or `huangzhengxiang` on ModelScope. Attach or record a
0.1–30 second WAV and choose **Transcribe**. Studio uses the prepared ASR
pipeline (Auto prefers Metal with CPU fallback) and reports both LLM and audio backends. The original ASR language
template transcribes English and Chinese/English fixtures without a language
selector. Timestamps are unavailable. Cancel is observed between decoder tokens;
audio encoding and prefill must finish first. A missing audio graph or external
weight disables the prepared pipeline. Other Qwen3-ASR repository layouts require
separate adapters and validation.

## Paraformer 中文识别

在 Model Manager 安装固定版本的 KindBrave 或 guoera Paraformer online 8404-token
仓库后，在 Speech recognition 中选择该安装目录并提供 WAV。Studio 使用
`mnn_paraformer.json` 路由到独立中文 ASR 管线，结果显示实际 encoder/decoder 后端。
默认 CPU，输入 85 ms 至十分钟，无时间戳。中文夹英文可能误识别，英文质量未验证。
未准备或文件不完整的仓库无法运行；改名目录仍使用 profile 识别任务。

## Voice activity / Audio processing

安装 `Jeryuk/silero_vad-MNN` 后，在 Audio processing 中选择模型并提供或录制
WAV，点击 Run。结果按秒列出语音开始和结束时间，静音显示 No speech detected。
模型支持 8–96 kHz、最长十分钟，内部使用固定 16 kHz VAD；默认 CPU 单线程。
结果是语音区间，不包含文字转录或说话人身份。该量化导出与 float ONNX 的
已知边界误差见 [API](API.md#silero-vad)；其他音频模型仍需要各自的任务适配器。

## Keyword spotting (KWS) 中文唤醒词

已安装 `Jeryuk/kws-zipformer-wenetspeech-3.3M-MNN` 固定版本。在 Studio 选择
**Keyword spotting (KWS)** 和该模型，点击 **Load demo recording → Run**，应显示
“文森特卡索”和“法国”。**Use example wake words** 恢复“小爱同学 / 你好问问 / 小米小米”；
**Start listening** 使用麦克风连续检测，**Stop listening** 或 Escape 停止。
自定义关键词使用带声调的拼音声母/韵母 token，详见 [KWS 使用说明](KeywordSpotting.md)。

2026-10-23，Unity 2021.3.45f2、macOS ARM64 Mono 的验证证据：

- 独立 KWS 任务调整后，`TestArtifacts~/Kws/dedicated-task.xml`：102/102 通过。
  覆盖模型管理器分类、旧缓存离线迁移、任务筛选隔离、嵌套安装、Studio 窗口与真实演示推理。
  KWS 分类与任务追加在枚举末尾，保留已有序列化值；已安装模型无需重新下载。
- `TestArtifacts~/Kws/verified-editor.xml`：127/127 通过，包括 11 个 KWS 单元测试、
  5 个真实模型集成测试、1 个固定下载/嵌套安装测试，以及 Studio、模型分类和特征提取回归。
  真实窗口在 980×820 与 660×620 下渲染，演示通过 Studio worker 返回两项结构化检测。
- 七段发布录音逐项断言；第 3–6 段各命中两项，第 0–2 段无匹配。
  静音及无关默认短语不触发，不规则 PCM 分块与完整录音结果一致。
  另验证 44.1 kHz 重采样、取消后复位、异步输入副本、重复检测和损坏模型拒绝加载。
- `TestArtifacts~/MNNValidation/PlatformValidation/StandaloneOSX-Mono2x-arm64-inference-kws-verified/summary.json`：
  独立 Player 的 `kws_verified=true`；整体 111 通过、0 失败、88 因未提供其他模型资源跳过。
  KWS Player 测试实际识别演示、拒绝静音，并验证复位、取消及释放；不是桥接导出的测试。
- 包结构校验通过：1110 entries、6 assembly boundaries。

Editor 测试设置 `MNN_TEST_KWS_MODELS` 为已准备的模型目录，选择
`KeywordSpotterTests`、`KeywordSpotterIntegrationTests` 以及
`ModelDownloadTaskTests.KeywordSpotterManagerPinsInstallsNestedProfileAndDetectsDemo`。
运行 Player 验证使用已有隔离测试工程，不自动下载模型：

```sh
python3 Packages/com.eitan.mnn/Tools~/Testing/run_platform_tests.py \
  --unity /Applications/Unity/Hub/Editor/2021.3.45f2/Unity.app/Contents/MacOS/Unity \
  --project TestArtifacts~/CosyVoice/UnityProject \
  --platform StandaloneOSX --mac-architecture arm64 \
  --kws-root Assets/StreamingAssets/MNN/Models/Jeryuk--kws-zipformer-wenetspeech-3.3M-MNN \
  --label-suffix verified
```

当前使用既有官方 MNN C++ 绑定和本项目限定的 macOS Mono ABI，没有新增 C++、
自定义 C 导出或修改官方 MNN 源码。本次未进行真实麦克风拾音、远场/噪声误唤醒率评测；
录音与流式测试不代表任意唤醒短语的实际质量，也不证明其他平台或 IL2CPP 可用。

## Audio processing · Smart Turn

安装 `Jeryuk/smart-turn-v3.2-gpu-MNN`，在 Audio processing 中选中它，提供或录制
当前说话轮次的 WAV，停顿后点击 Run。结果展示完成/未完成、完成概率和实际使用的
最多八秒上下文。它与 Silero VAD 共用工作区，模型选择决定执行哪种任务。
后台运行、取消和结果展示沿用 Studio 工作任务；不生成聊天记录或语音转录。
预处理、阈值与平台边界见 [API](API.md#smart-turn-endpoint-detection)。

## Kokoro v1.1 external weights

`KindBrave/kokoro-multi-lang-v1_1-MNN-Custom` 新增双平台固定安装方案：
Hugging Face revision `8902386456c12c2c325d14eeb122e257be627966`，ModelScope
revision `2a80a70abf25777810d89099ae1451f0af253318`。八个资源逐项固定尺寸和 SHA-256，
两站的图、原始外置权重、词典和 103 音色表一致。安装不重复下载 espeak 语言包或训练资源。
`MNNKokoro.Load` 直接使用原始 `model.mnn.weight`；托管代码仅在内存图中设置公开
`Op.externalPath` 字段，保留磁盘原图，不创建副本或临时权重、不增加 C++。

Studio 提供全部 103 个音色，中英文词典、语速、播放与 WAV 保存已经接通。
示例文字根据音色语言选择。全部音色能生成有限非静音波形，但内容验证尚未全部通过：
同一句话的 SenseVoice 检查 76/103 通过；其余 27 个中，Qwen 独立转写有 20 个包含完整
内容断言，7 个仍不满足，不能宣布全部音色质量已通过。详见 Testing 中的证据与边界。

## MMS Tamil

模型管理器下载 `developerabu/mms-tts-tam-mnn` 的固定 INT8 profile 后，Studio 的
Speech synthesis 可选择 Tamil 单音色、插入 Tamil 示例、调节语速、播放并保存 16 kHz WAV。
目录改名仍通过准备文件识别，不会错误路由为通用 LLM 或 Supertonic。最多 300 字符，
输入需匹配 Tamil 词表；请用文字写出数字。真实问候语已经独立 ASR 检查，完整句子的
发音/转写仍有偏差，精度菜单提供 INT8、FP16/INT8 和 FP32，切换时释放旧推理实例；后两者的严格 ASR 内容检查尚未通过。原模型许可见安装目录中的
`MMS-SOURCE-NOTICE.txt`。

## Chinese Sherpa VITS

`KindBrave/sherpa-vits-zh-ll-MNN-Custom` 两站固定安装已接通 TTS 工作区。
可选择 `suyingxue`、`gunian`、`fushiyu`、`bingjiao`、`bazong`，调整语速，
插入中文示例、播放并保存 16 kHz WAV。五个说话人的参考句均经过真实合成与独立 ASR。
前端使用仓库注音词典，未接入 Jieba/FST；数字请写成中文，多音字读法受词典限制。
最多 300 字符，未知字词会明确拒绝。改名目录仍按配套文件识别为 TTS。

## MOSS Nano

模型管理器可安装 `kiven2008/moss-tts-nano-100m-mnn` 和
`ZhangZes/MOSS-TTS-Nano-MNN`。也可直接选择已下载的模型根目录，嵌套 `tts/` 布局会自动识别。
Speech synthesis 工作区显示全部 18 个参考音色和 seed；Use example 按音色填入参考语言文本。
输出 48 kHz，可试听、保存 WAV；本图没有语速控制。最多 250 个 token、30 秒语音，超限明确报错。
数字请写成口语文字。内置日语音色等仍有内容测试失败，详情见 Testing；示例按钮不代表质量已通过。

## MOSS Nano 与 Qwen3-TTS 实验（2026-10-19）

报告位于 `TestArtifacts~/ModelDiscovery/Tts/`。`moss-two-layout-regression.xml`
**72/72 passed**：两种布局的真实中文合成，缓存布局同实例英文合成，两个 Studio 生成/实际播放/WAV
保存，固定十/九文件的本地 HTTP 下载、SHA 校验和完整安装零 HTTP 续传，以及目录缓存、强制 Refresh、
并发、发现和共用生成算法回归。真实图与官方 C++ API 调用仅验证项目限定的 macOS arm64 Mono ABI。
下载服务测试使用本地真实文件响应，不声称重新验证实时 CDN 传输。

`moss-nano-all-voices.xml` 的严格内容审计失败：固定采样布局全部 18 音色均产生有效非静音波形，
10 个参考句通过，Adam 和七个日语音色（Soyo/Saki/Mortis/Umiri/Mei/Anon/Arisa）未通过。
独立 Whisper 复核仍存在错误：Adam 将 Paris 读成/识别成 Pyrus；日语样本的“音声合成”均未正确恢复。
第二识别器的执行成功不改变原质量测试失败。分词基准使用 SentencePiece 0.2.1 读取发布者的原始
`tokenizer.model`，保存在 `Sources/Moss/tokenizer-reference.json`；尚未以此证明全部字符规范化一致。

Qwen3-TTS 使用只读官方源中的 `Omni::generateTTS` 导出符号，未新增 C++ 或自定义导出。
ModelScope `huangzhengxiang/Qwen3-TTS-0.6B-Base-{INT8,FP16}-MNN` 资产均校验大小与 SHA。
`qwen3-tts-long-cap.xml`、`qwen3-tts-reference.xml`、`qwen3-tts-fp32-cache.xml` 均未通过。
FP16 有一次中文参考句正确，但英文及后续调用未稳定结束；不能据此开放支持。
`qwen3-tts-raw-diagnostic.xml` 仅为执行诊断：INT8 第一帧后 63 帧全零码，FP16 重复码和近静音。
带参考音频、提高精度及关闭 attention 缓存优化均未解决，内部实验保持未开放。

`moss-cached-voice-audit.xml`：两种词表的日语 token ID 对照 **2/2 passed**。
缓存图布局的 18 音色均产生有效波形，11 个中文/英文参考句通过，全部七个日语音色未通过严格内容断言；
该质量审计保留失败状态。固定图布局的 Adam 失败不能用另一个布局的通过结果覆盖。

## DhVaani v1.1（2026-10-19）

`dhvaani-studio-validation.xml`：**54 passed，0 failed，3 skipped**。
跳过项为未配置当前 runner 的其他媒体任务；DhVaani 三项真实集成测试、固定安装测试和 Studio 测试均执行通过。
参考资产固定 HF revision `9ecccbbba2d6269dc083ab1975800de8b0dbe1c1`，所有下载文件校验 SHA/大小，
非 LFS 文件另核对 Git blob ID。使用现有官方 Module API，无新增 C++、导出、JNI 或虚表访问。

`Sources/DhVaani/make_reference.py` 直接提取发布者 `dhvaani_torchfree.py` 的 NumPy DSP 函数，
结合原始 `mel_fb.bin` / `vocos_head.bin` 生成独立基准。25049 个输入样本包含尾帧舍入边界，
100 维 log-mel 和四帧投影/ISTFT 均要求最大绝对误差低于 2e-5，测试通过。
原始图的标量必须 rank 0；文本编码器特化参考条件，流模型通过公开 SliceTf/Reshape 将全部五项输入
合为单输入图，保留 Module 内部控制流。流模型与声码器跨合成复用，并覆盖不同长度文本。

英文参考与目标句验证 Paris/France，取消后恢复及同实例第二次不同 seed/词序均通过。
Hindi 目标“नमस्ते, यह आवाज़ का परीक्षण है।”由英语参考跨语言合成；Whisper-small
将末词拼为“परीक्षन”，首轮严格测试 `dhvaani-content-dsp.xml` 保留失败。
第二独立 Qwen2.5-Omni 转写完整恢复“नमस्ते, यह आवाज़ का परीक्षण है。”，最终测试要求
两个识别器至少之一包含每个原有内容锚点，没有删除该断言。Whisper Hindi prefix 仅用于测试，
没有宣称公开 ASR API 新增 Hindi 支持。

Studio 验证缺参考录音/原文时拒绝启动，真实生成后实际播放并保存 24 kHz WAV。
这一机器八步/CPU4 样本生成 5.496 秒、音频 2.464 秒，RTF 2.231；不宣称实时或已达最佳性能。
固定八文件的本地 HTTP 下载测试校验全部资产、重命名目录路由、完整安装零 HTTP 续传，以及拒绝未验证旧 revision。

`dhvaani-native-projection.xml` **55/55 passed**，`dhvaani-dynamic-cache.xml` **5/5 passed**。
输出投影改用内存图中的 MatMul/Add 后，与独立线性计算最大绝对误差为 1.7683e-5；
NumPy DSP、中文字符拒绝、英文重复合成、Hindi 内容及 Studio 测试仍通过。

`dhvaani-cpu-performance.xml` 的显式性能检查通过：每个候选先运行一次，再测量三次，
并检查其最终音频内容和线程间波形误差。M5 CPU 1/2/4/8 线程的重复调用中位数分别为
8.797 / 5.577 / 4.802 / 4.912 秒，对应同一 2.72 秒音频的 RTF 3.234 / 2.050 / 1.766 / 1.806。
本次 4 线程最快，默认仍为 4；这不代表其他硬件的最优配置，也没有达到实时合成。
冷调用分别为 9.087 / 6.210 / 5.060 / 5.153 秒（不含 Load 的文件校验/图准备）。
`dhvaani-cpu-performance.json` 保存各项数据；旧 Mono 格式化器导致的末字段 JSON 序列化问题已修复，
本次 RTF 按原报告的 medianSeconds/audioSeconds 计算，XML 中的原始测量输出保留。

## Qwen3-TTS 显式 KV 导出（2026-10-19）

支持 HF `yunfengwang/Qwen3-TTS-12Hz-0.6B-Base-MNN`，revision
`e17666b5a41fb6208378943869d3208754e0b2b9`，固定 47 文件 / 2,047,292,687 字节。
这是独立的显式 KV 图协议，不改变前述 ModelScope Omni 导出测试失败的结论。
使用现有的 Interpreter/Module 官方 API、托管 BPE/采样与磁盘 FP16 嵌入查表，
没有加载 PyPI 包的 native `.so`，没有新增 C++ 或自定义导出。仍仅验证本项目限定 macOS Mono ABI。

`qwen3-explicit-prefill.xml` 通过：中/英/日/韩混合文本的 token IDs 与发布者 Python 分词器一致；
同一参考声音、目标文本、语言构造的完整提示嵌入与独立 NumPy 基准最大误差 2.3842e-7。
`Sources/Qwen3Explicit/make_reference.py` 只使用解压的 Python 源码与原模型权重生成基准。
622 MB 文本嵌入表按行读取，不展开为常驻 FP32；矩阵投影使用公开 MatMul/Add 图在 MNN 执行。

`qwen3-explicit-content.xml` 首次中文、英文均通过正常 EOS、波形和独立内容识别。
`qwen3-explicit-integration.xml` **5/5 passed**：同一实例中英文（含异步）、取消拒绝、
达到帧数上限后报错及下次恢复、独立提示对照、固定下载/SHA/改名目录路由/零 HTTP 复用、
目录可用性，以及 Studio 实际生成、播放与 WAV 保存。
首轮图复用前中/英文耗时 14.603 / 16.344 秒；跨请求复用后该回归样本中英文分别 14.633 / 10.670 秒。
Studio 的 3.52 秒中文音频生成耗时 16.282 秒、RTF 4.626；不同测试设置不能据此宣称稳定加速比或实时。

此前验证范围为中文/英文及仓库内置 demo 参考音色；该导出不含原始 WAV 的声音编码器。
参考声音的来源说明保留在下载的 `voices/demo/meta.json`。不把原始 WAV 克隆或所有语言当作已支持。

`tts-moss-dhvaani-final-regression.xml` 的原始结果保留：95 passed / 1 failed；
MOSS Studio 有一次结果未送达窗口，未报告原生错误。`tts-studio-discovery-recheck.xml` 的
两种 MOSS Studio 和 TTS/聊天目录回归均通过（3/3），暂未复现；后续测试增加窗口持有 session
及 session 已产生结果的诊断断言，没有删除生成/播放或内容断言。

`qwen3-explicit-performance.xml` 显式性能检查通过：每个候选先合成一次，再测量三次同句同 seed，
最终波形与基准最大误差低于 .02，且各候选均通过独立中文内容识别。
关闭形状缓存/CPU4：冷调用 15.082 秒，重复中位 8.961 秒，RTF 2.546；
开启十五种 CP 形状会话缓存/CPU1、2、4、8：重复中位分别为
10.087 / 9.491 / 8.019 / 7.997 秒，RTF 2.866 / 2.696 / 2.278 / 2.272。
CPU4 开启缓存时冷调用 22.998 秒，进程驻留样本约 8.51 GB，关闭时约 3.90 GB。
这里的 residentBytes 是同一 Unity 测试进程的全进程样本，包含分配器保留，不能当作纯模型内存或跨线程独立内存比较。
默认 CPU4 且不开启形状缓存，保留跨请求的三阶段图复用与按行读取嵌入。
可选 `Load(directory, threads: 4, cacheCodePredictorShapes: true)` 换取约一成重复调用速度提升；
本机 CPU8 比 CPU4 的缓存结果只快约 .3%，不据此改变默认线程数或宣称全局最优。

### Piper Lessac high 完整安装

Hugging Face `ekremabiii/pipertts_mnn` 固定 revision
`56874de93c7d2908d7c384d656ddadea34ef2870` 使用标准 `input` / `input_lengths` /
`scales` 图，输出 22,050 Hz 单声道。安装计划同时固定下载声学图、配置、音素映射及
`csukuangfj/vits-piper-en_US-lessac-high` revision
`a3cffbe82b4df3b9edefa73189f4443ed638d9a1` 的七份英文 eSpeak-NG 数据和原始
`MODEL_CARD`。这 11 个文件逐项校验长度和 SHA-256；不需要手工拷贝其他模型的词典。
eSpeak-NG 可执行程序仍独立安装/自动发现，已用 1.52.0 实测补充数据兼容性。
标准导出仅接受已实现的单人英语及匹配音素表；不能据此推断任意 Piper 语言均受支持。

`TestArtifacts~/ModelDiscovery/Tts/piper-lessac-complete.xml` 为 10/10 通过：
Lessac 两次合成、取消、独立 ASR、旧三音色回归、Piper 路径发现、完整安装、
改名文件夹路由、重复安装零请求、固定 revision 拒绝以及 Studio 合成/播放/WAV 保存。
本轮实测限定为当前 macOS64 Mono / MNN 3.6.2 构建的 ABI；没有新增原生代码。

下载的模型实际文件统一存放于工程 `Assets/StreamingAssets/MNN/Models/`，Studio
刷新本地模型后即可发现；旧 `TestArtifacts~` 模型位置仅保留兼容测试脚本的目录链接。
迁移清单见 `TestArtifacts~/ModelDiscovery/model-migration.json`，不需要再次下载。
原始实验导出的质量限制仍然适用，放入 Models 并不表示未通过的导出已经可用。

迁移后的共享回归 `tts-piper-migration-regression.xml` 为 152/152 通过，包含缓存、
强制刷新、请求合并/并发/限流、TTS 前端/图执行和 Studio；这是 Kokoro 单音色修改之前的报告。
新增单音色报告 `kokoro-single-complete.xml` 为 5/5，通过中文、英文独立 ASR、原始音色
表逐字节比对、完整 8 文件安装/重复安装零请求、目录分类和 Studio 合成/播放/保存。
StreamingAssets 发现测试现检查 24 个可运行的 TTS 安装目录，并拒绝两个未验证 Qwen Omni 导出。

Kokoro v1.1 expanded report `kokoro-expanded-complete.xml` 为 5/5 通过，包含 ModelScope/Hugging Face 固定计划、103 个有名音色、中文/英文实际生成、Studio 播放保存，以及迁移目录的零请求复用。

Bert-VITS2 三处镜像的 23 个推理文件均与原验证模型逐项哈希相同，复用中文/英文/混读
原生及 Studio 检查。安装复查 `bert-vits2-aliases-install-recheck.xml` 为 2/2；此前
`bert-vits2-aliases-complete.xml` 为 4/5，通过推理测试但 HTTP fixture 的精确请求次数断言
未通过。fixture 不实现 Range，却使用四连接分段，触发了预期的 200 响应回退；复查改为
单连接验证逐文件请求和零请求复用，未为此修改生产下载器。
安装索引回归 `tts-install-index-regression.xml` 为 47/47；迁移模型固定文件零请求检查
该阶段覆盖 14 个安装，注册身份清单见 `TestArtifacts~/ModelDiscovery/registered-models.json`。

Kokoro expanded 全音色审计 `kokoro-expanded-all-voices.xml` **未通过**：103/103 份波形
满足有限值、采样率、长度和能量检查。内容检查只抽取 3 个英文音色与旧版 7 个问题音色，
10 项中 4 项通过、6 项未通过；不是对全部 103 个音色的内容验证。
旧失败音色 `zm_096` 本轮通过；`zf_046`、`zf_079`、`zm_030`、`zm_035`、`zm_080`、
`zm_100` 仍未通过 Qwen/SenseVoice 的严格内容锚点。逐项结果在
`kokoro-expanded-all-voices.tsv`，原始失败报告保留。这些质量限制也展示在目录能力说明中。

进一步使用 Whisper small 中文对上述 6 份原始 WAV 独立诊断，均仍存在“合长/合舱/银河”等
转写偏差。`kokoro-expanded-quality-diagnostics.xml` 仅验证诊断执行、图哈希拒绝策略与目录缓存，
不将诊断程序成功视为这 6 个音色的内容测试通过；原始严格审计继续保持失败。

### Pocket-TTS MNN 全链路

`developerabu/pocket-tts-mnn` revision `4e75a38d20a9c532ed55591186584a244c381095`
的五阶段 MNN 英语管线已完成参考 WAV 编码、文本条件、主模型、flow 与解码器检查。
固定安装计划含 11 个文件、123,879,145 字节；不下载重复量化图或 ONNX 依赖。
发布者预设音色仓库需要授权，未将无法取得的音色列为可用；实际使用原始参考 WAV。
词表为 SentencePiece UNIGRAM 4000，161 个独立分词样本逐项一致。
修正编码器公开序列化图中丢失的两个 Shape 切片语义后，与独立 ONNX 声音特征相比，
最大误差 .00621636、RMSE .0007517、相关性 .9999064。
空序列 priming 通过公开图依赖特化完成；原模型文件保持不变，不新增原生代码。

`pocket-complete.xml` **7/7**：包含实际 MNN 语音、两个 seed 的独立英语 ASR、
安装/零请求复用、目录分类和 Studio 生成、播放、保存。
`pocket-regression.xml` 原始共享结果为 **99/100**，唯一失败是已过期的导入数量断言；
审计确认 64 项官方 MNN/STL 或系统 libc++ 导入，修正后 `pocket-abi-final.xml` **3/3**，
同时检查编码器数值、取消、无效参数和帧数上限拒绝。未将原报告改写为全通过。
Pocket 完成时 Studio 发现检查覆盖 23 个 TTS 目录，固定文件零 HTTP 复用覆盖 15 个安装。

`pocket-performance.xml` **1/1**；CPU 1/2/4/8 线程使用同句、同 seed，
各先冷合成一次再重复三次，且各候选均通过独立 ASR。1.76 秒音频的冷调用分别为
5.9102 / 5.7292 / 5.5333 / 5.3061 秒，重复中位为
3.98245 / 3.94534 / 3.93093 / 4.03075 秒。保留默认 4 线程，RTF 2.2335，尚非实时。
原 JSON 因 Mono 格式化行为出现字面量 R，保存在 `pocket-performance-original.json`；
修复的 `pocket-performance.json` 只从实测耗时和采样数重算 RTF，并记录修复范围。

原始图 priming 的 reshape 失败、Interpreter 拒绝及 Module 原生崩溃报告均保留在
`TestArtifacts~/ModelDiscovery/Tts/pocket-prime-*`，危险原始 Module 探针已从包测试移除，
源码保存在该目录的 `Sources/PocketTtsProbeTests.cs`。成功报告使用修正后的公开序列化图。
原生验证仅覆盖 MNN 3.6.2 / Apple clang / libc++ ABI v1 / macOS64 Mono 约定；
本轮未重跑依赖新版 Unity Roslyn 路径的跨平台编译矩阵，也不推断其他平台可调用此 ABI。

### IndicF5 MNN

`developerabu/IndicF5-MNN` 的 INT8 DiT、动态 Vocos FP32 和匹配字符词表已接入，
七个安装文件固定 revision、长度和 SHA-256。原始模型位于
`Assets/StreamingAssets/MNN/Models/IndicF5-MNN`；位置常量按公开序列化图在内存适配，
不改写源文件，不新增 C++ 或桥接导出。

`indicf5-final-regression.xml` **60/60 passed**，包含独立 NumPy mel/分词基准、
发布者 Vocos 样本对照、DiT 线程比较、导出哈希拒绝、Hindi 实际合成与独立内容检查，
以及 Studio 生成、播放、保存 WAV。共享检查覆盖目录缓存、强制刷新、并发与限流；
Studio 发现覆盖 24 个 TTS 目录，固定文件零 HTTP 复用覆盖 16 个安装。
mel 前端与独立基准最大误差为零；Vocos 对照允许发布者样本的幅度差异，并检查拟合后的残差。

Hindi 内容锚点通过两个识别器复核。Tamil 能生成有效 24 kHz 波形，但严格内容检查
`indicf5-tamil-content-recheck.xml` 仍失败：两个识别器对“சோதனை”存在近音偏差，
Studio 标记为 experimental。其他语言和任意参考录音的质量未验证。

`indicf5-cpu-performance.csv` 比较 256 帧单次 DiT 执行的 CPU 1/2/4/8 线程，
重复中位约 319/344/168/165 ms；这不是整句合成耗时。保留默认四线程，
32 步短句实测约 35 秒，未达到实时。原生验证范围仍为上述项目限定 macOS Mono ABI。

## Qwen3-TTS Studio 接入复核（2026-10-23）

本轮证据在仓库根目录 `TestArtifacts~/QwenTts/verification-summary.json`。
39 项离线回归通过（`unit-results.xml`）：预编码音色数据与模板校验、损坏音色排除、
截断权重拒绝、自定义音色取代 demo、模型任务识别、推荐目录与下载分类。
测试使用的稀疏占位文件只验证准入规则，不是有效推理权重。

另有 5 项真实资产检查通过（`results.xml` 中对应的 5 个 Qwen 测试）：
独立 tokenizer/prefill 基准、同一实例中英文合成与取消/截断拒绝、中文 Studio、
英文 Studio，以及 47 文件固定计划的 SHA-256 校验和零额外请求重复安装。
原始综合报告中的推荐列表旧断言已在后续离线回归修正并通过；
`verification-summary.json` 按相关用例的最新结果汇总为 **44 passed / 0 failed**。
额外执行的全目录实时联网审计曾因非 Qwen 的 KWS 元数据源不可用而失败，
原始证据保留在 `unit-with-live-audit.xml`，不纳入这 44 项 Qwen 相关验证。

本轮真实音频包括 `qwen3-explicit-{zh,en}.wav` 与
`studio-Qwen3-TTS-Explicit-{zh,en}.wav`，均为 24 kHz 单声道。
独立转写分别为“你好，这是语音合成测试。”和“Paris is the capital of France.”。
Studio 用例验证实际自动播放、停止、WAV 编码保存及语言请求路由。
额外预编码音色通过协议校验后可选择；本轮内容质量验证使用 demo，不代表所有自备音色质量。

运行环境仍限定为 MNN 3.6.2 / Apple clang / libc++ ABI v1 / macOS64 Mono。
使用现有官方 C++ API 声明与托管前后处理，没有新增 C++、自定义导出或官方源码修改。
ModelScope Talker INT8/FP16 实验导出的质量失败结论保持不变，不能套用本轮成功结果。

## PP-DocLayoutV3 文档版面验证

`DocumentLayoutTests` 校验模型协议拒绝、阈值、坐标拉伸还原、裁剪及阅读顺序。
`DocumentLayoutIntegrationTests` 显式使用已有固定权重与 Paddle 官方 `layout.jpg` 页面，
核对两张表格的人工标注范围、13 个区域的类别与阅读顺序、重复执行、取消、释放和 Studio backend。
记录加载/首轮/复用毫秒数和进程 RSS；RSS 包含 Unity 与所有已加载资源，不等于模型独占内存。
图的 `scale_factor` 输出坐标约定另以两组实际输入验证，不复制庞大的 mask 输出。
Metal Normal 得到无效几何，High 精度仅保留一个区域，均未通过；该导出限定 CPU。
桌面报告位于 `TestArtifacts~/TaskIntegration/DocumentLayoutFinalABI/`。

`DocumentLayoutPlayerTests` 用相同真实页面核对 Android/iOS Player 结果，并记录进程常驻/峰值内存、
iOS physical footprint 和加载/推理耗时。将两个固定资产与 `layout.jpg` 放到设备
`persistentDataPath/document-layout/` 后，使用 `run_platform_tests.py --document-layout-root <existing-assets>`
要求两项用例都通过；缺少资产或跳过不能算作验证通过。桌面 Player 可直接指定目录。
`--test-filter` 可选择专项测试，报告仅覆盖筛选后的用例；该 runner 同时要求平台契约与基础推理检查。

## Candy / DeepLab 图像任务独立参考与 Player 检查

共用转换工具 `Tools~/Models/prepare_image_profile.py --profile candy|deeplab` 使用已有
未修改的官方3.6.2转换器，严格检查源模型及图SHA；只规范化公开Net.mnn_uuid。
共用参考工具 `Tools~/Testing/generate_image_reference.py --profile candy|deeplab` 从
已有固定源文件生成ONNX Runtime/LiteRT参考，不下载。Runtime不依赖这些Python运行时。

最新 ARM64 Editor 合并回归85项通过，Android ARM64 IL2CPP117项通过；
x86_64 Mono Player（Apple Silicon / Rosetta）117项通过。均无失败/跳过，包含两类
CPU/Auto实际权重；Android实际CPU，macOS Auto实际Metal。每类只有一张独立参考真实图片，
不代表数据集精度、其他架构或所有模型。iPhone 17 ARM64 IL2CPP 同专项117项通过、无失败/跳过，
包含四项CPU/Auto→Metal实际权重对照；记录见本地合并报告的iOS-results.xml。

具体门槛、限制和命令见 [Candy](StyleTransfer.md) 和 [DeepLab](Segmentation.md)。
Player runner要求 `--style-root` / `--segmentation-root` 对应的两项测试全部实际通过，
不会把缺少权重的跳过计为模型验证。测试结果保留在
`TestArtifacts~/MNNValidation/PlatformValidation/Android-IL2CPP-inference-image-tasks/` 和
`StandaloneOSX-Mono2x-x64-inference-image-tasks-final/`；Editor见
`TestArtifacts~/TaskIntegration/SegmentationVerified/`。

## MoveNet 单人姿态估计

`PoseTests` 覆盖 RGB/int32/NHWC、尺寸取整／黑边、COCO 排列、坐标逆映射、
低分／图外点保留、只读结果、形状／类型／范围拒绝、取消、骨架阈值及 Studio
独立页面的实际请求／绘制。`PoseIntegrationTests` 显式使用固定 ONNX 转换图，
与 ONNX Runtime 独立结果比较，并执行共享 Runtime 与 Studio native backend。

`PosePlayerTests` 使用同张真实照片中的两个人物裁剪，各跑 CPU/Auto，要求
输入逐元素一致、原始 51 个数的严格误差门槛、源坐标与分数、重复执行、取消、
释放及模型推理速度／进程内存检查。参考 SHA 固定；缺少资产不能通过
`run_platform_tests.py --pose-root` 的四项用例门槛。

2026-10-25 桌面 Editor 专项 50 项全部通过，包含以上真实权重、页面及既有任务路由。
Android OPPO PELM00 ARM64 IL2CPP 的 117 项全部通过，无失败／跳过；其中
MoveNet 两例各 CPU/Auto 四项真实推理通过。该专项不证明其他模型的新设备验证。
官方 float16 TFLite 仅补充几何对照，分数与本 ONNX profile 存在差异，不能称为等价导出。
细节和平台边界见 [姿态估计](Pose.md)，本地证据见
`TestArtifacts~/TaskIntegration/Pose/validation-report.md`。

## English Laya structured text decisions

`MNNTextDecision` uses the pinned English `yunfengwang/laya-MNN-fp16` graph with a shared
ModernBERT NFC/ByteLevel BPE engine. It exposes immutable choice, ordinal score and
Boolean results, checkpoint probabilities, entropy confidence, action probability,
input token count and truncation flags. The Studio NLP page uses the same Runtime
API and asks for ordered options/criteria. Auto selects CPU/High; explicit Metal is
rejected because this publisher profile is CPU-only.

The final desktop suite has 72 targeted tests. It includes eight independent Rust
`tokenizers` cases, strict original PyTorch six-case references, native MNN logits and
probability comparisons, typed Studio backend routing and page parsing. iPhone 17
ARM64 IL2CPP ran 115 cases (CPU and Auto→CPU), six cases per backend, with maximum
probability error 0.000430. The 845.9 MB graph took 7.64–8.55 s to load and about
2.36–4.12 s per inference; process peak RSS reached 2.76 GB and iOS emitted memory
warnings. This is a full-process observation and is not a model-only memory budget.
The upstream action logits are saturated, so only their exposed softmax probability is
compared for these examples; this does not establish action calibration or threshold
safety. English INT8 and multilingual Laya variants remain unverified.

## Depth Anything V2 Small 与 Android English Laya 补测

`DepthTests` 覆盖 DPT 几何、attention 内存上限、相对逆深度解码、
源图采样、PFM 行序和独立页面请求／绘制。`DepthIntegrationTests` 使用原始 ONNX
独立预处理／输出参考对照共享 Runtime 与 Studio native backend；相关 Editor 回归 74 项通过。
`DepthPlayerTests` 两张真实图片各 CPU/Auto 四项，校验 fixture SHA、预处理、浮点网格、
重复执行、取消／释放并记录内存与速度。此图 Auto 固定 CPU/High，Metal 数值验证失败。
iPhone 17、macOS ARM64 和 x64 Rosetta 专项各 117 项通过；Android 合并 English Laya
专项 119 项通过，均无失败／跳过，Android 还核对 APK 内嵌 build id 与 XML 一致。
runner 的 `--depth-root` 要求四项均通过才报告 depth_anything_v2_verified。
模型准备、工具命令与验证边界见 [深度估计](Depth.md)。

Android English Laya CPU/Auto 各六个原始 PyTorch 参考用例通过，最大概率误差
0.000428。首次／复用约 13–16 秒，加载约 36–44 秒，进程峰值 RSS 约 3.07 GB。
INT8 与多语言仍未验证，饱和的 act 概率不能证明原始 act logits 等价或阈值校准。
长 native 测试可能使 Unity Editor 心跳先超时；runner 在设备进程仍存活时继续使用原始
30 分钟 deadline 恢复带相同 build id 的设备结果。本轮在修复前手动恢复 XML，
原始超时／缺权重跳过记录保留，不能把这些过程的 runner 返回值当作成功。
本地产物位于 TestArtifacts~/TaskIntegration/Depth/ 和 NLP/，后者含 Android-results.xml。

本轮还运行了 macOS x64 / Rosetta Mono Player：118/118 通过，0 失败/跳过，11 项原生测试，CPU 与 Auto→Metal 仿射图通过；这不是实体 Intel 硬件结果。
