# MNN-Pocket 接入

包内目录和 Studio 收录[官方合集](https://modelscope.cn/collections/MNN/MNN-Pocket-03B)的六种权重。
`MNN/MNN-Pocket-0.3B` 是指令模型名称；端侧推理使用官方
`MNN/MNN-Pocket-0.3B-MNN` 导出，二者在配置和下载来源中分别保留。

| 权重 | 格式与用途 | 当前接入 |
| --- | --- | --- |
| MNN-Pocket-0.3B | BF16 指令源权重 | 目录收录；对话默认项映射到官方 MNN 部署仓库 |
| MNN-Pocket-0.3B-MNN | INT4 W4A16 QAT，block 64 | 固定下载、校验、安装、文本生成、会话与 Studio |
| MNN-Pocket-0.3B-Base | BF16 预训练基座 | 目录收录，需导出 MNN |
| MNN-Pocket-0.3B-SFT | BF16 SFT 中间权重 | 目录收录，需导出 MNN |
| MNN-Pocket-0.3B-GGUF | llama.cpp F16/Q8_0/Q4_1 | 格式说明；MNN 不加载 GGUF |
| MNN-Pocket-0.3B-Dec | LoRA adapter、指针头、源模型 | 用途说明；上游尚未发布对应 MNN 决策图 |

这些源权重条目保留在目录中，不能作为可运行的语言 provider 选择。
现有包的决策 API 不代表已适配 Pocket-Dec 的非自回归指针头。

## 资源协议

`MNNPocketModel` 集中声明模型身份、部署仓库、版本与六个文件的大小和 SHA-256。
固定版本为 `92aaad61ae9ecc5a560402a70d1583c08f1c6442`，包含：

- `config.json`、`llm_config.json`：保留官方采样配置、ChatML Jinja 模板与 tied embeddings 参数。
- `llm.mnn`、`llm.mnn.weight`：官方 W4A16 图及外部权重。
- `tokenizer.mtok`：原生 MNN 分词器，保持原格式。
- `LICENSE.txt`：上游 Apache 2.0 许可证。

总大小 174,160,405 字节。下载器的 `master` 请求解析到上述版本，显式不匹配版本会在写入前拒绝。
在线发现使用同一固定身份，刷新后保留对话默认别名。

```csharp
using System.IO;
using System.Linq;
using MNN.Unity;
using UnityEngine;

var entry = new MNNConversationModelCatalog().Entries
    .Single(model => model.modelId == MNNPocketModel.ModelId);
// Player 的下载目标需使用可写目录；Editor 可用 StreamingAssets 下的模型目录。
string root = Path.Combine(Application.persistentDataPath, "MNN", "Models");
string directory = MNNModelInstallation.DirectoryFor(root, entry.remote,
    MNNConversationModelCatalog.DefaultInstallationName(entry.remote));
await MNNModelInstallation.EnsureAsync(directory, entry.remote);
using (var model = MNNLlm.Load(directory))
    Debug.Log(model.Generate("中国的首都是哪里？只回答城市名称。", 32).Text);
```

本地开发权重位于 `Assets/StreamingAssets/MNN/Models/MNN-Pocket-0.3B-MNN/`，
可由 Studio 复用。正式 Player 沿用包现有的模型交付与可写安装目录规则。

Base、SFT 或其他精度的源模型可按官方仓库说明，使用未修改的 MNN `llmexport.py`：

```sh
python llmexport.py --path /path/to/MNN-Pocket-0.3B-Base --export mnn --quant_bit 4 --quant_block 64
```

完成导出后，通过本地目录传入 `MNNLlm.Load`；需要完整的配置、分词器、图和权重。
导出与目标设备验证由调用者执行，不会在模型目录刷新时下载训练依赖或源权重。

## SiriAI 默认项与验证范围

新配置与 `SiriAI.unity` 默认选择 `MNN/MNN-Pocket-0.3B`，线程预算继续按硬件计算。
首次启动保留该模型；已保存的设置、手动选择和自定义 provider 继续有效。
已有设备可在 Providers 选择 Pocket 后保存。被忽略的本地 SiriAI 源码改动，另以
[迁移工具](../Tools~/Migration/README.md)的 `--stage pocket` 保存和复现。

macOS Unity 2021.3.45f2 Mono 已实测 `.mtok` 分词、中文“北京”、英文“Paris”、
中文算术“4”，以及 CPU 会话 provider 的官方 ChatML 模板。
文本生成沿用现有官方 C++ API 的明确编译器/平台 ABI；本次未新增 C++、桥接库或自定义导出。
这些结果不代表 Android/iOS/Windows/Linux 的本轮实机验收，也不代表整个系列源权重已转换。

模型能力存在局限：提示 `What is 1 + 1? Answer with just the number.` 的贪心输出为 `1`；
现有未修改的官方 `llm_demo` 使用相同权重也输出 `1`。接入验证不保证任意算术或任务准确率。
