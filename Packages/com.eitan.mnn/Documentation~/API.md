# MNN for Unity - C# API Documentation

统一分层、权限和调用约定见[统一 API 层](PublicApiLayer.md)；类型、重载和参数的源码清单见[公共 API 参考](PublicAPI.md)。

## Overview

本文档介绍 MNN Unity Package 的 C# API。当前接口直接调用官方 C++ API，
要求随包 MNN 3.6.2 及对应平台的编译器/STL ABI。桌面 Mono 包含 macOS ARM64/x64
（Apple libc++）、Windows x64（MinGW-w64 GCC 16.2）与 Linux x64（GCC 13.3），
核心、Express、LLM、原生检索和媒体输入的系统要求见[桌面 Mono](DesktopMono.md)。
Windows Wine Mono 与 Linux host Mono 各通过 79 项任务断言和 22 项核心断言，
Windows x64 Mono/IL2CPP 真机 Player 各通过 157 项选定回归，0 失败/跳过；
见[Windows Player](WindowsPlayer.md)。Linux Unity Player 尚待验证；Windows 随包库启用 CPU/OpenCL，Linux 保持 CPU 构建。
macOS 当前核心/平台/Express Player 回归两种架构各通过 120/120，Metal 与模型验证范围见[测试说明](Testing.md)。
六类代表模型的独立 Player 推理已在 arm64 和 Rosetta 下的 x86_64 验证；
未在 Intel 真机验证。Android/iOS ARM64 IL2CPP 复用同一套 Interpreter/Session/Tensor、LLM、
Express 和原生检索 partial；编译器契约及逐任务验证边界见 [移动端说明](MobileInterop.md)。
Express/检索的新绑定尚未完成当前源码的移动真机推理验证。

`MNNPlatformSupport.IsSupported` 和 `UnsupportedReason` 可查询当前绑定的 ABI 可用性；
该属性不代表具体模型或 GPU 后端已经通过测试。不支持的平台在原生调用前抛出
`PlatformNotSupportedException`，任务模型加载也会在创建缓存目录前拒绝。

## 文本、多模态与检索任务

`MNNLlm.Load(modelDirectory)` 加载包含配置、tokenizer 和权重的模型目录。
`Generate` / `GenerateAsync` 用于文本，`GenerateMultimodal` / `GenerateMultimodalAsync`
接受文本和单张图片、单个 WAV，使用 `Capabilities` 查询支持的模态。
`generateSpeech: true` 当前在所有平台抛出 `PlatformNotSupportedException`：
尚无满足当前约束的 `std::function` 波形回调编组实现，已移除旧的手工回调对象/虚表实现。
`SpeechOutput` 不再作为可用能力上报；独立托管图 TTS 管线按各自验证范围提供合成。

`MNNEmbedding.Load` 提供 `Dimension`、`Encode` / `EncodeAsync`、`EncodeQuery`；
`MNNReranker.Load` 提供 Qwen3 查询/文档评分 `Score` / `ScoreAsync`。
LLM 默认采用设备/模型自动性能策略，显式参数优先，详见 [LLM 性能配置](LlmPerformance.md)。
Embedding/Reranker 默认 `Auto` 加速策略和 `MNNPrecisionMode.Normal`，
任务兼容例外（例如 Qwen Embedding CPU）见 [自动推理加速](Acceleration.md)，
可通过可选参数 `precisionMode` 选择 Normal、High、Low。每个实例串行执行推理与释放。
`Backend` 报告主模型 runtime，`MediaBackend` 报告图片/音频处理器 runtime。

示例、代表模型、实际测试结果和平台边界见 [真实推理与测试](Testing.md)。

## BERT / MiniLM 向量模型

模型管理器为以下三个 Hugging Face 仓库提供固定 revision、文件大小和 SHA-256 校验的安装方案。
每个方案只下载 MNN 图、分词器和模型说明，安装后生成 `mnn_embedding.json`；不会下载仓库里的 ONNX、GGUF 或其他备选权重。

| 仓库 | 图文件 | 池化 | 最大 token 数 | 维度 |
| --- | --- | --- | --- | --- |
| `darkmaniac7/bge-small-en-MNN` | `encoder.mnn` | CLS | 512 | 384 |
| `developerabu/bge-small-en-v1.5-mnn` | `model.mnn` | CLS | 512 | 384 |
| `darkmaniac7/minilm-l6-v2-mnn` | `minilm.mnn` | Mean | 256 | 384 |

```csharp
using (var encoder = MNNEmbedding.Load(installedModelDirectory))
{
    float[] query = encoder.EncodeQuery("What is the capital of France?");
    float[] document = encoder.Encode("Paris is the capital of France.");
    // Both arrays are independently owned, L2-normalized sentence embeddings.
}
```

`EncodeQuery` 为上述 BGE profile 添加检索指令；MiniLM 不添加。`Encode` 编码文档或原始文本。
Studio 使用同一查询入口。分词器实现 BERT WordPiece、BertNormalizer、BertPreTokenizer，
支持 `tokenizer.json` 或 `vocab.txt`；超过上限时截断并保留 CLS/SEP。
不适用于 SentencePiece、RoBERTa、E5 或任意同名 `.mnn` 图。

其他明确符合协议的图可调用 `MNNBertEmbedding.Load(directory, config)`，使用
`MNNBertEmbeddingConfig` 指定相对图路径、词表、输入/输出名称、维度、池化、归一化及查询前缀。
当前支持 int32/int64 的 `[1,L]` 输入和 `[1,L,D]` / `[L,D]` 的 `last_hidden_state` 输出；
可通过空 `attentionMask` 或 `tokenTypes` 禁用不存在的可选输入。未验证的自定义 profile 不自动成为精选模型。

macOS 上默认 Auto 尝试 Metal，Windows 随包构建可探测 OpenCL，Linux 使用 CPU。
Windows 本次 GPU 验收覆盖上述 LLM/核心/Express 选定回归，不代表所有 BERT 模型的 GPU 质量。
非有限输出或零向量会触发 Metal High (FP32) 重试，仍无效时切换 CPU；
后续请求使用成功的会话。`Backend` 可查看实际主后端，个别算子可能由 MNN 回退 CPU。
已发现 `developerabu` BGE 在默认和 High Metal 下均产生非有限值，实测最终使用 CPU。
配置/形状错误和取消不会作为数值异常重试；原生 kernel 不能被取消令牌抢占。
真实平台与质量验证记录见 [测试说明](Testing.md)。此管线复用现有官方 C++ Interpreter/Tensor 调用，
没有新增自定义原生导出、桥接库、C++ 或修改官方 MNN 源码。
底层绑定遵循[桌面 Mono](DesktopMono.md)的逐平台 ABI；这些 BERT/MiniLM profile 的历史质量结果
不自动扩展到 Windows/Linux，原生 Embedding 入口的独立验证见[测试说明](Testing.md)。

ModelScope 的 `TorryJi/bge-small-zh-v1.5-MNN` 使用官方 LLM 导出的另一种 BERT 协议：
保留 `config.json`、`llm_config.json`、`tokenizer.mtok`、`embeddings_int8.bin`、
`llm.mnn` 与 `llm.mnn.weight`，直接调用 `MNNEmbedding.Load(directory)`，输出 512 维向量。
它复用既有官方 `Embedding` 调用及该管线的 CPU 兼容策略，不需要 `mnn_embedding.json`。
查询需要指令时，调用者可在文本前添加“为这个句子生成表示以用于检索相关文章：”；
这个 native 入口的 `EncodeQuery` 保持文本原样。模型管理器使用通用 ModelScope 清单下载并校验，
不为该仓库覆盖上游配置或自动转换 tokenizer。

## 独立语音合成与图片生成

这些托管入口使用官方 Interpreter/Tensor 和 Express/Module 图调用；
本轮仅验证 macOS arm64 Mono。它们不调用未导出的高层 TTS/Diffusion 类。
模型应保留下载仓库的配置、tokenizer、音色和外部权重目录结构。
Bert-VITS2 接入陈曦中文、英文和中英混读，Piper 接入 Amy/Kathleen/Ryan 英文 fp16 模型（16 kHz）和标准 Lessac high 导出（22.05 kHz）；
两者默认 `Auto` 加速后端，CPU 线程参数默认 1，实测 macOS arm64 Unity 2021.3.45f2 Mono。
这是特定编译器 ABI 的托管封装，并非 MNN 官方提供的跨平台 C# API。
Bert-VITS2 的条件分支通过公开 `Module::forward` 执行：每次请求将声调、语言和 BERT 特征
写入托管内存中的序列化图常量，再调用单输入 Module，原模型文件保持原样。
因此每次合成会重新加载约 50 MB 生成器，适合离线合成，尚未优化实时延迟。
英文读取仓库的 `eng_dict.bin`，使用英文音素、重音和语言 ID；词典外单词按字母拼读。
与上游陈曦生成器一致，英文 BERT 特征为零，不依赖英文 BERT 图。
Supertonic 当前随包 Metal 图在长文本、数字和部分标点下会返回有限但错误的输出，因此 Metal/Auto 选择 CPU 执行完整语音管线，以通过已有质量检查；这会增加合成耗时。

```csharp
using MNN.Unity;

using (var tts = MNNSupertonic.Load(supertonicDirectory))
{
    MNNGeneratedAudio audio = tts.Synthesize(
        "The capital of France is Paris.", voice: "M1", steps: 5, speed: 1.05f, seed: 42);
    // audio.Waveform: mono float PCM; audio.SampleRate: 44100.
}

using (var tts = MNNBertVits2.Load(bertVits2Directory))
{
    MNNGeneratedAudio audio = tts.Synthesize("你好，欢迎使用语音合成。"); // 44100 Hz
}

using (var tts = MNNPiper.Load(piperDirectory))
{
    // Voices contains installed voice names, e.g. en_US-amy-low.
    MNNGeneratedAudio audio = tts.Synthesize("The capital of France is Paris.", "en_US-amy-low"); // 16000 Hz
}

using (var diffusion = MNNStableDiffusion.Load(sdDirectory))
{
    MNNGeneratedImage image = diffusion.Generate(
        "A red bicycle beside a white wall", negativePrompt: "blurry",
        steps: 20, guidance: 7.5f, seed: 42);
    // image.Rgb: RGB24, top-left origin; image.Width/Height: 512.
}

using (var sana = MNNSana.Load(sanaDirectory))
{
    MNNGeneratedImage edited = sana.Edit(
        "Change the bicycle to blue", referenceRgb512, steps: 10, seed: 42, guidance: 4.5f);
}
```

模型生成的 Markdown/富文本可先调用通用的 `MNNSpeechTextCleaner.Clean(text)`，再交给任意 TTS：

```csharp
string spoken = MNNSpeechTextCleaner.Clean("**Hello.** <en>Welcome.</en> `code()`");
if (spoken.Length > 0)
{
    MNNGeneratedAudio audio = tts.Synthesize(spoken, language: "en");
}
```

清理器保留正文、链接文字、Unicode 字母、数字和常用语音标点，过滤 Markdown 标记、
行内/围栏/缩进代码、JSON 花括号内容、图片和链接地址、HTML/语言/模型控制标签、
script/style/think/analysis 内容、公式、emoji 和其他符号；纯格式内容返回空字符串。
常见无围栏代码按行首语法识别；它是语法过滤器，不做任意源码的语义判断。
语言仍通过合成 API 的 `language` 参数传入。

流式输出每个回复新建一个 `MNNSpeechTextCleaner`，先将 `Append(delta)` 的返回值交给
分句器/语音队列，生成结束后再传入一次 `Complete()` 的返回值。清理器跨增量保留未闭合的
代码、链接和标签状态，结束时丢弃未闭合内容，不能对每个原始 token 单独调用静态 `Clean`。
`MNNRealtimeSpeechTextChannel.AppendAsync(text, cancellationToken)` 会分块提交较长的清理结果、
等待队列反压并观察取消；返回 `false` 表示该回复已失效。每次调用须等待完成，之后仍需调用
队列的 `Complete()` 并处理其反压。清理器和队列的写入端都由单一生产者拥有。
SiriAI 的本地和远程 TTS 均使用此通用 API，展示文本保留原始格式。

Supertonic 1 的英语 fp16 支持 M1/M2/F1/F2；Supertonic 2 的 INT8/fp16、Supertonic 3 的 INT8 已验证全部十种音色的英语样本。默认语速 1.05，与上游一致；v2/v3 用 `language` 参数选择版本对应的语言标签，`Languages` 返回支持的标签。这些语言尚未全部通过内容质量验证。输入限制 500 字符，
长文本按句末或空格拆成约 160 字符的短片段，每段预测时长最多 30 秒；返回包含所有片段及拼接停顿的完整音频。超过 160 字符的连续单词或数字会拒绝。数字、小数、金额和百分比先展开成英文，再按展开后的长度分段。换行、常见不可见控制符和 emoji 会规范化；纯标点、纯 emoji 及不支持的词表字符会在推理前拒绝。异常长度、非有限值或严重超幅的音频会报错，不会通过硬限幅作为正常音频返回。SD 1.5 支持仓库根目录或 `general` 目录，
输出固定 512×512；Sana 的参考输入必须为 512×512、从左上角开始的 RGB24。
Bert-VITS2 支持中文、常用数字/标点、词组拼音及常见变调，最多 300 字符；
支持词典内英文及中英混读；英文使用零 BERT 特征，词典外单词按字母拼读。当前前端未实现完整 Jieba 词性变调，
因此不能保证任意词句的发音质量。Piper 最多 1000 字符，需上游 eSpeak-NG 可执行程序与仓库
`espeak-ng-data`。`MNNPiper.Load(modelDirectory)` 在底层自动查找模型根目录、bin 和
嵌套子目录中的工具/启动脚本，再检查 `MNN_ESPEAK_NG_PATH`、标准安装位置和 PATH；
标准位置覆盖 Windows 的 Program Files/用户安装目录及 macOS/Linux 的常见安装目录。
Editor 中还会复用项目 TestArtifacts~ 下已存在的 eSpeak-NG 依赖，无需在业务代码配置。
数据目录自动按 config.json 的 asset_folder、模型根目录和嵌套子目录查找并检查完整性。
仅在自动查找无法满足当前安装时，通过 `espeakExecutable` 或 `espeakDataDirectory` 显式覆盖。
Studio 的 Browse 仅选择数据文件夹，工具另行自动发现。
工具路径会检查当前 OS 对应的程序/脚本格式，跳过其他系统的二进制，并排除 `espeak-ng-data` 内的数据文件；显式传入字典文件时在加载阶段拒绝，无效环境变量则跳过并继续自动查找。
Piper 的 phonemizer 是官方外部工具，不是桥接库。

Piper 的标准单说话人导出从配套 `.onnx.json` / `.mnn.json` 自动读取 eSpeak 语言、音素 ID 表、
采样率及推理参数，元数据可位于模型根目录、assets 或嵌套目录；旧版 fp16 音色仍保留已验证的英文协议。
`Languages` 返回当前音色声明的语言，而不是 eSpeak 程序本身支持的全部语言。
`MNNPiper.Synthesize` 遇到当前音色不支持的文字系统，会自动复用模型根目录或项目模型目录中
已安装、可读取的中文/多语言模型，不会下载新权重；中英混读保留英文 Piper 音色，逐段合成中文，
统一到当前 Piper 的采样率后拼接。音色可以随语言变化，`LastSynthesisModelDirectory` 可用于诊断实际回退模型。
没有任何匹配模型时抛出可恢复的 `MNNSpeechLanguageException`，不会把汉字读成 “Chinese letter” 或静默删除。

通用 `MNNLocalSpeechFallback` 供其他本地 TTS provider 复用；SiriAI 已自动接入。
Supertonic 省略 `language` 时按当前版本支持的文字系统自动选择语言；例如第三版日文输入使用 `ja`，
同为拉丁字母的语言默认 `en`，需要区分西语、法语等时可显式传入语言。当前版本不支持中文时，
与 Piper 一样复用本地中文模型。此回退会改变音色，不会扩展原声学模型本身的语言能力。
候选模型仅使用各前端实际实现的语言：Kokoro 根据随附中英文词典、标准 Piper 根据音色元数据、
Supertonic 根据版本中的语言表，其余前端使用已实现的语言范围。脚本检查用于保留混合输入的所有文字，
不是统计语言识别器，不能可靠区分使用同一种字母表的语言。显式语言偏好仍保留。
本地缺少相应音色时，SiriAI 完成可见的文字回复并释放当前语音请求，继续后续对话，不上报整段会话故障。
不会把未朗读的文字写成已播放的语音历史。Player 默认搜索与模型同级的已安装目录及启动时捕获的
StreamingAssets/persistentData 模型目录；APK/网页中的模型须先按交付清单准备为本地可读目录。
语言路由复用现有推理后端，不改变各平台的原生 ABI/phonemizer 支持范围。

模型数据目录的查找逻辑不限定平台；APK 或网页 URL 中的随包资源先由底层
`MNNModelDelivery.PrepareDirectoryAsync` 按构建清单校验并准备到可读目录，再使用相同查找逻辑。
WebGL 的本地文件准备不依赖托管工作线程。这些资源能力不代表推理后端已支持所有平台：
当前 Piper 命令行 phonemizer 不支持 Android、iOS、WebGL，加载时明确报告缺少进程内后端，
不会要求通过修改路径解决；Windows/Linux 的 MNN 原生推理支持仍受平台 ABI 约定限制。
Sana 已验证推理和提示词影响，但“红车改蓝车”的语义效果仍未达标。

`Synthesize`、`Generate` 和 `Edit` 均接受 `CancellationToken`，在模型图调用之间检查取消；
不能中断正在执行的原生 kernel。TTS/SD 另提供 `SynthesizeAsync` / `GenerateAsync`；
Sana 同步调用可交给工作线程。一个实例串行执行推理和释放，返回数组独立于模型生命周期。
图片转 Unity Texture2D 时须反转行顺序，并在 Unity 主线程创建和销毁纹理。
Studio 已提供 WAV 播放/保存与图片预览/PNG 保存。

## Quick Start

```csharp
using MNN.Unity;

// 1. 加载模型
var interpreter = MNNInterpreter.CreateFromFile("model.mnn");

// 2. 创建会话（Auto 优先可用加速器，保留 CPU 备用）
var session = interpreter.CreateSession();

// 3. 获取输入输出
var input = session.GetInput();
var output = session.GetOutput();

// 4. 设置映射后的输入数据
var inputSpan = input.MapForWrite<float>();
// ... 填充数据 ...
input.Unmap();

// 5. 运行推理
session.Run();

// 6. 读取映射后的输出数据
var outputSpan = output.MapForRead<float>();
// ... 处理结果 ...
output.Unmap();

// 7. 清理
session.Dispose();
interpreter.Dispose();
```

## Core API

### MNNInterpreter

模型解释器 - 负责加载模型和创建会话。

#### 工厂方法

```csharp
// 从文件加载
MNNInterpreter interpreter = MNNInterpreter.CreateFromFile(string modelPath);

// 从字节数组加载
MNNInterpreter interpreter = MNNInterpreter.CreateFromBuffer(byte[] buffer);
```

#### 会话管理

```csharp
// 创建会话
MNNSession session = interpreter.CreateSession(MNNSessionConfig config = null);

// 释放模型数据（节省内存）
interpreter.ReleaseModel();
```

#### GPU缓存

```csharp
// 设置缓存文件（加速后续启动）
interpreter.SetCacheFile(string cacheFilePath);
```

### MNNSession

推理会话 - 管理推理执行和张量访问。

#### 张量访问

```csharp
// 获取输入张量
MNNTensor input = session.GetInput(string name = null);

// 获取输出张量
MNNTensor output = session.GetOutput(string name = null);
```

#### 执行推理

```csharp
// 同步执行
session.Run();

// 异步执行
await session.RunAsync();
```

#### 动态形状

```csharp
// 调整输入大小
session.ResizeInput(string name, int[] shape);

// 更新GPU缓存
session.UpdateCache();
```

### MNNTensor

数据张量 - 多维数据容器。

#### 属性

```csharp
int[] Shape { get; }              // 张量形状
MNNDataType DataType { get; }     // 数据类型
MNNDimensionType DimensionType { get; } // 维度类型 (NCHW/NHWC)
int ElementCount { get; }         // Shape 的乘积；不含通道填充
int ByteSize { get; }            // 原生存储字节数；可能包含 NC4HW4 填充
int Dimensions { get; }          // 维度数量
```

#### 映射访问

```csharp
// 映射用于写入
Span<float> data = tensor.MapForWrite<float>();
// ... 填充数据 ...
tensor.Unmap();

// 映射用于读取
ReadOnlySpan<float> data = tensor.MapForRead<float>();
// ... 处理数据 ...
tensor.Unmap();
```

映射返回的 span 仅在解除映射前有效；GPU 映射仍可能包含设备/主机传输，不能保证端到端零拷贝。
用 `try/finally` 确保调用 `Unmap()`，不要跨 `await` 保存 span。

#### 数据拷贝

```csharp
// 拷贝到数组
float[] data = tensor.CopyToArray<float>();

// 从数组拷贝
tensor.CopyFromArray(float[] data);

// 从Span拷贝
tensor.CopyFromSpan(ReadOnlySpan<float> data);
```

#### Unity集成

```csharp
// 从Texture2D拷贝
tensor.CopyFromTexture(Texture2D texture, bool normalize = true);

// 拷贝到Texture2D
tensor.CopyToTexture(Texture2D texture, bool denormalize = true);
```

### MNNSessionConfig

会话配置 - 控制后端、性能和精度。

#### 创建配置

```csharp
// 默认配置
var config = new MNNSessionConfig();

// 自动选择最佳配置
var config = MNNSessionConfig.CreateForCurrentPlatform();

// 高性能配置
var config = MNNSessionConfig.CreateHighPerformance();

// 低功耗配置
var config = MNNSessionConfig.CreateLowPower();

// 高精度配置
var config = MNNSessionConfig.CreateHighPrecision();
```

#### 属性

```csharp
MNNBackendType BackendType { get; set; }      // 默认 Auto；枚举存在不代表 ABI 或 runtime 可用
int ThreadCount { get; set; }                  // CPU线程数
MNNBackendType BackupBackendType { get; set; } // 创建会话时固定为 CPU 备用
MNNMemoryMode MemoryMode { get; set; }        // Normal/High/Low
MNNPowerMode PowerMode { get; set; }          // Normal/High/Low
MNNPrecisionMode PrecisionMode { get; set; }  // Normal/High/Low/LowBF16
MNNGpuTuningMode GpuTuningMode { get; set; }  // Metal/OpenCL/Vulkan GPU调优模式
MNNGpuMemoryMode GpuMemoryMode { get; set; }  // GPU内存模式
```

## 枚举类型

### MNNBackendType

```csharp
CPU = 0      // CPU后端
Metal = 1    // Metal (iOS/macOS)
CUDA = 2     // CUDA (NVIDIA)
OpenCL = 3   // OpenCL (通用GPU)
Auto = 4     // 自动选择
NN = 5       // CoreML/NNAPI
OpenGL = 6   // OpenGL
Vulkan = 7   // Vulkan
```

### MNNDataType

```csharp
Float = 0    // float32
Double = 1   // float64
Int32 = 2    // int32
Int64 = 3    // int64
UInt8 = 4    // uint8
Int8 = 5     // int8
Half = 6     // float16
```

### MNNDimensionType

```csharp
TensorFlow = 0  // NHWC格式
Caffe = 1       // NCHW格式
CaffeC4 = 2     // NC4HW4格式
```

### MNNErrorCode

```csharp
NoError = 0           // 无错误
OutOfMemory = 1       // 内存不足
NotSupport = 2        // 不支持的操作
ComputeSizeError = 3  // 计算大小错误
NoExecution = 4       // 无执行器
InvalidValue = 5      // 无效值
InputDataError = 10   // 输入数据错误
CallBackStop = 11     // 回调停止
TensorNotSupport = 20 // 张量不支持
TensorNeedDivide = 21 // 张量需要分割
```

## 平台支持

### 自动后端选择

```csharp
var config = MNNSessionConfig.CreateForCurrentPlatform();
```

此方法默认 `Auto`，在已实现的 ABI 上探测 Metal → NN → CPU，设置适量 CPU 备用线程。
所有任务模型的 `Load` 同样支持可选 `backendType`。后端诊断、回退、缓存和 NPU/平台边界见
[自动推理加速](Acceleration.md)。

### 手动后端选择

```csharp
var config = new MNNSessionConfig
{
    BackendType = MNNBackendType.Metal,
    BackupBackendType = MNNBackendType.CPU
};
```

## 性能优化

### 1. 使用映射访问

```csharp
// 直接写入当前映射，无需另外创建托管输入数组
var span = tensor.MapForWrite<float>();
span[0] = 1.0f;
tensor.Unmap();

// 数组 API 适合已有托管数据的调用方
var array = new float[tensor.ElementCount];
array[0] = 1.0f;
tensor.CopyFromArray(array);
```

### 2. 释放模型数据

```csharp
var session = interpreter.CreateSession();
interpreter.ReleaseModel(); // 节省内存
```

### 3. 使用GPU缓存

```csharp
interpreter.SetCacheFile(System.IO.Path.Combine(
    UnityEngine.Application.persistentDataPath, "mnn-cache.bin"));
var session = interpreter.CreateSession();
// 首次运行后调用 session.UpdateCache() 保存缓存；速度由模型与设备决定
```

### 4. 选择合适的精度

```csharp
// 显式选择 Low；速度和输出质量须在已支持的设备/模型上验证
config.PrecisionMode = MNNPrecisionMode.Low;

// 默认 Normal；需要更高精度可选择 High
config.PrecisionMode = MNNPrecisionMode.Normal;
```

### 5. 异步执行

```csharp
await session.RunAsync(); // 不阻塞主线程
```

## LLM 流式对话

`MNNLlm.GenerateConversationStreaming` / `GenerateConversationStreamingAsync` 使用官方逐 token
decode；`MNNGenerationUpdate.Text` 是累计文本，`GeneratedTokens` 是累计生成 token 数。
消息使用 `MNNChatMessage` 的 System / User / Assistant 角色；图片与音频属于最后一条用户消息。
回调在推理线程同步执行，Unity UI 必须通过主线程消费队列或最新快照更新，不能直接调用 Repaint。

长回答建议使用 `GenerateConversationStreamingDeltas` / `GenerateConversationStreamingDeltasAsync`。
回调参数 `MNNGenerationDelta.Text` 只包含本次新产生的完整 UTF-8 文本，按顺序追加一次；
`GeneratedTokens` 仍是累计 token 数。该路径复用字节/字符缓冲，不逐 token 创建完整回复快照；
最终返回的 `MNNMultimodalResult.Text` 仍包含完整回复。不能把 delta 当作累计文本覆盖 UI。
原有累计回调接口保持原行为。两种路径的线程、取消、异常恢复与媒体限制相同。

```csharp
var reply = new System.Text.StringBuilder();
var result = await model.GenerateConversationStreamingDeltasAsync(
    new[] { new MNNChatMessage(MNNChatRole.User, "解释彩虹是怎样形成的。") },
    delta => reply.Append(delta.Text), maxNewTokens: 128);
// reply 在生成期间由推理线程独占；此时 await 完成后才在调用线程读取。
// 实时 UI 应通过有界队列或锁同步消费，再在主线程更新。
```

流式结果的 `FirstTextMicroseconds` 是生成开始到首次非空文本回调的时间，不含模型加载、
异步排队或等待模型锁；非流式调用或无文本时为 0。`ProgressCallbackMicroseconds`
累计调用者同步回调耗时，包含 UI/TTS 消费者等待；等待期间原生解码暂停。
`PrefillMicroseconds` / `DecodeMicroseconds` 是 MNN 的阶段计时，不能与回调计时直接相加，
应结合包含回调的 `TextGenerationMicroseconds` 与端到端计时判断瓶颈。

可用 `MNNLlm.Load(directory, new MNNLlmPerformanceOptions { ThreadCount = 4,
BackendType = MNNBackendType.Auto, PrecisionMode = MNNPrecisionMode.Low,
PowerMode = MNNPowerMode.High })` 显式调整线程、精度和功耗策略。
省略设置时自动匹配已验证的模型/设备配置；整数线程重载保留显式线程值，省略的精度/功耗仍自动选择。
性能选项对象同样逐字段区分未赋值和显式值，不会覆盖显式 Normal、4线程或其他设置。
`tuning.Resolve(directory)` 返回解析后的独立快照；`MNNLlmCache.Acquire` 使用同一解析结果生成身份。
旧完整 CLR 签名保留；使用旧可选参数编译的调用方需重新编译，才能区分源码中省略的参数。
传入稳定的 `cacheDirectory` 可跨实例保留官方 `tmp_path` 缓存，调用者负责隔离并发写入，
以及在模型、原生库或驱动变化后使缓存失效。

`MNNLlmCache.Acquire(root, modelDirectory, nativeBuildIdentity, deviceIdentity, tuning)`
提供 Runtime 的稳定目录与独占槽位。先释放模型，再释放 lease；并发实例使用不同槽位，
释放后的槽位可再次使用。`nativeBuildIdentity` 应为原生构建清单 hash，或随原生插件更新而
变化的 Player build GUID；`deviceIdentity` 包含 OS/GPU/驱动。默认扫描模型文件大小、时间戳，
并纳入 config 内容；同大小且保留时间戳的权重修改需传入新的 `modelIdentity`。
传入已校验的完整交付清单身份可跳过模型文件枚举。缓存根目录必须在模型目录之外。
该目录保存官方 shader/mmap 缓存，不保存可恢复的聊天/KV 快照。

`DynamicQuantization`、`FlashAttention`、`PrefillChunkSize` 默认 null，保留模型配置。
`EnableWeightReordering` 未赋值时也继承；显式 true/false 只设置/清除 bit 3，保留动态量化低位。
`DynamicQuantization` 可选 PerChannel / PerTensor / PerBlock；FlashAttention 只改变 attention bit 3，
保留原有 KV 量化位。chunk 为 0 表示关闭分块；含 `chunk_limits` 的模型拒绝单独覆盖 chunk。
这些是加载前选项，后端/导出模型可能有额外限制，不能在生成中途切换。

`CountTokens(text)` 按实例缓存精确文本计数，最多 128 条、131072 个 UTF-16 字符，
单条超过 32768 字符不缓存；模型重建/释放时清空。`CountConversationTokens(messages)`
使用官方 chat template 后分词，包含模板开销，不改变当前会话状态。
`PrepareConversation(messages, contextTokens, reservedOutputTokens)` 返回只读 Messages、
InputTokens 和 RemovedMessages；超预算时移除最早完整轮次，保留首条 system 与当前 user。
每个候选均重新按完整模板计数，不用逐消息 token 数相加。调用者显式选择实际模型上下文上限；
这些 API 不推断模型上限，不摘要历史，不能精确计数媒体 embedding，遇媒体标签会拒绝。

```csharp
// nativeBuildHash、deviceAndDriverIdentity 来自应用部署清单和主线程捕获的设备信息。
using (var cache = MNNLlmCache.Acquire(cacheRoot, modelDirectory,
    nativeBuildHash, deviceAndDriverIdentity, tuning))
using (var model = MNNLlm.Load(modelDirectory, tuning, cacheDirectory: cache.DirectoryPath))
{
    // PrepareConversation 包含同步原生模板/分词工作，应和加载一样放到工作线程。
    var context = model.PrepareConversation(history, contextTokens: modelContextLimit,
        reservedOutputTokens: 128);
    var result = model.GenerateConversationStreamingDeltas(context.Messages,
        delta => ConsumeDelta(delta.Text), maxNewTokens: 128);
}
```

对话默认尝试复用同一实例的 KV：重新应用官方聊天模板并逐 token 比对，只有完整原生历史
与新提示前缀一致、KV 长度一致时才计算后缀。模板改写历史、独立 `Generate`、媒体请求、
取消或回调异常会使复用失效。`model.EnableConversationCache = false` 可强制完整预填充。
`PromptTokens` 是本次实际预填充量，`ReusedPromptTokens` 是复用量，`TotalPromptTokens`
是完整提示长度。KV 复用不保证浮点运算分块变化后逐 token 输出与完整预填充完全相同。
`ConversationCacheStatus` 报告 Cold / Reused / Disabled / Media / UnsupportedModel / UnsupportedRuntime /
HistoryMismatch / Interrupted；HistoryMismatch 表示实际历史/状态不满足精确续接条件，
不能据此认为可以安全回退到某个最长公共前缀。
固定 x64 原生运行时的 SmolLM2 续接测试出现 Italy → Florence（完整 prefill 为 Rome）的质量回归，
且计数/不计数两条续接路径输出相同。因此 x64 现使用完整 prefill，返回 UnsupportedRuntime；
不通过放宽答案质量断言来保留该路径。ARM64 继续按精确前缀及模型能力判断。

Qwen3.5 的模板会改写历史推理内容，通常不能满足完整历史前缀匹配，因此回退完整预填充。
当前不启用手动切分固定前缀、`eraseHistory` 回退或磁盘前缀快照：中文质量回归发现这些
实验路径与完整预填充有实质输出差异。不能将部分英文基准的加速外推为已验证的通用优化。

```csharp
var updates = new System.Collections.Concurrent.ConcurrentQueue<MNNGenerationUpdate>();
using (var stop = new System.Threading.CancellationTokenSource())
{
    var result = await model.GenerateConversationStreamingAsync(
        new[] { new MNNChatMessage(MNNChatRole.User, "解释彩虹是怎样形成的。") },
        update => updates.Enqueue(update), maxNewTokens: 128,
        cancellationToken: stop.Token);
    // 在 Update / EditorApplication.update 中消费 updates。
    // 停止按钮调用 stop.Cancel()；result.Cancelled 表示保留了部分输出。
}
```

取消在 token 边界观察；预填充和媒体编码不能在调用中途打断。原生语音输出当前明确拒绝。
提前取消可能抛出 `OperationCanceledException`；开始生成后取消通常返回带 `Cancelled` 的部分结果。
这些 API 使用限定编译器与随包二进制的 MNN 3.6.1 ABI 契约；移动端历史验证范围见
[MobileInterop](MobileInterop.md)，不能作为通用、官方支持的跨平台 C# API。

## 错误处理

原生执行错误抛出 `MNNException`；参数、文件、对象生命周期及平台错误分别使用对应的 .NET 异常：

```csharp
try
{
    var interpreter = MNNInterpreter.CreateFromFile("model.mnn");
    var session = interpreter.CreateSession();
    session.Run();
}
catch (MNNException ex)
{
    Debug.LogError($"MNN Error: {ex.ErrorCode} - {ex.Message}");
}
catch (FileNotFoundException ex)
{
    Debug.LogError($"Model file not found: {ex.Message}");
}
```

## 资源管理

所有MNN对象都实现了`IDisposable`，使用using语句或手动释放：

```csharp
// 方式1：using语句（推荐）
using (var interpreter = MNNInterpreter.CreateFromFile("model.mnn"))
using (var session = interpreter.CreateSession())
{
    session.Run();
}

// 方式2：手动释放
var interpreter = MNNInterpreter.CreateFromFile("model.mnn");
var session = interpreter.CreateSession();
try
{
    session.Run();
}
finally
{
    session.Dispose();
    interpreter.Dispose();
}
```

## 示例

完整示例请参见：

- [BasicExample](../Samples~/BasicExample/) - 基础推理示例
- [MNNInferenceExample.cs](../Samples~/BasicExample/Scripts/MNNInferenceExample.cs) - 详细代码

## API参考

完整API文档请参见：

- [API Reference](https://github.com/EitanWong/com.eitan.mnn/wiki) - 在线文档

## 版本历史

- v3.6.1 - 初始版本；可用接口与平台限制见本页。

## CosyVoice-300M-MNN 初代语音合成

使用 `MNNCosyVoice300M.Load(directory)` 与 `Synthesize(text, seed: 42)`，输出 22050 Hz；
默认完整参考、10 步 Flow、High 计算精度，声学阶段自动选择后端。
Model Manager 可直接搜索并安装 ModelScope `EitanWang/CosyVoice-300M-MNN`；
使用固定提交及30个文件的大小/SHA-256清单校验，安装后在 Studio 语音合成页加载。
模型资产、量化选项、Studio 与平台验证范围见 [CosyVoice-300M-MNN](CosyVoice300M.md)。

## CosyVoice3 语音合成

`MNNCosyVoice` 支持 `VicenTrent/Cosy-Voice-MNN` 的完整 fp16 release。
在 Model Manager 下载该模型后，ZIP 会经过 SHA-256 校验并自动解压到安装目录的 `model/`。
API 可接收安装根目录或该 `model/` 子目录：

```csharp
using (var voice = MNNCosyVoice.Load(modelDirectory, threads: 4))
{
    MNNGeneratedAudio audio = voice.Synthesize("你好，欢迎使用语音合成。", seed: 42,
        cancellationToken: cancellationToken);
    // audio.Waveform: mono float PCM; audio.SampleRate: 24000
}
```

当前使用内置参考音色和 C# RAS/HiFT 处理。默认 `backendType: MNNBackendType.Auto`，
在可用时为 LLM 配置 Metal High 精度；Conditioner、两步 Flow、F0 和 HiFT 仍使用 CPU。
完整发布包的 Flow 在 Metal 上出现非有限输出，HiFT 在 Metal Normal/High 上输出接近静音，
因此这两个阶段不启用 GPU。`voice.Backend` 表示 LLM 的配置后端，不保证每个算子均在 GPU。
可传 `backendType: MNNBackendType.CPU` 强制全 CPU；GPU 加载或 token 生成报错时自动重试 CPU。
GPU 不保证端到端更快，应比较相同设备、文本和预热条件下的 RTF。
输入限制为 1–200 个 UTF-16 字符，
必须包含文字或数字；空白、纯标点、控制字符和模型特殊标记会在推理前拒绝。
长输入按句末、分句和完整单词边界拆段（约 80 个拉丁字符或 26 个汉字的预算，
连续单词不硬切），缺少结束标点时补句号。最短语音长度按目标文本的模型 token 数计算，
避免英文字符数把自然 EOS 屏蔽过久，导致尾部重复或多读。
每段独立生成并检查 EOS，段间保留全部 PCM 并加入 40 ms 停顿；任一段达到 token 上限时
整次请求明确报错，不将已完成的前几段作为成功结果返回。分段可能改变停顿和韵律，
EOS 和长度检查不等同于逐字内容校验，不能保证模型在所有输入/seed 下都不漏字。
逐 token 的 C# 回读仅复制末行的 6,563 个 speech/EOS logits 并复用采样缓冲区，
仍由官方 `readMap` 完成原生同步；HiFT 前后的 16 点变换使用托管 FFT。
取消会在原生步骤之间检查；单次正在执行的原生调用不能中途打断。
同一实例的合成和释放串行执行，可在取消后继续合成，释放可重复调用。

官方 C++ API 调用限定于随包 MNN 3.6.1 / Apple clang / libc++ ABI v1 / 64-bit macOS Mono
约定。已验证 macOS arm64 Unity Editor；其他平台及 Player 的 CosyVoice 推理尚未实测。
本实现未新增 C++、自定义导出或桥接库。当前不包含声音克隆/注册流程。

## Kokoro 与 Indic VITS

`MNNKokoro.Load(directory)` 读取原始 `tts.mnn` / 稀疏 `tokenizer.txt` / `voices/*.bin`，
也支持 `voices.mnn` 常量音色表，以及 Sherpa v1.0 的 `model.mnn` / `tokens.txt` / `voices.bin`。
保留配套中文和 US English IPA 词典；也支持 KindBrave Kokoro v1.1 的原始外置 `.weight`；其他语言前端尚未接入。
`Voices` 返回已安装音色；最多 2000 字符，词典外词句明确拒绝，按最多 200 个音素分段。
`Synthesize(text, voice, speed)` 返回 24 kHz 单声道波形；当前固定 CPU High。
Kokoro 混合模型运行仍有待定位的偶发内容错误，不能视为已经稳定验证的推荐管线。

模型管理器为 `dr3334/Kokoro-82M-v1.0-MNN`、`wangjazz/kokoro-82m-mnn`、
`dr3334/Supertonic-3-INT8-MNN` 和 `developerabu/vits-tts-mnn` 使用固定版本文件方案，
按大小及 SHA-256 校验；wangjazz 的缺失词典和来源许可由固定版本的配套仓库补齐。
下载不获取 ONNX 或训练权重；旧的可变版本续传计划不能覆盖这些文件方案。

`MNNIndicVits.Load(directory)` 仅适配 `developerabu/vits-tts-mnn` 的 q8 导出和
`IndicVitsTokenizer` 字符词表。公开的 `Voices` 按 speaker ID 0–19 排序，
默认 speaker 18（Tamil female）、emotion 0；emotion ID 范围 0–31。
最多 300 字符，文本小写化并合并空白；不支持的字符在原生调用前拒绝。
输出 24 kHz；20 个说话人及 14 个已标注情绪已检查非静音输出，
只有 Tamil 样本进行了独立 ASR 内容抽查，仍有部分词句发音/识别错误。
这不代表所有语种、情绪和文本均达到质量要求。

```csharp
using (var tts = MNNIndicVits.Load(indicDirectory))
{
    MNNGeneratedAudio audio = tts.Synthesize("வணக்கம் உலகம்.", speakerId: 18, emotionId: 0);
}
```

两种入口均提供 `SynthesizeAsync`、取消、重复调用与 `Dispose`。
当前只验证已有 macOS Mono / Apple clang / libc++ ABI 合约，不能推断其他平台可用。

## IndexTTS-2.5 与 FireRedTTS3

模型管理器提供 `yunfengwang/IndexTTS-2.5-mnn`（fp16）和
`yunfengwang/fireredtts3-mnn` 完整配置。每个文件固定版本并校验 SHA-256；
FireRed 缺少的 `text_tokenizer/` 会自动从 `FireRedTeam/FireRedTTS3` 的固定版本补齐。
它们以独立文件发布，下载后直接保留上游目录结构，不需要 ZIP 解压，也不依赖 Python。

```csharp
var reference = MNNReferenceAudio.FromWaveFile(referenceWavePath);
using (var voice = MNNIndexTts25.Load(indexDirectory, threads: 4))
{
    var audio = voice.Synthesize("你好，今天我们测试本地语音合成。", reference,
        language: "zh", seed: 42, cancellationToken: cancellationToken);
    // audio.SampleRate == 22050; mono float PCM
}
using (var voice = MNNFireRedTts3.Load(fireRedDirectory, threads: 4))
{
    var audio = voice.Synthesize("你好，今天我们测试本地语音合成。", reference,
        referenceText: "你好，欢迎使用语音合成。", language: "zh", seed: 42,
        cancellationToken: cancellationToken);
    // audio.SampleRate == 24000; referenceText 必须与参考录音实际内容一致
}
```

`MNNReferenceAudio` 复制输入 PCM；参考录音必须为 0.5–15 秒、8–96 kHz 的非静音语音。
WAV 支持 mono/stereo PCM16/24/32 和 IEEE float32，立体声会混为单声道；
也可用 `new MNNReferenceAudio(monoSamples, sampleRate)` 直接传入有限、归一化的 PCM。
损坏容器、未支持的压缩格式、非法采样和时长会明确报错。

合成语言可选 `zh` / `en`。目标文本最多 200 个 UTF-16 字符；Index 另限制 120 个 BPE token，
FireRed 参考原文最多 400 字符，参考原文与目标合计最多 512 个 BPE token。
数字应写成实际读法；当前没有接入上游 WeText 文本规范化，也不提供情绪控制、批处理或流式语音。
空白文本、非空白控制符、模型特殊标记与非法 Unicode 会在图执行前拒绝。
相同输入与 seed 在本次 CPU 测试中得到一致波形；取消后可继续复用实例，Dispose 可重复调用。
原生图执行期间不能中断，取消在各图调用和托管处理之间检查。

图执行复用现有官方 Interpreter/Tensor C++ API 的平台限定绑定，没有新增 C++ 或自定义导出。
实测环境为 macOS arm64 Mono / Unity 2021.3.45f2 / CPU，依赖随包 MNN 3.6.1、
Apple clang、libc++ ABI v1 的既有约定；不代表其他平台、IL2CPP 或 Player 推理已验证。
Index 适用 Bilibili Model Use License，FireRed 参考实现适用 Apache-2.0；详见 [许可证](../LICENSE.md)。

## YOLOv8 目标检测

模型管理器可安装 ModelScope `rap20171201/YOLOv8-N-MNN`，验证
`yolov8n.mnn` 的大小和 SHA-256 后生成 `mnn_yolov8.json`。
此 profile 对应 float32 RGB NCHW `[1,3,640,640]` 输入和 `[1,84,8400]` 输出；
前四通道是 DFL 期望距离，需按 stride 8/16/32 解码，后 80 通道为 COCO 类别概率。
该导出的输入在原生后端按 NC4HW4 存储；公开 Tensor map/unmap 负责布局转换。
`ElementCount` 始终是逻辑形状乘积，`ByteSize` 可大于该数量乘元素大小。

```csharp
// rgb24: 从左上角开始逐行排列的 RGB 字节；构造器持有独立副本。
var image = new MNNRgbImage(rgb24, imageWidth, imageHeight);
using (var detector = MNNYoloV8.Load(modelDirectory))
{
    MNNDetection[] boxes = await detector.DetectAsync(image);
    // box.Label / Score / X / Y / Width / Height；坐标单位为原图像素。
    Debug.Log(detector.Backend);
}
```

预处理保持宽高比，半像素双线性缩放、填充 RGB 114、除以 255；
后处理做逐框最高类别选择、同类别 NMS 和原图边界裁剪。
可配置置信度、IoU 阈值、最大检测数和取消令牌；运行中的原生调用在返回后检查取消。
重叠调用与释放串行执行。`DetectAsync` 不依赖 Unity 图片 API，可在 worker 上运行。

手工 profile 还可声明 `CenterSize` 输出，或调整 tensor 名称、类别、尺寸和有序 strides。
它们需要各自的模型协议和结果验证，不能由输出形状相同就认定兼容。
本 profile 不接收 DFL 原始 logits、带 objectness 的 YOLOv5、分割或姿态输出。
平台仍限现有 MNN 3.6.1 / Apple libc++ / macOS Mono ABI；未增加 native 导出或 C++。

## PP-OCRv5 文本行识别

Hugging Face `qefro/pp-OCRv5-mnn` 的固定安装包含十二组识别器：英文、中文、Latin、
Cyrillic、希腊文、阿拉伯文、天城文、Eastern Slavic、韩文、泰米尔文、泰卢固文和泰文，
分别校验图和字典的 SHA-256。英文 profile 在安装根目录，其他十一组在
`recognizers/{chinese,latin,cyrillic,greek,arabic,devanagari,eslav,korean,tamil,telugu,thai}/`，
可将对应子目录传给下面的 API。
新安装另外包含固定的 `PP-OCRv5_mobile_det.mnn`，可通过下述整页 API 使用。
另外提供 `recognizers/chinese-v4` 和 `recognizers/chinese-fp16`，共十四组 profile。
这两组分别使用配套 v4 / v5 fp16 检测图；全部三十项上游图和字典经 SHA 校验。
方向分类不在该固定仓库中，当前没有 0/180 度自动校正。

```csharp
using (var recognizer = MNNPaddleTextRecognizer.Load(modelDirectory))
{
    MNNTextRecognition result = await recognizer.RecognizeAsync(croppedLineRgb);
    Debug.Log(result.Text + " / " + result.Confidence);
}
```

`croppedLineRgb` 为正向、水平的单行 `MNNRgbImage`；此 API 不自动定位整页文本框。
`mnn_paddle_text.json` 声明图、字典、tensor 名称、输入高度、宽度范围和附加空格约定。
这些图使用 `x` BGR float32 NCHW `[1,3,48,width]`，按高度等比例缩放并在右侧填零，
归一化为 `pixel/127.5-1`；输入 RGB 由托管层转换为 BGR。
宽度按 8 对齐，至少 320、至多 2048；超过该长宽比时需先切分文本行。
英文输出 `fetch_name_0` 为 `[1,time,438]` 概率；其他已验证图的最后一维按字典大小变化，CTC 解码去 blank/连续重复，blank 可分隔重复字母。
置信度为保留字符的概率均值，空文本为零。字典在 index 0 增补 blank，并按 profile 附加空格。
`reverseArabic` 采用 PaddleOCR 的阿拉伯文字符分组反转规则，保留连续 ASCII 字母、数字和
指定标点；它不是通用 Unicode 双向排版器。英文等其他 profile 不启用该处理。
韩文 profile 的 `preferredBackend=CPU` 使 Auto 使用通过文字质量检查的 CPU；显式 Metal
仍可指定，但该韩文裁剪未通过质量检查。单行应尽量贴近文字边界：测试中的大留白韩文裁剪
在 CPU/Metal 都遗漏词间空格，同一文字的紧裁剪 CPU 及整页检测管线能保留空格。
纯色图没有字形边缘，直接返回空文本和零置信度，避免部分识别头对空白图产生伪文字。
手工 profile 必须与模型语言、字典和输出协议吻合；没有自动宣称其他 PP-OCR 图兼容。

## PP-OCRv5 整页识别

同一安装的 `mnn_paddle_ocr.json` 声明 DB 检测图、tensor 名称、最大边长、阈值和后端偏好。
识别配置的 `pageConfig` 只能指定页面根目录中的文件名；默认 `mnn_paddle_ocr.json`，
v4 / fp16 分别指定 `mnn_paddle_ocr_v4.json` / `mnn_paddle_ocr_fp16.json`。
`MNNPaddleOcr.Load(root, "recognizers/chinese-v4")` 自动选择匹配检测图；显式传入 config 时以调用者配置为准。
v4 识别输出为 `softmax_11.tmp_0`，检测输出为 `sigmoid_0.tmp_0`；v5 输出仍为 `fetch_name_0`。
v4 在已有中文整页语料中遗漏“世界 2026”之间的空格，单行也可能输出尾部空格；
结果保留模型原文，不自动填空格或 Trim。标准 v5 仍为默认，v4 作为独立可选变体。
英文页面使用根识别器；其他语言传上面对应的 `recognizers/<profile>`，
共用一份检测权重，不复制检测图或放开父目录路径。

```csharp
using (var ocr = MNNPaddleOcr.Load(modelDirectory, recognizerProfile: "recognizers/chinese"))
{
    MNNOcrResult result = await ocr.RecognizeAsync(pageRgb, cancellationToken);
    Debug.Log(result.Text);
    foreach (MNNTextRegion line in result.Regions)
        Debug.Log(line.Text + " / " + line.Confidence + " / " + line.Box.Corners[0]);
}
```

`MNNTextRegion` 保存 CTC 识别与检测框；`Box.Corners` 是原图像素中的左上、右上、右下、左下
四点，`Box.Confidence` 是框内检测概率均值，`Confidence` 为保留字符的概率均值。
`MNNOcrResult.Text` 按几何阅读顺序以换行拼接，空白页返回零个 region、空文本和零置信度。
`MNNPaddleTextDetector.Load(...).Detect(...)` 可单独定位文字；`MNNPaddleOcr.RecognizeLine(...)`
可绕过检测直接读已裁剪的正向单行，同一对象上的调用与释放串行化，支持取消。

预处理遵循 PaddleOCR 的 max-side resize，默认最长边 960、尺寸按 32 对齐；
BGR NCHW 输入按均值 `[.485,.456,.406]`、标准差 `[.229,.224,.225]` 归一化。
`x` → `fetch_name_0` 的检测输出必须是 `[1,1,height,width]` 有限概率图。
托管后处理使用 8 邻域连通分量、凸包最小面积矩形、框内打分与矩形扩张，
再做四角坐标还原、透视双线性采样和逐行 CTC。高窄 crop 参照 Paddle 旋转 90 度。
该实现没有 OpenCV RETR_LIST 的内孔轮廓和 Clipper 圆角扩张，像素边界打分也不保证与其一致；
它是经已知页面验证的托管实现，不能宣称逐像素复现官方 Python 后处理。
没有接入 0/180 度方向分类、弯曲文本矫正、多栏文档版面理解或其余语言的字典。

当前固定检测图在受支持的 MNN 3.6.1/macOS Mono 运行时上，Metal 产生旋转页多余框；
识别图在部分整页 rectified crop 上即使使用 High 精度也未通过逐字门槛。
因此整页 profile 的 `Auto` 默认为 CPU 检测与 CPU 识别，`DetectionBackend` / `RecognitionBackend`
报告实际后端。显式请求 Metal 或修改后端偏好属于未通过此页面语料质量验证的配置。
独立正向文本行 API 的 CPU/Metal 验证保持单独记录，不能用于证明整页 Metal 正确。
预处理按行并行、归一化查表并复用最多两个输入数组和后处理工作区，线程值仍由调用者控制。

### Zipformer v1 speech recognition

Install either official `sherpa-mnn-streaming-zipformer-en-2023-02-21` or
`sherpa-mnn-streaming-zipformer-bilingual-zh-en-2023-02-20` with Model Manager.
Installation validates the known encoder, decoder, joiner and token SHA-256s and
writes `mnn_zipformer.json`. The matching ModelScope and Hugging Face assets have
identical verified hashes. Runtime loading verifies hashes again.

```csharp
using (var recognizer = MNNZipformerRecognizer.Load(modelDirectory))
{
    MNNSpeechRecognition result = recognizer.RecognizeWaveFile(wavPath);
    UnityEngine.Debug.Log(result.Text);
    // Also: Recognize(float[] monoPcm, int sampleRate, CancellationToken token).
    // TokenIds and Timestamps (seconds) correspond to emitted subword tokens.
}
```

The public API transcribes complete recordings; internally it preserves the
streaming graph's 39-frame windows, 32-frame stride and recurrent state. Greedy
search emits at most one token per encoder frame and retains decoder output on
blank frames. Audio is normalized mono PCM at 8–96 kHz; WAV accepts mono/stereo
PCM16/24/32 or float32. Array input is limited to ten minutes; WAV files also have
a 64 MiB container limit. Silence is valid. `RecognizeAsync` copies its input and
runs on a worker; cancellation is checked between frontend frames and graph calls.
Each request starts with fresh recurrent state. A recognizer serializes requests
and disposal.

This profile intentionally accepts only the two verified graph protocols, with
500/6254 network tokens. Other Zipformer releases need their own tested metadata,
state dimensions and preprocessing profiles. No portable native ABI is implied:
the existing MNN 3.6.1 Apple clang/libc++ macOS Mono restriction applies.

### SenseVoiceSmall speech recognition

Install `wo105090/SenseVoiceSmall-MNN` from ModelScope or `xinliu/sensevoice_mnn` from Hugging Face with Model Manager. The package verifies the chosen graph, its external `.weight` file when present and `tokens.txt`, writes `mnn_sensevoice.json`, and routes Studio transcription through a CPU greedy CTC pipeline. The managed frontend applies the graph's 80-bin Hamming/LFR/CMVN protocol.

```csharp
using (var recognizer = MNNSenseVoiceRecognizer.Load(modelDirectory))
{
    MNNSpeechRecognition result = recognizer.RecognizeWaveFile(wavPath);
    Debug.Log(result.Text);
}
```

The pinned profile supports `auto`, `zh`, `en`, `ja`, `ko` and `yue`; the integration fixtures cover English and Chinese/English. Exact digital silence returns an empty result.

`Text`, `TokenIds` and `Timestamps` contain speech tokens only. The first four
encoder positions are rich metadata labels; speech CTC decoding starts after
those positions, even if a metadata prediction is blank or repeated. Rich event
tokens predicted later in the sequence are omitted from plain transcription.

SenseVoice recordings must be mono normalized PCM or a supported mono/stereo WAV
at 8–96 kHz, 85 ms–60 seconds. This API processes one recording per call; there is
no live streaming or automatic segmentation. The graph supports five languages,
but the current fixtures only exercise English and Chinese/English, with known
code-switching errors. CPU selection is deliberate; the tested Metal path lost
words. Silence with background noise can still hallucinate without an external
VAD. Runtime uses the package's existing MNN 3.6.1 Apple clang/libc++ macOS Mono
ABI contract, with no additional C++ or custom exports.

### Whisper speech recognition

Model Manager provides five pinned installation profiles:

| Provider | Repository | Enabled transcription |
| --- | --- | --- |
| Hugging Face | `developerabu/whisper-tiny-en-mnn` | English |
| ModelScope | `rap20171201/sherpa-mnn-whisper-tiny.en` | English |
| Hugging Face | `developerabu/whisper-tiny-mnn` | English |
| Hugging Face | `developerabu/whisper-base-mnn` | English |
| Hugging Face | `developerabu/whisper-small-mnn` | English and Chinese |

Each installer pins a revision, downloads only its matching encoder, decoder
and byte vocabulary, verifies SHA-256, and writes `mnn_whisper.json`. Runtime
verifies the same hashes before loading. Configurations select the profile's
cache dimensions, vocabulary and decoding prefix; filenames alone do not enable
other exports.

```csharp
using (var recognizer = MNNWhisperRecognizer.Load(modelDirectory))
{
    MNNSpeechRecognition result = recognizer.RecognizeWaveFile(wavPath);
    Debug.Log(result.Text);
    // TokenIds are available; Timestamps is empty for this no-timestamps decoder.
}
```

These profiles accept normalized mono PCM or a supported mono/stereo WAV at 8–96 kHz,
25 ms to 29.5 seconds. `RecognizeAsync` copies PCM input and runs on a worker;
requests and disposal are serialized, and cancellation is checked between
feature frames and decode steps. Exact digital silence returns empty text.
Long recordings must be segmented by the caller. No VAD, translation, language
detection, streaming or token alignment is provided. All profiles default to
English. On the small profile, set `recognizer.Language = "zh"` before Chinese
transcription; setting it back to `"en"` changes the next request. Other profiles
reject `"zh"`. This limits the exposed API to tested configurations: tiny had
substantial Chinese errors, and base did not terminate on the bilingual fixture.
Small retained the fixture's key Chinese content but mixed Traditional Chinese
and English; it does not provide a Simplified Chinese normalization guarantee.

The frontend uses 400-point periodic Hann/Slaney mel energies and sherpa's log10,
eight-decade clamp and normalization. A cached Bluestein FFT kernel avoids a
quadratic DFT and per-frame trigonometry. Encoder length follows recording
length plus ten seconds of feature padding, capped at 3000 frames. Express
computes logits and both self-attention caches together and preserves their
shapes. Cross-attention inputs are bound once per recording, and every request
starts with fresh self caches. CPU is the verified configuration; the existing
MNN 3.6.1 Apple clang/libc++ macOS Mono ABI restriction applies.

Other languages and Whisper exports remain outside the verified profiles.
See [Testing](Testing.md#whisper-asr) for the recording and platform scope.

### Qwen3-ASR speech recognition

Model Manager prepares `hb-dev/Qwen3-ASR-0.6B-INT8-MNN` (Hugging Face) and
`huangzhengxiang/Qwen3-ASR-0.6B-INT8-MNN` (ModelScope) as `mnn_qwen3_asr.json`.
Both use the same eight SHA-256 verified inference assets. Hugging Face is pinned
at `7426689dc24394df2ba693f384cb5b07426ddf70`; ModelScope pins each file separately
because its `llm_config.json` was updated after the weights. Source notices are
installed alongside the assets. Other Qwen3-ASR exports need their own validation.

```csharp
using (var recognizer = MNNQwen3AsrRecognizer.Load(modelDirectory, language: "auto"))
{
    MNNSpeechRecognition transcript = recognizer.RecognizeWaveFile(wavPath, cancellationToken);
    Debug.Log(transcript.Text);
}
```

The recognizer accepts finite normalized mono PCM (`Recognize`/`RecognizeAsync`)
or mono/stereo PCM16/24/32 or float32 WAV at 8–96 kHz. Recordings must be 0.1–30
seconds. It resamples to 16 kHz and passes a temporary float WAV to the existing
official MNN Llm audio API; feature extraction stays in MNN. Temporary inputs are
deleted on success, failure and cancellation. Exact zero silence returns an empty
transcript without running the decoder. Each request resets generation state.

Backend Auto prefers Metal with CPU fallback, using greedy decoding and repetition
penalty 1.0. The explicit language overload accepts `auto`, `zh`, `en`, `ja`, `ko`
and `yue` (or their English names). It applies Qwen's role newlines and optional
forced-language prefix through the existing official configuration API. Automatic
language output is parsed so its language header is excluded from `Text`. English
and Chinese are the measured accuracy scenarios; other language options require
their own quality evaluation.

The original overload without a language argument retains the installed template
for compatibility. `MNNSpeechRecognizer.Load` also applies explicit language
options to Qwen; an omitted or blank option retains the installed template.
SiriAI exposes the same explicit choice in recognition settings. Installed model
files remain unchanged. There is no timestamp alignment. `TokenIds` contains a copy
of the actual native output IDs, including generated metadata in automatic mode.
`MNNMultimodalResult.GeneratedTokenIds` also
exposes an owned, read-only snapshot for multimodal and conversation generation.

Cancellation is checked before inference and between decoder tokens. Native audio
encoding and prefill finish synchronously; disposal waits for an active request.
A transcript reaching the 256-token guard fails instead of returning truncated
text. Explicit backend/precision options are available for measurement. The same
macOS Mono / bundled MNN 3.6.2 Apple ABI requirements as `MNNLlm` apply; this is
not a portable official C# API. No bridge or new C++ implementation is involved.

`Recognize` and `RecognizeWaveFile` optionally report decoder progress on the
calling worker. A callback may request cancellation; calling recognition again or
calling `Dispose` on that same instance from the callback throws
`InvalidOperationException`. Disposal from another thread waits for recognition.

## Paraformer 中文语音识别

`MNNParaformerRecognizer` 支持固定版本的 KindBrave/guoera online 8404-token 导出。
先用 Model Manager 安装，获得经过文件 SHA-256 校验的 `mnn_paraformer.json`：

```csharp
using (var recognizer = MNNParaformerRecognizer.Load(modelDirectory))
{
    var result = recognizer.RecognizeWaveFile(wavPath);
    UnityEngine.Debug.Log(result.Text);
    // Also: Recognize(normalizedMonoPcm, sampleRate, cancellationToken).
    // RecognizeAsync copies the caller's PCM before scheduling work.
}
```

输入为 8–96 kHz、85 ms 至十分钟的音频；WAV 支持来自共用解析器的单/双声道
PCM16/24/32 和 float32。结果包含独立 token ID 快照，`Timestamps` 为空。
每次录音重置 CIF 与 decoder cache；取消在特征处理及图调用之间检查，不能中断
正在执行的原生图。`Dispose` 等待当前识别结束。

默认 Auto 选择 CPU（4 线程），避免 Metal 编码器已观察到的非有限输出。
显式指定加速器不代表质量已验证；错误会报告为异常。
中文官方测试音频通过逐字比对；夹英文测试有多余的 “is”，英文质量未验证。
增量接口保留跨块 encoder/CIF/decoder 状态，接受 16 kHz 单声道归一化 PCM：

```csharp
using (var recognizer = MNNParaformerRecognizer.Load(modelDirectory))
using (var stream = recognizer.CreateStream())
{
    // On a dedicated inference worker, for each captured PCM slice:
    string provisional = stream.AcceptWaveform(pcm, offset, count, cancellationToken);
    // At the utterance boundary; idempotent until Reset:
    MNNSpeechRecognition final = stream.InputFinished(cancellationToken);
    stream.Reset(); // New utterance or capture discontinuity.
}
```

每次调用最多 30 秒、单流累计最多十分钟；PCM 缓冲有界，声学推进为 600 ms，
特征需要 25 ms 上下文。首字还取决于模型出词、采集和排队，不能把 PCM 投递粒度当成首字延迟。
增量文本为累计的临时假设，句末 `InputFinished` 排空一秒模型上下文（计算补零，不要求用户等待一秒录音）；
最终文本由模型实际解码决定。未提供时间戳、标点恢复或 VAD。
每个 stream 交给一个 worker；多个 stream 的递归数组独立，但共享 recognizer 的原生图锁，
不会并行执行同一图。中断推理后的流必须 `Reset`；参数校验失败不改变状态。
销毁 recognizer 前先停止并释放 streams。设备回调应只投递有界 PCM，不直接调用模型。

编码器导出缺少 `enc_len` 的生产节点，加载器通过公开的 MNN FlatBuffer
序列化格式在内存中补充常量，并合并 encoder/CIF 输出以每块只执行一次编码器；
磁盘原始权重保持不变。沿用包内限定的平台 ABI，不新增 C++ 或自定义 native exports。
测试范围见 [Testing](Testing.md#paraformer-online-8404)。

## Silero VAD

Model Manager 可安装固定版本 `Jeryuk/silero_vad-MNN` 并生成
`mnn_silero_vad.json`。`MNNSileroVad` 检测语音区间：

```csharp
using (var vad = MNNSileroVad.Load(modelDirectory))
{
    MNNVoiceActivityResult result = vad.DetectWaveFile(wavPath);
    foreach (var segment in result.Segments)
        UnityEngine.Debug.Log($"{segment.StartSeconds:F3}–{segment.EndSeconds:F3} s");
}
```

`Detect(float[] samples, int sampleRate, float threshold = .5f, CancellationToken)`
接收归一化单声道 PCM，采样率 8–96 kHz、非空且最长十分钟，内部重采样到
16 kHz。`DetectAsync` 在排队前复制 PCM。结果独立持有每 32 ms 一项概率及
以秒表示、末端不包含在内的区间。每次调用重置上下文和循环状态；取消在图调用
之间检查，不能中断执行中的原生图。最短语音 250 ms、分段静音 100 ms、
边界补白 30 ms；可调整语音阈值，暂不提供实时逐块接口。

Auto 使用 CPU 单线程。M5 上 CPU1 比 2/4/8 线程更快；Metal 概率与区间明显
偏离参考且更慢，不作为默认。显式加速器没有质量保证。量化 MNN 与对应
float ONNX 存在差异：六组参考中五组边界完全一致，补静音的中英混合样本
最后区间提前 64 ms；不把该差异归为对齐成功。VAD 仅检测语音活动，不能
生成转录、识别人或可靠区分所有噪声。参见 [测试说明](Testing.md#silero-vad)。

## Smart Turn endpoint detection

Model Manager 安装固定版本 `Jeryuk/smart-turn-v3.2-gpu-MNN` 后生成
`mnn_smart_turn.json`。通过独立的音频轮次检测 API 判断当前轮次是否结束：

```csharp
using (var detector = MNNSmartTurn.Load(modelDirectory))
{
    MNNTurnDetectionResult result = detector.PredictWaveFile(wavPath);
    Debug.Log($"Complete: {result.IsComplete}; probability: {result.CompletionProbability:P2}");
}
```

`Predict(float[], sampleRate, threshold = .5f, cancellationToken)` 接受归一化单声道
PCM，8–96 kHz、非空、最多十分钟。重采样只计算最终使用的末尾八秒，保留完整输入的
滤波相位和上下文；不足八秒先在开头补零，再做零均值/单位方差归一化和 centered
Whisper 80×800 log-mel。默认概率严格大于阈值才判为完成。
`DurationSeconds` 保留完整录音时长；`PredictAsync` 复制输入并在工作线程执行。
取消在托管处理和原生调用边界检查，单次原生前向中无法立即中断。

Auto 使用 CPU4。M5 上 CPU4/8 接近、CPU4 使用更少线程；这不是所有设备的最佳配置承诺。
显式后端/线程可通过 `Load` 配置。原始文件名含 gpu，但该 MNN 导出已经 INT8 权重量化，
其概率会与原始 FP32 ONNX 不同。模型用于用户当前轮次出现停顿后判断结束概率；
不应连续把独立 VAD 帧当成完整轮次，也不提供转录、说话人身份或语音区间。
仅沿用已有 macOS64 Mono ABI；不增加 C++、桥接库或原生导出。
验证范围见 [测试说明](Testing.md#smart-turn-v32)。

## Kokoro v1.1 external weights

`KindBrave/kokoro-multi-lang-v1_1-MNN-Custom` 新增双平台固定安装方案：
Hugging Face revision `8902386456c12c2c325d14eeb122e257be627966`，ModelScope
revision `2a80a70abf25777810d89099ae1451f0af253318`。八个资源逐项固定尺寸和 SHA-256，
两站的图、原始外置权重、词典和 103 音色表一致。安装不重复下载 espeak 语言包或训练资源。
`MNNKokoro.Load` 直接使用原始 `model.mnn.weight`；托管代码仅在内存图中设置公开
`Op.externalPath` 字段，保留磁盘原图，不创建副本或临时权重、不增加 C++。

Studio 提供全部 103 个音色，中英文词典、语速、播放与 WAV 保存已经接通。
示例文字根据音色语言选择。全部音色能生成有限非静音波形，但内容验证尚未全部通过：
同一句话的 SenseVoice 检查 76/103 通过；其余 27 个中，Qwen 独立转写有 20 个包含完整
内容断言，7 个仍不满足，不能宣布全部音色质量已通过。详见 Testing 中的证据与边界。

## MMS Tamil TTS

```csharp
using (var tts = MNNMmsTamil.Load(modelDirectory, threads: 4))
{
    MNNGeneratedAudio audio = await tts.SynthesizeAsync("வணக்கம் உலகம்.", speed: 1);
    // audio.SampleRate == 16000; audio.Waveform is independent managed PCM.
}
```

Model Manager installs `developerabu/mms-tts-tam-mnn` INT8 at
`fb2efd8800e1f7992712aceca63aaa59bc041cd9`, with size/SHA-256 checks for the graph,
character tokens and publisher README. The prepared `mnn_mms_tamil.json` keeps
Studio routing stable after the directory is renamed. The API checks official graph
metadata and token IDs before native loading; it freezes the published VITS length,
noise and speed inputs through the existing official Module path. It supports
1–300 characters, Tamil vocabulary, speed 0.5–2, cancellation between native calls,
async synthesis and disposal. Unknown characters are rejected; unsupported punctuation
is omitted. Write numbers as Tamil words. CPU / existing macOS Mono ABI only.

Real output passed greeting anchors with independent Whisper-small Tamil decoding;
the complete sentence still has word errors. The same pinned installation now includes all three precision exports; choose
`precision: "int8"`, `"fp16_int8"` or `"fp32"` in Load, or use Studio’s Precision
selector. All three execute synthesis and lifecycle checks. FP16/INT8 and FP32
failed the strict Tamil greeting ASR anchors in the first all-precision audit;
they remain unverified for content quality. The generated
`MMS-SOURCE-NOTICE.txt` records the original `facebook/mms-tts-tam` model's
CC-BY-NC-4.0 license; the export repository's Apache metadata does not replace it.

## Chinese Sherpa VITS

```csharp
using (var tts = MNNSherpaVitsChinese.Load(modelDirectory))
{
    MNNGeneratedAudio audio = await tts.SynthesizeAsync("你好，这是语音合成测试。", speakerId: 0, speed: 1);
}
```

The five-speaker `KindBrave/sherpa-vits-zh-ll-MNN-Custom` export has pinned
Hugging Face revision `debc94f4d18e3783ec0d054a8c7401b4c064c511` and ModelScope
per-file revision `07b16a90eac0b7803692ffc7173b16e3adb40048`. Both plans verify the
same six graph/weight/config/token/lexicon files (about 32.6 MB). Original external
weights are loaded through public serialized fields, without disk copies or C++.

Speaker IDs 0–4 are `suyingxue`, `gunian`, `fushiyu`, `bingjiao`, `bazong`.
All five produced the exact Mandarin test sentence according to independent Qwen
ASR. Supports 16 kHz PCM, 1–300 characters, speed 0.5–2, async synthesis,
cancellation and disposal. Studio offers voices, speed, examples, playback and WAV
saving. The frontend uses longest lexicon matches and character fallback; it does
not implement the repository's Jieba or FST normalizers. Write numbers as Chinese
words. Polyphonic words follow the supplied lexicon; unknown characters/Latin words
are rejected rather than omitted. These single-sentence checks do not establish
accuracy for arbitrary Chinese. CPU / current macOS Mono ABI only.

## MOSS Nano

`MNNMossNano.Load(directory, threads: 4)` 支持 ModelScope
`kiven2008/moss-tts-nano-100m-mnn` 的固定采样图布局，以及
`ZhangZes/MOSS-TTS-Nano-MNN` 的 `tts/` + `audio_tokenizer/` 缓存图布局。
安装器分别下载固定版本的十个、九个文件并校验 SHA-256；不下载 ONNX、训练权重或未使用的编码器。

```csharp
using (var tts = MNNMossNano.Load(modelDirectory))
{
    var audio = tts.Synthesize("你好，这是语音合成测试。", voice: "Junhao", seed: 1234);
    // audio.SampleRate == 48000; stereo codec output is mixed to mono.
}
```

`Voices` 返回 manifest 中的 18 个内置参考音色。`SynthesizeAsync` 提供相同参数；
最多 500 字符、250 个文本 token、375 帧（30 秒）。达到帧上限会报错，不能返回截断语音作为成功。
使用托管 SentencePiece BPE；未实现 WeText/FST 数字、缩写、多音字处理，请直接输入口语文字。
中文/英文样本及日语分词已与独立实现对照；全音色内容审计仍有失败，见 Testing。
未验证的语言不能因词表能编码就视为已支持。
合成和释放串行，取消在图调用之间检查，不能中断正在执行的原生调用。
每次合成创建并释放阶段会话；缓存图布局使用显式全局/局部 KV 张量。
当前仅验证项目限定的 macOS64 Mono / MNN 3.6.1 / Apple clang / libc++ ABI 合约。

Qwen3-TTS 的官方 `Omni::generateTTS` 调用仍是内部实验：已测试的 INT8/FP16 导出存在
不结束、重复音频码或近静音问题，未开放 Studio 可运行配置，也未作为可用公共 API 发布。

## DhVaani v1.1

`MNNDhVaani.Load(directory, threads: 4)` 支持 Hugging Face
`Bbkblo/DhVaani-0.5-MNN` 的修正版 v1.1 图。固定安装八个文件（约 146 MB），不获取 Android/JNI、
ONNX、NPZ 和训练文件；旧版曾产生静音的 revision 不会套用当前安装计划。

```csharp
var reference = MNNReferenceAudio.FromWaveFile(referenceWavePath);
using (var tts = MNNDhVaani.Load(modelDirectory))
{
    var audio = tts.Synthesize("The capital of France is Paris.", reference,
        referenceText: "Paris is the capital of France.", steps: 8, speed: 1, seed: 666);
    // audio.SampleRate == 24000; mono PCM
}
```

参考音频必须为 0.5–15 秒，`referenceText` 必须逐字对应录音。
Hindi 和 English 使用字符词表，不静默丢弃未支持的字符；数字、缩写需先写成口语形式。
最多 500 字符、30 秒输出；步数 1–32，语速 0.5–2。较少步数可能降低内容与音质。
提供同参数的 `SynthesizeAsync`，实例串行运行与释放，取消在阶段/采样步之间检查。
特征提取、Euler 采样与 ISTFT 使用托管 C#；输出投影通过官方 MatMul/Add 图执行，避免 C# 的逐帧矩阵乘法。
为调用公开的单输入 `Module::forward`，只在内存中将输入用官方 SliceTf/Reshape 图操作拆分；
流模型与声码器会话在实例内复用，输入长度改变时由 Module 重新推导形状；
不访问原生虚表，不修改下载资产或官方 MNN 源码。
已验证英文重复合成、Hindi 跨语言参考样本及 Studio；不代表所有文本与声音克隆质量均通过。
当前平台边界仍为 MNN 3.6.1 / Apple clang / libc++ ABI v1 / macOS64 Mono。

## Qwen3-TTS 显式 KV

`MNNQwen3ExplicitTts.Load(directory, threads: 4)` 支持固定的 HF
`yunfengwang/Qwen3-TTS-12Hz-0.6B-Base-MNN` 显式 KV 布局。
输出 24 kHz mono，语言为 `zh` 或 `en`，默认使用下载包内预编码的 demo 参考声音。
`MNNQwen3ExplicitTts.InstalledVoices(directory)` 返回校验通过的 `voices/<name>` 音色目录名。
可把相同导出协议准备好的 `meta.json`、`ref_code.i32.bin`、`spk_emb.f32.bin` 放入
`voices/<name>/`，然后使用 `voice: "<name>"` 切换音色；切换复用已加载的图。
没有 demo 时默认选择按名称排序的第一个有效音色。音色元数据、token 模板、码本及说话人向量均在加载前检查。
原始 WAV 声音克隆及其他 Qwen3 导出不使用此入口。

```csharp
using (var tts = MNNQwen3ExplicitTts.Load(modelDirectory))
{
    var audio = await tts.SynthesizeAsync(
        "Paris is the capital of France.", language: "en", seed: 42,
        maxFrames: 360, cancellationToken: cancellationToken, voice: "demo");
    // audio.Waveform / audio.SampleRate
}
```

最多 200 字符，`maxFrames` 范围 2–360，默认 360（约 30 秒）。达到帧数上限仍未收到 EOS 会抛出异常，
不返回截断语音。取消在帧/码本计算之间检查；同实例操作串行，Dispose 等待当前调用释放。
嵌入表按行读取，图与投影在实例内复用。CPU 与原生调用仍受本项目 macOS Mono ABI 限制。

`cacheCodePredictorShapes: true` 可缓存十五种固定 CP 形状以减少重复 resize，默认关闭。
实测 CPU4 重复调用中位由 8.961 降到 8.019 秒，但首次调用更慢，进程驻留内存样本明显增加。
仅在有足够内存且重复合成时考虑启用；详见 Testing 中的完整测量。

Kokoro 还支持 `Jeryuk/kokoro-v1_0-MNN` 的单 `zf_xiaoxiao` 音色，以及
`starsharp06sharp/kokoro-multi-lang-v1.1-MNN-Multilingual` 的 103 个有名称音色。
后者附加的 53 份未命名表不作为音色公开；当前前端仅实现中文与美式英文词典。
两者均通过 `MNNKokoro.Load(directory)` 加载，安装器补齐已验证的固定资源。

### Pocket-TTS

`MNNPocketTts.Load(directory, threads: 4)` 读取固定的
`developerabu/pocket-tts-mnn` english_2026-04 导出。五个阶段全部使用 MNN，
托管 SentencePiece unigram 分词不依赖外部程序。限定当前项目 macOS64 Mono ABI。

```csharp
using (var tts = MNNPocketTts.Load(modelDirectory))
{
    var reference = MNNReferenceAudio.FromWaveFile(referenceWavePath);
    var audio = await tts.SynthesizeAsync(
        "The capital of France is Paris.", reference, seed: 42);
}
```

参考录音为 0.5–15 秒 WAV，自动转为 24 kHz；无须原文。
输出 24 kHz 单声道，最多 50 token / 30 秒；取消、未正常结束和文件哈希不匹配会报错。
调用者按句拆分长文本。`maxFrames` 为 12–375，不能用较小上限静默截断语音。
原始图文件保持不变，内存中恢复固定导出的 Shape 切片并化简空序列拼接。
最近一次参考声音状态及五阶段图在实例内复用，`Dispose` 释放；不包含受限预设音色。
六层 KV 缓存按本次参考步数、文本 token 数与 `maxFrames` 的总和预留，容量不超过导出的
1000 行；参考声音快照仅保存已填充行。每次请求使用独立副本，未使用行保持 NaN，
取消或消费者异常不会修改参考快照。较小的 `maxFrames` 可以减少缓存，但必须足够生成完整语音。

`SynthesizeStreaming(text, reference, onPcmChunk, seed: 42)` 每步保留 Mimi decoder 状态，
生成 1920 点（80 ms）PCM 后立即同步回调，不等待整段完成。回调参数为
`(samples, offset, count, endsSegment)`；最后一个有效块标记 `endsSegment=true`。
调用者在回调返回前复制或送入有界队列，不保留数组，不重入/释放同一模型，不调用 Unity API。
回调运行在调用合成的线程上；应用应使用长期合成 worker，并在队列满时处理背压和取消。
播放端可在后续块生成时消费已有 PCM；这个接口本身不创建播放设备。

现有 `MNNRealtimeSpeechStream.Produce` 可直接组合稳定文本通道、同步流式合成器和有界输出。
每回复使用一个新的 stream，在合成拥有者线程调用一次；它不会另建线程或接管模型生命周期。
以下为应用装配片段，`tts`、`reference` 和 `cancellationToken` 由应用作用域持有：

```csharp
var format = new MNNPcmFormat(tts.SampleRate, 1);
var output = new MNNRealtimeOutputPipeline(format, framesPerBlock: 240, blockCapacity: 6);
var response = output.BeginResponse();
var textChannel = new MNNRealtimeSpeechTextChannel(output, response);
var stream = new MNNRealtimeSpeechStream(output, response);
var production = Task.Factory.StartNew(
    () => stream.Produce(textChannel, format,
        (stableText, publish, token) => tts.SynthesizeStreaming(
            stableText, reference, publish, cancellationToken: token),
        cancellationToken),
    CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
```

模型拥有者持续 `Append`；背压后停止追加并 `DrainPending`，EOS 时重试 `Complete`。
播放设备同时消费 `output.Render`；控制线程排空回执，并以设备呈现时钟确认历史。
`Produce` 将借用的 PCM 拆成队列块，同步复制后返回，队列满时保留当前块，以请求 1 ms
休眠的循环检查取消和回复代号；实际唤醒受 OS 调度影响，不是延迟上限。不会缓存整段波形。
仅最后一片携带段结束标记。相同调用线程之外、
合成返回后的回调、空/非有限/越界 PCM、缺少最终块和重复最终块均拒绝。
PCM 采样率和声道必须与输出一致；本循环不隐式重采样、裁幅或绕过模型的 token 上限。
文本段字符上限不能保证 Pocket 的 50-token 上限，应用仍须按模型配置切分并处理拒绝。

`Produce` 在文本 EOS 且各段合成完成后关闭输出生产，已排队 PCM 仍可播放；回复失效或输出
故障时返回 false。外部取消与合成错误向调用方传播，不会自动静音一个可能已切换的新回复。
控制器应立即门控旧回复、取消其 worker、等待其静止，再恢复上下文或释放模型。
`SynthesisMeter` 每个解码块记一次，从合成调用至首回调、以及相邻回调间的墙钟时间计入工作；
文本等待和 PCM 入队重试时间单独排除。调度竞争计入，不能把该读数当成物理播放延迟。
同步 native 调用期间仍依赖 backend 的合作取消，不能从队列取消测试推导 native 最长停止时间。

原有 `Load(directory, threads: 4)` 保持 Normal 精度。
`Load(directory, threads: 4, mainPrecision: MNNPrecisionMode.Low)` 可显式选择主图低精度；
只影响逐帧自回归主图，参考编码、priming、flow 和解码等阶段仍为 Normal。
目前仅接受 Normal / Low，其他值会在读取模型与调用 native 前拒绝。
低精度可能改变 EOS、音频时长和声音，需要按设备重新验证内容、音质和性能。
流式输出不等于实时合格：必须分别测首块延迟、持续/滑动窗口 RTF、实际播放欠载和 Omni 并发开销。

### IndicF5

`MNNIndicF5Tts.Load(modelDirectory)` 使用 `developerabu/IndicF5-MNN` 的固定 INT8 DiT、动态 Vocos
与匹配字符词表。`Synthesize(text, reference, referenceText, seed: 42, steps: 32, speed: 1)`
返回 24 kHz 音频。参考 WAV 为 0.5–12 秒且必须提供准确原文；总长度最多 2048 mel 帧。
支持 Hindi / Tamil 示例，Tamil 的独立识别仍有近音拼写限制；当前 ABI 为 macOS64 Mono。

### Turn-based buffered speech preflight

`MNNRealtimePreflight.Check` and `MNNRealtimeRouteSelector.SelectBest` keep strict streaming
requirements by default. Applications that intentionally synthesize a complete waveform may pass
`allowBufferedSpeech: true` together with `requireMeasuredSpeech: false`. The selected producer
must explicitly report `BufferedSpeech`; the audio endpoint must explicitly report
`HalfDuplexDevice`; and audio turn routes still require VAD. This opt-in does not relax native
duplex, echo-control, ABI, or continuous-understanding requirements, and does not claim an RTF
measurement or progressive PCM. Applications should gate capture during synthesis and playback,
then reopen it only after the presentation clock confirms the response has drained.

## 唤醒词检测

`MNNKeywordSpotter` 提供 `Detect`、`DetectAsync`、`DetectWaveFile` 和
`Reset` / `AcceptWaveform` / `InputFinished` 流式接口。
`MNNKeywordDetection` 包含关键词、声学置信度、起止秒数和 token ID。
当前支持固定 Wenetspeech 3.3M 中文 Zipformer2 模型，沿用随包 macOS Mono ABI；
关键词格式、Studio 操作和示例见 [KeywordSpotting](KeywordSpotting.md)。

### Text replies with realtime input topology

`MNNRealtimeRouteSelector.SelectBest`, the route overload of `MNNRealtimePreflight.Check`, and `MNNRealtimeFallbackRuntime` accept optional `speechOutput: false`. The default remains `true`. Text replies use AudioTextToSpeech or CascadedSpeech as the input topology, skip TTS/speech-throughput and playback echo requirements, and expose `MNNRealtimeReport.ProducesSpeech == false`. Model execution/ABI, cancellation and recovery, ASR (for cascaded input), VAD and capture capabilities remain checked. The supplied runtime factory must implement text generation without speech synthesis when `ProducesSpeech` is false; preflight itself does not mute or intercept audio. Fallback retains the selection across route retries.

### MobileCLIP 图文向量

`MNNImageEmbedding` 是共享的图像/文本编码 API，使用固定的
`budaoshou/MobileCLIP-MNN` 双 MNN 图和 `tokenizer.json`。它在 Android/iOS/桌面使用同一
`MNNGenerationGraph` 实现，不建立移动专用类型；`EncodeImage`、`EncodeText` 返回 512 维
L2 归一化向量，`MNNVectorSimilarity.Cosine` 可计算跨模态相似度。当前只认证这个精确导出，
其他 CLIP 或 Qwen-VL embedding 仓库仍需要各自的图输入和 tokenizer 协议。

### ERes2NetV2 声纹

`MNNSpeakerEmbedding` 使用 `Jeryuk/3dspeaker_speech_eres2netv2_sv_zh-cn_16k-common-MNN`
的官方图、Kaldi 25/10 ms fbank、每频带均值归一化和 192 维 L2 向量。`EncodeWaveFile` 支持
8–96 kHz 输入并统一重采样到 16 kHz；应用应自行使用同一数据集校准 cosine 阈值。

### PP-DocLayoutV3 文档版面

`MNNDocumentLayout.Load(directory)` 核验固定 `tansen87/PP-DocLayoutV3_mnn` 权重与
`layout-meta.json` 的 SHA-256。`Analyze(MNNRgbImage, scoreThreshold: .5f)` 或 `AnalyzeAsync`
返回 `MNNDocumentLayoutResult`；`Regions` 按模型预测阅读顺序排列，每个区域包含
`ReadingOrder` 和源图像像素坐标下的 `MNNDetection`（类别、置信度、位置与尺寸）。
同一 API 用于桌面、Android 与 iOS，不增加原生桥接。模型输入为 RGB、800×800 拉伸、pixel/255；
25 类标签只适用于这个精确导出。结果不包含文字识别、表格单元格或分割掩码。
此图在 Metal Normal / High 精度均未通过真实页面正确性检查，因此 `Auto` 选择 CPU；
显式 Metal 请求被拒绝。取消在预处理和原生执行前后检查，不中断已进入的原生会话运行。

## MoveNet 单人姿态估计

`MNNImageTask` 的 `MoveNetLightning` profile 使用共享官方 C++ 图调用。
`Process` / `ProcessAsync` 返回 `MNNImageTaskResult.Pose`：只读 COCO 17 关节、
源图连续像素坐标、置信度和 `IsInBounds`。保留低分／填充区预测，适合应用自行筛选。
Studio 提供独立姿态页面、骨架阈值、关节表和 PNG 保存。
精确权重、int32 NHWC 输入、letterbox 逆变换及验证范围见 [姿态估计](Pose.md)。

## DTLN AEC 与共享 FFT

`MNNDtlnAec.Load(directory, threadCount: 1, backendType: MNNBackendType.Auto)` 加载
Model Manager 安装的 128/256/512 profile，固定 CPU/High。`ProcessHop` 每路接受 128
个 16 kHz 单声道归一化采样并写入调用方输出；实例保留状态，`Reset` 开始新录音。
`Process` / `ProcessAsync` 处理两路等长数组，`ProcessWaveFiles` 读取 WAV；输出保留
24 ms 延迟，离线入口遵循上游帧数与裁剪规则。完整推理须放在工作线程。

`MNNFftPlan(length, preferBurst: true)` 为包内共享 radix-2 FFT。`Transform(real, imaginary,
inverse: false)` 原地处理独立的 `double[]`，逆变换除以长度；预热后无托管分配。
可选 Burst 程序集自动检测已安装的 Burst 1.8+，未安装时使用相同 C# 内核。
用法、DSP 约定与验证边界见 [DTLN AEC](DtlnAec.md)。

## Depth Anything V2 Small 深度估计

`MNNImageTask.Load(directory)` 读取已准备、校验哈希的 DepthAnythingV2Small profile，
通过共享 `Process` / `ProcessAsync` 返回 `MNNImageTaskResult.Depth`。
`MNNImageDepthResult` 提供原始相对逆深度网格、防御副本、最小／最大值和源图像素采样。
CPU / High，Auto 选择 CPU；显式加速后端拒绝。Studio 有独立页面和 PFM 导出。
模型准备、非米制单位、动态几何／内存限制及验证边界见 [深度估计](Depth.md)。
