# 项目结构与维护规范

`Packages/com.eitan.mnn` 是可分发的 UPM package。仓库根目录的 `Assets`、
`ProjectSettings` 和 `Packages/manifest.json` 属于开发用 Unity 工程，不能复制到包中。
生成的工程文件、构建、日志、模型与临时诊断不能进入 package 源码目录。
测试产物统一放在仓库根目录的 `TestArtifacts~/`。

## 目录职责

```text
Packages/com.eitan.mnn/
├── Runtime/
│   ├── Core/
│   │   ├── Common/                枚举、异常、版本与平台支持
│   │   ├── Inference/             Interpreter、Session、Tensor 与扩展
│   │   ├── Models/                模型配置、元数据、加载选项与路径
│   │   ├── Language/              LLM、对话、多模态、流式输出与结果类型
│   │   ├── Retrieval/             Embedding 与 Reranker
│   │   ├── Generation/
│   │   │   ├── Common/            图执行、常量特化与共用数值算法
│   │   │   ├── Audio/             TTS、文本/音素处理与音频结果
│   │   │   └── Image/             图像生成、分词、调度与图像结果
│   │   └── Performance/           可选 Burst/Collections 实现
│   ├── Components/                MonoBehaviour 及 Unity 生命周期适配
│   ├── Interop/                   按职责拆分的 MNNInterop.*.cs
│   │   └── Handles/               SafeHandle 与原生资源所有权
│   └── Plugins/                   按平台/架构存放的原生二进制与导入设置
├── Editor/
│   ├── Localization/             编辑器本地化
│   ├── ModelManagement/          模型目录、下载服务与管理窗口
│   ├── Studio/
│   │   ├── Core/                 Conversation、History、Media、Models、Search
│   │   ├── UI/                   Studio 控件与展示
│   │   └── Window/               窗口与按交互职责拆分的 partial 文件
│   └── UI/Common/                跨编辑器功能复用的控件
├── Tests/
│   ├── Editor/                   Editor 测试程序集
│   │   ├── Core/                 基础类型、异常与配置
│   │   ├── Generation/           无需真实生成权重的算法/数据处理检查
│   │   ├── Language/             LLM 参数校验
│   │   ├── Models/               模型路径、仓库与下载服务
│   │   ├── Platform/             ABI、原生库打包与版本检查
│   │   ├── Studio/               编辑器状态、路由、窗口与替身 backend 测试
│   │   ├── Integration/          真实原生调用或本地模型测试，按功能细分
│   │   └── Fixtures/             此程序集共享的测试辅助代码
│   ├── Runtime/                  PlayMode/Player 测试程序集
│   │   ├── Inference/            实际推理测试
│   │   ├── Platform/             平台/后端支持与拒绝策略
│   │   └── Fixtures/             可在 Player 使用的内嵌测试数据
│   └── Fixtures/                 提交到版本库的小型确定性测试资源
├── Tools~/
│   ├── Testing/                  本地 fixture、模型与平台测试 runner
│   └── Validation/               无 Unity/网络依赖的包结构检查
├── Native~/                      官方库构建工具与本项目限定构建的 ABI 约定
├── Samples~/                     可导入的示例，各自具备说明和程序集边界
└── Documentation~/               面向使用者与维护者的文档
```

`~` 后缀用于 Unity 忽略的工具、文档和未导入示例。`Tools~/Testing` 中的
`PlatformValidationSetup.cs` 仅由 runner 复制到隔离工程的 Editor 目录，不能放进 Runtime。
原生 `.framework`、`.xcframework` 与 `.bundle` 按完整插件资产处理，不能为内部文件补 Unity meta。

## 程序集与依赖方向

| 程序集 | 定义位置 | 可依赖的包程序集 | 编译范围 |
| --- | --- | --- | --- |
| `MNN.Unity` | `Runtime/MNN.Unity.asmdef` | 无 | Editor 与 Player |
| `MNN.Unity.Editor` | `Editor/MNN.Unity.Editor.asmdef` | `MNN.Unity` | Editor |
| `MNN.Unity.Tests` | `Tests/Editor/MNN.Unity.Tests.asmdef` | Runtime、Editor | Editor、`UNITY_INCLUDE_TESTS` |
| `MNN.Unity.Runtime.Tests` | `Tests/Runtime/MNN.Unity.Runtime.Tests.asmdef` | Runtime | PlayMode/Player、`UNITY_INCLUDE_TESTS` |
| `MNN.Unity.Samples.BasicExample` | `Samples~/BasicExample/MNN.Unity.Samples.BasicExample.asmdef` | Runtime | 导入 sample 后的 Editor 与 Player |

Runtime 不依赖 Editor 或测试程序集。测试辅助代码分别归属对应程序集；
跨 Editor/Player 的静态资源放 `Tests/Fixtures`，不使用源码目录隐式共享测试代码。
移动程序集定义时保留名称及 GUID，避免破坏 `InternalsVisibleTo`、测试 runner 与下游引用。

## 文件与代码规则

- 先按层级划分，再按职责归类；不要重新建立无明确职责的 `Utils`、`Misc` 或 `Temp` 目录。
- 托管 API 放 `Runtime/Core` 的对应功能目录，Unity 组件放 `Runtime/Components`。
  目录细分不改变现有公开 `MNN.Unity` 命名空间。性能任务保留 `MNN.Unity.Performance`。
- 一个主要类型一个同名文件。仅紧密关联的小枚举/配置数据可随所属类型放在同一文件，
  例如 `MNNChatRole` 与 `MNNChatMessage`。实现辅助类型与结果类型独立存放，不能隐藏在无关模型文件中。
- 同一类型的职责拆分使用 `类型名.职责.cs`，例如 `MNNLlm.Streaming.cs`；partial 文件与主文件同目录。
- 原生声明统一为 `MNNInterop.*.cs` partial 文件，库名/调用约定共用；
  所有句柄类型使用 `MNN.Unity.Interop.Handles` 命名空间并放在 `Interop/Handles`。
  不新增 C++、桥接库、自定义 C 导出或官方源码修改。
- 测试文件与主要 fixture 同名，共享 helper 独立放 `Fixtures`。测试类和 category 名称用于 runner 筛选，
  结构整理时优先保留。`AdditionalTtsUnitTests` 为历史 fixture 名称，含真实 Express 调用和本地词典检查，
  因而归入 `Integration/Generation`；不能把它当作纯离线单元测试证据。
- 联网测试保持显式执行；真实权重检查、原生调用和替身测试分别说明验证范围。
  测试不得自动下载大型模型。测试产物与缓存放 `TestArtifacts~/`。
- 移动 Unity 资产时同时移动 `.meta`；拆分出的新类型分配新 GUID，原文件的主要类型保留旧 GUID。
  不手工重写插件导入设置，不提交孤立 meta 或重复 GUID。
- 使用仓库 `.editorconfig`：UTF-8、LF、C# 四空格缩进与独立大括号；JSON/asmdef 两空格。
  Unity 生成的插件 meta 保持 Unity 的序列化格式。

## 维护检查

从仓库根目录运行，无需安装依赖或下载模型：

```bash
python3 Packages/com.eitan.mnn/Tools~/Validation/validate_package.py
```

检查目录边界、程序集引用、测试位置、句柄归属、资源 meta/GUID、文档本地链接、
模板残留与生成产物。它只验证结构，不能代替 C# 编译、Unity 测试、原生 ABI 或真实推理验证。
编译和实际推理的入口见 [测试说明](Testing.md)。
提交范围、忽略规则、本地保存与分支流程见 [仓库维护](RepositoryManagement.md)。
