# 自动推理加速

所有已实现的模型加载入口默认 `backendType: MNNBackendType.Auto`，并应用任务兼容策略。
在当前支持的 macOS Mono ABI 上，Auto 先探测 Metal GPU，再探测 NN，最后使用 CPU。
探测通过小型内嵌 MNN 图创建真实 Session，并用官方 `getSessionInfo(BACKENDS)`
确认主后端；结果按进程缓存。不会只凭平台名称、枚举值或已注册的 runtime 判定设备可用。
显式指定不可用的后端也会退到 CPU。

当前随包 MNN 3.6.1 启用了 Metal，没有启用 CoreML、CUDA 或 OpenCL。
因此这份二进制目前可用的是 Metal 和 CPU，不能宣称已使用 Apple Neural Engine。
其他操作系统及 IL2CPP 的直接 C++ ABI 尚未实现，仍会在调用原生符号前拒绝。
后端枚举不是平台支持承诺，NN runtime 可用也不等同于 NPU 硬件放置证明。

## 使用与诊断

```csharp
Debug.Log(MNNAcceleration.PreferredBackend);
bool metalAvailable = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal);

using (var model = MNNStableDiffusion.Load(sdDirectory))
{
    // TextEncoder / UNet / VaeDecoder 使用同一默认加速策略，各阶段可独立回退。
    var image = model.Generate("A red bicycle beside a white wall", steps: 20);
    foreach (var stage in model.StageBackends)
        Debug.Log($"{stage.Key}: {stage.Value}");
}

// 保留显式 CPU 入口，用于兼容性检查或在目标设备上比较性能。
using (var model = MNNLlm.Load(llmDirectory, backendType: MNNBackendType.CPU))
    Debug.Log(model.Generate("What is 1 + 1?").Text);

using (var omni = MNNLlm.Load(omniDirectory))
{
    var speech = omni.GenerateMultimodal("Say hello.", generateSpeech: true);
    // 当前 Metal 兼容配置：主模型 Metal，图片/音频处理器 CPU。
    Debug.Log($"Thinker/Talker={omni.Backend}; Media={omni.MediaBackend}");
}

using (var interpreter = MNNInterpreter.CreateFromFile(graphPath))
using (var session = interpreter.CreateSession())
    Debug.Log($"Requested={session.RequestedBackend}; Actual={session.ActualBackend}");
```

`MNNLlm`、`MNNEmbedding`、`MNNReranker`、`MNNPiper` 提供 `Backend`。
`MNNLlm.MediaBackend` 单独报告图片/音频处理子图配置的 runtime。
SD、Sana、Supertonic、Bert-VITS2 提供 `StageBackends` 快照。
Sana Edit V2 在此构建的 Metal 路径出现非有限张量和失真的饱和图像，FP32 重试也未解决；
因此当前整条 Sana pipeline 在调用前使用 CPU，保留已有语义编辑效果限制。
Piper 的 fp16 voice 导出在 Metal 上产生有限但无法正确转写的波形，当前使用 CPU。
Embedding 的 GPU 路径在 Normal 和 High 下均未通过相关性排序回归，当前也使用 CPU。
这些兼容限制针对随包构建及当前已实现任务；升级原生库时须重跑质量验证再解除。
Bert-VITS2 的陈曦控制流生成器在随包 Metal Module 路径会触发原生空输出崩溃，
因此生成器在调用前选择已验证的 CPU；其中文 BERT 阶段仍尝试加速并检查非有限输出。
Interpreter Session 的 `ActualBackend` 来自官方查询；Express executor 和任务模型的
`Backend` 是已探测后配置的 runtime 类型，并非逐算子硬件轨迹。
MNN 仍可将设备不支持的算子交给 CPU 执行，所以主后端 Metal 不意味着全部算子运行在 GPU。

LLM 初次加载时，文本与多模态子图配置相同的加速后端；Reranker 的 Module 也使用这个策略。
Omni 全 GPU 语音在 Normal 下失真；独立 High runtime 和共享 High runtime
仍未通过不同句子与流式输出的扩大回归，不能作为可靠默认配置。
首次 `generateSpeech: true` 请求在生成/流式回调前自动重载：
Thinker/Talker 保留 High 精度 Metal，官方 `mllm` 处理器（PreDiT、DiT、BigVGAN）使用 High 精度 CPU。
此后实例保持这个混合配置，无需每次重载；加载或可捕获执行失败仍可整模型回退 CPU。
`Backend` 此时为 Metal，`MediaBackend` 为 CPU；后续图片/音频输入处理也使用 CPU runtime。
显式 CPU 加载保持 CPU，其他尚未验证的加速器语音使用 CPU 兼容路径。
这个策略保留已验证的 GPU 阶段，并不宣称整个语音 pipeline 都在 GPU 执行。
Embedding/Reranker 中的小型主机数据处理使用 CPU scope，模型 Module 持有自己的 runtime。
Express 图执行不会再强制 `compute(..., true)` 回到 CPU。
SD 1.5 的普通 `.mnn` 图及其外部权重可交给 Metal，不要求另行下载所谓 GPU 版模型。

## 回退、精度与缓存

Session 创建失败时再尝试 CPU，正常执行时使用 MNN 自身的 CPU 算子备用后端。
高层生成图的 Metal 浮点输出非有限时，先以 High/FP32 精度重试 GPU；
仍失败、或遇到其他可捕获的 MNN 执行失败/空输出后，用相同输入在 CPU 重试一次，此后该阶段保持 CPU；Bert-VITS2 的独立 Express 生成器也执行此策略。
LLM、Embedding、Reranker 在可捕获的原生加载/推理错误后重新加载 CPU 模型并重试。
CPU 仍失败则向调用者抛出异常。低层 `MNNSession.Run` 的错误直接向调用者报告，
不会隐式重建用户持有的 tensor view；原生崩溃或进程被系统终止无法由 C# 回退恢复。

流式对话只在首次进度回调前重试 CPU，避免重复交付文本或执行用户回调。
取消与用户回调异常不应触发请求重放。精度默认保留 `Normal`；
不会自动改低精度或量化权重。已有 `precisionMode` / Session 精度设置仍可显式使用。
`CreateForCurrentPlatform` 的 CPU 线程预算按逻辑核心数的一半设置，限制在 1–16；
任务加载保留各自可选线程参数。OpenCL/Vulkan 的 GPU 调优位与 CPU 线程数分别处理，
不把 GPU 位掩码当作 Metal 的线程数。

固定生成图自动使用 MNN 官方 GPU kernel cache。缓存位于
`Application.persistentDataPath/MNN/GpuCache`，可用环境变量
`MNN_INFERENCE_CACHE_DIRECTORY` 改到其他目录。测试统一改到项目 `TestArtifacts~/`。
key 包含图/外部权重路径、大小与修改时间、设备/驱动、架构、MNN/ABI 版本、后端及精度。
不扫描大型权重全文；原位替换模型时应更新修改时间。动态常量特化图不复用此缓存。
首次推理及 shape 调整后更新缓存，缓存写入失败只关闭缓存。
LLM/检索任务使用官方 `tmp_path`；自动缓存也遵循 `MNN_INFERENCE_CACHE_DIRECTORY`，
位于该目录的 `Tasks/<唯一标识>` 下，未设置时使用 persistentDataPath 中的 MNN/Cache。
自动创建的缓存随实例释放删除，调用者显式指定的缓存不会删除。
SD 去噪循环复用 batch/noise 缓冲区，降低每步的托管数组分配。

设备可用不代表对所有模型都最快。短图的提交/拷贝、动态形状、CPU 备用算子、显存预算
都会影响速度。默认策略实现加速器优先；实际性能与输出质量仍须在目标设备/模型验证。
实测覆盖及限制见 [测试说明](Testing.md)。
