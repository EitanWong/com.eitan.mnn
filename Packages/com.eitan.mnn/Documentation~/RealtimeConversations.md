# 共享实时对话

`MNNConversationRuntime` 位于 `Runtime/Core/Language/Realtime`。它由 SiriAI 的实时对话核心提取而来；SiriAI 和 MNN Studio 使用同一个实现。应用层负责设备、模型选择和界面，无需再写一套录音轮次、识别、生成和播报循环。

## 组合会话

以下代码在设备已经协商 PCM 格式、绑定 `output` 后执行。`capture` 是 `IMNNAudioCaptureSource`；Unity 应用可使用 `MNNRealtimeAudioDevice` 和 `MNNUnityMicrophoneCapture`。具体设备必须持续消费输出并提交播放回执。

```csharp
MNNRealtimeSession session = null;
var ports = new MNNConversationPorts(capture, () => session);
var providers = new MNNConversationProviders
{
    Model = MNNConversationMnnProviders.Language(chatDirectory, maximumTokens: 256),
    Asr = MNNConversationMnnProviders.Recognition(asrDirectory),
    Tts = MNNConversationMnnProviders.Speech(ttsDirectory)
};
var runtime = new MNNConversationRuntime(ports,
    new MNNConversationOptions(), output, MNNRealtimeRoute.CascadedSpeech, providers);
session = new MNNRealtimeSession(runtime, output);
await session.StartAsync();
ports.SetRealtime(true);
```

引擎在工作线程加载模型和运行推理。开始之前用 `MNNRealtimePreflight.Check` 校验模型、ASR、TTS、VAD 和设备能力。当前本地适配器使用逐轮任务 API 和完整语音波形，需要显式允许 `allowBufferedSpeech: true`、`requireMeasuredSpeech: false`。能量 VAD 的声明由 `MNNConversationMnnProviders.EnergyCapabilities` 提供。持续采集并不表示模型具有原生连续理解能力。

自定义本地或远程 provider 实现 `IMNNConversationTextProvider`、`IMNNConversationAsrProvider`、`IMNNConversationTtsProvider`；可选接口支持增量识别、VAD、轮次检测、意图判断、模型直接生成语音和响应路由。`IMNNConversationContext` 是声学前处理、说话人选择、唤醒交接和通知的扩展边界。SiriAI 通过这个边界保留其现有声学和混合路由装配。

## 主线程与音频线程

- 主线程协商麦克风，轮询 `ports.TryRead` 更新界面。文本更新按相邻同一响应合并，用户轮次保持 FIFO 顺序；工作线程不会为每个 token 创建完整显示字符串。
- 采集端持续向有界 PCM 队列写入。采集、VAD、增量识别、轮次判断与生成各自拥有工作状态。丢帧会使受损语句失效，并要求用户重说。
- 输出设备的唯一音频回调调用 `output.Render`。现有 Runtime 设备的 DSP 回调不分配内存、不等待模型。
- 主线程排空回执并确认实际呈现位置。Unity 的 DSP 缓冲只能提供估计，应传 `estimated: true`。硬件和操作系统额外延迟仍需设备实测。

没有健康的回声控制时，默认上下文采用半双工解释策略：采集保持运行，播报期间抑制自动语句解释，并保留恢复窗口。`CanInterruptSpeech` 只能在应用已具备可靠隔离或回声处理时启用。用户按钮始终可通过 `session.InterruptAsync()` 立即关闭旧输出，再异步取消、等待并恢复模型上下文。

## 文本、历史与关闭

输入关闭时也可调用 `runtime.SubmitTextAsync` 或提交单声道 16 kHz 的 `SubmitAudioAsync`。新的请求不得排队在仍运行的响应后面；调用方先中断并等待旧响应，或等它完整生成和播放。语音历史只提交已确认呈现的片段；完成的纯文本响应直接进入历史。清空前关闭输入并等待当前响应，然后调用 `ResetConversationAsync`。

构造时复制 options、语言列表和 provider 注册。会话拥有传入的 provider；同一实例注册多个职责只释放一次。不要把同一可变 provider 交给并行会话。`Prepare`、`Release` 钩子用于已有注册/诊断系统，必须遵守相同所有权。

闲置退出由引擎按单调时钟、输入 revision、有效语句和回复活动统一判断，默认 60 秒；设 `realtimeInactivitySeconds = 0` 可关闭。应用消费 `ports.TakeRealtimeExit()` 后停止或退出实时模式。自主管理连续会话的 provider 可复用 `MNNConversationInactivity`。

停止顺序：关闭输入发布和输出门，停止物理采集/播放，`await session.StopAsync()`，成功后销毁设备。停止会等待模型任务退出，再释放 provider。原生调用的取消只能在现有 API 的检查点生效；关闭窗口不会在主线程等待该调用，也不会在它运行时释放句柄。若等待或释放失败，应保留资源所有者并允许重试。

## 术语

| 术语 | 含义 |
| --- | --- |
| Port / 上下文端口 | 应用提供的采集、输入状态、声学和界面边界 |
| Provider | 一个模型/远程服务职责及其资源所有权 |
| Turn / 用户轮次 | 经端点、识别与可选语义校验接受的一句话 |
| Response / 响应 | 一个输出 epoch；中断后旧 epoch 无法写入新回复 |
| Presented / 已呈现 | 设备已确认播放的片段，可进入后续对话历史 |

本轮只改变托管 C# 编排与 Editor 适配，没有新增 C++、C ABI 桥或修改官方 MNN。托管回归和布局/编译检查不代表验证了官方 C++ 直接调用、所有设备实时吞吐或端到端语音延迟。
