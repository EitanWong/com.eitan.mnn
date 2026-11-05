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
