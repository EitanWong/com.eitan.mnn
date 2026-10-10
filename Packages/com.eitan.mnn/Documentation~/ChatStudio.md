# MNN Studio 编辑器工作区

入口：`Window > MNN > Chat Studio`（Cmd/Ctrl + Shift + M）。需要 Unity 2021.3 或更新版本。
当前真实推理验证范围为 macOS 64 位 Mono，官方 MNN 3.6.1 与 Apple libc++ 的特定 ABI。
这是本项目限定版本的 C# 编组契约，不是官方提供的跨平台 C# API；Windows、Linux、移动端、
WebGL 和 IL2CPP 不具备相同推理支持。没有新增 C++、桥接库、自定义 C 导出或官方源码修改。

## 对话与媒体

- Chat：真实多轮角色对话与逐 token 流式回复，显示耗时、token 数和停止/长度上限状态。
- 视觉与 Omni：选择或拖入 PNG/JPEG/BMP，每轮一张图；显示缩略图与可移除附件。
- 音频与 Omni：上传 WAV，或录制最长 30 秒麦克风音频；支持语音输出的模型可生成回复音频。
- `Spoken replies`：普通聊天完成后自动播放生成语音；回复下方也提供 Play、Stop、Save audio。
  播放状态来自 Unity 音频后端，播放进度驱动音量反馈；失败会显示错误，不用计时器模拟播放。
  当 Unity Editor 全局静音时，回复行会显示 `Enable & Play`；只有用户点击后才解除全局静音并播放。
- Embeddings：真实向量、余弦相似度、维度、耗时和 JSON 复制。
- Reranking：真实查询/文档相关性概率。
- 任务工作区：23 个入口覆盖当前 Model Manager 的全部分类，按 Language、Multimodal、Audio、Vision、Generation、Retrieval 分组。
  左侧 `Tasks & models` 打开任务目录，显示本地模型数量、可运行或模型准备状态。
- Speech recognition：专用 WAV 上传/录音与 Transcribe，默认逐字转写；使用支持音频输入的 LLM，结果不追加到聊天历史。
- Image understanding、Audio understanding、Omni、OCR/document reading：专用指令与媒体选择，支持单次流式结果和复制。
  Omni 可按模型能力勾选生成语音，并播放、停止、保存 WAV。OCR 通过视觉语言模型提示词实现，质量由模型决定。
- Code generation、Safety/moderation：按模型类型筛选并运行独立文本请求；审核须遵循模型卡的提示词与标签格式。
- Image generation：支持 SD 1.5 `general` 文生图（提示词、负面提示词、步数、guidance、seed、PNG 保存）；Sana Edit V2 参考图编辑管线可运行、显示并保存结果，但本轮语义颜色编辑效果尚未达标。
- Speech synthesis：支持 Supertonic fp16（M1/M2/F1/F2 音色、语速、步数、seed、播放和 WAV 保存）。Bert-VITS2 陈曦中文（44.1 kHz）和 Piper 英文 Amy/Kathleen/Ryan（16 kHz）也支持合成、播放和 WAV 保存。
- Sherpa-MNN streaming Zipformer 中文/英文模型提供 ASR 分类、完整仓库下载、目录发现和文件准备指引；Studio 尚未接入 Sherpa 流式识别器运行时，下载这些模型不能直接转写。独立 OCR、分类、检测、分割、姿态、人脸、超分辨率、风格迁移及其他 NLP/音频任务也仍提供准备引导。

扫描 `Assets/StreamingAssets/MNN/Models`、`Application.persistentDataPath/MNN/Models` 和设置中的
额外目录。任务在顶部菜单切换，模型菜单随任务筛选。媒体能力读取 `llm_config.json`；
Embedding/Reranker 及专用 pipeline 按安装标记中的仓库 ID 或目录名识别。右上角更多菜单提供刷新、Model Manager 与 Markdown 导出。
刷新保留仍存在的模型与会话；模型缺失时保留文字并禁用发送。

## 按任务准备模型

1. 在顶部工作区菜单选择任务，或从左侧 `Tasks & models` 目录进入。
2. 无兼容模型时显示三步准备页面：推荐仓库/模型卡、所需完整文件、下载与选择方法。
   图片生成推荐 `MNN/stable-diffusion-v1-5-mnn`、`MNN/MNN-Sana-Edit-V2`；
   独立 TTS 推荐 `MNN/supertonic-tts-mnn`、`MNN/bert-vits2-MNN`、`MNN/piper-voices-MNN`；
   转写推荐 `MNN/LFM2.5-Audio-1.5B-MNN`。Sherpa-MNN streaming Zipformer 可在 ASR 分类中准备：
   `MNN/sherpa-mnn-streaming-zipformer-bilingual-zh-en-2023-02-20`（中文/英文，int8）和
   `MNN/sherpa-mnn-streaming-zipformer-en-2023-02-21`（英文）。两者需要完整保留 `config.json`、
   `configuration.json`、encoder/decoder/joiner `.mnn` 和 `tokens.txt`；当前 Studio 不含 Sherpa runtime。
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
减少动态效果可关闭持续形变。当前验证的语音输出契约为 Qwen2.5-Omni，24 kHz 单声道。

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
另验证普通聊天的 Spoken replies 自动播放。Player 验证角色历史、流式回调、停止后再次推理和 Omni 语音。
拖拽测试发送 IMGUI 鼠标与拖放事件；隔离系统拖拽启动调用，避免 macOS 模态拖拽阻塞批量测试。

此前聊天工作区验证结果见 `TestArtifacts~/MNNValidation/Studio/live-summary.json` 与 `live-final-results.xml`；
平台编译/Player 报告在 `TestArtifacts~/MNNValidation/PlatformValidation/`。
条件编译成功不能作为其他平台真实推理支持或所有模型兼容的证据。

此前完整 EditMode：154 通过、0 失败、2 项 Explicit 联网测试跳过（总计 156）；
Studio/流式相关 55/55，最终 UI 回归 9/9。macOS arm64 与 x86_64 Mono Player 各 115/115；
x86_64 使用 Apple Silicon + Rosetta。13 组平台条件编译检查全部通过。

本次任务目录扩展 EditMode：48 通过、0 失败、0 跳过。验证见 `TestArtifacts~/StudioTasksValidation/results.xml` 与 `unity.log`：
覆盖模型分类与下载筛选、专用模型隔离、完整文件夹发现、23 个任务在紧凑/宽窗口的真实 IMGUI 布局，
以及通过专用工作区运行已有视觉、音频转写和 Omni 模型。不将模型准备页测试作为对应 pipeline 已接入的证据。

### 运行已验证的生成模型

在 `Speech synthesis` 中选择 Supertonic 完整模型文件夹，输入英语文本；选择音色、步数和语速后运行，结果可播放、停止或保存 WAV。默认优先使用 fp16，模型最多接受 500 字符和 30 秒预测时长。超出词表的文字会明确拒绝。

在 `Image generation` 中选择 SD 1.5 的仓库根目录（含 `general`）或 `general` 本身，输入提示词后运行。默认 20 步、guidance 7.5、seed 42，输出固定 512×512，可预览并保存 PNG；默认自动选择可用的 Metal GPU，失败时按阶段回退 CPU，生成耗时取决于机器；Stop 在当前模型阶段结束后生效。模型需要三个 `.mnn` 及对应 `.mnn.weight`，并保留 CLIP 的 `vocab.json` / `merges.txt`。

Sana Edit V2 请选择完整仓库根目录，再选择参考图片并输入编辑指令。参考图会居中填充并缩放至 512×512；默认 10 步、guidance 4.5、seed 42。结果可预览并保存 PNG。已验证管线和资源生命周期，但本轮“红车改蓝车”的结果仍主要为红色，颜色编辑效果尚未达标。

在同一个 `Speech synthesis` 工作区中也可选择：

- `MNN/bert-vits2-MNN`：下载 `config.json`、`tts_generator_w_bert_chenxi_0310_int8.mnn`、`common/mnn_models/chinese_bert.mnn` 及其 `.weight`，以及 `common/text_processing_jsons`。输入中文，例如“你好，欢迎使用语音合成。”。当前支持陈曦音色与中文前端，尚不支持英文或中英混读，最多 300 字符。
- `MNN/piper-voices-MNN`：下载 `config.json`、所需的 `en_US-*-low_fp16_public.mnn` 和 `espeak-ng-data`。`espeak-ng-data` 默认自动从模型文件夹读取，无需选择；**Browse…** 打开目录选择器，手动选择数据文件夹时检查其完整性并按模型记住。eSpeak-NG 工具从模型目录、环境变量、常见安装位置、PATH 和项目已有测试依赖自动查找；缺失时安装上游 eSpeak-NG（macOS 可用 `brew install espeak-ng`），或设置 `MNN_ESPEAK_NG_PATH`。选择 Amy、Kathleen 或 Ryan，输入英文，最多 1000 字符。文件缺失或工具启动失败会给出错误提示。

本项目的测试依赖位于 `TestArtifacts~/GenerationImplementation/Dependencies`，Studio 会自动发现其中的
`espeak-ng/run-espeak-ng.sh`，该脚本为下载的上游可执行程序设置动态库搜索路径；不需要手动选择或重复安装。
Browse 选择的是数据目录，不是此脚本或可执行文件；选择/取消目录均保留当前输入文本。
旧版若误将 `af_dict` 等字典文件保存为工具路径，Studio 会自动清除该设置并重新发现工具；数据文件不会被当作程序启动。环境变量中的无效工具路径也会跳过。
运行时根据 `config.json` 判断架构，模型文件夹改名仍能正确识别。播放/保存输出与 Supertonic 共用。

本次验证模型保存在项目 `TestArtifacts~/GenerationImplementation/Models`，可用 Choose local folder 直接选择，无需重复下载到 Assets。
