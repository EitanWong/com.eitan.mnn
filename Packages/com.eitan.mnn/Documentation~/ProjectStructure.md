# 项目结构与维护规范

`Packages/com.eitan.mnn` 是可分发的 UPM package。仓库根目录的 `Assets`、
`ProjectSettings` 和 `Packages/manifest.json` 属于开发用 Unity 工程，不能复制到包中。
生成的工程文件、构建、日志、模型与临时诊断不能进入 package 源码目录。
测试产物统一放在仓库根目录的 `TestArtifacts~/`。

## 目录职责

```text
Packages/com.eitan.mnn/
├── Runtime/
│   ├── Core/
│   │   ├── Common/                Types、Platform、Security、Settings、Audio、Numerics、Layout、Diagnostics
│   │   ├── Inference/             Backend、Loading、Interpreter、Session、Tensor
│   │   ├── Models/                Configuration、Catalog、Downloads、Delivery、Transfer、Serialization、Profiles
│   │   ├── Language/              Llm、Decisions、Tokenization、Realtime、Remote、Providers、Agent、Markdown
│   │   ├── Retrieval/             Bert、Embedding、ImageEmbedding、Reranking、Tokenization
│   │   ├── Vision/                Common、YoloV8、PaddleOcr、DocumentLayout、ImageTasks
│   │   ├── AudioProcessing/       按音频任务及模型细分（见下文）
│   │   ├── SpeechRecognition/     Common、Paraformer、Qwen3Asr、SenseVoice、Whisper、Zipformer
│   │   ├── Generation/
│   │   │   ├── Common/            图执行、常量特化与共用数值算法
│   │   │   ├── Audio/             Common、Realtime 与各 TTS 模型目录
│   │   │   └── Image/             Common、Sana、StableDiffusion
│   │   └── Performance/           可选 Burst FFT 适配（独立程序集）
│   ├── Components/                MonoBehaviour 及 Unity 生命周期适配
│   │   ├── Models/                单模型加载、集中模型管理
│   │   ├── Inference/             推理执行
│   │   └── Realtime/              PCM 设备端口、会话生命周期、麦克风采集
│   ├── Interop/                   按职责拆分的 MNNInterop.*.cs
│   │   └── Handles/               SafeHandle 与原生资源所有权
│   └── Plugins/                   按平台/架构存放的原生二进制与导入设置
├── Editor/
│   ├── Settings/                  项目级设置与 Project Settings 页面
│   ├── ModelPackaging/            模型打包计划、构建清单与可恢复暂存
│   ├── Components/               GameObject/MNN 组件菜单与共用 Undo 操作
│   ├── Localization/             编辑器本地化
│   ├── ModelManagement/          Catalog、Downloads、Installation、Initialization、Window
│   ├── Studio/
│   │   ├── Core/                 Conversation、History、Media、Models、Search
│   │   ├── UI/                   Studio 控件与展示
│   │   └── Window/               窗口与按交互职责拆分的 partial 文件
│   └── UI/Common/                跨编辑器功能复用的控件
├── Tests/
│   ├── Editor/                   Editor 测试程序集
│   │   ├── Core/                 基础类型、异常与配置
│   │   ├── Generation/           Common、Audio/模型或 Realtime 算法/数据处理检查
│   │   ├── Language/             LLM 参数校验
│   │   ├── Models/               Catalog、Downloads、Delivery
│   │   ├── SpeechRecognition/    按模型归类的识别前后处理检查
│   │   ├── AudioProcessing/      VAD、轮次、关键词等模型检查
│   │   ├── Vision/               按视觉管线归类的配置/结果检查
│   │   ├── Retrieval/            检索模型与分词检查
│   │   ├── Platform/             ABI、原生库打包与版本检查
│   │   ├── Studio/               编辑器状态、路由、窗口与替身 backend 测试
│   │   ├── Integration/          独立程序集；按领域/模型细分，专用 helper 放 Fixtures
│   │   └── Fixtures/             独立共享程序集；供 Editor 两类测试引用
│   ├── Runtime/                  PlayMode/Player 测试程序集
│   │   ├── Inference/            实际推理测试
│   │   ├── Platform/             平台/后端支持与拒绝策略
│   │   ├── Models/               Runtime 模型目录与下载 API 的确定性 HTTP 替身测试
│   │   ├── Language/Markdown/    图片lease与Player延迟销毁生命周期
│   │   └── Fixtures/             可在 Player 使用的内嵌测试数据
│   └── Fixtures/                 提交到版本库的小型确定性测试资源
├── Tools~/
│   ├── Testing/                  本地 fixture、模型与平台测试 runner
│   └── Validation/               包结构检查与使用本地 Unity 引用的逐程序集编译
├── Native~/                      官方库构建工具与本项目限定构建的 ABI 约定
├── Samples~/                     可导入的示例，各自具备说明和程序集边界
└── Documentation~/               面向使用者与维护者的文档
```

`~` 后缀用于 Unity 忽略的工具、文档和未导入示例。`Tools~/Testing` 中的
`PlatformValidationSetup.cs` 仅由 runner 复制到隔离工程的 Editor 目录，不能放进 Runtime。
原生 `.framework`、`.xcframework` 与 `.bundle` 按完整插件资产处理，不能为内部文件补 Unity meta。

## 模型目录与共享实现

`AudioProcessing` 先按任务、再按模型组织：`Enhancement` 为语音增强和降噪，
`EchoCancellation` 为回声消除，`Separation` 为多声源分离，
`TargetSpeakerExtraction` 为参考音频或视觉条件下的目标提取，
`SpeakerDiarization` 为说话人分离及聚类，`SpeakerEmbedding` 为声纹表征，
`VoiceActivityDetection` 为语音活动检测，`TurnDetection` 为轮次和端点检测。
`KeywordSpotting`、`Analysis`、`Realtime` 和跨任务 `Common` 保持独立。
模型目录保留各自原有 Unity GUID，跨任务共用代码不复制到模型目录。
新增模型时将它放入对应任务目录，并更新包结构校验规则。

`EchoCancellation/Common` 保存与模型无关的延迟估计、参考PCM时间线和自适应FIR；
`AudioProcessing/Realtime` 保存增强/VAD/ASR输入路由、设备延迟校准及播报fade；
`AudioProcessing/Common` 保存源贡献和说话人选择，`Analysis` 保存渲染波形/特征快照。
这些是可独立使用的托管API，不依赖SiriAI、EasyMic或具体场景。
会话目录、草稿、选择、日志恢复与外部执行事件去重位于 `Language/Realtime` 的
`MNNConversationCatalog` / `MNNConversationTranscriptStore`；模型上下文使用
`MNNConversationMemory.Restore` 从文字消息恢复，不注入显示用工具数据。Agent 的公开执行
记录由 `Language/Agent/MNNAgentTrace` 提供。可展开的执行时间线、行布局与 Markdown
虚拟化归 `Language/Markdown`；上层只绑定 UI toolkit、字体/颜色与调用展开接口。
详见 [会话与执行时间线](ConversationTimeline.md)。

共享图片资源缓存位于 `Language/Markdown`，UI组件只持有显示lease，Editor和Player不复制LRU。
行为测试随实现放在 `Tests/Editor/AudioProcessing`、`Tests/Editor/Language/Markdown`；
纯托管算法测试与实际权重/设备测试保持明确的验证范围；图片Player生命周期在
`Tests/Runtime/Language/Markdown` 验证帧末销毁，不以Editor的立即销毁测试代替。

模型的运行器、配置、模型资产描述、分词/特征提取和图适配代码放在同一个模型目录中。
例如 `SpeechRecognition/Whisper` 包含 `MNNWhisperRecognizer`、`MNNWhisperConfig`、
`MNNWhisperModel` 和 `MNNWhisperFeatures`；`Generation/Audio/CosyVoice` 包含模型描述、
资源、采样器、声码器和运行器 partial。目录划分不改变 `MNN.Unity` 命名空间及类型所属程序集。

音频生成模型目录为 `BertVits2`、`CosyVoice`、`CosyVoice300M`、`DhVaani`、`FireRedTts3`、`IndexTts25`、
`IndicF5`、`IndicVits`、`Kokoro`、`MmsTamil`、`MossNano`、`Piper`、`PocketTts`、
`Qwen3Tts`、`SherpaVitsChinese` 和 `Supertonic`。跨模型语言路由、参考音频、文本清理、
图生命周期和 DSP 位于 `Audio/Common`；流式输出与文本分段位于 `Audio/Realtime`。
多种模型共用的固定安装协议注册表保留在 `Models/Profiles/Speech`，同一注册表的 partial
保持同目录，模型自己的执行逻辑不进入注册表。

共享操作按职责归属：RIFF 解码在 `Common/Audio/MNNWaveAudio`，语音重采样、特征、
声码器数学和波形拼接在 `Generation/Audio/Common/MNNSpeechDsp.*`，
图执行与特化在 `Generation/Common`。模型之间通过这些共享实现复用，
不要为了调用一个通用算法而依赖另一个具体模型的内部方法。

大类型按可独立理解的职责拆分：`Loading`、`Text`、`Synthesis`、`Tokens`、`State`、
`Graphs`；主文件保留所有权状态和生命周期。小类型保持单文件，避免无实际职责的碎片。
Model Manager 按 Catalog、Filters、List、Details、Actions 分为 partial；Studio 窗口
继续按交互职责拆分，backend 的 Loading、Execution 与释放独立。

## 程序集与依赖方向

所有可挂载的 MNN 运行时包组件必须使用 `AddComponentMenu`，统一放在 `MNN/...` 根分类下，
按模型、推理、实时音频等职责分组。Editor 程序集在 `GameObject/MNN/...` 提供对应
菜单命令，作用于 Hierarchy 当前选择；没有场景对象选择时在当前 Stage 新建对象。
支持多选、右键上下文和整步 Undo，跳过已有同类型组件与不可编辑对象，不直接修改 Project 中的
prefab 资产。导入示例后，示例组件的菜单由 sample 内独立 Editor 程序集提供。不要为同一
职责另建重复组件；模型加载、模型注册、推理执行和音频设备/会话职责分别保持独立。

| 程序集 | 定义位置 | 可依赖的包程序集 | 编译范围 |
| --- | --- | --- | --- |
| `MNN.Unity` | `Runtime/MNN.Unity.asmdef` | 无 | Editor 与 Player |
| `MNN.Unity.Burst` | `Runtime/Core/Performance/Burst/MNN.Unity.Burst.asmdef` | Runtime、Unity.Burst | 仅已安装 Burst 1.8+ |
| `MNN.Unity.Editor` | `Editor/MNN.Unity.Editor.asmdef` | `MNN.Unity` | Editor |
| `MNN.Unity.Tests` | `Tests/Editor/MNN.Unity.Tests.asmdef` | Runtime、Editor、Tests.Fixtures | Editor、`UNITY_INCLUDE_TESTS` |
| `MNN.Unity.Integration.Tests` | `Tests/Editor/Integration/MNN.Unity.Integration.Tests.asmdef` | Runtime、Editor、Tests.Fixtures | Editor、`UNITY_INCLUDE_TESTS` |
| `MNN.Unity.Tests.Fixtures` | `Tests/Editor/Fixtures/MNN.Unity.Tests.Fixtures.asmdef` | Runtime | Editor、`UNITY_INCLUDE_TESTS` |
| `MNN.Unity.Runtime.Tests` | `Tests/Runtime/MNN.Unity.Runtime.Tests.asmdef` | Runtime | PlayMode/Player、`UNITY_INCLUDE_TESTS` |
| `MNN.Unity.Samples.BasicExample` | `Samples~/BasicExample/MNN.Unity.Samples.BasicExample.asmdef` | Runtime | 导入 sample 后的 Editor 与 Player |
| `MNN.Unity.Samples.BasicExample.Editor` | `Samples~/BasicExample/Editor/MNN.Unity.Samples.BasicExample.Editor.asmdef` | Runtime、Editor、BasicExample | 导入 sample 后的 Editor |
| `MNN.Unity.Samples.BasicExample.Tests` | `Samples~/BasicExample/Tests/Runtime/MNN.Unity.Samples.BasicExample.Tests.asmdef` | Runtime、BasicExample | 导入 sample 后的 PlayMode/Player、`UNITY_INCLUDE_TESTS` |

Runtime 不依赖 Editor 或测试程序集。测试辅助代码分别归属对应程序集；
跨 Editor/Player 的静态资源放 `Tests/Fixtures`，不使用源码目录隐式共享测试代码。

模型目录发现、来源镜像与仓库下载实现属于 `Runtime/Core/Models/Catalog` 和
`Downloads`，上层通过 `MNNModelCatalog` / `MNNModelDownloader` 调用。Editor 的
同类旧入口作为兼容适配保留，缓存、安装索引、持久化任务和窗口生命周期仍归 Editor。
具体联网 API 与进度、取消、续传约定见 [模型获取 API](ModelAcquisition.md)。

`MNN.Unity.Burst` 使用 version define 和 define constraint 自动选择，不加入 package
dependencies；基础 Runtime 不反向引用它。通过友元权限注册同一 FFT 内核的函数指针，
不引用 Collections、Mathematics 或 Jobs；未安装 Burst 时这一个可选程序集不参与编译。
移动程序集定义时保留名称及 GUID，避免破坏 `InternalsVisibleTo`、测试 runner 与下游引用。

`MNN.Unity` 保留现有公共 API 和可序列化组件的程序集身份。Interop、Core、Components
目前共用内部配置、句柄和模型生命周期，不逐模型新增互相依赖的 asmdef。
Runtime 与 Editor 仅向实际使用内部 API 的测试程序集开放友元权限；共享 Fixtures
仅向两类 Editor 测试开放内部 helper。两类测试互不引用，所有测试程序集均
`autoReferenced: false` 且受 `UNITY_INCLUDE_TESTS` 约束。

`MNN.Unity.Tests` 仍含少量既有本地资产/原生检查，程序集名称不代表纯托管验证范围。
`Integration` 的真实权重、原生调用和联网检查独立归档；联网测试继续显式执行。
按类型或 category 筛选仍使用原名称；按程序集筛选原生/模型测试时改用
`MNN.Unity.Integration.Tests`，完整 Editor 回归选择两个测试程序集。

## 文件与代码规则

- 先按层级划分，再按职责归类；不要重新建立无明确职责的 `Utils`、`Misc` 或 `Temp` 目录。
- 托管 API 放 `Runtime/Core` 的对应领域/模型目录，共享模型交付与传输放 `Core/Models`，Unity 组件放 `Runtime/Components`。
  目录细分不改变现有公开 `MNN.Unity` 命名空间。性能任务保留 `MNN.Unity.Performance`。
- 一个主要类型一个同名文件。仅紧密关联的小枚举/配置数据可随所属类型放在同一文件，
  例如 `MNNChatRole` 与 `MNNChatMessage`。实现辅助类型与结果类型独立存放，不能隐藏在无关模型文件中。
- 同一功能跨平台共用类型和实现，不建立 `MobileXXX` 平行 API；平台 ABI 差异放在现有 partial 内。
  Player 权重测量使用 `PlayerRepresentativeTests.Measurements.cs`，共享 helper 为
  `PlayerMeasurements` / `PlayerResults`；保留历史测试 category 与设备计划路径兼容。
- 同一类型的职责拆分使用 `类型名.职责.cs`，例如 `MNNLlm.Streaming.cs`；partial 文件与主文件同目录。
- 原生声明统一为 `MNNInterop.*.cs` partial 文件，库名/调用约定共用；
  所有句柄类型使用 `MNN.Unity.Interop.Handles` 命名空间并放在 `Interop/Handles`。
  不新增 C++、桥接库、自定义 C 导出或官方源码修改。
- 测试文件与主要 fixture 同名，共享 helper 独立放 `Fixtures`。测试类和 category 名称用于 runner 筛选，
  结构整理时优先保留。`AdditionalTtsUnitTests` 为历史 fixture 名称，含真实 Express 调用和本地词典检查，
  因而归入 `Integration/Generation/Audio/Common`；不能把它当作纯离线单元测试证据。
- 联网测试保持显式执行；真实权重检查、原生调用和替身测试分别说明验证范围。
  测试不得自动下载大型模型。测试产物与缓存放 `TestArtifacts~/`。
- 移动 Unity 资产时同时移动 `.meta`；拆分出的新类型分配新 GUID，原文件的主要类型保留旧 GUID。
  不手工重写插件导入设置，不提交孤立 meta 或重复 GUID。
- 使用仓库 `.editorconfig`：UTF-8、LF、C# 四空格缩进与独立大括号；JSON/asmdef 两空格。
  Unity 生成的插件 meta 保持 Unity 的序列化格式。

## 维护检查

从仓库根目录运行，无需安装依赖或下载模型：

```bash
python3 Packages/com.eitan.mnn/Tools~/Validation/validate_package.py
```

检查目录边界、程序集引用与循环、测试位置、partial 同目录、句柄归属、资源 meta/GUID、文档本地链接、
模板残留与生成产物。它只验证结构，不能代替 C# 编译、Unity 测试、原生 ABI 或真实推理验证。
编译和实际推理的入口见 [测试说明](Testing.md)。

使用已安装的 Unity 逐个编译基础八个程序集，包括未导入的 BasicExample；仅传递
asmdef 声明的包依赖，以发现内部可见性和依赖缺失：

```bash
python3 Packages/com.eitan.mnn/Tools~/Validation/check_assembly_compilation.py \
  --unity /Applications/Unity/Hub/Editor/2021.3.45f2/Unity.app/Contents/MacOS/Unity
```

输出保存在 `TestArtifacts~/PackageEngineering/Compilation`。此检查不运行 Unity，
也不验证原生链接、设备运行或模型精度。
提供 `--burst-assembly /path/to/Unity.Burst.dll` 时也编译第九个可选性能程序集。
提交范围、忽略规则、本地保存与分支流程见 [仓库维护](RepositoryManagement.md)。

## 实时对话核心与应用装配

`Runtime/Core/Language/Realtime/MNNConversationRuntime.*` 拥有共享对话策略：
采集消费、VAD 轮次、ASR 预览、语音校验、退出意图、文本/TTS 并行、插话恢复、
呈现历史和耗时诊断。`IMNNConversation*` 定义应用与模型边界，
`MNNConversationPorts` 提供有界默认通知端口，`MNNConversationMnnProviders` 适配已有托管任务 API。
SiriAI 的同名兼容入口只做装配/转发；不要在示例或 Studio 再复制这套策略。
Studio 的 `MNNStudioRealtimeConversation` 只管理 Editor 设备与异步生命周期，
Unity PCM 设备继续归 `Runtime/Components/Realtime`。组合示例及所有权见
[共享实时对话](RealtimeConversations.md)。

完整应用能力组合见[AI应用架构](ConversationApplicationArchitecture.md)。`AudioProcessing/Acoustics`
拥有跨声学任务的模型角色、前端和唤醒；`Realtime`拥有可注入设备会话控制；
`Language/Providers`拥有惰性阶段工厂，`Agent`拥有计划、工具和权限注入。
`Common/Layout`仅放通用几何/运动，`Common/Diagnostics`放可复用系统采样，
平台公开导出统一声明在对应`MNNInterop.*`文件。不得把Siri的产品视觉或业务默认值放进这些目录。


## Harness 核心所有权

`Runtime/Core/Language/Agent/Harnesses` 拥有纯托管任务领域、持久任务运行时、文件存储、
结果租约/回执、远端客户端、提交日志、Host配置与任务呈现数据。它不引用Unity或SiriAI应用；
`Tools~/HarnessCore/MNN.HarnessCore.csproj` 提供独立 .NET Standard 2.1 构建。
对应行为测试与共享夹具位于 `Tests/Editor/Language/Agent/Harnesses` 及其 `Fixtures`。

迁移保留 `MNN.Samples.SiriAI.Harnesses` 公开命名空间、同名程序集及现有Unity GUID，
这些名称用于既有消费者兼容，源码所有权已经归包。不得在SiriAI再保留或编译一份相同核心。
应用及独立Host引用此程序集；产品UI、视觉、设备装配和SiriAI默认值继续由应用层拥有。
`HarnessTaskTool` 拥有 Submit/Status/Cancel/Update 工具契约、输入解析与受理语义；
`HarnessTaskObserver` 拥有提交日志重放、单写入者轮询和 revision 去重；
`HarnessResultFeedback` 拥有结果领取、续租和展示回执，使用 `IHarnessResultPresenter` /
`IHarnessResultReply` 注入应用会话与真实展示证据，`HarnessResultConnection` 捕获客户端和会话范围。
这些 API 不引用 Unity、MNN.Unity 或 SiriAIChat，也不创建前台模型或音频设备。
调用方在所属上下文启动观察器与反馈；它们的 await 保留该上下文，Unity 事件仍在主线程执行。
暂停只取消本地观察/投递，后台任务通过显式 Cancel 工具控制；模型生成完成、音频排队均不能伪造展示回执。
SiriAI 四个适配器保留序列化字段、Unity 生命周期、凭据/设置和语音会话装配，调用上述包 API。
不能让包反向引用 SiriAI 程序集。


独立后台宿主位于 `Tools~/HarnessHost`（.NET8）及 `Tools~/DotCraftHost`（.NET10官方SDK），
统一引用包内HarnessCore项目，保留既有可执行程序集名和配置协议。
`Tools~/HarnessChecks`拥有独立协议对端、行为runner和包独立消费者；`harness_paths.py`
集中声明包/工程根路径。所有Host/测试输出归工程`TestArtifacts~/`，Tools~不是Unity程序集。
SiriAI仅引用包能力并提供应用配置、UI和设备装配，不再保存Host或协议检查实现的副本。
