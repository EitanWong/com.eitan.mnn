# MNN 3.6.2 升级验收

本包已更新到官方 MNN tag `3.6.2`，源码提交 `7e3c6fb5225d3ccf22807ee641332669df34650d`。没有修改官方源码，没有新增 C++、桥接库或自定义导出；C# 仍直接调用官方 C++ 符号，并受本项目记录的编译器/`libc++` ABI 契约限制。

## 已完成

- macOS universal（arm64/x86_64）、Android ARM64 和 iOS ARM64 device/simulator 原生库已安装。Windows x64、Linux x64 和 Android ARMv7 也换成官方 3.6.2 release 文件，但因当前没有对应托管 ABI，Unity 导入保持禁用。
- 版本检查、包版本、构建脚本、移动审计报告和构建清单统一为 3.6.2。官方移动库每个审计均包含 72 个必需绑定、无自定义 `MNN_*` 导出；macOS 每个架构审计包含 66 个项目绑定。
- Qwen3 Embedding 的 legacy `attention_mask=float` 图由托管层复制到缓存目录，并只修改公开 Attention `kv_cache` 字段；源模型、外部权重和配置不会被写入。原始模型回归 12/12 通过，Android/iOS 真实模型检索回归通过。
- Qwen3.5 Apple CPU 的默认 Low/4 线程精度会使算术质量回归失败；默认 CPU 配置现在使用 Normal，显式精度仍由调用方控制。该组合及 Normal/High/Metal 对照结果均已记录。

## 验证证据

- macOS ARM64 Player：119/119；x86_64 Player（Rosetta）：119/119。
- Android ARM64 IL2CPP 真机：16/16，包含四个真实语言/多模态模型、Embedding、Reranker 和基础 Interpreter；实际后端包括 CPU/OpenCL，结果含匹配 build ID。
- iOS ARM64 IL2CPP 真机：119/119，包含文本、图片、音频、Embedding、Reranker 和默认精度回归；结果含匹配 build ID。
- 六类代表模型 Editor 回归：34/34。Embedding 专项：12/12。包结构检查和逐程序集编译均通过。
- 上游 3.6.2 Android/Windows/Linux release archive SHA-256 与 GitHub release digest 一致；归档文件和所有测试日志位于 `TestArtifacts~/`。

## 已知边界

- 这是项目维护的编译器专用 ABI，不是 MNN 官方可移植 C# SDK。换用其他编译器、STL、Unity 或 IL2CPP 版本必须重新构建和审计。
- iOS simulator 仅完成构建和符号审计；macOS x86_64 在 Rosetta 下验证，未在实体 Intel 设备验证。
- 未配置模型目录或独立参考数据的数百个 Explicit/专项测试不属于本次通过标准；全量无条件 Editor 扫描会把缺失 fixture 报为失败。分组原因见 `TestArtifacts~/MNN-3.6.2-editor-failures.json`。
- Android 曾出现一次 Unity/Vulkan vendor crash，堆栈位于 Unity/Vulkan 驱动且发生在失焦后；不计作 MNN 3.6.2 推理通过或失败。

详细动作日志见 `TestArtifacts~/MNN-3.6.2-upgrade-log.md`，原始插件备份见 `TestArtifacts~/MNN-3.6.2-original-plugins/`。
