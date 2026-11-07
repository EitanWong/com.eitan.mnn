# MNN for Unity

基于 [MNN](https://github.com/alibaba/MNN) 3.6.2 的 Unity 绑定与编辑器工具，
提供本地张量推理、语言与多模态模型、检索、语音合成和图像生成 API。
Model Manager 用于管理模型下载，Chat Studio 用于在编辑器中运行推理与对话。

[English](https://github.com/EitanWong/com.eitan.mnn#readme) · [文档索引](../README.md) · [更新日志](../../CHANGELOG.md)

## 使用要求

- Unity 2021.3 或更高版本；验证环境为 Unity 2021.3.45f2。
- 桌面 Mono：macOS 11+ ARM64/x64、Windows 10+ x64（UCRT）或 Linux x64
  （GLIBC 2.38+、GLIBCXX_3.4.32），见[桌面 Mono 契约](../DesktopMono.md)。
- Android/iOS ARM64 IL2CPP 使用独立的[移动端契约](../MobileInterop.md)。
- 单独获取的兼容模型，并遵守模型自身的许可。

绑定通过指定的编译器与 ABI 契约调用官方 MNN C++ 符号，由本项目维护。
MNN 不提供可移植的 C# SDK，随包原生库须与匹配的托管声明一起使用。
桌面 IL2CPP、Windows/Linux ARM64 和 WebGL 不受支持。
部署前请查阅[ABI 契约](../../Native~/README.md)和[验证范围](../Testing.md)。

## 开发状态

桌面 Mono 已实现核心推理、Express、LLM 文本/聊天/流式生成、原生向量与重排、Omni 媒体输入。
Windows Wine Mono 与 Linux 容器中的 host Mono 各通过 79 项任务断言和 22 项核心断言，
包含真实 CPU 模型推理；尚未验证 Windows/Linux Unity Player，也不代表所有模型均已通过。
这两个平台的随包原生库仅启用 CPU。所有平台均未实现原生波形回调；独立 TTS 管线按各自模型说明使用。

macOS ARM64 与 Rosetta x64 Unity Mono Player 各通过 120/120 项核心/平台/Express 检查，
聚焦 Editor 检查 14/14、条件编译 13 组及基础包程序集编译 9 组通过；可选 Burst 未安装。
此前 OmniVoice 缺失类型的干净构建问题已解决，见[CI 记录](../development/ContinuousIntegration.md#isolated-validation-evidence)。
模型、硬件与质量验收仍分别记录在[测试说明](../Testing.md)，静态检查不代替推理验证。

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
