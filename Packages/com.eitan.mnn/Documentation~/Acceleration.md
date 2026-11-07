# 自动推理加速

所有已实现的模型加载入口默认 `backendType: MNNBackendType.Auto`，并应用任务兼容策略。
在 macOS/iOS 上，Auto 先探测 Metal GPU，再探测 NN，最后使用 CPU；
Android 上按 OpenCL → Vulkan → CPU 探测。Windows/Linux 随包构建仅启用 CPU，
`Auto` 使用 CPU，显式 GPU 请求也回退 CPU。具体模型仍应用质量/兼容策略。
探测通过小型内嵌 MNN 图创建真实 Session，执行并校验 `2*x+1` 的结果，
再用官方 `getSessionInfo(BACKENDS)` 确认主后端；结果按进程缓存。不会只凭平台名称、枚举值或已注册的 runtime 判定设备可用。
显式指定不可用的后端也会退到 CPU。

当前随包 macOS MNN 3.6.2 启用了 Metal，没有启用 CoreML、CUDA 或 OpenCL。
这份 macOS 二进制目前可用的是 Metal 和 CPU，不能宣称已使用 Apple Neural Engine。
Android ARM64 IL2CPP 的官方库启用 OpenCL、Vulkan、NNAPI 与 ARMv8.2 FP16 内核；
运行时仍检查设备实际可用性。包附带可选 `libOpenCL.so` 的 Android manifest 声明，
支持 Android 12+ 对厂商 OpenCL 库的可见性要求。iOS ARM64 IL2CPP 支持 CPU/Metal，具体绑定和验证范围见
[移动端说明](MobileInterop.md)。Windows/Linux x64 Mono 的核心、Express、LLM、检索和媒体输入
使用独立 GNU ABI，已验证 CPU 任务，尚未验证对应 Unity Player；见[桌面 Mono](DesktopMono.md)。
WebGL、桌面 IL2CPP 和 Windows/Linux ARM64 在原生调用前拒绝。
后端枚举不是平台支持承诺，NN runtime 可用也不等同于 NPU 硬件放置证明。

## 使用与诊断

DTLN AEC 128/256/512 的 `Auto` 固定使用 CPU/High，显式 GPU/NPU 请求被拒绝。
包内 FFT 在已安装且启用 Burst 1.8+ 的项目自动使用可选程序集；基础 Runtime 不依赖
Burst/Collections/Mathematics/Jobs，未安装时运行预计算的 C# 指针内核。FFT 加速与 MNN
神经网络后端分别选择，测量范围见 [DTLN AEC](DtlnAec.md)。

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
    var text = omni.GenerateMultimodal("Describe this picture.", imagePath: picturePath);
    Debug.Log($"Model={omni.Backend}; Media={omni.MediaBackend}");
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
Omni 语音输出当前在所有平台拒绝执行：旧的手工 `std::function` 回调实现已移除。
历史 GPU/CPU 语音测量不代表当前版本支持该功能；图像/音频输入仍使用各自配置的 runtime。
Embedding/Reranker 中的小型主机数据处理使用 CPU scope，模型 Module 持有自己的 runtime。
Express 图执行不会再强制 `compute(..., true)` 回到 CPU。
SD 1.5 的普通 `.mnn` 图及其外部权重可交给 Metal，不要求另行下载所谓 GPU 版模型。

## 回退、精度与缓存

Session 创建失败时再尝试 CPU，正常执行时使用 MNN 自身的 CPU 算子备用后端。
高层生成图的 Metal/OpenCL/Vulkan 浮点输出非有限时，先以 High/FP32 精度重试 GPU；
仍失败、或遇到其他可捕获的 MNN 执行失败/空输出后，用相同输入在 CPU 重试一次，此后该阶段保持 CPU；Bert-VITS2 的独立 Express 生成器也执行此策略。
LLM、Embedding、Reranker 在可捕获的原生加载/推理错误后重新加载 CPU 模型并重试。
CPU 仍失败则向调用者抛出异常。低层 `MNNSession.Run` 的错误直接向调用者报告，
不会隐式重建用户持有的 tensor view；原生崩溃或进程被系统终止无法由 C# 回退恢复。

流式对话只在首次进度回调前重试 CPU，避免重复交付文本或执行用户回调。
取消与用户回调异常不应触发请求重放。精度默认保留 `Normal`；
不会自动改低精度或量化权重。已有 `precisionMode` / Session 精度设置仍可显式使用。
`CreateForCurrentPlatform` 的 CPU 线程预算按逻辑核心数的一半设置，限制在 1–16；
任务加载保留各自可选线程参数。Metal/OpenCL/Vulkan 的 GPU 调优位与 CPU 线程数分别处理；
官方 `Backend::Info` 联合字段在GPU后端读取为 `gpuMode`，不可把CPU线程数2误传为Heavy调优。
ERes2NetV2 显式使用Fast调优并隔离缓存策略，避免大量变长CNN形状触发意外Heavy搜索。

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

## Android 动态模型与 NNAPI

`MNNAcceleration.NeuralDevices` 使用 Android NDK 的公开 NNAPI API 返回驱动名称、
类型与 feature level，并按进程缓存。设备枚举、`NN` 会话类型和实际 NPU 放置是不同证据；
例如 MediaTek `mtk-gpu` 的 NNAPI 设备类型也可能报告 accelerator，不能据此称为 NPU 推理。

MNN 3.6.2 的 NNAPI 动态图转换/resize 在不支持的形状上会在原生层退出，托管异常无法捕获。
本包在 Android 的高层动态图/LLM 路径将显式 NN 请求回退 CPU，Auto 不探测 NNAPI。
低层 `MNNInterpreter.CreateSession(new MNNSessionConfig { BackendType = MNNBackendType.NN })`
保留给已经验证的静态图与设备组合。不要把“NNAPI 可用”当成任意模型可安全运行的证明。
没有增加 C++ 桥接或修改官方 MNN 源码；直接符号调用仍受固定的 NDK/libc++ ABI 约束。

Mali-G57 的已测 Qwen3.5-0.8B 与 INT8 Supertonic 在 Auto 下选择 CPU；
同机对照分别为 Qwen 短回答 CPU 0.83 s / OpenCL 9.57 s，Supertonic 热运行
CPU 1.20 s / OpenCL 1.70 s。测量只代表这台设备、这段输入及当前模型。
Adreno 740 的 Qwen3.5-0.8B 也在 Auto 下选择 CPU：PJA110 / Android 16 的
Unity ARM64 IL2CPP 实测中，OpenCL 首次短回答的首文本回调为 3.317 s，第二次为
0.166 s；374-token 提示词触发 `ConvBufLowMemoryExecution` 的原生空 kernel 崩溃。
CPU 同输入完成且短回答首回调约 0.10–0.18 s；这是指定模型/设备组合的策略，
显式 OpenCL 请求仍会执行该后端，不能依赖托管回退捕获这类崩溃。
同机 INT8 Supertonic 的 Auto 也选择 CPU，避免已观察到的 OpenCL 输入映射访问崩溃。
三个英文短句、各两次的真机回归与 CPU 参考波形一致；该结论不扩展到其他 GPU 或精度。
Android SenseVoice 的 Auto 保持 CPU。显式 OpenCL/Vulkan 使用 FP32，避免默认 GPU
精度的非有限 logits；HF Express 导出保持 CPU。Mali-G57 的实测 FP32 OpenCL 热运行
比 CPU 慢，因此不能为使用 GPU 而牺牲识别速度或 UI 帧率。

通过 Package 的异步加载入口可避免把原生模型加载放在 Unity 主线程：

```csharp
using var model = await MNNModelLoading.LoadAsync(
    () => MNNSupertonic.Load(directory, precision: "int8", backendType: MNNBackendType.OpenCL),
    cancellationToken);
```

factory 只能访问线程安全的模型 API；Unity 对象操作仍留在主线程。原生加载不可中断，
取消后会释放已创建、尚未交给调用方的模型。GPU kernel 首次编译/调优仍与图形共享硬件，
后台线程不能消除其 GPU 竞争；应比较冷启动、热运行、输出质量与实际整帧耗时。
`MNNPlatformSupport.AbiContract` 可供应用诊断显示当前平台编译 ABI。
