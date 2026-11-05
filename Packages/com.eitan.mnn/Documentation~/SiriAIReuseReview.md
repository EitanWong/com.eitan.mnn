# SiriAI → MNN 复用审查与架构契约

审查日期：2026-11-05。审查基线为 `758b650` 和本地 SiriAI 导入源码。
范围是 `Assets/Samples/SiriAI/Scripts` 的 **401 个 C# 文件、60905 行**，其中
Runtime 254 个、Editor 18 个、测试 129 个；不包含任何历史 `TestArtifacts~` 快照。
对照包 Runtime 的 613 个 C# 文件，按领域、调用链、所有权、失败出口和测试进行审查。
清点覆盖全部文件；这不是所有路径的动态覆盖率，也不是逐行形式化验证。

## 结论和分层

已有 `MNNConversationRuntime` 应继续作为唯一共享会话引擎。SiriAI 和 Studio
都应通过 provider、设备端口和 context 扩展它。通用算法、协议、资源管理及模型任务
适配放在包内；应用保留产品配置、授权政策、设备装配、交互和视觉。
“下沉”是放进 **MNN Unity Package 的托管 C# 层**，不是给官方 MNN 增加 C++。
本次没有增加 C++、桥接库、自定义 C ABI 或修改官方源码；现有原生调用仍受明确的
平台/编译器 ABI 契约约束，不能把托管测试描述为官方跨平台 C# API 的验证。

```text
应用：产品配置 / UI / 授权政策 / 场景装配
  ↓ 命令、不可变状态、provider 与设备端口
MNN Components：Unity 生命周期、采集、渲染和主线程适配
  ↓
MNN Core：会话 / 声学 / 模型资源 / 远程协议 / 检索 / 生成
  ↓
MNN Interop：既有受支持 ABI 契约、SafeHandle
  ↓
未修改的官方 MNN
```

## 逐领域决策

| SiriAI 范围 | 本次判断和包内归属 | 应用应保留 |
| --- | --- | --- |
| Core/Conversation、Backends/Conversation | 会话执行、轮次、识别、分段播报、epoch、中断和已呈现历史已在 Language/Realtime；继续移除重复状态 | options/provider/context 映射、产品装配 |
| Core/Audio | 延迟估计、参考时间线、FIR、前处理、源贡献、说话人选择、校准、fade、波形已在本轮下沉 | 产品声学配置、模型选择、耳机/设备策略 |
| Components/Audio | 采集队列/格式/设备桥已有包实现；设备回调只搬运PCM、渲染及交回执 | EasyMic/APM 可选适配、平台路由装配 |
| Backends/Providers/Mnn | 高价值下一单元；统一任务 adapter、backend/performance、增量ASR、多语言TTS和只读诊断 | 序列化配置和模型目录选择 |
| Providers、Configuration | 共享 IMNNConversation* 协议及构造时快照；不要再造第二套公共provider协议 | Siri接口兼容壳、产品默认值 |
| Backends/Providers/Cloud | OpenAI transport已在包；其他服务协议、音频解码和受限HTTP应进入可选Remote任务适配 | 服务preset、账户选择、产品错误文案 |
| Backends/Hybrid、Tools | planner解析、预算、工具执行/取消和来源验证适合可选Agent层 | 工具授权、天气/系统音量等业务能力、路由政策 |
| Core/Models | 下载、校验、安装身份、分页cache和profile准入应有唯一包实现 | stage投影、默认别名、设置列表 |
| Core/Security | 已调用共享 secret store；可分发的bootstrap密钥不等于系统安全保管库 | ScriptableObject入口和产品凭据装配 |
| Core/Presentation | 显示历史存储、bounded图片资源缓存具有复用价值；模型历史和显示历史必须分开 | 文案、偏好、本地化、软键盘和系统外观 |
| Core/Layout、Rendering | 通用UI算法可独立为可选UI工具；不加入推理核心 | orb、背景、tonemapping、视觉动作和画质政策 |
| Components/Presentation、Rendering | 消费共享命令/通知/测量，不持有另一套推理状态 | 布局、动画、交互、Unity UI资源 |
| Editor | 只适配Editor生命周期；共享算法通过Runtime调用 | Siri scaffold、Inspector、预览 |
| Tests | 算法/协议行为测试随共享实现进入包；应用测试验证适配等价 | 产品UI/图形、设备和端到端测试 |

## 本轮已实施

下列公共API已经由包维护，旧 Siri 类型保留原命名空间、程序集和 `.meta`，仅委托实现：

| API | 职责 | 内存与线程契约 |
| --- | --- | --- |
| `MNNEchoDelayEstimator` | 16kHz粗延迟、置信度、激励/证据老化 | 固定ring/投票；单处理worker |
| `MNNAudioReferenceTimeline` | 采样游标对齐、缺口失效、有界回看 | 构造分配一次；超长写入只保留后缀 |
| `MNNAdaptiveEchoFilter` | IPNLMS线性回声残差、相干度 | 固定2048 taps；工作线程；输入与输出不别名 |
| `MNNAcousticInputProcessor` | 重采样、增强、独立VAD/ASR路由、尾部排空 | 借用回调buffer；禁止重入；失败后Reset |
| `MNNAudioMixtureContribution` | 独立增益/DC下的两源相对能量 | `TryEstimate` 通过out返回，无结果数组分配 |
| `MNNAutomaticSpeakerSelector` | 贡献/身份连续性与过期 | 单worker；slot不当作说话人身份 |
| `MNNAecDelayCalibration` | 新鲜稳定设备延迟窗口 | 固定窗口；复用排序scratch，避开Mono排序分配 |
| `MNNSpeechFade` | 按response进行连续增益斜坡 | Process用于单render writer；异步请求不在DSP回调创建 |
| `MNNPlaybackWaveform` | signed peak/RMS/特征的有界快照 | 单render writer、单UI reader；旧response/过期快照失效 |

同时修复了共享输入在无效discontinuity参数前重置模型、重置异常未标faulted的问题；
wake handoff 在公开DTO边界验证整帧/游标/revision和120秒上限；识别预览拒绝
超过65536字符及null更新，避免覆盖已接受内容。

Siri模型目录cache异常现在必定完成共享请求并清理in-flight，允许后续刷新重试；
磁盘预算超限保留本次内存数据并报告未保存。默认OpenAI transport新增真实307/308
回归，外部HttpClient的重定向责任在公开契约中明确：已有client的handler无法由本API
事后修改或证明安全。

另新增公共 `MNNMarkdownImageCache`，由Editor/Player共同使用LRU及显示lease；缓存默认
32项/64MiB估算native预算，逐出后仅在最后显示引用释放时Destroy，active lease另计。
隐藏/重显会取消并重启在途请求，外部传入纹理保持借用。Hybrid释放失败保留owner并可重试，
已成功释放及同一实例的多角色按引用去重；开始释放后拒绝新工作。

专项集成为561通过、0失败、42跳过；无Siri/EasyMic的独立包验证为86通过、0失败、
3跳过；新增Player图片生命周期3/3通过。固定相同视觉源码后的全Editor验证保留原基线
相同的52项失败；全PlayMode也有3项原有失败，已用原始源码重现，不能宣称全套已全绿。
8个实际程序集及13种平台条件编译通过，但不等于目标Player构建或真机推理。

Apple M5/Homebrew Mono单次托管测量中，校准64次调用分配从8192B降为0B；
组合声学8ms hop的p95为2.530ms、测量区间分配0B，不包含神经推理、设备或模型内存。
专项验证及所有数字见本次仓库内
`TestArtifacts~/SiriAIReuseAudit/verification.md`。详细领域报告、逐文件归类、前后源码
摘要和原始XML也保存在同一目录；它们不是分发包依赖。

## 尚需迁移的完整单元

这些是审查后的工程待办，不能把本轮首批下沉描述为整套迁移已完成。

| 顺序 | 完整单元 | 完成门槛 |
| --- | --- | --- |
| 1 | 一套bounded notification mailbox，替代 SiriAIChatContext 与 MNNConversationPorts 的两份队列/快照代码 | 相邻合并、user FIFO、epoch、满队列背压、preview revision、UTF-16、重置和分配测试跑同一组contract |
| 2 | 手动录音/input revision/命令排序服务 | 录音上限、最短长度、丢帧、旧ASR取消/等待、clear/reset、重复启停；波形observer可选 |
| 3 | 本地Conversation provider完整工厂 | backend/performance、Omni、增量Paraformer、语言/voice fallback、流式和缓冲能力等价；保留实际支持差异 |
| 4 | 唯一模型profile、安装回执与目录cache | 同长度文件损坏不得凭size复用；幂等安装、同目标单writer、取消、hash/revision和失败重试；未知模型不虚报可运行 |
| 5 | 第三方云语音与HTTP增量解码 | HeadersRead、读取期间byte budget、deadline、取消、慢流、3xx、PCM分块背压、真实服务能力 |
| 6 | Agent planner/tools和知识检索 | typed计划、上下文预算、权限注入、工具取消、来源/引用约束；测试不依赖真实账户 |
| 7 | 显示历史磁盘存储 | 分页、配额、写入/清理失败、崩溃恢复、长会话；模型history ledger继续独立 |

现有 `MNNConversationMnnProviders` 只覆盖简化Language/Recognition/Speech；直接将
Siri全部provider替换为它会丢失显式backend、Omni、增量ASR、语言/voice处理、VAD/Turn
及诊断。因此先补包的完整能力契约和等价测试，再删上层实现。

## 必须跟进的风险

- 图片像素预算在 `LoadImage` 后检查，不能约束解码前峰值；UnityWebRequest轮询Abort也
  不是严格的瞬时下载内存上限。下轮需格式维度预检、受限接收器与并发解码预算。
- 模型元数据部分路径在返回前全量缓冲，随后才检查长度；改用HeadersRead并在读取中限字节。
- 安装回执只验路径/长度会短路hash校验；同尺寸损坏文件应有独立回归。
- 模型目录逐页重建、线性查找与多次JSON clone有规模化分配风险；用归一化identity字典和不可变snapshot。
- 静态安装gate不能无条件Remove，否则可能制造两个writer；须按waiter生命周期安全回收。
- EasyMic capture worker的反射GetValue有bool装箱；不能把纯托管声学API的0分配推广到整个设备链。
- 原生调用的取消检查点、模型驻留和真实音质仍需要实际权重/设备验证；不能用替身或包布局检查替代。

## 可持续工程验收

一个单元按“现有用户行为红灯 → 共享实现 → 应用薄适配 → 包与宿主同组contract”推进。
模型、session、capture/render与UI分别拥有可变状态，跨边界传不可变快照或明示借用buffer。
停止必须先关输入/输出，再等待worker退出，最后释放；释放失败保留owner以便重试。
公共API写明frame/sample单位、线程、所有权、容量、取消与重入语义。

性能验收应按目标设备建立预算：预热后的capture/render分配、每8ms hop的p50/p95/p99、
首字幕/首token/首音频/真实播放延迟、中断到静音/推理退出、峰值managed/native/GPU/RSS，
以及60分钟会话和重复启停后的高水位。不同设备、prompt、runtime和包含范围不能直接算加速比。
缓存预算与active lease、模型工作集分别计账；“缓存条数有界”不是进程内存有界。

本轮没有可用的crapper/dryer/mutator，不声称CRAP<10、重复度<0.8、完整变异或100%覆盖率。
实际采用行为回归、逐程序集编译、条件编译、手工定点变异和显式工具限制报告。
现有CI还应增加不需要模型的managed包级回归；被忽略的Siri本地源码不能成为fresh checkout
必然存在的隐式测试依赖。迁移补丁与安装说明见[迁移工具](../Tools~/Migration/README.md)，
声学API用法见[共享声学处理](AcousticPipeline.md)，目录规则见[项目结构](ProjectStructure.md)。
