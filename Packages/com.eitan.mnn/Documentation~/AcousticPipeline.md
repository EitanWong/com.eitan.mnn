# 共享声学处理

这些托管API从SiriAI提取，由 `MNN.Unity` 程序集提供。应用无需复制算法，
设备、产品配置和模型选择通过接口/委托装配；不需要新增原生桥或C++。
完整会话使用[共享实时对话](RealtimeConversations.md)，本页介绍可独立组合的声学积木。

## 输入与增强

```csharp
var input = new MNNAcousticInputProcessor(
    new MNNPcmFormat(48000, 2), capacity: 960,
    vad: MNNRealtimeFrameVoiceActivity.Energy(.025f),
    enhance: Enhance512, reset: ResetEnhancer, observe: ObserveFrame,
    enhanceRecognition: true, enhanceVoiceActivity: false);

// 在单个模型worker处理：offset以scalar samples计，frames以声道帧计。
input.Process(interleavedPcm, offset: 0, frames: 960,
    update: OnVoiceActivity, onFrame: OnRecognitionFrame);
// 输入结束后恰好排空一次；包含最多512个采样的增强延迟尾部。
input.Complete(OnVoiceActivity, OnRecognitionFrame);
```

上例中的回调由应用绑定：`Enhance512(float[] pcm, int offset)` 原地处理单声道16kHz、
512采样；`ResetEnhancer()` 重置其模型状态；`ObserveFrame(float[] original, int offset,
float[] enhanced)` 只在调用期间借用原始/增强数据；`OnVoiceActivity(MNNRealtimeVadUpdate)`
接收结果；`OnRecognitionFrame(float[] pcm, int offset, MNNRealtimeVadUpdate)` 借用识别帧。
不需要增强时传 `enhance: null`，VAD仍可独立工作。能量VAD只是模型无关门限，不能声称
具有神经VAD的资格。

设备输入保持调用者所有，processor不会原地覆盖它。回调不得保留/异步使用借用buffer，
也不得重入processor。`Process`、`Complete` 和 `Reset` 不能重叠调用；任何回调失败后必须
成功 `Reset` 才能继续。成功的 `Complete` 幂等，完成后新流也需要Reset。
模型调用和重采样在worker执行，不能直接塞进Unity DSP callback。

## 回声处理积木

| 类型 | 调用次序与边界 |
| --- | --- |
| `MNNAudioReferenceTimeline` | `Write(firstSample, samples, count)` 保存同一clock的参考PCM；`Read`只能取完整窗口，缺失时清零并false；时间不连续使旧窗口失效 |
| `MNNEchoDelayEstimator` | 输入同长、有限、[-1,1]单声道16kHz PCM；只有 `IsLocked` 才是当前有证据的延迟，未锁定时不能把默认0当作实测 |
| `MNNAdaptiveEchoFilter` | 对齐后先 `EstimateCoherence`，再 `Process`；residual与两路输入分别存储，adaptation∈[0,1]；冻结适应仍保留已学习路径的回声预测 |
| `MNNAecDelayCalibration` | `Add(milliseconds, captureFrame, generation, now, out delay)`接收新鲜快照和单调秒；不同route/generation、抖动或无效值重置证据 |

每个流各自拥有这些对象；不跨并发会话共享可变滤波器/模型状态。FIR有2048 taps，
负责128ms线性尾部；粗延迟估计器处理0–500ms bulk delay。二者不是WebRTC AEC3的
完整实现，也不代替DTLN等神经回声模型。采样对齐、参考流质量、双讲和设备延迟仍需实测。

`MNNAudioReferenceTimeline` 默认32768采样，容量必须为正2次幂；超长write仅保存可见后缀。
公共边界先验证形状/游标，避免异常发生后留下半更新状态。具体DSP PCM入口验证有限且归一化；
reference timeline接受已经过采集边界校验的PCM，不重复扫描每次copy。

## 说话人与呈现

`MNNAudioMixtureContribution.TryEstimate(mixture, first, second, out a, out b)` 根据同clock两源
和混音估计能量占比，允许独立增益、反相与DC；沉默、重复候选或无法解释混音时false，
两个out均为0。没有结果数组分配。`MNNAutomaticSpeakerSelector` 结合占比和前一说话人的
相似度选择候选，身份偏置10秒后过期；它不是声纹模型，分离输出slot不代表身份。

`MNNSpeechFade.FadeOutAsync(response)` 从控制线程请求24ms淡出，单render writer在
`Process`中按完整interleaved frames应用平滑斜坡。新response恢复增益；旧response请求
不会淡出新回复。等待最多500ms避免设备停止渲染使关闭永久挂起；这是控制侧等待上限，
不是硬件静音测量。DSP callback只调用Process，不创建异步请求。

`MNNPlaybackWaveform.Publish` 在fade之后发布实际渲染PCM。UI使用固定64点数组调用
`TryRead`或仅取 `TryReadFeatures`，取得signed peaks、RMS和 `MNNAudioFeatures`。
单writer/单reader的有界快照不等待模型；response不符或超过200ms会返回false、清空输出。
波形用于表现测量，不作为实际播放位置回执；语音历史仍由output pipeline的presentation确认。

## 验证范围

包内 `AcousticCoreTests`、`AdaptiveEchoFilterTests`、`EchoDelayEstimatorTests` 和
`AcousticUtilityTests` 覆盖合成延迟/漂移/沉默、FIR回声与双讲、ring覆盖/缺口/溢出、
原始/增强VAD和ASR四种路由、尾部恰好一次、回调失败/重入/重置、独立源增益、身份过期、
fade连续性、新response、快照与参数边界。

分配测试先验证运行时计数器确实可用；不支持时明确跳过，不把恒0读数当作0分配。
组合hop基准测timeline+delay+coherence+FIR，记录mean/p95/p99，排除设备、神经推理和调度。
没有目标设备的预算与真实音质证据时，不承诺端到端性能已达标。
