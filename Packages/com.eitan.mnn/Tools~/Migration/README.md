# SiriAI 共享核心迁移

## 对话重置恢复

`conversation-reset` 修复停止或故障清理后重置仍调用已释放工厂的问题：等待清理结束，
确认旧资源释放后创建新会话，再清空历史。正常重置继续复用模型；清理失败时保留旧 owner，
允许重试。切换对话通过新注册恢复选中上下文。

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage conversation-reset
```

此 stage 校验四份源码的完整字节和现有 meta GUID；不匹配时零写入，重复运行零写入。
回归使用真实托管工厂与模型替身，不涉及原生推理。

## 后台任务独立开关

本地 SiriAI 已加入“设置 → 后台任务 → 使用后台任务”。默认保留原有启用状态；
关闭后隐藏任务图标和窗口、停止连接轮询与结果投递，并从下一轮对话的工具列表移除后台工具。
选择会保存；纯对话聊天继续可用，主机已经受理的任务继续由主机管理。

开关现支持热切换：当前模型、会话和正在生成的前台回复继续使用；下一轮对话重新生成可用工具
列表。开启或更换主机时更新工具绑定，关闭后阻止旧规划再次调用后台工具。连接主机默认也不重建
前台会话。

匹配 `SiriAIBackgroundTasks.files.json` 基线的导入版本可运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage background-tasks
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage background-hot-switch
```

已应用独立开关的版本只需执行 `background-hot-switch`，后者有单独的精确基线。
补丁保留现有 GUID 和程序集名称。热切换已验证 96 项设置、任务窗口、工具与生命周期回归，
包括流式回复不中断、模型只准备一次、次轮工具注入、主机绑定更新和轮询停止/恢复。
另验证补丁重复执行、版本不符时零写入和原始字节回滚。
验证使用固定包快照及托管替身，不涉及模型下载或原生推理。

仓库忽略 `Assets/Samples/`，本地 SiriAI 的原始源码不受版本控制。本轮已在工作区应用迁移；为了保存可审查、可重复应用的改动，而不把整个本地示例及无关资源加入版本库，精确迁移补丁保存在这里。

在相同 SiriAI 版本的仓库根运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py
```

脚本先检查完整补丁，再应用；重复运行直接报告已迁移。版本不匹配时不写文件。补丁保留命名空间、程序集和已有类型入口，将对话实现替换为共享 Runtime 的薄适配；原 partial 的 meta GUID 已随实现移至包内，所以导入旧示例后应先迁移，再让 Unity 刷新。

受影响的流式测试明确关闭退出意图判断，以便替身的首个 delta 属于回复生成。退出意图仍由专门测试覆盖。可用以下 runner 在隔离工程验证：

```sh
python3 Packages/com.eitan.mnn/Tools~/Testing/run_conversation_tests.py \
  --graphics --include-package-tests --without-easymic \
  --filter 'MNN.Unity.Tests.ConversationReuseTests;MNN.Samples.SiriAI.Tests.SiriAIInputTests;MNN.Samples.SiriAI.Tests.SiriAIChatTests;MNN.Samples.SiriAI.Tests.SiriAIStreamingTests'
```

只有包代码时加 `--package-only`。产物全部位于根 `TestArtifacts~/`。真实声学实验需要另备权重、语料和硬件证据；本迁移不下载这些资产。

## 2026-11-05：声学与资源复用迁移

已经使用共享会话引擎、且与本次SiriAI基线匹配的导入版本，运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage reuse
```

`SiriAIReusableCapabilities.patch` 保存本次9项声学API的兼容壳、共享图片cache/lease接入、
模型目录失败完成/重试与Hybrid释放失败重试，以及宿主行为回归。它不包含模型、凭据、
其他视觉改动或项目设置。现有 `.meta` GUID 保留，新增测试分配独立GUID。

两个stage分别有固定基线。`conversation` 是历史会话提取补丁，`reuse` 是后续能力迁移，
不要对已演进的导入版本反复执行历史stage。每次都先完整check，再写入；版本不同会拒绝，
不猜测部分迁移是否完成。对于精确匹配的版本，重复执行同一stage为无写入操作。
已验证本次reuse补丁的逐文件结果、重复执行、版本不符时零写入与原GUID稳定。

包级API和工程边界见[声学用法](../../Documentation~/AcousticPipeline.md)及
[复用审查](../../Documentation~/SiriAIReuseReview.md)。专项回归示例：

```sh
python3 Packages/com.eitan.mnn/Tools~/Testing/run_conversation_tests.py \
  --label reuse --graphics --include-package-tests \
  --filter 'MNN.Unity.Tests.AcousticCoreTests;MNN.Unity.Tests.AcousticUtilityTests;MNN.Unity.Tests.AdaptiveEchoFilterTests;MNN.Unity.Tests.EchoDelayEstimatorTests;MNN.Unity.Tests.ConversationBoundaryTests;MNN.Unity.Tests.MarkdownImageCacheTests;MNN.Samples.SiriAI.Tests.SiriAIOnlineModelCacheTests;MNN.Samples.SiriAI.Tests.SiriAIHybridLifetimeTests;MNN.Samples.SiriAI.Tests.SiriAIMarkdownImageTests'
```

缺少本地Siri导入时使用 `--package-only` 和包fixture筛选。Unity运行时不提供有效分配计数器时，
分配断言会跳过；组合hop计时仍执行并明确输出 `allocation_bytes=unavailable`。

## 完整能力迁移

已经应用 `reuse` 的本轮基线（`9b5ec11` 对应本地导入源码）运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage full
```

`SiriAIFullCapabilities.patch` 包含模型资源/安装、完整provider与Agent、会话输入/命令、
声学与设备控制、历史/Markdown及通用布局/平台能力的Siri薄适配与行为回归。
不包含模型、凭据、产品视觉资产、并行视觉测试修改或项目设置。
`SiriAIFullCapabilities.files.json` 对每个受影响文件记录迁移前后SHA-256；脚本在任何
写入前检查所有受影响文件的完整bytes。部分迁移或独立编辑会拒绝，不能靠相邻hunk
匹配猜测兼容。完全匹配迁移后版本时重复运行零写入。原有meta内容保持不变。

三个stage是顺序演进且各有固定基线；不能在已演进版本上强行回放旧stage。
工作区的本地Siri已经迁移，不需要再执行。没有本地Siri导入时，新应用可直接使用包，
无需先导入Siri或EasyMic。实用接入与所有权见[AI应用架构](../../Documentation~/ConversationApplicationArchitecture.md)。
完整回归可使用 `run_conversation_tests.py --sample-source` 指定仓库 `TestArtifacts~` 中的
完整Siri资产快照；`--package-only` 单独验证共享实现，不隐式依赖本地被忽略的示例。

## 当前验收契约迁移

对已经完成上述共享能力迁移、且验收文件精确匹配本轮基线的 Siri 导入，运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage acceptance
```

`SiriAIAcceptanceContracts.patch` 更新三处过期契约：场景通过稳定 audio router 选择 EasyMic、
当前级联路线使用 `CascadedSpeech`；无 ASR 的 Omni + 外部 TTS 测试关闭需要 ASR 的可选退出意图/过滤；
原 `OmniOwnSpeechUsesSameOutputPipeline` 使用显式具备完整波形能力的注入 producer 验证共享 ledger/render/presentation；
本地 Omni 自有波形仍由 `OmniOwnSpeechRejectsUnavailableNativeWaveform` 按当前真实能力明确拒绝。
测试使用独立模型设置 profile，在 sceneLoaded、Start 之前选择设备实际公布的 EasyMic 输入/通道/输出，
隔离用户持久化声学偏好，并在失败后完成其余安全清理再报告原始异常。
模型输出、记忆和渲染/呈现断言继续保留；真实模型/设备结果由实际 PlayMode 执行验证。

该 patch 同时修复 Siri 的停止资源边界：当 session 已完成资源释放但保留运行故障时，仍释放辅助模型和声学 owner，
并向调用者保留该故障；若 worker 未完成 quiescence，则继续保留它可能使用的资源。
新增两项注入故障测试覆盖这两种情况及失败后的成功重试。
托管准备/重试 fixture 显式配置不依赖原生模型的声学设置，避免继承用户保存的唤醒偏好；
声学资源所有权测试仍单独启用 frontend。此隔离不修改真实 KWS 资产的 SHA-256 校验。

该 stage 同时校验所有修改源码完整字节和原 meta GUID，并校验新增测试原本不存在，版本不同、部分迁移或独立编辑均零写入拒绝；
重复运行零写入。它不把当前不受支持的原生 waveform 能力标成支持。
对应包修复把 Omni 模型准备要求限制为音频输入，外部 TTS 拥有语音输出；显式模型语音请求仍需通过原生 ABI/能力检查。

## Recording capture gate

For the current SiriAI import, apply the recording fix with:

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage recording
```

`SiriAIRecordingGate.patch` removes the Unity and EasyMic frame loops' repeated
capture-enable requests. The shared device session owns capture while draining
the delayed echo tail; re-enabling it during that pause added a discontinuity
inside an otherwise complete recording. Explicit input commands still enable
capture, and `Tick` supervises the session without replacing its request.

The patch includes a regression through the Unity endpoint, echo worker, voice
processor, and recording state, plus an opt-in physical microphone check using
an existing DTLN installation. No models are downloaded. File hashes are checked
before any write; a repeated migration makes no changes, and a partial or edited
baseline is rejected. Existing asset GUIDs are preserved.

## 语音时的玻璃与光片连续性

对本轮视觉基线完全匹配的本地 SiriAI 导入，可运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage glass
```

`SiriAIGlassContinuity.patch` 保存共享球壳、固定散射介质、场景线性光谱归一化，以及
更通透的活动光片、折边和内缘光量修正。它保留纯实时 Shader、原待机端点及独立
Talking 主光核，不打包参考视频、参考纹理或模型。附带桌面/移动、明暗背景的球壳
一致性回归和实际相机曝光下的折边回归。对照时区分运动积分时钟与颜色的实际时钟。

SHA-256 清单检查全部修改文件和原 meta GUID；基线不同或部分修改时零写入拒绝，
重复应用零写入。本地工作区已应用。相关 Editor GPU 回归 206/206 通过，样例结构
校验通过；这些结果不代表手机设备实测或与官方视频逐像素一致。连续语音、状态往返、
参考对照、残差与动作记录位于本地 `Assets/Samples/SiriAI/TestArtifacts~/GlassContinuity20261106/`。

## 保留玻璃样式的内部光源反馈

对上述玻璃连续性修订后的精确本地基线，可运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage reflection
```

`SiriAISourceReflections.patch` 保留已认可的三层球壁光影剖面、柔和度、颜色与亮度，
仅将独立丝带时钟替换为实际丝带高度/倾斜、Thinking 六球分布或 Talking 光核位置。
不增加亮环、不提升全局反光增益，不修改内部光片材质或独立 Talking 主光核。
采用解析光源反馈近似，保留校准的径向外观；不是完整多次反射光线追踪。
运行时保持纯 Shader。
光核横向/深度移动时，以有限光源照度相对居中位置的比值重新分配同一光影的光量；
居中时比值为 1，过渡沿用现有聚合权重。

相关 Editor GPU、声音与状态回归 218/218 通过，包含内部物体静止时拒绝无关时钟
驱动、内部物体移动时同一柔和光影必须响应，以及横向/深度移动时光量方向正确的
行为检查。首秒待机与本轮基线平均 RGB 差约 `2.26e-7`，隔离 Talking 主光核
10/10 组完全一致。最终连续输入、状态
往返、官方参考对照与限制记录在本地 `Assets/Samples/SiriAI/TestArtifacts~/SourceReflections20261106/`。
局部色散、多层光片和光晕仍有参考残差；这些检查不代表手机设备实测或与官方逐像素一致。

SHA-256 清单检查源码和原 meta GUID。精确应用字节一致，重复零写入；基线不符或
部分应用时零写入拒绝。本地工作区已应用，无需再次运行。该补丁只保存此次六文件
改动，早前基线不包含在内。

## 活动光片的色散与柔光肩部

对上述光源反馈修订后的精确本地基线，可运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage optical
```

`SiriAIOpticalDetail.patch` 保存本轮共享光片/profile、实际曝光回归与说明。
活动积分层共用局部 RGB 光路，色散由现有截面和解析导数驱动；各波长共用两端，
端点色散收为零。折边光量保持归一化，细化薄层、半透明暗面与蓝金柔光肩部。
球壳与固定介质实现不变，独立 Talking 主光核保持原样；运行时为纯 Shader。

相关 Editor GPU、声音与状态回归 220/220 通过。104 帧固定时间/投影的对照中，
局部色散 90% 分位指标误差约下降 48%，外侧柔光肩部 RGB 误差约下降 11%。
更窄中央窗口 RGB 误差略增，主亮折边走向仍有差异；不宣称所有区域改善或逐像素
等同官方。20 组隔离球壳和 10 组隔离 Talking 主光核完全一致，待机基本不变。
完整比较口径、候选、连续 PCM 输入、状态往返和动作记录位于本地
`Assets/Samples/SiriAI/TestArtifacts~/OpticalDetail20261106/`。验证限于 Editor Metal，
移动 Shader 覆盖不等于手机设备实测。

SHA-256 清单检查全部修改文件及原 meta GUID；完整应用字节一致，重复零写入，
基线不同或部分应用时零写入拒绝。本地工作区已应用。该补丁只保存此次四文件差异，
早前视觉基线不包含在内。

## 折边光量跟随光片形态

对 optical 修订后的精确本地基线，可运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage caustics
```

`SiriAIFoldCaustics.patch` 保存方向性光量、实际曝光回归与说明。移除独立的快速左右
光量摆动，使亮折边与局部色散共用同一光片截面和解析切线。保持归一化光量，保留
现有球壳、介质、光片轮廓/薄层/色温，以及 Thinking 和独立 Talking 实现。

相关 Editor GPU、语音和状态回归 222/222 通过。104 帧固定时间/投影中，六列亮折边
高度误差约下降 61%，柔光肩部 RGB 误差约下降 12%，白色亮区面积误差也降低；
色散分位误差略增。31 帧待机、20 组隔离球壳、
10 组隔离 Talking 主光核均与本轮基线完全相同。仍存在局部遮叠、焦散和色散残差，
不宣称与官方逐像素一致。连续语音、状态往返和动作记录位于本地
`Assets/Samples/SiriAI/TestArtifacts~/FoldCaustics20261107/`，验证环境为 Editor Metal。

SHA-256 清单检查修改文件和原 meta GUID；完整应用字节一致，重复零写入，基线
不同或部分应用时零写入拒绝。本地工作区已应用。补丁仅保存本轮三文件差异。

## 丝带左右端点的柔和玻璃接触

对 caustics 修订后的精确本地基线，可运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage soft-ends
```

`SiriAIRibbonSoftEdges.patch` 保存共享活动光片端点扩散、实际曝光/接触回归及说明。
撤销端部锐利白线的直射增强，使同一光片的横截面靠近球壁时逐渐展宽、归一化，
直射光平滑淡入原有散射场。中央折边、轮廓、色温、球壳、Thinking 和独立 Talking
实现不变，运行时仍为纯 Shader。内缘接触按有/无光源的可见增量和有限柔光覆盖
验证，替换与官方柔和端点相冲突的旧白峰阈值；另设绝对/相对梯度回归。

相关 Editor GPU、语音及状态回归 224/224 通过。104 帧固定时间/投影下，端部
最大梯度误差约下降 76%，峰值误差约下降 63%；端部完整光晕宽度误差仍增加，
不能称为全方面改善或逐像素一致。31 帧待机、20 组隔离球壳、10 组隔离 Talking
主光核均与本轮基线完全相同。连续语音、状态往返、全部口径和日志位于本地
`Assets/Samples/SiriAI/TestArtifacts~/RibbonSoftEdges20261107/`，验证环境为 Editor Metal。

SHA-256 清单检查本轮三个修改文件与原 meta GUID；完整应用字节一致，重复零写入，
基线不同或部分应用时零写入拒绝。本地工作区已应用，无需再次运行。

## 球壁折射与粗糙透射

对 soft-ends 修订后的精确本地基线，可运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage refraction
```

`SiriAIGlassRefraction.patch` 保存共享玻璃光学、桌面/移动调用、实际背景回归及说明。
径向折射随折射率/壁厚连续变化，空气或零壁厚时不留固定压缩；高画质透射的模糊
覆盖随投影球径和掠射深度变化。九点归一化采样保留平均光量，Balanced/Performance
保留原先单采样路径。按官方首秒轻调已有内外壁光带的径向位置/宽度，保持原光量、
色温与实时光源反馈；丝带形态、端点柔化和独立 Talking 实现不变。

相关 Editor GPU、语音、状态回归 232/232 通过。31 帧固定时间/投影下球壁 RGB
误差约下降 3%，同口径整球误差约下降 5%；仍非逐像素一致。官方暗背景不能唯一
确定背景位移或 IOR，棋盘背景验证光学边界/连续性和粗糙透射，不能当作官方对应。
高画质背景采样成本增加；验证限于 Editor Metal，不代表手机设备帧率。完整口径、
连续画面、原始数据及日志位于本地 `Assets/Samples/SiriAI/TestArtifacts~/GlassRefraction20261107/`。

SHA-256 清单检查本轮五个修改文件与原 meta GUID；完整应用字节一致，重复零写入，
基线不同或部分应用时零写入拒绝。本地工作区已应用。

## MNN-Pocket 默认模型

对本轮 SiriAI 基线，运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage pocket
```

`SiriAIPocketModels.patch` 将新配置及场景的本地语言模型设为 `MNN/MNN-Pocket-0.3B`，
由共享包目录解析到固定版本的官方 `MNN/MNN-Pocket-0.3B-MNN` 部署资源。
首次启动保留该默认项，线程预算仍按设备计算，保存设置和显式模型选择继续有效。
源权重、GGUF、Dec 保留在模型列表中并标明用途；设置归一化和验证拒绝尚不能运行的条目。

补丁包含相关默认值、设置和回归测试修改，保留已有 `.meta` GUID；逐文件 SHA-256
检查拒绝不匹配或部分迁移，重复应用零写入。权重、凭据及其他场景配置不加入包。
模型格式和验收范围见 [MNN-Pocket 接入](../../Documentation~/MNNPocket.md)。

## 产品交互、玻璃可读性与任务设置

对本轮精确匹配的 SiriAI 导入基线，可运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage product
```

`SiriAIProductPolish.patch` 保存透明桌面/常规布局共用的可读玻璃、窗口收纳后的
全局录音/取消/转写、连续文字输入、设置分类、错误/加载详情及弹窗过渡。
任务设置复用包中的自动环境检测和后端目录，默认选择完整的本机CLI＋适配器；
远端发现后通过选择器指定后端和工作区。详情与接入边界见
[Harness后端接入](../../Documentation~/HarnessBackendSetup.md)。

本机导入已应用，不必重放。迁移器逐文件检查完整SHA-256，版本不符和部分迁移
拒绝且不写入，重复应用零写入；原有meta GUID保持不变。补丁仅包含当前任务
涉及的示例源码、Shader、测试及meta，不包含模型、凭据、用户配置或临时验收输出。
验证覆盖Editor行为/GPU、真实macOS服务生命周期和独立协议替身；不代表全部
第三方账号/模型、其他桌面系统或物理移动设备已经完成发布验收。

## Tasks设置稳定性与自定义宿主配置

在已经完成 `product`、且匹配本轮基线的本地导入上，运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage tasks
```

`SiriAITaskSettings.patch` 保留正在编辑的连接字段和打开的选择器，后台只局部更新
任务内容；无变化的任务快照、本机检测和技能历史不再发出多余更新。
真正切换宿主/账户时仍清除旧任务草稿和过期操作。
远程列表排除当前及旧版自动本地配置，用户自行保存的localhost配置仍可选择。
用户可命名、新增、编辑和删除宿主配置；删除有界面确认，清除连接与对应凭据，
已接收的宿主任务继续运行。修改名称保留连接，修改后端或工作区保留凭据供重新连接。

该stage使用独立的前后SHA-256，不改写历史 `product` 补丁；重复应用零写入，
不匹配或部分应用拒绝且零写入，原meta GUID保持不变。本机源码已应用。

远程连接一步自动发现配置，名称可留空采用主机名；保留有效的后端/工作区选择。
本地优先恢复仍有效的已保存项目，再使用会话目录和应用项目目录。

## Desktop comfort and independent background work

After the `tasks` stage, apply this stage only to its exact imported SiriAI baseline:

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage comfort
```

`SiriAIDesktopComfort.patch` preserves larger move/resize targets, completed header
double-clicks, shared window background depth, settings frosting, background action
routing and nonblocking completion notifications. The matching package directly
provides the five-second admission deadline and identity validation in `HarnessTaskTool`.
The patch contains imported source, shaders, documentation and tests; model assets,
credentials, user settings and diagnostic output remain local.

Complete before/after SHA-256 checks preserve existing meta GUIDs, reject mismatched
or partial imports without writing, and make repeat application a zero-write operation.
This local import is already updated. All 211 selected Editor behavior/GPU checks and
nine independent HTTP/ACP fixture checks pass. Pristine and rolled-back source both
reproduce the same twelve failing behaviors; the seventeen new cases all pass after
the change. This validates Editor behavior and rendering, without measuring desktop
Player feel, production model decisions or performance on every operating system.


## MiniMind-o 语音案例

对于精确匹配当前基线的 SiriAI 导入：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage minimind-o
```

`SiriAIMiniMindOVoice.patch` 接通 MiniMind-o 的独立适配器筛选、自动语音路由、专用
`minimind-o` 设置 profile 和默认场景。选择 **Use MiniMind-o voice** 后直接输入音频、
使用模型自身的语音回答，同时保留 Silero VAD 和 SmartTurn，不依赖单独的 ASR/TTS。
模型通过共享目录发现并从已固定 revision 的 ModelScope 安装；补丁不包含模型权重。
修改前后文件哈希、原 GUID、新增文件和重复执行均使用现有迁移器检查。

真实权重验证覆盖通过采集接口发送的生成语音 PCM、VAD/轮次模型准备、输出渲染和
第二轮语音。物理麦克风/扬声器和声学环境需要另测；该模型仍是轮次式推理。
加速对比和运行限制见 [MiniMind-o](../../Documentation~/MiniMindO.md)。


## Omni End-to-End 轮次检测修复

在已应用 `minimind-o`、精确匹配对应基线的 SiriAI 导入上执行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage omni-turns
```

`SiriAIOmniTurns.patch` 将本地对话模式显示为 **Omni End-to-End**，保留序列化枚举值。
启动恢复、应用配置和运行时快照共用同一约束：本地 Omni 语音模式启用 VAD 和
Turn Detection 的 Provider；旧 profile 中错误的 false 标志会修复并持久化。
简单设置也显示这两个必需阶段及模型选择入口；级联模式仍保留用户的可选检测偏好。

该 stage 不修改历史迁移补丁、权重或账户凭据。重复执行零写入；版本不匹配或
部分应用在写入前拒绝；现有 Unity GUID 保持不变。本地导入已应用。
测试包括旧设置恢复、持久化幂等性、模式切换、简易设置，以及真实权重下两轮
带噪声尾部的生成语音采集与模型语音输出。测试为软件 PCM，未验证物理声学回声。

Omni 表示模型直接接受音频并生成语音，不等于模型原生 Full-duplex。MiniMind-o
发布权重仍按完整轮次推理；此补丁不会把它升级为持续输入输出的全双工模型。
详见 [MiniMind-o](../../Documentation~/MiniMindO.md)。

## 2026-11-16：初始化引导体验

本地 SiriAI 已改为复用共享安装清单判断所选模型文件是否完整：权限通过且模型完整时，
跳过下载页直接进入主界面初始化；仍缺少模型时提供明确下载、后台继续和重试操作。
文件检查不代表模型已通过原生加载验证；完整性校验和加载仍由正常启动流程执行。
已运行的会话跳过准备页。必要权限仍须先授予。

引导加入四阶段矢量图标、模式选择卡片、阶段淡入、初始化呼吸/旋转反馈、真实下载进度，
以及深浅主题、中文和减少动态效果支持。新的图标与动画均使用现有 Unity UI，无额外依赖。

匹配 `SiriAIOnboardingExperience.files.json` 基线的导入版本可运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage onboarding
```

补丁保留现有 GUID；脚本逐文件验证修改前后 SHA-256，重复运行不写入，版本不符拒绝修改。
验证包括 Unity 引导行为与 GPU 界面检查、同输入修改前/修改后/回滚对照和补丁重建。
测试不下载模型；安装检查使用固定文件夹具，运行时交接使用托管云端/音频替身，
不作为原生推理或设备授权的验证。

引导对齐补充：统一标题图标、阶段栏、内容卡片和底部按钮的 24 单位外边距；
阶段文字与图标垂直居中，窄屏阶段图标在等宽单元中居中；模式标题和说明使用
不重叠的文本区域，选中标记与模式图标共享中线。对齐回归覆盖 320、480、520、568、
800 五种宽度，并检查深浅主题的四阶段实际渲染；同输入回滚恢复修改前布局。


## 2026-11-17：视觉独立开关

本地 SiriAI 的 **设置 → 视觉 → 启用视觉** 使用现有 `visualPresence` 序列化字段，
保存后控制摄像头与视觉模型。关闭时同时关闭身份门控、图像上下文和视听拾音，
避免残留依赖使设置校验失败；这三个子开关在视觉关闭时不可操作。普通语音、唤醒、
参考说话人提取与重叠语音分离保持各自配置。摄像头、模型路径与身份名单保留。
视觉开启不依赖声学处理总开关，重新开启视觉不会自动恢复视觉子开关。

对精确匹配 `SiriAIVisionSwitch.files.json` 基线的导入版本执行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage vision-switch
```

本地示例已应用。该 stage 独立保存本轮七文件差异，不修改历史迁移、模型、原生库或 GUID。
迁移前后 SHA-256 检查确保重复应用零写入，版本不符时拒绝修改。
54 项 Editor 行为回归通过，覆盖视觉开关、纯音频独立性、保存恢复、视觉源释放、模型选择、
中文文案及设置事务。原始与回滚源码在同输入下复现关闭后身份门控残留的问题，修改后通过。
测试使用托管夹具，未验证物理摄像头采集或原生模型推理。

## 摄像头与语音独立采集

`voice-input` 将摄像头预览与人脸语音门控分开：启用视觉默认保留麦克风和持续语音；
可单独开启“仅在人脸可见时接受语音”。身份验证和视听目标拾音继续要求目标在场。
关闭视觉时隐藏预览和相关设置，保存后释放摄像头。

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage voice-input
```

补丁只适用于 sidecar 中的精确导入基线；完整版本检查后再写入，重复执行不写入。
C930c 摄像头与自带麦克风已验证两种启动顺序，测试只记录帧数与采样数。

## 本地语音模型音色选择

匹配 `SiriAISpeechVoices.files.json` 基线的导入可运行 `--stage speech-voices`。
Runtime Settings 和 Configurable Backend Inspector 使用包级 `GetVoiceOptions`，
从当前模型导出读取音色 ID，改用选择列表；参考录音模型继续配置 WAV 和转写。
未安装或元数据损坏时不猜测音色，旧的无效选项提示并回到模型默认。
详见[语音合成音色选择](../../Documentation~/SpeechVoices.md)。


## 实时存在感知与手动语音

`realtime-presence` 在 `voice-input` 后应用。人脸存在感知只控制实时聆听；
空格和长按录音、转写、提交不等待目标入镜。目标离开只暂停实时采集，保留用户的模式选择。
摄像头先读取首帧再检查尺寸；重新打开前等待原设备释放，自动画质超时后在同一设备重试 640×480、15 FPS。

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage realtime-presence
```

补丁保留全部 `.meta` GUID，校验精确基线，重复执行不写入。
C930c 与 EasyMic、已有真实人脸检测模型已完成三次启动和重启，持续收到视频帧与麦克风 PCM。
首帧协商问题有确定性回归覆盖；用户现场的间歇性超时未在硬件测试中重现。

## Camera startup fallback

For the matching SiriAI import, run:

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage camera-startup
```

When the initial camera mode times out, automatic quality now reopens the same
selected device without imposing dimensions or a frame rate. The previous retry
forced 640×480 at 15 FPS and could remain playing with Unity's provisional 16×16
buffer. Startup still requires complete readable pixels, permits one retry, and
releases the previous capture owner before reopening. Manual quality is unchanged.

The opt-in `SiriAIVisualCameraTests.C930cVisualCameraStartsAndReopensDuringMicrophoneCapture`
checks readable frames, simultaneous microphone capture, release/reopen, and
recovery after a forced first-attempt stall on the connected C930c. It stores
counters only. Managed fault injection separately checks a device rejecting both
imposed modes. Current hardware checks do not reproduce the original intermittent
stall; they verify startup, fallback and continued capture. No models are downloaded.

The migration checks complete file hashes, rejects partial/edited imports before
writing, and makes no changes when repeated. Existing asset GUIDs are preserved.

## Camera startup and shutdown ordering

After `camera-startup`, matching imports can run:

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage camera-lifecycle
```

SiriAI now waits for audio to finish opening before starting visual presence, and
finishes visual preparation before loading the conversation models. Disabling or
destroying the chat releases its camera immediately, even while audio shutdown is
pending. This removes overlapping device startup and delayed camera ownership.

Two controlled regressions fail on the previous source and restored source, and
pass with this change; the surrounding conversation lifecycle checks also pass.
Actual-scene verification now uses explicit Editor Play transitions: the former
opt-in NUnit coroutine replayed startup during domain reload and has been removed.
The macOS camera authorization section below records the full-scene hardware
result and the independently launched Editor used for it.

The migration checks complete hashes, rejects partial or edited imports before
writing, and preserves existing asset GUIDs. Repeating it makes no changes.

## macOS camera authorization and Unity Hub

For an import matching `SiriAICameraAuthorization.files.json` after `camera-lifecycle`:

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage camera-authorization
```

A macOS launch through Unity Hub can leave `WebCamTexture` playing at 16×16
without delivering pixels. In the reproduced failure, TCC attributed capture to
`com.unity3d.unityhub` and refused camera prompting because the hardened Hub
process lacked `com.apple.security.device.camera`. Unity 2021's legacy
`HasUserAuthorization(WebCam)` still returned true. Changing FPS or startup
ordering cannot authorize that launch.

Quit the existing project Editor, then double-click the imported sample's
`Tools~/Testing/open_editor_macos.command`. It resolves the project's installed
Editor and opens it through LaunchServices with the project path; an existing
project lock makes it stop before launching. For a custom Editor installation,
set `UNITY_EDITOR_APP` to its `.app` path. macOS can then use the Editor's own
camera identity. Allow camera access if prompted. The launcher does not alter
Unity Hub, signatures, entitlements, or privacy databases.

The sample now reads real AVFoundation camera authorization through managed
Objective-C interop on macOS, shared by capture and the camera settings status.
If a stream receives no frames while authorization remains ungranted, it reports
an actionable permission failure and skips the ineffective mode retry.
Authorized cameras retain readable-pixel checks and bounded mode fallback.
No C++ code, native bridge, custom exports, or MNN changes were added.

Validation on the actual SiriAI scene with its saved EasyMic, camera and model
settings completed two Play cycles in the same independent Editor: each stream
was 1280×720 with 921600 complete pixels; two-second frame counts were 47 and 48;
both stops released the camera. The prior Hub-attributed launch failed with the
reported FPS=0 timeout and explicit TCC entitlement errors. Managed fault injection
also verifies the authorization error and authorized fallback. These results
supersede the earlier locked-machine hardware limitation above. No recordings or
model downloads are part of the check. Existing native model loading is not a
validation of direct official MNN C++ calls.

The migration checks exact file hashes, rejects partial or edited imports before
writing, preserves GUIDs, and makes no changes when repeated.


## 无人在场时的休眠与摄像头授权

`presence-sleep` 为光球增加去饱和、降低亮度和约六秒一次的呼吸动画；暂停无人时的自动音频与 ASR，目标界面降至 12 FPS。摄像头保持连接并以低频分析等待返回；空闲后每次休眠只清理一次无引用资源和 GC，手动录音与返回可恢复。默认视觉关闭、图像上下文偏好启用，原有用户选择保留。macOS 引导增加可选摄像头授权和官方 AVFoundation 回调，Player 补齐用途说明。

在已完成 `camera-authorization`、精确匹配 `SiriAIPresenceSleep.files.json` 的样例上运行：

```sh
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage presence-sleep
```

升级共享 package 后再应用本阶段；此 stage 只更新样例。保留已有 GUID，完整哈希不匹配时零写入，重复应用零写入。详情见样例 `Documentation~/PresenceSleep.md`。

`--stage sleep-scene` applies actual SiriAI scene defaults (camera off, image context on, Dimmed), camera-off inactivity sleep, and manual wake after `presence-sleep`. Saved camera choices are preserved; select Dimmed standby for existing saved settings. Verified in the actual scene with sleep blend >0.99, 12 FPS, one cleanup, and wake restoration.

`--stage presence-feedback` follows `sleep-scene`: realtime absence immediately fades UI and the complete orb to 25% opacity, including desktop compositor alpha. Actual sleep hides both; returning fades in. Removes the separate Dimmed standby selector and defaults to Hidden. Grace keeps ongoing interaction without delaying visual feedback. Manual mode and settings remain accessible.

`--stage chat-follow` follows `presence-feedback`: non-realtime conversations share the UI/orb presence fade. New and streaming messages follow the bottom; transcript/window gestures interrupt following, history browsing stays put, and a circular liquid-glass down-arrow jumps to the latest message and resumes following.

`--stage presence-buffer` follows `chat-follow`: visual departure must remain stable for 0.65s and arrival for 0.4s after detector confirmation before shared UI/orb fading begins. Opposite observations cancel pending transitions and restart the continuous timer. Actual sleep hides without an additional delay; settings/onboarding stay accessible.
