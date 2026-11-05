# SiriAI → MNN 完整复用审查与迁移

2026-11-06 的后续实现与真实验收见[验收状态](SiriAICompletionAcceptance.md)。
下面的全量数字保留首轮快照含义；后续结果不将不同范围相减，也未宣称七项全部关闭。

审查日期：2026-11-05。原始范围为 `Assets/Samples/SiriAI/Scripts` 的401个C#文件、
60905行，对照包Runtime原613个C#文件，覆盖领域归属、调用链、资源所有权、失败出口、
测试和热路径。首轮提交 `bf40588` / `277ece9` / `9b5ec11` 完成声学基础和边界修复；
本轮以 `9b5ec11` 及本地Siri导入源码为迁移前基线，继续完成其余通用执行单元。
文件清点不等于动态路径覆盖率或逐行形式化验证。

## 最终分层

```text
应用：序列化配置、业务政策/工具、场景/插件绑定、文案和视觉
   ↓ 配置、基本设备操作、命令、状态快照
MNN Components：Unity设备和生命周期适配
   ↓
MNN Core：会话、任务provider、Agent、声学、模型资源、输入、历史和Markdown
   ↓
MNN Interop：既有MNN绑定、系统公开ABI声明
   ↓
未修改的官方MNN / 系统公开API
```

下沉位置是MNN Unity Package的托管C#层。没有新增C++、native bridge、自定义MNN导出
或修改官方MNN。OS探针声明集中到 `MNNInterop.*`；既有MNN绑定的平台/编译器约束继续
存在，不能把托管行为测试描述成跨平台官方C# API或直接C++调用验证。

新应用的接入顺序、API、线程/所有权、容量/取消和性能边界见
[AI应用架构](ConversationApplicationArchitecture.md)。
[最小组合示例](../Samples~/BasicExample/Scripts/MNNConversationExample.cs)参与程序集编译。

## 已完成的通用单元

| 原Siri职责 | 包内唯一实现 | 上层职责 |
| --- | --- | --- |
| 会话循环、epoch、轮次、识别、分段播报、中断、呈现历史 | `Language/Realtime/MNNConversationRuntime` | options/context映射 |
| 本地Language/Omni/ASR/TTS/VAD/turn完整任务适配 | `Language/Providers/MNNConversationLocalProviders` | 模型目录、backend、语言/音色配置 |
| 阶段规划、惰性创建、prepare、diagnostics、转写和失败释放 | `MNNConversationRuntimeFactory` 与stage注册/plan | 产品preset和装配 |
| OpenAI、DashScope、Volcengine、MiniMax协议及受限HTTP | `Language/Remote` | 凭据、服务/model preset、产品授权 |
| planner、预算、工具执行/取消、知识与引用、精确算术 | `Language/Agent` | intent白名单、路由政策、天气/系统业务工具 |
| 模型身份、深snapshot、目录发现/分页cache、任务profile | `Models/Catalog` | stage投影、默认别名和选择列表 |
| 安装回执、固定manifest、SHA-256、同路径单writer、进度 | `Models/Downloads`、`Delivery`、`Transfer` | 受信任manifest和存储目录 |
| 参考时间线、延迟估计/FIR、输入路由、源贡献/说话人、校准 | `AudioProcessing` 对应领域 | 声学参数和产品选择 |
| 完整声学frontend、模型角色、wake bank/handoff、AEC worker | `AudioProcessing/Acoustics`、`EchoCancellation/Common` | 模型可用性与真实设备证据 |
| capture/output transport、权限、格式协商、预热、路由、半双工、回执、stop/retry | `AudioProcessing/Realtime/MNNRealtimeDeviceSession`及policy | Unity/EasyMic公开API基本操作 |
| 输入revision、bounded mailbox、按住录音、排空和命令排序 | `Language/Realtime` shared input/mailbox/recording/commands | UI手势和允许的入口 |
| 显示历史、持久化恢复、分页cache、Markdown切片/测量/可见区 | transcript store、`Language/Markdown/MNNMarkdownHistoryLayout` | 字体、主题、间距 |
| 图片接收/维度预检、cache/显示lease和销毁 | `Language/Markdown` image request/header/cache | 图像显示与lease生命周期 |
| 空闲区域计算、解析弹簧、键盘遮挡、OS外观、系统采样 | `Common/Layout`、`Platform`、`Diagnostics` | viewport、动画参数、偏好和显示 |

旧Siri类型保留命名空间、程序集和既有`.meta` GUID，内部只保留配置投影、接口兼容、
插件基本操作及产品生命周期装配。没有引入包对Siri或EasyMic的反向依赖。

Siri保留：本地化、交互偏好/布局参数、业务工具与授权政策、服务/凭据选择、序列化资产、
orb/背景/材质/tonemapping/画质策略、Inspector和scaffold。通用Markdown布局与运动/几何
已分离，不以“UI”为由继续留两份算法。设备插件绑定仍调用实际公开API；共享session
负责控制流程。真实设备和模型资格验证是部署验收工作，不是待迁的通用实现。

## 修复的关键问题

- 安装不能再凭路径/长度复用：schema2 receipt逐文件SHA-256，同尺寸损坏会重装；
  已有无效receipt禁止结构性adoption。调用前快照配置，UI不做大权重同步hash。
- 同路径gate登记所有holder/waiter，取消不拆锁，最后一个退出才回收；尾斜杠归一。
  catalog使用统一发布锁，entry/嵌套metadata/warnings隔离，避免并发静默丢条目。
- 元数据使用HeadersRead、声明长度检查、读取预算+1和body deadline，避免先全量缓冲。
  发现请求完成失败也会清除in-flight，可重试；单个调用方取消不取消共享发现。
- pinned archive支持manifest真实路径与精确文件布局，先受限暂存并校验再发布，
  不再依赖隐藏的CosyVoice固定zip名；旧Cosy协议通过显式配置兼容。
- 创建失败或释放失败保留owner供重试；同实例多角色去重，成功释放的不重复释放。
  停止回调抛异常仍执行close/join；join失败保留仍在使用的声学资源和lifetime。
- 重复finish recording不丢首个排空buffer，旧ASR即便忽略cancel也不能覆盖新草稿；
  timeout保留实际drain任务，capture回调异常不会永久占住命令reservation。
- 图片在LoadImage前验证PNG/JPEG尺寸和pixels/texture预算；接收器限制编码bytes。
  active lease独立计账，最后显示引用释放才销毁。
- 历史journal热路径批量writer/flush并复用编码buffer；Markdown cache限制项数与字符量。
  APM反射诊断移到control监督，PCM worker只读取缓存值。

## 验证与可核查范围

本轮测试使用隔离工程和冻结源码。迁移前基线包含package全部Editor及Integration与Siri
测试：4687项，3574通过、681失败、432跳过。缺失本地权重/native资产等导致既有失败，
迁移后按同名失败集合对照，不把总数增长误解为回归，也不宣称全套全绿。
最终完整Editor为4864项：3749通过、681失败、434跳过；失败身份与基线完全相同，
无新增失败。原有失败仍需按各自模型、native或既有断言分别处理，不能宣称全绿。
不依赖Siri/EasyMic的独立包为207项：205通过、0失败、2项因分配计数器不可用而跳过。
PlayMode为402项：145通过、32失败、225跳过，无新增失败；23个实际程序集及13组平台
C#条件编译全部通过。这些编译结果不代表native ABI或目标设备推理验证。
最终数字、失败身份差异、平台矩阵、红/绿证据、性能测量和局限保存在仓库根
`TestArtifacts~/SiriAIFullMigration/verification.md` 与原始XML。

独立包测试不导入Siri/EasyMic，用实际托管实现与受控provider/HTTP验证行为；Siri测试验证
兼容适配及Unity生命周期。平台矩阵为C#条件编译，不等于目标Player构建、native链接或真机。
没有下载大型模型或服务凭据，实际权重、音质、云账户、设备路由和60分钟高水位仍需目标
环境验收。crapper/dryer/mutator不可用，不声称CRAP<10、重复度阈值、100%覆盖或完整变异通过。

Mono单次托管历史基准中，1万短消息热态追加从本轮迁移初版约24ms/2.4MB分配优化到
约1.6–1.7ms/16KB；原Siri约1.45–1.48ms/405KB。带校验的随机读1000条约7.3ms/4.7MB，
原Siri约6.0–6.2ms/6.8MB；性能和完整性成本一起报告。capture/queue/render1000次操作
测得0B分配，Unity计数器无效时明确跳过，不能外推整个设备链或神经推理的0分配。

## 交付与维护

Siri本地导入路径被Git忽略。本轮源码在工作区已经迁移，同时通过完整版本指纹和幂等
补丁保存可审查交付；版本不匹配先拒绝，零写入，原GUID不变。见
[完整迁移工具](../Tools~/Migration/README.md)。包源码、行为测试、文档和工具正常纳入版本库。

以后新增同类能力先扩展包的共享契约和行为测试，再让应用配置和消费它。公共API明确
所有权、单writer、借用buffer、容量、单位、取消和重试；通用算法不复制到产品或Editor。
性能验收分别记录托管/native/GPU与active lease，使用目标设备、权重和相同包含范围。
