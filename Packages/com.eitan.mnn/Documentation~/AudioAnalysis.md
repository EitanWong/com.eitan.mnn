# 音频分析与动画驱动 API

`MNN.Unity` 中的托管 API 可独立于模型、音频设备和 Unity 场景使用。
SiriAI 的麦克风输入与实际播放语音复用这套实现，示例自己的公开类型保留为兼容适配。
开发者可以将同一组测量用于光带、光核、角色、仪表或其他交互。

## PCM → 特征 → 包络 → 表现

```csharp
using MNN.Unity;

// 每路音频创建一个分析器；持续复用，不能让多个写入线程共用。
var analysis = new MNNAudioAnalysis();
var envelope = new MNNAudioEnvelope(attackSeconds: .065f, releaseSeconds: .18f);

// 在音频生产者侧：PCM 为归一化 float，立体声等格式按通道交错。
MNNAudioFeatures frame = analysis.Analyze(pcm, pcm.Length, channels: 1, sampleRate: 16000);

// 将不可变的 frame 通过应用自己的线程同步交给表现层。
// 在消费端按它自己的时钟推进；deltaTime 单位为秒。
float strength = envelope.Update(frame.GetEnvelope(sensitivity: 2f), deltaTime);
// strength 可映射振幅/速度，frame.LowBand、HighBand、Onset 可用于其他视觉参数。
```

输入为 `float[]`，采样率支持 8000–192000 Hz；不会改写输入。
`Analyze(pcm, offset, count, channels, sampleRate)` 支持缓冲区片段，offset/count
以标量采样数计，必须覆盖完整通道帧。格式切换会自动清空历史；断流、切换源或跳转后
调用 `analysis.Clear()`，避免旧音高混入新片段。空块应跳过分析，并给表现层零强度。
有限 PCM 样本被限制在 [-1,1]，NaN/Infinity 视作静音。RMS ≤ .002 的块输出零特征。

| 特征 | 含义与范围 |
| --- | --- |
| `Rms` / `Peak` | 下混为单声道后的均方根与绝对峰值，0–1 |
| `LowBand` | 约 250 Hz 低通能量占比，0–1 |
| `HighBand` | 约 1.6 kHz 低通以外的残差能量占比，0–1 |
| `PitchHz` / `PitchConfidence` | 70–450 Hz 周期音高估计与置信度；短窗口或不确定时为零 |
| `Onset` | 相对近期基线的起音强度，0–1 |

频段为轻量时域滤波估计，不是完整频谱；音高是语音范围内的周期估计，不代替 VAD、
语音识别或情绪判断。相位相反的多声道内容在单声道下混时可能抵消。
`WithGain(gain)` 可调整 RMS/峰值而保留频段比例和音高。

## 平滑与所有权

`frame.GetEnvelope(sensitivity)` 与 `MNNAudioEnvelope.Measure(rms, peak, sensitivity)`
采用同一套静音门限和感知音量映射，既跟随元音能量，也保留有界的辅音峰值贡献。
`MNNAudioEnvelope.Update` 使用无 Unity 依赖的阻尼响应；attack/release 为约 99% 收敛时间，
允许 .02–5 秒，deltaTime 必须有限且非负。`Reset()` 清空包络和速度。
已有状态容器可调用 `MNNAudioEnvelope.Smooth(ref value, ref velocity, target, duration, deltaTime)`。

分析器拥有历史音高窗口，由单个音频写入者使用；包络由单个消费者推进。
每路输入和每路输出使用不同的分析器。`MNNAudioFeatures` 是不可变值，但跨线程发布仍需
调用方使用适合自己的同步机制。分析和包络在构造后无每块分配，也不会启动推理或读取设备。

## SiriAI 接入

SiriAI 的 `SiriAIVoiceAnalysis`、`SiriAIVoiceFeatures` 和 `SpeechEnvelope` 入口继续可用，
内部委托给上述包 API。麦克风特征通过会话驱动器传给 `SetListening`，使 Strands 随用户
输入强弱波动，静音后回到仍有轻微起伏的待机光带。播放语音传给 `SetSpeechFeatures`，
Talking 保留原来的光核形态和随声音变化的内部光效。动画形态、配色和玻璃材质仍属于示例表现层。
