# MNN for Unity

基于 [MNN](https://github.com/alibaba/MNN) 3.6.1 的 Unity 绑定和编辑器工具，
提供张量推理、语言与多模态 API、模型管理和 Chat Studio。需要 Unity 2021.3 或更高版本。

[English](../README.md) | [文档](../Packages/com.eitan.mnn/Documentation~/README.md) | [更新日志](../CHANGELOG.md)

## 安装

在 Unity Package Manager 中选择 **Add package from git URL**，输入：

```text
https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#dev
```

将 `dev` 替换为提交 SHA 可以固定版本。从包的 Samples 标签页导入 **Basic Inference**，
并提供兼容模型即可开始使用。编辑器工具位于 **Window > MNN > Model Manager** 和
**Chat Studio**。本仓库不包含模型权重。

## 平台支持

托管绑定通过各平台指定的 ABI 契约调用官方 MNN C++ 符号，由本项目维护，
并非 MNN 官方提供的可移植 C# API。平台与任务的支持和验证范围各不相同；
部署前请查阅[原生 ABI 说明](../Packages/com.eitan.mnn/Native~/README.md)和
[测试记录](../Packages/com.eitan.mnn/Documentation~/Testing.md)。

## 文档与许可

[文档索引](../Packages/com.eitan.mnn/Documentation~/README.md)包含 API、模型、加速、
测试和项目结构说明。项目采用 [Apache License 2.0](../LICENSE)。
