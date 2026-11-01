# DTLN AEC 回声消除

DTLN AEC 128、256、512 已接入 Model Manager 和 Studio。三个规格代表 LSTM
单元数；都使用 16 kHz 输入、512 点分析块和 128 点步长（8 ms）。一次处理需要
麦克风录音和同步的扬声器播放参考，输出保留上游模型的 384 个采样（24 ms）延迟。

## 安装与 Studio

1. 打开 `Window > MNN > Model Manager`，搜索 `DTLN`、`AEC` 或 `回声消除`。
2. 选择一个规格并下载：
   [128](https://modelscope.cn/models/EitanWang/DTLN-aec-128-MNN)、
   [256](https://modelscope.cn/models/EitanWang/DTLN-aec-256-MNN)、
   [512](https://modelscope.cn/models/EitanWang/DTLN-aec-512-MNN)。
3. 从 `Open in Studio` 进入 `Echo cancellation (AEC)`，选择麦克风 WAV 和播放参考
   WAV，点击 `Cancel echo`。结果可以播放、停止、保存 WAV。

下载计划固定发布 revision，并逐文件校验 SHA-256。只下载两阶段 `.mnn` 和上游
`LICENSE`，生成 `mnn_dtln_aec.json` 及安装标记；不下载 original、TFLite、ONNX、
数据集或转换依赖。默认目录为 `Assets/StreamingAssets/MNN/Models/DTLN-aec-{units}-MNN`。
即使服务端尚未索引或本地 catalog 缓存较旧，三个固定 profile 也可搜索。

两路音频须等长、同步且为 16 kHz。数组 API 接收单声道归一化 PCM，WAV 解码器也接受
双声道并取均值；建议使用单声道文件以明确参考信号。不会自动重采样、估计设备延迟、
归一化音量或剪裁输出。播放参考应来自实际发送给扬声器的信号，单路麦克风录音不足以做 AEC。

## C# API

```csharp
using MNN.Unity;

// Use a filesystem directory prepared by Model Manager/runtime model delivery.
using (var aec = MNNDtlnAec.Load(modelDirectory))
{
    // Equal-length normalized mono PCM at 16 kHz; run on a worker thread.
    float[] cleaned = await aec.ProcessAsync(microphone, playbackReference);
}
```

连续流使用 `ProcessHop(microphone128, reference128, output128)`，缓冲区由调用方复用。
同一实例保留两阶段 LSTM 状态和 overlap-add 缓冲区；切换到无关录音时调用 `Reset()`。
加载、推理和锁操作应放在工作线程，不能直接在 `OnAudioFilterRead` 等实时回调中运行。
离线 `Process` / `ProcessAsync` 在开始和结束时重置状态，最长十分钟，输出长度与输入相同，
遵循上游的补零、帧数和裁剪规则，包含非 128 整数倍的尾部处理。
`ProcessWaveFiles` 提供同步 WAV 文件入口。取消在帧之间检查；不能中断正在运行的原生算子。

当前 profile 默认 **CPU / High**、一个线程。`Auto` 选择 CPU。显式 Metal 使用经过源参考验证的分段图：归一化保留 CPU，两阶段循环网络/解码器交给 Metal；需要额外四张 `_front.mnn`/`_core.mnn`。旧的仅双图下载 profile 继续支持 CPU，尚不包含这些 GPU 文件。NNAPI 请求明确拒绝。
沿用包中限定 MNN 3.6.1/编译器 ABI 的官方 C++ API 绑定，没有新增 C++ 或自定义导出。
基础绑定的平台范围见 [MobileInterop](MobileInterop.md)；本次 DTLN Unity 实际权重验证为
M5 Mac 上的 Unity 2021.3.45f2 ARM64 Editor，不能替代 Android/iPhone Unity Player 验证。

## 包内 FFT 与可选 Burst

`MNNFftPlan` 是共享的 C# 原地 radix-2 复数 FFT，支持 2–65536 的二次幂。
计划预计算位逆序和旋转因子，变换不创建托管缓冲区，正变换不缩放，逆变换除以长度。
DTLN 将两路实信号打包到一次复数 FFT，从共轭对恢复各自频谱，再执行一次逆变换；
每帧共两次 512 点变换。频谱转 float32 后送入模型，保持上游 complex64 取整规则。

基础 `MNN.Unity` 不引用 Burst、Collections、Mathematics、Jobs 或外部 DSP 库。
项目**已经安装 Burst 1.8+** 时，独立 `MNN.Unity.Burst` 程序集通过版本约束启用，
启动时同步编译并缓存完整 FFT 的函数指针。未安装或启动时禁用 Burst 则运行相同的 C#
指针内核，Mono/IL2CPP 均无需另一套库。`MNNFftPlan.IsBurstAvailable` / `UsesBurst`
可查看选择，`PreferBurst = false` 可强制托管内核。MNN 包不自动安装任何可选性能包；
Burst 自身的传递依赖由 Unity 管理，基础 Runtime 不引用它们。

在本机预热后的 512 点复数 FFT 测量中，C# 约 10 µs、Burst 约 2 µs，二者托管分配均为 0。
这是 DSP 内核加速；默认神经网络由 MNN CPU 后端执行，显式 Metal 的神经网络核使用分段 GPU 路线。不同设备、Editor/Player 和编译器的
结果需重新测量。

实现选型查阅了 [Unity 官方函数指针说明](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/csharp-function-pointers.html)、
[NAudio 的 C# FFT](https://github.com/naudio/NAudio/blob/master/NAudio.Core/Dsp/FastFourierTransform.cs)
及 [DTLN 上游处理脚本](https://github.com/breizhn/DTLN-aec/blob/master/run_aec.py)。包内独立实现
FFT 算法，没有引入这些项目的二进制或包依赖；没有添加模型不需要的窗函数或重采样。

## 验证范围

本地证据位于开发仓库 `TestArtifacts~/DTLNAecIntegration/`，不随 package 发布。

- FFT 对照独立 O(N²) DFT，并检查逆变换、无效输入和预热后零托管分配。
- 128/256/512 连续流分别对照独立 Python MNN 3.6.1 CPU / NumPy FFT 参考，
  检查 131072 个采样；输入来自 AEC Challenge 公开数据。
- 三个规格分别检查双讲、远端单讲、近端单讲和非整帧尾部，完整波形逐采样对照。
- 检查状态重置、取消、异步输入快照、释放和非法采样率/PCM。
- Model Manager 的固定 revision、下载内容、哈希验证、已校验文件复用及 Studio
  双音频准入、真实推理和 WAV 输出均有专项测试。安装测试使用本地已有的发布文件，
  不再次下载模型；不冒充新的一轮公网传输测试。

托管与 Burst 两种配置分别运行。连续流最大误差低于 5×10⁻⁷，预热后完整 `ProcessHop`
链路零托管分配。约 8.19 秒的三个片段验证集不等于完整数据集、全部设备或主观音质认证。
此前转换项目的 TFLite/MNN 与 AECMOS 数据集结果属于独立证据，不能扩展成每个 Unity
平台或每个规格的音质保证。

2026-10-30 新增官方 TFLite 独立参考：21/21 Editor 测试（3规格、10条件/重复、32帧两阶段输出和状态、CPU/Metal零分配与耗时）及8/8流式/Studio回归通过。原 `run_aec.py` 使用LiteRT2.3内置FP32且禁用默认delegate；不把Python MNN自身当作独立原模型oracle。三个分段TFLite的两阶段输出/状态与原图逐位一致。全Metal归一化与FP16权重因实际源参考误差被拒绝。细节、主机耗时与Player状态见各模型README。
