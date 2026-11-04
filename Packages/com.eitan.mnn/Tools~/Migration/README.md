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
