# MNN for Unity

基于 [MNN](https://github.com/alibaba/MNN) 3.6.1 的 Unity 绑定与编辑器工具，
提供本地张量推理、语言与多模态模型、检索、语音合成和图像生成 API。
Model Manager 用于管理模型下载，Chat Studio 用于在编辑器中运行推理与对话。

[English](https://github.com/EitanWong/com.eitan.mnn#readme) · [文档索引](../README.md) · [更新日志](../../CHANGELOG.md)

## 使用要求

- Unity 2021.3 或更高版本；验证环境为 Unity 2021.3.45f2。
- 已提交的托管推理绑定要求 macOS 11+ 和 64 位 Mono。
- 单独获取的兼容模型，并遵守模型自身的许可。

绑定通过指定的编译器与 ABI 契约调用官方 MNN C++ 符号，由本项目维护。
MNN 不提供可移植的 C# SDK，其他平台的插件文件也不代表托管推理已受支持。
部署前请查阅[ABI 契约](../../Native~/README.md)和[验证范围](../Testing.md)。

## 开发状态

当前开发快照中的 OmniVoice 存在[干净构建阻塞](../development/ContinuousIntegration.md#known-clean-build-blocker)。
静态检查通过不代表包可以编译，当前快照尚不具备发布条件。

## 安装

在 Unity Package Manager 中选择 **Add package from git URL**：

```text
https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#dev
```

`dev` 是开发快照，`main` 是已合并版本；替换为提交 SHA 可以固定版本。
URL 中的包子目录不能省略。

在包的 Samples 标签页导入 **Basic Inference** 并提供兼容模型即可开始。
编辑器工具位于 **Window > MNN > Model Manager** 和 **Chat Studio**。
本仓库不包含模型权重。

## 参与贡献与许可

请先阅读[贡献指南](../development/Contributing.md)。问题与建议请通过
[GitHub Issues](https://github.com/EitanWong/com.eitan.mnn/issues) 提交；
安全问题请遵循[安全策略](../community/Security.md)。

项目采用 [Apache License 2.0](../../LICENSE.md)。模型权重和可选第三方工具保留各自的许可。
