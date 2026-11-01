# Unity 组件与菜单

所有包组件都在 `Add Component > MNN` 下；同名入口也在 `GameObject > MNN` 与
Hierarchy 右键菜单。选中场景对象后直接添加，支持多选；没有场景选择时在当前 Stage 创建
新对象。命令跳过已有同类型组件和不可编辑对象，整个操作可一次 Undo。
Project 中的 prefab 资产不会被直接修改；请先进入 Prefab Mode 再添加。

| 分类 / 菜单名 | 组件 | 职责 |
| --- | --- | --- |
| Models / Model Loader | `MNNModelLoader` | 单模型路径、获取和 Interpreter 生命周期，发布加载事件 |
| Models / Model Manager | `MNNModelManager` | 应用级模型注册表、共享已加载实例与统一卸载 |
| Inference / Inference Component | `MNNInferenceComponent` | 消费 Loader 的 Interpreter，维护推理 Session 和纹理输入输出 |
| Realtime / Audio Device | `MNNRealtimeAudioDevice` | 有界 PCM 采集端口和 Unity 流式播放回调 |
| Realtime / Session | `MNNRealtimeSessionComponent` | 接管已注入的实时 runtime，负责启动、中断和异步停止 |
| Realtime / Unity Microphone Capture | `MNNUnityMicrophoneCapture` | Unity 麦克风采集适配，将 PCM 写入 Audio Device 的采集端口 |

单个纹理推理对象从 `Inference Component` 起步，会自动添加 `Model Loader`。
`Model Manager` 用于需要跨对象共享模型的应用，和 Loader 是两种所有权入口；不要让两者
分别加载同一份模型并期待自动共享。组件复用 Core 推理 API，不各自实现模型计算。

实时播放和麦克风采集各使用独立 GameObject / AudioSource，避免同一个 AudioSource 同时
承担录音环形缓冲和响应播放。Audio Device 本身不打开麦克风、不执行模型、不提供 AEC；
Session 不负责设备采集；Microphone Capture 不决定对话轮次。这些组件通过 Core 的
runtime、PCM 端口和输出管线组合，添加组件后仍需由应用配置连接。
Session 组件的控制入口要求主线程；Core 会话在发布生命周期任务后异步调用 runtime。
runtime 自行将设备和模型操作派发到各自拥有者，不应假设这些回调在 Unity 主线程执行。
Unity Microphone 适配属于原型路径，不能据此宣称全平台低延迟或声学回声消除已经验收。

实时路线选择属于 Core 的 `MNNRealtimeRouteSelector`，不另建 MonoBehaviour。
`SelectBest(model, device, tts, asr, vad, excludedRoutes)` 按原生双工语音、原生双工文本/TTS、
轮次端到端语音、Omni 文本/TTS、VAD/ASR/文本 LLM/TTS 的顺序预检。
`selection.RequireSelected()` 返回已有 Session 组件 `Configure` 接受的能力报告；
全部不支持时返回各路线缺项，不创建资源、不替换所选模型。
按轮 Omni 音频/TTS 与级联路线都检查 VAD；级联另检查主模型文本输入和 ASR 能力。
所有路线仍检查合法调用、取消/恢复、设备和流式 RTF。
选择器只做决策；`MNNRealtimeFallbackRuntime` 将启动及打断恢复阶段的切换接入同一个 Session。
应用提供 `IMNNRealtimeRuntimeFactory`，以 `sessionComponent.Configure(fallbackRuntime)` 注入。
只有匹配当前路线的 `MNNRealtimeRouteUnavailableException` 才允许降级；先停止、全局静止与释放旧
runtime，再准备下一条路线，复用同一输出流水线。每条路线在该会话生命周期最多一次，不重放旧回复。
取消、公共故障、未知异常或清理失败终止切换；正在生成时的任意故障仍通过 `ReportFaultAsync` 停机。
`Preflight` 跟随最新选择，准备中不表示运行；路线耗尽时为 null，须同时读取 `State` / `Fault`。
具体模型/设备 factory 尚未接入，不意味着已有可直接启动的真实对话后端。

以下是控制层装配片段，参数需由应用的实际后端提供，不会自动加载或认证模型：

```csharp
var fallback = new MNNRealtimeFallbackRuntime(
    factory, output, selectedModelCapabilities, deviceCapabilities,
    ttsCapabilities, asrCapabilities, vadCapabilities);
sessionComponent.Configure(fallback);
await sessionComponent.StartSessionAsync(cancellationToken);
// User interruption: await sessionComponent.InterruptAsync();
// Shutdown: await sessionComponent.StopSessionAsync();
```

`factory` 实现 `IMNNRealtimeRuntimeFactory.CreateAsync`，按传入报告返回全新、未准备的 runtime，
成功返回即转移唯一所有权；抛异常时自行清理尚未转移的资源。创建阶段不启动采集或生成。
Prepare 支持部分初始化后的清理，并恢复应用持有的已提交用户历史和输出 Ledger 的已呈现完整段；
未呈现或只呈现部分的段不恢复为完整已说文本。跨设备实例复用输出时保持 PCM 格式和全局呈现帧
时间轴，旧 runtime 的全局静止完成前排空最终回执并提交可信呈现水位。
这些是具体 provider 的实现要求，托管生命周期检查不能替代真实后端验收。

导入 **Basic Inference** sample 后，`MNN > Examples` 增加三个独立教学入口：
`Basic Status Example`（版本与状态）、`Inference Example`（张量推理）、
`Unified Inference Example`（纹理推理）。它们用于展示 Core API，不是额外的生产推理层。
sample 菜单放在独立 Editor 程序集中，复用 `MNNComponentMenu.AddToSelection<T>` 的
选择、Stage 与 Undo 行为；无需给 Runtime 增加 Editor 依赖。

仓库的 SiriAI 示例统一位于 `MNN/Examples/Siri.AI`：Add Component 提供 `Chat`、
`Unity Audio`、可选的 `EasyMic Audio`、`Renderer`；GameObject 与 Hierarchy 右键提供 `Chat Scaffold`、
`Unity Audio`、可选的 `EasyMic Audio`、`Renderer`。创建脚手架时自动优先选择已安装的 EasyMic 集成，
没有时复用 `MNNUnityMicrophoneCapture` 与 `MNNRealtimeAudioDevice`。
Chat、Session、音频管理、输入和输出使用分离对象，通过序列化引用组合，核心示例程序集不依赖 EasyMic。
`MNNUnityMicrophoneCapture.StartCaptureNegotiated` 可在启动 DSP callback 前按实际通道数协商格式；
`MNNRealtimeAudioDevice.CaptureEnabled` 门控入队，`RenderedOutputFrames` 统计渲染源帧，不能作为扬声器呈现证明。
Renderer 只负责可视化，不负责模型、录放或会话；
示例不会在安装包时自动导入。

源码按职责放在 `Runtime/Components/Models`、`Inference`、`Realtime`；移动时保留原有
脚本 GUID，已有场景和 prefab 的组件引用保持不变。分类入口只影响编辑体验，真实推理支持
范围和验证结果见 [测试说明](Testing.md)。
