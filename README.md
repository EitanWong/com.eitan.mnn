# MNN for Unity

[![Unity](https://img.shields.io/badge/Unity-2021.3+-black?logo=unity)](https://unity.com)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

基于阿里巴巴 [MNN 3.6.1](https://github.com/alibaba/MNN) 的 Unity 推理包，提供计算图、
语言、多模态、检索、语音合成与图像生成的托管接口，以及模型管理器和 Chat Studio。

## 当前支持范围

C# P/Invoke 调用随包库的官方 C++ 符号，不新增 C++、自定义 C 导出或桥接库。
当前绑定明确限定 **MNN 3.6.1、Apple clang / libc++ ABI v1、macOS 11+、64 位 Mono**。
STL 编组、聚合返回和虚调用依赖该编译器 ABI；这是本项目维护的特定构建契约，
不是 MNN 官方提供的可移植 C# SDK，也不能替换为任意同版本原生库。
详细构建与假设见 [原生库与 ABI](Packages/com.eitan.mnn/Native~/README.md)。

| 范围 | 当前状态 |
| --- | --- |
| macOS arm64 Mono Editor / Player | 已真实验证 CPU；Metal 加速覆盖见测试文档 |
| macOS x86_64 Mono Player | 已在 Apple Silicon + Rosetta 验证六类代表模型的 CPU 推理；未验证 Intel 真机或 x86_64 GPU |
| IL2CPP、Windows、Linux、Android、iOS、WebGL | 推理绑定尚未实现；API 在原生调用前拒绝 |
| CoreML / NPU / Apple Neural Engine | 当前构建未启用 CoreML，不声明硬件支持 |

随包保留的其他平台原生插件和后端枚举不代表对应 C# 推理可用。
已验证环境为 Unity 2021.3.45f2；包要求 Unity 2021.3+，更新版本仍需自行验证。

## 功能

- Interpreter / Session / Tensor：文件或内存加载、张量映射、动态形状、同步与异步推理。
- LLM 与多模态：角色历史、逐 token 流式回复、单张图片或单个 WAV 输入、Qwen2.5-Omni 语音输出。
- 检索：Qwen3 Embedding 和 Reranker。
- 生成：SD 1.5 文生图；Supertonic、Bert-VITS2 陈曦中文、Piper 英文语音合成。
  Piper 需要另行安装上游 eSpeak-NG。Sana 编辑管线可运行，但语义颜色编辑效果尚未达标。
- 编辑器：完整模型仓库下载、暂停续传与并行下载；Chat Studio 的会话、搜索、媒体附件、播放和导出。
  23 个任务入口中，尚未接入专用运行时的任务提供模型准备引导。

默认 Auto 策略优先选择可用 Metal，并按任务保留 CPU 兼容路径。Embedding、Piper、Sana
及 Bert-VITS2 生成器使用 CPU；Omni 语音使用 High 精度 Metal 主模型与 CPU 媒体处理器。
模型主后端不等同于全部算子都在 GPU 执行。策略、缓存与限制见
[自动推理加速](Packages/com.eitan.mnn/Documentation~/Acceleration.md)。

## 安装

在 Unity 中打开 **Window > Package Manager > + > Add package from git URL**。
仓库包含开发工程，安装时必须指定包的子目录。获取当前开发版本：

```text
https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#dev
```

`main` 保存已合并版本，可将结尾换成 `#main`。需要可复现安装时使用已存在的提交 SHA。
旧 `upm` 分支不作为本次开发版本的安装来源。

也可添加到项目的 `Packages/manifest.json`（与已有 dependencies 合并）：

```json
{
  "dependencies": {
    "com.eitan.mnn": "https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#dev"
  }
}
```

## 快速开始

先检查平台契约和原生库：

```csharp
using MNN.Unity;
using UnityEngine;

public class MNNExample : MonoBehaviour
{
    private void Start()
    {
        if (!MNNPlatformSupport.IsSupported)
        {
            Debug.LogError(MNNPlatformSupport.UnsupportedReason);
            return;
        }

        if (MNNVersion.IsLoaded())
            Debug.Log($"MNN Version: {MNNVersion.GetVersion()}");
        else
            Debug.LogError("MNN failed to load");
    }
}
```

版本检查不等于模型推理验证。在 Package Manager 导入 **Basic Inference** sample，
配置已有兼容模型，或打开 **Window > MNN > Chat Studio**，按任务选择并下载完整模型仓库。
模型不随源码提交，也不会在普通测试中自动下载。

随应用打包的模型放 `Assets/StreamingAssets/MNN/Models`；运行时下载与热更新放
`Application.persistentDataPath/MNN/Models`。仓库忽略 StreamingAssets，发布应用时需自行准备模型。

## 文档

- [文档索引](Packages/com.eitan.mnn/Documentation~/README.md)
- [API 参考](Packages/com.eitan.mnn/Documentation~/API.md)
- [Chat Studio](Packages/com.eitan.mnn/Documentation~/ChatStudio.md)
- [真实推理、测试证据与复现](Packages/com.eitan.mnn/Documentation~/Testing.md)
- [项目结构与程序集边界](Packages/com.eitan.mnn/Documentation~/ProjectStructure.md)
- [仓库维护、忽略规则与提交](Packages/com.eitan.mnn/Documentation~/RepositoryManagement.md)
- [更新日志](Packages/com.eitan.mnn/CHANGELOG.md)
- [MNN 官方文档](https://www.mnn.zone/)

## 开发检查

从仓库根目录执行：

```bash
python3 Packages/com.eitan.mnn/Tools~/Validation/validate_package.py
```

该命令检查目录、程序集、Unity meta/GUID 和文档链接，不代替编译或原生推理测试。
测试工具见 [Tools](Packages/com.eitan.mnn/Tools~/README.md)。本地快照、日志、生成媒体、
模型验证和隔离 Unity 工程统一保留在被忽略的 `TestArtifacts~/`。

## 许可

本项目和 MNN 引擎采用 Apache License 2.0。详见 [LICENSE](LICENSE)。
