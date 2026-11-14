# 自动选择模型与资源预算

`MNNModelRecommendation` 是离线、纯托管的公开 API。它接收硬件、实际工作负载和候选导出资料，
返回推荐、排除原因、内存构成、速度估计及证据。不会请求权限、下载权重、加载模型或启动基准测试。
适用于语言、ASR、TTS、视觉、Embedding、音频处理及宿主自定义任务；不依赖 SiriAI。

```csharp
var hardware = MNNModelHardware.Capture(); // Unity 主线程；也可由宿主提供精确数据
var workload = new MNNModelWorkload
{
    Task = MNNModelRecommendationTask.Language,
    ContextTokens = 8192, // 保留的输入 + 输出总数，不是宣传的模型最大上下文
    ConcurrentSequences = 1,
    MinimumThroughputPerSecond = 12,
    RequiredCapabilities = new[] { "text" }
};
var profiles = MNNModelRecommendationProfiles.CreateDefault();
var best = MNNModelRecommendation.Recommend(hardware, workload, profiles);
var alternatives = MNNModelRecommendation.Rank(hardware, workload, profiles);
if (best != null)
    UnityEngine.Debug.Log($"{best.ModelId}: {best.Memory.PeakBytes} bytes, {best.Status}");
```

返回值可为 `null`。无法满足预算、语言、功能、导出兼容性或速度要求时，不会假装找到了合适的模型。
`Rank` 保留被排除的候选及原因。`Suitable` 具有精确匹配的速度测量；`Provisional` 使用估算，
或没有被要求的性能测量；`Rejected` 不满足硬条件。它们都不是原生推理通过或质量评测通过的声明。
未提供带宽/算力、又要求最小吞吐时会排除该候选，不伪造速度。未知首结果延迟会单独标注。
`AllowEstimatedPerformance=false` 可要求真实测量。

## 内存计算

| 项目 | 规则 |
| --- | --- |
| 权重 | 优先采用导出文件的实际驻留字节。缺少实际值时，使用总参数 × 位宽 / 8，加量化组的 scale/zero 元数据；MoE 采用总专家参数驻留。 |
| 全注意力 KV | 每组 `层数 × KV heads × (K head dim + V head dim) × 缓存元素字节 × 保留 tokens × 并发序列数`。GQA/MQA 使用 KV heads。 |
| 滑动窗口 | 每组 tokens 取上下文与窗口的较小者，再按分配块向上取整。其他全注意力组继续保留完整上下文。 |
| 线性/循环状态 | `FixedBytesPerSequence × 并发序列数`；与上下文长度无关，不能按每层全 KV 计算。 |
| 工作区 | 固定峰值 + 输入规模系数 + prefill 分块系数，乘并发数；最后对权重、缓存、工作区加安全余量。 |
| 统一内存 | 只使用系统物理内存池，不把 graphicsMemorySize 再加一次。 |
| 独立显存 | 模型峰值必须装入显存，宿主权重暂存必须同时装入系统内存；宿主可提供更大的 staging 峰值。 |
| 可用预算 | 总量扣除系统/驱动与应用预留，再受当前可用内存、其他并存任务预留的约束。0 表示可用量未知。 |

默认系统预留为 `max(2 GiB, 物理内存的 20%)`，另给应用 1 GiB；显存预留
`max(512 MiB, 显存的 20%)`，峰值余量 15%。这些是可调整策略，不是系统查询结果。
当前可用量已反映运行中的其他进程；`Reserved*Bytes` 用于额外计划中的并存负载，避免重复扣减。
下载体积、内存驻留与每步权重访问量各自建模：9B 的独立 BF16 embedding 需要驻留，
但不应假设每生成一个 token 都顺序读取整个 embedding 文件。

模型导出、KV 数据类型、后端 unpack、视觉编码器、输入图像分辨率和音频长度都会改变实际峰值。
默认资料对完整导出体积做保守驻留估计；工作区与 FP16 KV 是明确的初始假设，不能替代实测。
调整工作负载或后端精度后，应更新资料并重新调用 API。API 不会替应用修改或强制执行上下文限制。

## 体验与联合选择

有匹配测量时优先采用测量；否则使用 roofline 的内存带宽与计算上限中较小者，
再乘持续性能系数。语言模型默认计算量使用活跃参数，权重驻留仍使用总参数。
ASR/TTS 等任务需提供该任务单位的运算量、访问量或实测吞吐，不能从参数量直接推导音频实时率。
批量语言推理共享权重读取，各序列独占 KV；算力与缓存压力随并发增加。

先排除不满足条件的候选，再综合任务质量、达到目标的响应能力和内存余量排序。
速度达到目标后不继续无限奖励小模型；质量未知时，语言容量只作为可替换的弱先验。
`QualityScore` 应由应用使用同一任务的真实评测填充，不能把不同任务或不同评测的分数直接混用。
所有权重、带宽效率、预留和搜索宽度可通过 `MNNModelRecommendationPolicy` 调整。

多个阶段应使用 `RecommendPlan`：

```csharp
var plan = MNNModelRecommendation.RecommendPlan(hardware, new[]
{
    workload,
    new MNNModelWorkload { Task = MNNModelRecommendationTask.SpeechRecognition,
        InputUnits = 30, RequiredLanguages = new[] { "zh" } }, // 30 秒输入
    new MNNModelWorkload { Task = MNNModelRecommendationTask.SpeechSynthesis,
        InputUnits = 120, RequiredLanguages = new[] { "en" } } // 本资料使用字符数
}, profiles);
```

联合规划会保守地累计各阶段驻留和峰值工作区，允许降低语言模型档位来容纳语音阶段。
不会让每个阶段独自获得全部空闲内存。使用有界搜索，默认每任务最多 16 个候选、每轮保留 64 个方案；
它是可重复的推荐启发式，不能宣称对任意巨大目录求得全局最优。
没有完整可行方案时返回 `IsComplete=false` 和空选择，避免上层误用部分结果。

## 扩展资料与校准

资料版本为 `MNNModelRecommendationProfiles.Version`。默认包含 Pocket、Qwen3.5 的
0.8B/4B/9B/27B，以及现有 SenseVoice、Supertonic 起点。新增模型时向 API 传入额外的
`MNNModelResourceProfile`，明确可用导出、任务、语言、能力、权重与缓存结构即可；算法不需要增加型号分支。
语言模型缺少缓存资料会被排除；无标准 KV 的模型也应显式提供固定状态组，不能靠空数组假装没有状态开销。
默认语音资料用于共享预算，并未虚构语音速度排名；可以添加更多已实现的 ASR/TTS 导出及真实评测。

`MNNModelHardware.Capture()` 使用 Unity 提供的内存、CPU 和设备身份。
Apple 基础芯片带宽来自公开规格；已收录 M5 Pro/Max/Ultra 的规格，Max 多 SKU 采用较低带宽。
旧芯片细分和 M6 的内存 SKU 信息不足时采用基础规格下限。
其余设备的带宽/算力未知时不猜测。独立 GPU 的实际可用显存、带宽和运行兼容性应由宿主提供；
`SupportedMemoryKinds` 由有资格的导出资料显式声明，不等同于官方 MNN 对所有 GPU/ABI 的保证。

实测资料必须绑定导出 `ExportIdentity`、硬件 `HardwareId`、运行库/构建 `RuntimeId`、
后端/线程/精度等 `ExecutionId`，并完全匹配上下文、prompt、prefill 分块、批量和输入规模。
`Capture` 不知道实际推理后端，因此不填 `ExecutionId`，避免自动误用 CPU/Metal 的历史速度。
测量不会跨版本、模型量化、后端或工作负载静默复用。建议记录多次暖机后的持续吞吐、首结果延迟、
系统/显存峰值、运行库构建和温度条件；再把现场数据反馈到 profile 与 policy。

在 M5、24 GiB、153 GB/s、8K 上下文和 12 token/s 目标的测试中，联合语音预算选择 Qwen3.5-9B；
8 GiB 的对应测试选择 4B，带宽降低到 20 GB/s 时选择 0.8B。真实本机硬件采集同样返回 9B。
这些是资源模型和推荐行为的验证，不是已经下载或测得这些模型的实际推理速度。

## 公开依据

- [Qwen3.5-9B 原始架构配置](https://huggingface.co/Qwen/Qwen3.5-9B/blob/main/config.json)：GQA、全注意力层、线性注意力状态尺寸。
- [MNN 9B 固定导出](https://modelscope.cn/models/MNN/Qwen3.5-9B-MNN/tree/4def89fe8459266be4be64d5ab7ae8bbe9066081)：实际图、量化权重、BF16 embedding 和视觉权重。
- [MNN 4B 固定导出](https://modelscope.cn/models/MNN/Qwen3.5-4B-MNN/tree/33045fd83cd206d66976af438f7a58255b258a58)。
- [Hugging Face KV cache](https://huggingface.co/docs/transformers/main/en/kv_cache)：缓存类型、滑动窗口、缓存量化与内存权衡。
- [vLLM 混合缓存设计](https://docs.vllm.ai/en/latest/design/hybrid_kv_cache_manager/)：不同注意力状态不能按统一的完整 KV 层处理。
- [JAX Scaling Book：Inference](https://jax-ml.github.io/scaling-book/inference/)：roofline、内存带宽、计算瓶颈与 batching。
- [Apple M5 公开规格](https://www.apple.com/newsroom/2025/10/apple-unleashes-m5-the-next-big-leap-in-ai-performance-for-apple-silicon/)：153 GB/s 统一内存带宽。
- [MacBook Pro 规格](https://www.apple.com/macbook-pro/specs/) 与 [Mac Studio 规格](https://www.apple.com/mac-studio/specs/)：M5 Pro 307、M5 Max 460/614、M5 Ultra 1200 GB/s；M6 基础内存配置 153 GB/s。

本轮抓取的原文、URL、SHA、HTTP 错误、测试输入和行为记录保存在仓库
`TestArtifacts~/DesktopResize20261107/ModelRecommendation20261110/`，不随包分发下载依赖或权重。
