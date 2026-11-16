# 轮次检测模型与延迟优化

轮次结束由 VAD、轮次模型和语音输入校验共同决定。默认等待 550 ms 连续静音后第一次检测，
模型否定后每 500 ms 重试；连续静音达到 `maximumTurnSilenceMilliseconds`（默认 2000 ms）时，
不再等待模型反复否定，进入原有语音校验。这个上限不是端到端延迟保证：在途推理、ASR、
输入校验和设备调度仍需要时间。没有通过语音校验的输入不会因为超时而提交。

重试只保留最后 800 ms 尾静音，保留语音和句中停顿，避免静音不断挤占模型的八秒语音上下文。
默认的旧选择 ID `SmartTurn-v3.2-MNN` 指向新的 FP16 部署，已保存的用户选择无需重选。

## 本机选择

以下是本次 CPU High 精度的源模型对照和机器样本计时，排除加载；各表的前端、分词和
生成循环范围不同，不用于横向排名。并行下载/测试可能影响计时。没有进行手机、GPU 或完整语料准确率评估。
样本输出一致不等于真实场景准确率一致，也不能称为全平台最优。

| 模型 | 输入 | 部署选择 | 默认线程 | 本次样本证据 |
| --- | --- | --- | --- | --- |
| SmartTurn v3.2 | 音频 | FP16 | 4 | 32 条概率对照，无阈值翻转；约 13–16 ms 图执行 |
| Bairong TurnSense 1.0 | 音频 | FP16 | 1 | 16 条，三分类无翻转；图执行约 28 ms，完整调用约 33 ms |
| Bairong TurnSense 1.1 | 音频 | FP16 | 1 | 16 条，三分类无翻转；图执行约 91 ms，完整调用约 111 ms |
| TEN Turn Detection 7B | ASR 转录 | INT4 RTN 对称 block64、INT8 head、FP16 scales、BF16 embeddings | 1 | 12 条中英文，三标签与 FP32 源一致；原图计时约 326 ms，托管完整分类约 385 ms |
| Easy-Turn | 音频 | 音频与解码器均 INT8 HQQ 非对称 block64、BF16 embeddings | 4 | 16 条，六种精度组合完整生成 token 与 FP32 源一致；完整转写加分类约 1.07 s |
| LiveKit multilingual v0.4.1 | ASR 转录 | FP16 | 4 | 12 条，最大概率差 0.000263，官方中英文阈值下无翻转 |
| LiveKit English v1.2.0 | ASR 转录 | 动态上下文 FP32；保留原 FP16 accelerator 资源 | 1 | 12 条，FP32 最大概率差约 1e-7；1 线程约 3.9 ms 图执行 |
| DualTurn | 双通道音频 | FP32 | 1 | 17 流、包含 128 ticks 静音和 16 个公开语音片段；最大概率差约 0.006253，无阈值翻转 |

线程复核使用实际 `MNNTurnDetector.IsComplete`，音频 16 条、转录 12 条，各两轮；
排除加载和文件读取，共 360 次调用。Bairong 1.0/1.1 与 TEN 的 1 线程快于 2/4，
Easy-Turn 的 4 线程最快。早期图计时的 2 线程选择保留作历史记录，运行默认以完整调用复核为准。
14 个模型的单样本加载/调用补测另存报告；例如 TEN 首次加载加校验约 30 s，
完整调用约 0.4 s，SmartTurn 完整调用约 65 ms，均不能等同于图执行或端到端提交耗时。

FP16 不等于 FP16 累加：这些新 CPU 部署使用 High 精度运行。SmartTurn、Bairong 的 INT4
出现判定翻转。Easy-Turn INT4 虽然更小却比 INT8 更慢。DualTurn 的 FP16、INT8、INT4
分别出现 1、2、55 个阈值翻转，因此保留 FP32。不要为了统一位宽替换这些结果。

原有 NAMO、VoxTurn、CueLight Nano/SSM、SmolLM2 TurnSense 和 SmartTurn FP32 的部署也统一
登记到 `MNNModelRegistry`，保留原有数学/前端适配。本次没有重新宣称这些模型已完成全部量化搜索。
Bairong 的 TurnSense 与 latishab 的 SmolLM2 TurnSense 是不同模型。

## 使用和选择

使用 Model Manager 搜索 `Smart Turn`、`TurnSense 1.0`、`TurnSense 1.1`、`TEN Turn Detection`、
`Easy Turn`、`LiveKit multilingual`、`LiveKit English` 或 `DualTurn`。
共享 Runtime catalog、Editor catalog 和安装器使用同一注册，包含真实 ModelScope 仓库、
不可变 revision 和每个文件的 SHA-256；训练仓库不能作为可安装的 MNN 导出。

`MNNTurnDetector.Load(directory)` 选择相应模型默认线程；`threadCount` 非零时覆盖。
Realtime provider 的 `MNNConversationLocalModelOptions.TurnThreads=0` 同样使用模型默认，
与语言模型/TTS 的 `Threads` 分开。大模型是可选语义判断器；最低延迟默认仍是 SmartTurn FP16。
TEN 需要 ASR，因此实际延迟还要加入转录时间。Easy-Turn 输出完整转写再分类，不宜当作最快默认项。

TEN 只把 `finished` 视为完成，`unfinished` 和 `wait` 不提交。Easy-Turn 只把终止
`complete` 标签视为完成，`incomplete`、`backchannel` 和 `wait` 不提交；缺失标签或生成超限抛出错误。
LiveKit multilingual 使用原始语言阈值，`zh=0.0066`、`en=0.011`，专用 API 和统一适配器的带 `language` 重载可显式选择语言；
会话 provider 使用 `MNNConversationLocalModelOptions.Language`，Studio 提供语言选择；
不能一律使用 0.5。未知语言不会悄悄使用一个伪造阈值。

**LiveKit 许可证限制**：英文与多语言原始权重及转换衍生版本使用 LiveKit Model License，
仅允许与 LiveKit Agents 一起使用；独立 Unity/MNN 应用需要相应单独许可。ModelScope 标记为
`other` 并保留原 LICENSE，不把它标记为 Apache-2.0。VoxTurn 原有非商业限制也继续保留。

## 验证范围和产物

模型资产在开发工程 `Assets/StreamingAssets/MNN/Models/`，没有嵌入 UPM package。
模型卡包含源 revision、量化方式、完整文件校验与计时范围。
本次报告、独立源 oracle、XML、stdout/stderr 和操作日志在
`TestArtifacts~/TurnDetection-20261116/`。官方 MNN Python 用于转换和源输出对照；
C# 验收复用已有执行设施，不能把既有 bridge 的结果描述为直接调用官方 C++ API 的 ABI 证明。
没有新增 C++、自定义导出或修改官方 MNN 源码。
