# Basic Inference

无需额外 UI、Burst 或 Collections 依赖的托管 API 示例。

1. 在 Unity Package Manager 中选择 MNN for Unity，导入 **Basic Inference** sample。
2. 新建场景和空 GameObject，从 `Add Component > MNN > Examples > Basic Status Example` 添加组件，运行后查看 Console 与屏幕状态。
3. 需要实际推理时，添加 `MNNInferenceExample`，在 Inspector 的 `Model Path` 中设置已有兼容
   `.mnn` 文件的绝对路径；按空格写入随机 float 输入并读取推理输出。
4. 需要纹理输入/输出时，使用 `MNNUnifiedExample`，设置可读纹理与匹配的模型路径和张量形状。

脚本位于 `Scripts`，统一使用 `MNN.Examples` 命名空间，程序集为
`MNN.Unity.Samples.BasicExample`，仅引用 `MNN.Unity`。导入后，`GameObject > MNN > Examples` 和 Hierarchy 右键菜单提供
同名入口；没有场景选择时新建对象，有选择时添加组件，支持多选、去重与 Undo。
这些菜单独立编译进 `MNN.Unity.Samples.BasicExample.Editor`，Player 不引用 Editor。
示例不附带场景或模型，不自动下载权重。
纹理示例不适用于任意形状/类型的模型；具体转换要求见 [API 参考](../../Documentation~/API.md)。

基础推理与移动端入口的支持范围以包说明中的具体编译器/平台 ABI 合同为准。
本会话示例的完整本地语言/ASR/TTS 验收以匹配随包原生库的 macOS 64 位 Mono 为目标；
移动端基础推理/LLM 的既有验证不能推导出此完整语音链路也已验证。
Auto 策略及 CPU、Metal 的实际覆盖见 [加速说明](../../Documentation~/Acceleration.md)。
示例导入后能够编译，不代表目标平台具备完整推理与音频能力。
平台与 ABI 限制见 [包说明](../../README.md) 和 [测试说明](../../Documentation~/Testing.md)。

## AI会话装配

完整独立示例不依赖 SiriAI、EasyMic 或 UI 包：

1. 导入整个 sample，创建空 GameObject，添加 `MNN > Examples > Standalone Conversation Example`。
   `GameObject > MNN > Examples` 菜单也提供此入口。
2. 在 Inspector 填写 `Model Directory`、`Recognition Directory`、`Speech Directory`。
   可填绝对路径，或相对 `StreamingAssets/MNN/Models/` 的已安装模型目录。
   例如本项目已有的 `SmolLM2-135M-Instruct-MNN`、`wo105090--SenseVoiceSmall-MNN` 和 `PiperLessac`。
   不复制或自动下载模型；目录必须包含对应托管 API 支持的配置与权重。
3. 场景保留一个有效 AudioListener。Play 后按 **Start**，允许麦克风权限，等待模型与音频就绪。
   示例会自动建立独立的 Unity 麦克风与 2D AudioSource 子对象；也可在 Inspector 指定独占端点。
4. 使用 **Send text**、**Start recording / Send recording**、**Voice to draft**，或 **Continuous listening**。
   默认使用缓冲语音、半双工：回复时抑制自动语音解释；没有声学 AEC 资格时不声明可靠插话。
   能量 VAD 仅为基础检测，嘈杂环境可换成已实现的模型 VAD registration。
5. **Close / retry cleanup** 在生成期间也可用；禁用或销毁组件立即停止物理端点，随后异步等待模型工作退出。
   正常关闭后可再次 Start；清理失败保留 owner，可重复 Close 重试，并显示错误。
   需要控制对象销毁顺序的应用，应先 `await StopConversationAsync()` 再销毁对象；`Shutdown` 可供外部等待。

`MNNStandaloneConversationExample` 只接 Unity 设备与简洁 OnGUI 界面；
包核心 `MNNConversationApplication` 装配设备会话、阶段工厂、命令、通知与 transcript store；
`MNNStandaloneConversation` 仅保留兼容旧示例 API 的薄构造壳。
采集重采样、录音收尾、推理调度、文本/TTS 管线、取消、播放回执与历史策略都来自包核心。
界面每帧最多消费 64 条通知，只绘制最新 32 条记录；完整历史由有界驻留存储管理，
会话关闭删除临时历史。Editor 临时记录在项目 `TestArtifacts~/StandaloneConversation/` 下，
Player 在 `Application.temporaryCachePath/MNN/Conversation/` 下。

自定义界面可直接调用组件公开的 `StartConversationAsync`、`SendTextAsync`、`BeginRecordingAsync`、
`FinishRecordingAsync`、`SetRealtimeAsync`、`StopConversationAsync`。
需要自定义模型或设备时，直接使用包核心可注入的 `MNNConversationApplication`：

```csharp
var app = new MNNConversationApplication(binding, deviceCapabilities, registrations, transcriptDirectory);
await app.StartAsync();
await app.Commands.SendTextAsync("Hello");
// 每个 UI Update 调用 app.Tick()，从 app.Transcript 读取显示记录。
await app.StopAsync();
```

控制入口和 `Tick()` 必须在同一 UI 线程调用。关闭时等待同一个已发布任务；清理失败不能丢弃 owner。
这是可检查的应用装配示例，不承诺所有上层应用无需生命周期代码。

`MNNConversationExample.Configure` 保留原有 public API，展示 session 组件的最小装配；
`CreateLocalRegistrations` 可单独复用。与完整示例一样显式启用缓冲语音并提供能量 VAD，
不把本地批次推理称为 NativeDuplex、渐进声波流或已达标的实时吞吐。
详见[应用架构](../../Documentation~/ConversationApplicationArchitecture.md)。

## 示例验收与长时间测量

`Tests/Runtime/MNNStandaloneConversationTests.cs` 使用确定性替身与注入设备，走完整共享工厂链路：
录音输入→ASR→文本→TTS→输出渲染→呈现确认→关闭，还覆盖权限取消、清理失败重试与推理中关闭。
`MNNStandaloneTelemetryTests` 保证完成序号能够可靠关联各轮测量。
导入 sample 后测试程序集为 `MNN.Unity.Samples.BasicExample.Tests`，只在测试构建启用，
不会把 NUnit 引入示例运行程序集。替身测试不是真实模型、麦克风、扬声器或音质证明。

`MNNStandaloneLocalModelTests` 是显式资源测试，不自动执行或下载权重。
`LocalTextProducesSpeechAndPresentedReply` 加载真实语言、ASR、TTS provider，检查实际文本/TTS输出；
该测试只输入文本，ASR 只验证准备成功，不验证识别准确率。设备渲染与 25 ms 尾部估计为注入模拟，
不会申请真实麦克风或把回执当作真实扬声器发声。
`RepeatedSessionsMeasureLocalModelSoak` 默认运行 3600 秒，每 5 轮关闭并重建整个应用。

在已有隔离 Unity 项目中导入整个 sample 后，从本仓库根目录运行：

```bash
python3 'Packages/com.eitan.mnn/Samples~/BasicExample/Tools~/run_standalone_acceptance.py' \
  --unity /path/to/Unity --project 'TestArtifacts~/StandaloneUnityProject' \
  --model 'Assets/StreamingAssets/MNN/Models/SmolLM2-135M-Instruct-MNN' \
  --asr 'Assets/StreamingAssets/MNN/Models/wo105090--SenseVoiceSmall-MNN' \
  --tts 'Assets/StreamingAssets/MNN/Models/PiperLessac' \
  --output 'TestArtifacts~/SiriAICompletion/Standalone/LocalSmoke'
```

加 `--seconds 60` 做短时间重复轮次，`--seconds 3600` 做一小时测量；
`--turns-per-session` 控制重启频率，`--backend CPU|Auto|Metal` 指定已支持的后端，
`--prepare-only` 仅检查路径并保存调用配置，不启动 Unity。
工具不会创建或修改隔离项目，不得对正在运行的同一 Unity 项目并发执行。
资源测试显式覆盖 Unity Test Framework 默认的 180 秒超时：单次验收上限为 1800 秒，
长测框架上限覆盖支持的 86400 秒加 1800 秒余量；实际运行另有更早的有界操作期限。
停止或释放测试迭代器会请求取消，`UnityTearDown` 异步等待同一任务完成清理，
不会在主线程同步等待需要主线程执行的后续任务。

产物包含 XML、Unity 日志、每轮 CSV、JSON 汇总、模型路径与后端诊断。
记录首 token、首 PCM、首次非零模拟渲染、呈现完成、启动/关闭时间、10 ms 输出管线 Render 的 p95/p99，
以及约 250 ms 采样的整个 Unity 进程 RSS、托管内存和 Unity tracked allocation 峰值。
Render 耗时是模拟设备回调耗时，不是原生模型 hop；缺少公开精确计数的 native-only 分配标为不可用，
不以 RSS 减托管内存冒充原生模型内存。峰值为观测采样峰值，不是完整分配器高水位。
`--rss-growth-mib` 与 `--managed-growth-mib` 可设置首次完整会话后的增长预算；
未设置预算只报告测量，不能据此宣称性能、内存或一小时稳定性已经达标。
