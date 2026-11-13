# Windows x64 Mono 与 IL2CPP

Windows Player 使用随包 MNN 3.6.2 GNU DLL，直接调用官方 C++ 符号。
限定 MinGW-w64 GCC 16.2.0、libstdc++ C++11、Microsoft x64 调用约定，
并非官方可移植 C# SDK；不能替换为同版本 MSVC DLL。没有新增 C++、桥接库、
自定义 C 导出或修改 MNN 官方源码。详细契约见[原生构建](../Native~/README.Windows.md)。
Windows ARM64、macOS/Linux IL2CPP 与 WebGL 仍不受支持。

## 实机回归

验证日期：2026-11-14。Windows 11 Enterprise x64，i7-10700F（8 核/16 线程）、
32 GiB RAM、RTX 3060 12 GiB，驱动 610.88。Unity 2021.3.45f2；
IL2CPP 使用 VS2022 Build Tools 17.14.45 与 Windows SDK 10.0.22621。

| 原 CPU 库与实际 Player | 通过 | 失败 | 跳过 |
| --- | ---: | ---: | ---: |
| Windows x64 Mono | 157 | 0 | 0 |
| Windows x64 IL2CPP | 157 | 0 | 0 |

相同 filter 包含 `LlmOptimizationPlayerTests`、`StreamingDeltaPlayerTests`、
`ColdPrefillPlayerTests`、`ConversationCachePlayerTests`、`WindowsPathPlayerTests`、
`PlayerInferenceTests`、`ExpressPlayerTests`、`PlatformContractTests`。
模型为已有的 SmolLM2-135M-Instruct-MNN 与 Qwen3.5-0.8B-MNN。
覆盖文本/聊天/增量流式、取消与错误恢复、上下文预算、缓存状态、分词、
仿射数值、Express 所有权/GC、文件路径以及平台拒绝策略。
计数包含策略检查，不代表 157 个独立模型或全部媒体/语音管线。
x64 KV 续接仍显式使用 full prefill，不能把缓存状态检查称作 KV 加速通过。

原始 Mono Player 因裁剪后没有 CP1252 编码程序集而无法加载模型；
现在用 Windows `WideCharToMultiByte` 严格转换，保留原有 DOS 短路径回退。
UTF-8 提示词与输出不经过 ACP。码页无法表示且无短路径的目录仍会明确拒绝。
另修复了 Windows Unity 编译器对 recognition-preview 局部变量的 CS0165 错误。

一次与其他 Player 同时运行的 Mono 全套尝试停在 10 项完成后，已保留 minidump；
原因尚未确认。Express 独立运行 2/2 通过，随后串行完整重跑 157/157 通过。
不要将该异常记录成已修复的原生死锁。测试和性能比较按项目、缓存、结果目录隔离，串行运行。

启用官方 OpenCL 的候选 DLL 也在相同硬件、相同 filter 下分别通过 Mono 157/157、
IL2CPP 157/157，0 失败/跳过；CPU 与 Auto 路径均被执行。
随包 Windows 库现启用 CPU/OpenCL；其他 GPU、驱动与模型不继承本次验收。

## 复现

在 Windows 安装 Unity Windows Build Support（Mono 与 IL2CPP）、VS2022 C++ 工具和 SDK。
把当前包同步到项目 `TestArtifacts~/` 中的独立 Unity 项目；为该项目设置已有模型根目录，
不要自动下载权重。管理员 SSH 服务只用于远程执行，不是包运行依赖。

使用 Python 3 从仓库根目录调用 `Tools~/Testing/run_platform_tests.py`，指定：

- `--unity`：Unity.exe 的绝对路径。
- `--project`：仓库 `TestArtifacts~/` 下已准备的独立项目。
- `--platform StandaloneWindows64 --backend Mono2x`，然后以另一个 label 执行 `--backend IL2CPP`。
- `--test-filter`：上面八个类名用 `|` 连接。
- `--label-suffix`：本次唯一标签，保留以前的失败与通过记录。

LLM fixtures 读取 `MNN_TEST_MODEL_ROOT`；缓存读取 `MNN_INFERENCE_CACHE_DIRECTORY`。
独立结果回调在明确设置的 `MNN_PLAYER_RESULTS_DIRECTORY` 写入完成的
`mnn-player-results.xml`、逐项 XML 与当前测试名。平台 runner 只接受本次 build ID
对应的完成结果，Editor 连接超时不覆盖 Player 的实际失败。

## 性能与质量边界

性能比较使用真实 Unity Player；每个配置新建进程，避免 MNN 全局线程池污染线程数对照。
一轮预热不计入结果，后续重复使用相同提示、64 个生成 token 和贪心采样。
检查 UTF-8 分词、Paris、算术答案 `4`、重复 token ID、流式拼接和精确 prompt token 数。
加载延迟单列，主机 working set 不包含独立 GPU 显存。

在上述 RTX 3060 上，Qwen3.5-0.8B 的 298-token 提示、64-token 输出：

| Player | CPU Normal/4 线程 | OpenCL Normal | 默认 Auto | CPU/OpenCL 加速比 |
| --- | ---: | ---: | ---: | ---: |
| Mono | 2186.6 ms | 910.0 ms | 920.3 ms | 2.40× |
| IL2CPP | 2188.7 ms | 916.5 ms | 910.4 ms | 2.39× |

均为一次预热后五次测量的中位数，每种配置独立进程。
CPU 与 OpenCL 的首个文本约为 864–869 ms 与 143–144 ms。
默认 Auto 实际选择 OpenCL，并通过同样的质量门槛。
CPU 2/4/8/16 线程、Low/Normal 与权重重排的额外矩阵没有证明增加线程或重排带来稳定收益，
因此保留现有 CPU 线程策略。第一轮 GPU 首次驱动编译约 4.1 秒，后续独立进程加载约 1 秒；
这些加载时间不计入上表，也不是清除操作系统与驱动缓存后的冷启动保证。
可复现基准源文件及运行方法见[Windows benchmark 工具](../Tools~/Testing/Windows/README.md)。

原始无 system 消息的算术提示在 SmolLM2 两个后端均返回 `2 + 2 is 5`；
该质量门槛失败的批次保留，不计为优化验收。集成回归通过不意味着小模型具备可靠算术能力。
Qwen3.5-0.8B 的同一门槛通过，性能结论仅限明确记录的模型、硬件与工作负载。

## NVIDIA 后续调优

`MNNLlmPerformanceOptions.OpenClTuningMode` 独立选择 `None`、`Heavy`、`Wide`、
`Normal` 或 `Fast`，默认仍为 `Wide`。不要再用 `ThreadCount` 传 GPU 标志；
该属性始终保留 CPU 线程数，CPU 回退也使用该数值。官方 MNN LLM 会自行加入
OpenCL buffer 模式。组合调优标志、内存模式位和零值会被拒绝。
持久缓存将 GPU 调优级别纳入身份，避免不同调优级别混用同一缓存槽。

```csharp
var performance = new MNNLlmPerformanceOptions
{
    BackendType = MNNBackendType.OpenCL,
    ThreadCount = 4,
    OpenClTuningMode = MNNGpuTuningMode.Wide,
    PrecisionMode = MNNPrecisionMode.Normal
};
```

2026-11-14 在同一 RTX 3060/610.88 上直接查询 OpenCL 3.0 能力：
扩展列表没有 `cl_khr_fp16`，`CL_DEVICE_HALF_FP_CONFIG` 查询不支持。
官方 MNN 后端因此将计算精度选为 FP32；请求 `Low` 不会解锁 FP16 或 Tensor Core。
上述 OpenCL 加速数据也不能视为 CUDA 或 Tensor Core 验证。
官方 Windows CUDA 构建使用另一套工具链；当前 GNU C++ ABI 的托管调用不能直接
替换为 NVCC/MSVC DLL。CUDA 路径需要另行落实 ABI 契约和独立实机验收。

新增配置检查在真实 Mono、IL2CPP Player 上分别验证五档 GPU 标志、CPU 回退、
非法位拒绝和缓存隔离；它们不加载模型，不属于新增推理性能验收。
本轮性能测试因其他服务占用约 11 GB 显存而被工具拒绝，未停止该服务，
没有据此更改默认调优级别，也没有新增加速比结论。
下一步应在空闲 GPU 上比较短/长提示词、五档调优、FlashAttention 与 prefill 分块，
同时保留正确性、token ID、加载耗时和显存/功耗轨迹。

本地证据、命令、stdout/stderr、退出码、DLL/source hashes 与操作记录位于
`TestArtifacts~/WindowsAcceptance20261113/`，不随 UPM 包发布。
