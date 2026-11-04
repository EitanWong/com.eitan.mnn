# MNN Studio 编辑器工作区

入口：`Window > MNN > Chat Studio`（Cmd/Ctrl + Shift + M）。需要 Unity 2021.3 或更新版本。
当前真实推理验证范围为 macOS 64 位 Mono，官方 MNN 3.6.1 与 Apple libc++ 的特定 ABI。
这是本项目限定版本的 C# 编组契约，不是官方提供的跨平台 C# API；Windows、Linux、移动端、
WebGL 和 IL2CPP 不具备相同推理支持。没有新增 C++、桥接库、自定义 C 导出或官方源码修改。

## 对话与媒体

- Chat：真实多轮角色对话与逐 token 流式回复，显示耗时、token 数和停止/长度上限状态。
- 视觉与 Omni：选择或拖入 PNG/JPEG/BMP，每轮一张图；显示缩略图与可移除附件。
- 音频与 Omni：上传 WAV，或录制最长 30 秒麦克风音频；当前 Omni 原生波形回调未实现，不提供语音回复。
- `Spoken replies`：仅在后端提供语音输出能力时，普通聊天完成后自动播放生成语音；当前原生 Omni 不启用此选项。音频结果行提供 Play、Stop、Save audio。
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
- Keyword spotting (KWS)：中文 Zipformer2 KWS 支持自定义拼音唤醒词、WAV/录音检测及 `Start listening` 麦克风连续检测；显示关键词、时间和置信度。`Load demo recording` 可直接验证已安装样例。参见 [唤醒词使用说明](KeywordSpotting.md)。
- Echo cancellation (AEC)：Model Manager 搜索 `DTLN`、`AEC` 或 `回声消除`，安装 128/256/512
  后选择同步、等长的 16 kHz 麦克风与播放参考 WAV。结果可播放、停止、保存；输出保留
  24 ms 模型延迟。当前为 CPU/High，可选 Burst 加速包内 FFT，见 [DTLN AEC](DtlnAec.md)。
- Audio processing：可使用 DTLN / DeepFilterNet3 / FRCRN 增强、MossFormer2 双人分离、VAD 和轮次检测。
  任务菜单另提供 Voice activity detection、Turn detection、Speech enhancement、Speech separation 和 Speaker diarization；本地模型按已安装的配置文件路由，未识别的模型仍可在原 Audio processing 工作区查看。
  AV-MossFormer2-TSE 接受混合 WAV 与同步的 25 FPS 目标人脸帧文件夹。输入约定、接口与数值验证范围见 [语音处理模型](SpeechProcessing.md)。
- 所有成功生成音频的结果统一显示 `RTF`、音频秒数和生成秒数。
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

## 编辑器渲染性能

长对话保留完整消息，但只为可见区域及少量预留区域创建消息控件；屏幕外使用已测量高度保留
滚动范围，搜索仍可跳转到任意消息。首次打开对话、改变内容宽度、主题或显示缩放时重新测量；
追加消息复用已有布局。侧栏只绘制可见会话行，每次布局统一分组历史记录，保留文件夹折叠与拖放。
Markdown 复用文本高度和布局选项，流式文本仅在实际显示时格式化最新快照；语音面板不解析隐藏的聊天正文。

可在已有 Unity 与测试依赖的环境中执行窗口回归（需要图形设备）：

```bash
python3 Packages/com.eitan.mnn/Tools~/Testing/run_conversation_tests.py \
  --package-only --graphics --label studio-rendering \
  --filter MNN.Unity.Tests.StudioRenderingPerformanceTests
```

该测试比较预热后 24/480 条消息与 24/960 个侧栏会话的布局加重绘中位耗时，并检查滚动范围、
搜索、缩放、追加和重置。数据保存在隔离工程的 `TestArtifacts~/StudioRendering/`，
XML 与日志位于 runner 的输出目录。这是 Editor UI 回归，不测量首次模型扫描、原生推理或模型吞吐量。

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
回复使用 `MNN.Unity` 的托管 Markdown AST，支持标题、段落、列表、引用、强调、删除线、行内代码、
围栏代码块、链接与图片链接；编辑器和 Player 复用同一解析器，代码内容仍可复制，图片只允许
`http`、`https` 和 `file` URL，其他 Markdown 保留为可读文字，不执行模型内容。
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

Chat 的 Voice 面板复用 SiriAI 提取的 `MNNConversationRuntime`，组合普通文本模型与独立 ASR/TTS。
Settings 中选择已安装的 Recognition / Speech 模型；缺少时在 Model Manager 安装后 Refresh。
面板提供动态小球、真实 RMS 音量反馈、Listening / Thinking / Speaking / Muted / Error 状态、
静音、打断、结束和返回聊天。减少动态效果可关闭持续形变。

麦克风持续采集，由共享核心执行能量 VAD、端点、识别、文本生成与分段播报；
采集和输出直接使用 Runtime PCM 设备，无逐轮 WAV 中转。没有回声控制时采用半双工解释策略，
播报期间抑制自动语句解释，Interrupt 按钮仍可立即关闭旧输出并继续听取下一句。
这是逐轮模型调用，尚未测定目标设备端到端实时吞吐。设备权限、实际听感与环境噪音须在目标机器验证；
自动测试不申请麦克风权限、不录制环境声音。组合与所有权见 [共享实时对话](RealtimeConversations.md)。

## 布局、性能与存储

采用 ChatGPT/Codex 风格侧栏、轻量顶部、居中阅读区、独立底部输入框及 Apple HIG 的留白与层级。
侧栏宽 208 GUI points，阅读区最大 760，最小窗口 760×540；同时验证 1180×800 布局。
浅/深主题采用语义颜色，文字对比度检查达到 4.5:1。语音面板动画最多 30 Hz，支持减少动态效果。
样式、Markdown 解析与图片预览缓存复用；图片最长边缩为 320 像素。小球使用程序化绘制，无图片下载。
VoiceOver、Dynamic Type、系统 Increase Contrast 尚未全面验证，不能视为完整原生 macOS 无障碍实现。

模型按需加载，普通工作区复用当前模型实例；进入语音前先等待原模型释放，再加载对话所需的各职责模型。
切换模型或任务、刷新后模型不可用、修改模型设置、手动卸载或闲置 5 分钟会释放实例。
首次加载、缓存准备、推理与模型释放在受跟踪的工作任务执行；窗口关闭后仍保留句柄，直到活动调用退出。
配置、模型类型和管线完整性复用发现快照，安装或修改外部数据后 Refresh 重新检查。
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

本项目的测试依赖位于 `TestArtifacts~/GenerationImplementation/Dependencies`，MNN Runtime 在 Editor 中会自动发现其中的
`espeak-ng/run-espeak-ng.sh`，该脚本为下载的上游可执行程序设置动态库搜索路径；不需要手动选择或重复安装。
Browse 选择的是数据目录，不是此脚本或可执行文件；选择/取消目录均保留当前输入文本。
旧版若误将 `af_dict` 等字典文件保存为工具路径，Studio 会自动清除该设置并重新发现工具；数据文件不会被当作程序启动。环境变量中的无效工具路径也会跳过。
Piper 当前安装的 Lessac/Amy/Kathleen/Ryan 是英文音色。中文输入会在 MNN Runtime 自动复用本地已安装的中文模型，
中英混读按文字系统分段并统一采样率，不下载权重；没有可用模型时给出明确的语言错误。
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
Qwen3-TTS 的其他导出和其他 VITS 等仍需专用适配与真实结果测试。

## CosyVoice-300M-MNN 初代

Speech Synthesis 可以发现本地 `CosyVoice-300M-MNN/mnn_cosyvoice300m.json` 标记目录，
选择 `Default` 音色后直接合成 22050 Hz 音频并播放或保存。默认完整参考、10 步 Flow，
显示本次实际后端与 RTF。该本地转换独立于下面的 CosyVoice3 下载安装流程。
部署、API、实验量化与平台范围见 [CosyVoice-300M-MNN](CosyVoice300M.md)。

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

## DhVaani

安装 `Bbkblo/DhVaani-0.5-MNN` 后选择 Speech synthesis。
选择或录制 0.5–15 秒参考 WAV，并填写录音中的准确原文，再输入 Hindi 或 English 目标文本。
支持 seed、1–32 采样步和 0.5–2 倍语速；默认八步。Use example 提供英文目标句，
不会自动替用户填写参考录音原文。输出 24 kHz，可播放和保存；数字需写成口语形式。

## Qwen3-TTS 显式 KV

安装 `yunfengwang/Qwen3-TTS-12Hz-0.6B-Base-MNN` 后选择 Speech synthesis。
选择 Chinese / English，输入对应语言文本，可调整 seed、试听并保存 24 kHz WAV。
Voice 菜单列出 `voices/<name>/` 中校验通过的预编码参考音色，默认 demo。
添加同协议的 `meta.json`、`ref_code.i32.bin`、`spk_emb.f32.bin` 后刷新模型即可选择。
此导出不含原始 WAV 声音编码器，不能直接上传 WAV 进行声音克隆。
最多 200 字符、360 帧（约 30 秒），超过生成帧数上限会明确报错，建议按句拆分。
模型权重尺寸及参考音色校验不通过时禁用运行。界面仅显示此管线支持的音色、语言和 Seed 控件。
其他 Qwen3-TTS 导出不自动套用此管线。

## Pocket-TTS

安装 `developerabu/pocket-tts-mnn` 或刷新本地模型后选择已有 `Pocket-TTS-MNN`。
在 Speech synthesis 中选择或录制 0.5–15 秒参考 WAV，输入英文目标文本，
可调整 seed、播放并保存 24 kHz WAV。参考录音不需要填写原文。
最多 50 token / 30 秒；当前按短句运行，超限明确提示拆分。
公开 `reference_sample.wav` 位于模型目录，可直接选择；受限的预设音色未自动下载。
全部推理由 MNN 执行，首次加载会核验固定版本文件，后续复用图和最近的参考音色。

## IndicF5

安装 `developerabu/IndicF5-MNN` 后选择 Speech synthesis。选择或录制参考 WAV，并填写录音原文；
当前提供 Hindi / Tamil 示例，输出 24 kHz 单声道 WAV。管线使用 INT8 DiT 和动态 Vocos FP32，
短句约 2048 mel 帧上限。Tamil 内容检查仍保留近音拼写限制。

## 文档版面分析

在 Model Manager 安装 `tansen87/PP-DocLayoutV3_mnn`，或选择包含固定权重和
`layout-meta.json` 的现有文件夹，再切换到 **Document layout**。
专用页面提供页面图片选择、置信度阈值、Analyze page，以及带类别和阅读顺序的区域预览。
点击区域或使用 Region 菜单可查看对应源图坐标，并复制完整区域列表。
该任务使用 CPU；表格检测只返回表格范围，文字读取和表格单元格解析需另外处理。

## 新增图像任务页面

[Style transfer](StyleTransfer.md) 使用固定 Candy 风格，选择照片即可运行，输出224×224，
支持预览与 PNG 保存。[Segmentation](Segmentation.md) 使用 DeepLabV3 MobileNetV2，
展示原图尺寸的 Pascal VOC 分类掩码、颜色图例和各类面积占比。两者共用现有
MNNImageTask / Studio backend，不要求文本提示，不把图像任务写入聊天历史。
两项目前通过 macOS ARM64 桌面和 Android 实际权重检查；iPhone 推理仍待验证。

## 性能验证范围

普通生成语音预览也直接创建 PCM AudioClip；只有保存音频或录音附件才需要 WAV 文件。界面仍按限频快照更新 Markdown，语音队列和通知队列有界。真实模型速度、声学质量、硬件呈现延迟和麦克风权限需在目标设备实测。
