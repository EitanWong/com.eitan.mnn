# 统一 API 层

应用、Studio 和 Unity 组件都通过包的托管公共 API 使用能力。开发者引用 `MNN.Unity`，
使用 `MNN.Unity` 命名空间；按任务选择下面的入口。公开类型、重载、参数默认值与源码说明
汇总在自动生成的[公共 API 参考](PublicAPI.md)，机器可读清单为 [api-surface.json](api-surface.json)。
已有类型、组件、程序集名称和 Unity GUID 保持兼容。

## 按任务选择入口

| 任务 | 公共入口 | 底层负责的工作 |
| --- | --- | --- |
| 平台与启动 | `MNNPlatformSupport`、`MNNRuntime.Initialize` | ABI 可用性说明、主线程环境捕获 |
| 通用图推理 | `MNNInterpreter` → `MNNSession` → `MNNTensor` | 原生资源所有权、会话、后端选择、张量编组 |
| 后台加载 | `MNNModelLoading.LoadAsync` | 工作线程加载、取消后的结果释放 |
| 模型获取与安装 | `MNNModelCatalog`、`MNNModelDownloader`、`MNNModelInstallation`、`MNNModelDelivery` | 发现、下载、检查点、校验、交付与本地可读路径 |
| 本地模型检查 | `MNNModelSupport`、`MNNModelRevisions`、`MNNModelSources` | 按 `MNNModelKind` 检查资产、统一版本策略、精选仓库标识 |
| 文本与多模态 | `MNNLlm`、`MNNLlmPerformanceOptions`、`MNNLlmCache` | 配置、生成、流式更新、独占缓存租约 |
| 检索 | `MNNEmbedding`、`MNNBertEmbedding`、`MNNReranker`、`MNNImageEmbedding` | 分词、向量、查询编码与重排 |
| 语音识别 | `MNNSpeechRecognizer`，或各模型的 Recognizer | 按已安装协议选择前端、分段、识别结果 |
| 语音合成 | `MNNSpeechSynthesizer`、`MNNSpeechSynthesisOptions` | 前端选择、音色、参考录音和合成 |
| 音频与声学 | `MNNWaveAudio`、`MNNAudioResampling`、`MNNAcousticFrontend` 及具体音频任务 API | WAV、重采样、增强、VAD、轮次、说话人与回声处理 |
| 视觉与图片生成 | `MNNYoloV8`、`MNNPaddleOcr`、`MNNImageTask`、`MNNDocumentLayout`、`MNNStableDiffusion`、`MNNSana` | 任务输入、图执行、后处理和结果 |
| 实时会话与 Agent | `MNNConversationRuntimeFactory`、`MNNRealtimeSession`、`MNNConversationRuntime`、`MNNAgentTrace` 等 Agent 契约 | 阶段装配、会话、取消、历史、工具与权限注入 |
| Unity 生命周期 | `MNNModelLoader`、`MNNInferenceComponent`、实时设备/会话组件 | 在公共 API 上适配 Inspector、设备、事件和生命周期 |

每个模型仍可提供专用公共 API，供需要模型特定参数的应用使用。统一层按领域组织，
避免让一个全局对象拥有所有模型、设备和可变会话。新增任务也遵守相同的边界。
Harness 的公共命名空间和程序集保留历史 `MNN.Samples.SiriAI.Harnesses` 身份，
实现归包的 `Core/Language/Agent/Harnesses`；应用通过这些公共契约接入。

## 权限与依赖方向

```text
应用 / Samples / Editor / Runtime/Components
                    ↓ 公共契约
             Runtime/Core 托管 API
                    ↓ 内部实现
        内部模型协议、图适配、缓存和数值算法
                    ↓
      Runtime/Interop 与 Interop/Handles（internal）
                    ↓
          随包官方 MNN 二进制与系统 API
```

`Runtime/Core` 按 Inference、Models、Language、Retrieval、Vision、SpeechRecognition、
AudioProcessing、Generation 和 Common 归类。公共任务类型拥有输入校验、模型策略与资源生命周期；
内部实现类型及方法保持 `internal` / `private`。公共签名不包含原生指针、SafeHandle 或 Interop 类型。

Editor 不再拥有 Runtime 的 `InternalsVisibleTo` 权限。测试程序集的友元权限用于测试内部行为；
可选 Burst 程序集的友元权限仅服务其内部内核注册。应用程序集没有内部访问权限。
Core 与组件保留原来的程序集身份。检查器把组件临时作为独立消费者编译，验证构造函数、
属性访问器和普通方法的真实权限：组件只能使用公共 API，不能访问 Interop；语义检查禁止 Core 反向依赖组件。

## 开发者调用示例

在 Unity 主线程初始化后，再启动后台加载。Player 启动会自动捕获环境；自定义 Editor 宿主可显式调用：

```csharp
using MNN.Unity;

MNNRuntime.Initialize();
using (var model = await MNNModelLoading.LoadAsync(
    () => MNNInterpreter.CreateFromFile(modelPath), cancellationToken))
using (var session = model.CreateSession())
{
    session.GetInput().CopyFromArray(inputValues);
    await session.RunAsync();
    float[] result = session.GetOutput().CopyToArray<float>();
}
```

张量的数据类型和形状必须与模型一致；输入/输出张量的有效期受会话所有者限制。
获取、准备模型目录后，可直接使用语音任务 API：

```csharp
MNNSpeechModelKind kind = MNNSpeechSynthesizer.DetectModelKind(modelDirectory);
string[] voices = MNNSpeechSynthesizer.InstalledVoices(modelDirectory);
using (var speech = await MNNModelLoading.LoadAsync(
    () => MNNSpeechSynthesizer.Load(modelDirectory,
        new MNNSpeechSynthesisOptions { Voice = selectedVoice }), cancellationToken))
{
    MNNGeneratedAudio audio = await System.Threading.Tasks.Task.Run(
        () => speech.Synthesize(text, cancellationToken), cancellationToken);
}
```

`DetectModelKind` 仅识别协议；`MNNModelSupport.IsComplete` 检查本地布局/记录大小。
这两项不下载文件、不执行推理，也不代替 SHA 完整性、平台或精度验证。
参考录音、语言、工具依赖和各模型限制仍由任务 API 检查。
模型目录传输、交付和取消示例见[模型获取](ModelAcquisition.md)与[模型交付](ModelDelivery.md)。

## 线程、所有权与错误

- Unity 对象、纹理、设备和 `MNNRuntime.Initialize` 在 Unity 主线程使用。
  `LoadAsync` 的工厂必须线程安全，且不能访问 Unity 对象。
- 应用拥有自己创建的模型、会话、缓存租约和取消源。先等待进行中的操作结束，
  再释放会话和模型；先释放模型，再释放其缓存租约。遵守具体任务的串行调用约定。
- `CancellationToken` 在托管边界与图调用之间生效，不能抢占正在执行的原生 kernel。
  `LoadAsync` 在加载完成后发现取消时会先释放结果，再报告取消。
- 下载进度、模型生成等回调可能来自工作线程，更新 Unity UI 前切回主线程。
  `MNNGenerationUpdate` 可用于自定义 provider，生成 token 数必须非负。
- `MNNGeneratedImage` 要求正尺寸和恰好 `width × height × 3` 个 RGB24 字节，调用方转交数组所有权。
  `MNNAudioResampling` 返回独立数组；静态音色/样式查询返回快照，修改快照不会改变共享表。
- 参数错误在托管边界抛出 `ArgumentException` 系列异常；文件问题和不支持的平台/任务保持具体异常。
  上层决定重试与展示方式，不捕获后伪造成功结果。

本层沿用已有平台 ABI 约定，未新增 C++、桥接库、自定义原生导出或修改官方源码。
它是本包的托管 API，支持范围见[平台限制](DesktopMono.md)和[测试说明](Testing.md)，
不能据此推断为 MNN 官方提供的可移植 C# API。

## 从源码持续更新文档与边界检查

在对应 C# 类型/成员上维护 XML `summary`，再从工程根目录运行：

```sh
python3 -B Packages/com.eitan.mnn/Tools~/Validation/validate_public_api.py \
  --unity /Applications/Unity/Hub/Editor/2021.3.45f2/Unity.app/Contents/MacOS/Unity --write
```

工具使用已安装的 .NET 8 SDK/Roslyn 和 Unity 元数据引用，无 NuGet restore 或自动下载。
它从真实公共符号生成签名、重载、默认值与已有 XML 说明，内部成员不会进入参考。
没有 XML 说明的旧 API 保留准确签名，说明覆盖数量写入报告；不会自动编造行为说明。
提交生成的两个文件后，用相同命令去掉 `--write` 检查文档是否与源码一致。
`--self-test` 验证检查器会拒绝原生类型泄漏和反向依赖。

检查器验证当前 Unity 2021.3 Editor macOS Mono 配置及独立 Harness 程序集。
可选 Burst 和其他平台的编译/实际运行使用已有平台验证流程；生成的文档不会代替这些结果。
CI 的统一 API 步骤持续检查权限、依赖方向与生成文档；发布前还须运行逐程序集编译和相关行为测试。
