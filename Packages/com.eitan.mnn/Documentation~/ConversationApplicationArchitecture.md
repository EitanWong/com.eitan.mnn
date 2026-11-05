# 可复用 AI 应用架构

SiriAI 的通用执行代码由 MNN Unity Package 的托管 C# 层维护。新应用直接配置共享
provider、模型目录和设备端口，再将 mailbox 通知投影成自己的 UI。不要复制 Siri 的
会话、安装、声学、Agent 或历史实现。可编译的最小装配见
[示例](../Samples~/BasicExample/Scripts/MNNStandaloneConversationExample.cs)。

一般应用直接使用核心 `MNNConversationApplication`，提供设备 binding、能力快照、
stage registrations 与临时历史目录。调用 `StartAsync()` 后通过 `Commands` 发送输入，
每帧 `Tick()` 投影通知与推进设备呈现，退出时等待 `StopAsync()`。
共享 owner 负责启动取消、命令 join、模型释放、失败清理重试与临时历史关闭。
`MNNStandaloneConversation` 是 sample 中的兼容构造壳，新应用无需复制它。

## 接入顺序

1. 使用 `MNNConversationModelCatalog` / `MNNOnlineModelCache` 选择模型；用
   `MNNModelInstallation.EnsureAsync` 或固定 SHA-256 的 `MNNPinnedModelManifest`
   安装。`HasRecordedFiles` 是轻量 UI 提示，实际准备必须完整校验。
2. 用 `MNNConversationStageRegistration` 注册 model/ASR/TTS/VAD/turn，构造
   `MNNConversationRuntimeFactory`。注册是惰性的，只有所选 route 才创建和准备资源。
   `MNNConversationLocalProviders` 提供本地任务，`MNNOpenAIConversationProvider` /
   `MNNCloudSpeechProvider` 提供云协议，`MNNAgentProvider` 组合模型、计划和工具。
3. `MNNRealtimeDeviceSession` 通过 `MNNRealtimeDeviceBinding` 操作具体设备，拥有权限、
   格式协商、预热、路由监督、输出呈现回执和关闭重试。Unity 使用包的
   `MNNUnityAudioDeviceBinding`；其他插件仅实现其公开 API 的基本操作，不复制控制流程。
4. 使用 `MNNConversationPorts`、`MNNConversationCommands` 和 `MNNConversationRecording`
   接入文本、实时模式和按住录音。用 `MNNConversationMailbox` 消费有界通知，保持单一输入状态。
5. UI 使用 `MNNConversationTranscriptStore` 保存显示历史，使用 `MNNMarkdownHistoryLayout`
   进行切片/测量/可见范围检索，用 `MNNMarkdownImageRequest` 与 cache/lease 加载图片。
   模型实际呈现历史继续由 runtime ledger 拥有，两者不混用。

最小装配示例只展示组合入口；应用仍要连接实际麦克风、扬声器、通知消费与生命周期。
`requireMeasuredSpeech: false` 适合交互预览，不能证明真实设备已经达到实时门槛。

## 模块与扩展点

| 能力 | 共享入口 | 应用提供 |
| --- | --- | --- |
| 应用生命周期与装配 | `MNNConversationApplication` | binding、能力、registrations、历史目录与UI调用 |
| 对话执行和资源组合 | `MNNConversationRuntimeFactory`、`MNNRealtimeSession` | 阶段注册、prompt 和产品选项 |
| 本地任务适配 | `MNNConversationLocalProviders`、`MNNConversationLocalModelOptions` | 目录、backend、语言、音色与缓存身份 |
| 云协议 | `MNNOpenAIConversationProvider`、`MNNCloudSpeechProvider` | 凭据、协议/model preset、网络授权 |
| Agent | `MNNAgentProvider`、`MNNAgentPlan`、`IMNNAgentTool` | 产品路由政策、工具权限、业务动作 |
| 模型资源 | catalog/cache/installation/pinned manifest/path gate | 产品stage映射、默认别名、受信任manifest |
| 声学 | `MNNAcousticFrontend`、`MNNEchoCancellationWorker`、模型角色工厂 | 声学选项、可用模型、实际设备证据 |
| 设备生命周期 | `MNNRealtimeDeviceSession`、`MNNRealtimeDeviceBinding` | 插件的基本操作与事实快照 |
| 输入和通知 | ports/mailbox/commands/recording/input state | UI手势、允许的入口、显示文案 |
| 历史与Markdown | transcript store、history layout、image request/cache | 显示主题、字体、间距和纹理lease |
| 通用表现辅助 | `MNNScreenSpaceFreeRegionSolver`、`MNNMotion` | viewport、占用区、动画参数 |
| 系统集成 | `MNNSoftKeyboard`、`MNNSystemAppearance`、`MNNSystemDiagnostics` | 主线程调用、展示和偏好 |

Siri 保留其序列化资产、命名空间和程序集身份；旧类型是兼容适配。业务天气/系统动作、
产品意图白名单、凭据选择、本地化、orb/背景/材质/画质和视觉布局属于应用。
这些边界允许新 Agent 或应用换业务，而无需改共享执行核心。

## 所有权和线程

| 所有者 | 可变状态与调用约定 | 结束顺序 |
| --- | --- | --- |
| UI/control线程 | commands、device session、Unity绑定、Markdown layout、系统探针 | 先关闭输入和输出门，再等待工作结束 |
| capture producer | 单producer PCM队列；回调buffer为借用，不可跨回调保存 | 禁止新publication后排空/等待 |
| runtime worker | 会话epoch、ASR/VAD/turn、模型和响应生成 | cancel → join → release |
| render writer | 输出pipeline、fade、waveform和最终参考PCM | 关闭response gate，再排空真实回执 |
| factory | 未移交的创建失败资源、可选录音转写 | session stop后调用 `ReleaseAsync`；失败保留owner再重试 |
| model services | identity-indexed detached快照、同路径单writer、原子receipt | 取消单个等待者不取消其他共享发现 |
| transcript owner | 有界索引、缓存页、日志与批量写缓冲 | Dispose/Flush提交尾部；持久化模式恢复完整记录 |

不要并发调用声明为单owner的会话工厂或设备控制API。跨线程只传不可变快照、明确借用的
PCM或有界队列。catalog/cache公开的数据与调用方隔离。设备的 `CloseAsync` 必须等待
声学worker退出；某次join失败时保留声学与lifetime，重试成功前不可释放仍在使用的模型。
停止与工厂清理都应尝试，即便其中一个失败；只有成功释放的owner才移除。

系统键盘和诊断对象在Unity主线程创建/读取/释放。Windows COM对象必须在创建线程释放。
OS接口使用系统公开导出；它们不是新增MNN导出或自定义native bridge。

## 边界与性能契约

- 模型元数据使用 HeadersRead，先检查声明长度，再按预算+1读取并施加body deadline；
  不先将任意响应完整缓冲。安装receipt记录非空文件SHA-256，同尺寸损坏也会失效。
  本地adoption仅记录接管时完整性，不提供远端来源证明。
- 安装校验和解包在后台运行。目录锁包括holder和waiter，最后一个退出才回收；尾分隔符
  归一化。显式传入HttpClient时，其handler和redirect政策由调用方负责。
- PCM采集/输出复用buffer，有界容量溢出必须显式报告；反射诊断在control监督阶段读取，
  不在PCM worker中逐块反射或装箱。模型推理的工作区与native/GPU内存另计。
- 图片接收限制编码bytes，PNG/JPEG在decode前检查宽高/pixels/maxTextureSize；cache
  有独立native估算预算，active显示lease另计。显示释放后才销毁纹理。
- transcript写入复用编码buffer并按block批量flush；分页cache有界。数据校验仍有成本，
  不将历史读写基准推广成整机AI性能。布局solver候选上限64，碎片屏幕保证返回有效空区，
  超过候选上限不保证全局最优。

真实设备验收应测首token/首音频、实际呈现、中断到静音、8ms hop p50/p95/p99、
峰值managed/native/GPU/RSS、60分钟会话和反复启停。离线替身测试不证明音质、真实服务、
权重执行或目标平台ABI。官方MNN源码未修改，没有新增C++或自定义导出；既有原生绑定的
平台/编译器约束继续适用。

## 领域词汇

| 名词 | 含义 |
| --- | --- |
| stage | model、ASR、TTS、VAD或turn任务的惰性注册 |
| route | 经设备和provider能力验证后的执行组合 |
| epoch / revision | 使旧输入、预览和响应失效的代次 |
| response gate | 限制旧响应继续写入或呈现的输出门 |
| receipt | 真实渲染/呈现确认；安装领域则指文件完整性回执，两者类型不同 |
| snapshot | 与内部可变状态隔离的已发布值 |
| borrowed PCM | 只在本次回调有效、接收方不拥有的采样buffer |
| join | 等工作实际结束；发出取消不等于join完成 |
| transcript | UI显示记录；不等于模型上下文或实际呈现ledger |
| binding | 调用平台/插件公开API并汇报设备事实的薄适配 |

完整审查与验证入口见[复用审查](SiriAIReuseReview.md)、
[实时对话](RealtimeConversations.md)、[声学](AcousticPipeline.md)和
[迁移工具](../Tools~/Migration/README.md)。
