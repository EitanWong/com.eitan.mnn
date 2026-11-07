# SiriAI 共享核心迁移

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
