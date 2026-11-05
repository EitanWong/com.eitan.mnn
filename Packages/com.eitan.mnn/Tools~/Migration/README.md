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
