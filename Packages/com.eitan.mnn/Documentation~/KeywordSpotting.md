# 中英文唤醒词检测（KWS）

支持 Wenetspeech 3.3M 中文和 GigaSpeech 3.3M 英文 Zipformer2 唤醒词模型。模型推理由现有的官方 MNN
C++ Interpreter、Tensor 和 Module API 执行；特征提取、关键词图、beam search、
置信度判定和流式缓存由 C# 实现。不依赖 sherpa 原生库，没有新增 C++ 或自定义 C 导出。

当前运行范围与包内直接 C++ 绑定一致：MNN 3.6.1、Apple clang/libc++ ABI v1、
macOS 64 位 Mono。它是本项目针对随包二进制维护的 ABI 契约，不是 MNN 官方跨平台 C# SDK。
其他操作系统及 IL2CPP 在加载前拒绝运行。

## 在 MNN Studio 使用

1. 打开 `Window > MNN > Chat Studio`，选择 `Keyword spotting (KWS)`。
2. 选择 `Jeryuk--kws-zipformer-wenetspeech-3.3M-MNN`。本工程已安装到
   `Assets/StreamingAssets/MNN/Models/`。其他工程可在 Model Manager 搜索
   `Jeryuk/kws-zipformer-wenetspeech-3.3M-MNN` 后下载，或选择已准备的本地目录。
3. 默认唤醒短语为“小爱同学”“你好问问”“小米小米”。可编辑 `Wake words`，一行一个。
4. 点击 `Start listening` 开始麦克风连续检测；结果显示关键词、时间和声学置信度。
   点击 `Stop listening` 或按 Escape 停止。监听期间不能切换模型或同时录音。
5. 也可选择 WAV 或 `Record audio`，停止录音后点 `Run` 检测全部匹配。
6. `Load demo recording` 会选中随模型安装的 `test_wavs/3.wav` 和它的关键词配置。
   点 `Run`，结果应包含“文森特卡索”和“法国”。`Use example wake words` 恢复默认短语。

直接输入想说的唤醒词，一行一个，无需填写拼音、token、`@` 标记或生成关键词文件：

```text
小爱同学
你好问问
小米小米
```

语言可选 `Automatic`、`Chinese` 或 `English`。单个 `MNNKeywordSpotter` 对应一种语言，
自动模式检查文字与所加载模型是否兼容；Studio 以选中的模型为准，选择英文模型时
默认 `Siri` 和 `Hey Siri`，并提供英文演示录音。选择中文模型时使用上述中文示例。
`MNNKeywordSpotter.Language` / `MNNKeywordSpotterConfig.Language` 公开模型语言。

SiriAI 运行时的唤醒词列表支持逐项添加、修改和删除，默认英文 `Siri` / `Hey Siri`。
选择自动语言后可在同一列表添加中文和英文短语，每项只使用一种语言；底层分组并仅加载
实际用到的语言模型，两种语言均存在时会同时运行两个检测器。也可以显式限定中文或英文，
不兼容的输入会在保存/启动前提示。单个短语内的中英混合、数字和标点暂不支持。

英文输入由 C# 自动规范化并按固定的 500-piece SentencePiece 词表分词。发布文件虽名为
`bpe.model`，其模型类型实际为 unigram，底层使用对应的 Viterbi 分词；开发者和用户
无需填写 token、转换文件或调用 Python。发音由英文声学模型负责，不用中文读音近似英文。

底层使用随包发布的离线汉字/词组发音表自动生成模型所需的声母、带调韵母并校验
词表和长度。常见多音词按最长词组匹配；人名、罕见字和特定语境读音仍需实际录音
确认。无需 Python、额外模型、联网转换或用户维护转换文件。
多个短语不能具有相同读音，否则会提示保留一个，避免同一声音对应多个结果。
旧版本保存的 token 配置仍可读取；新代码与 UI 只需填写自然文字。

阈值越高越难触发；boost 越高越容易保留关键词搜索路径，也可能增加误触发。
置信度是命中 token 的平均声学概率，不是校准后的“唤醒正确率”。重叠短语可能
优先返回较短短语；通过关键词配置及阈值在目标设备上评测。

## Runtime API

```csharp
using MNN.Unity;

using (var kws = MNNKeywordSpotter.Load(modelDirectory))
{
    const string keywords = "小爱同学";
    var matches = kws.DetectWaveFile(wavPath, keywords);
    foreach (var match in matches)
        UnityEngine.Debug.Log($"{match.Keyword}: {match.StartSeconds:F2}s, {match.Confidence:P1}");

    // 独立流式输入：16 kHz、mono、float32 [-1,1]，每次至多 30 秒。
    kws.Reset(keywords);
    var updates = kws.AcceptWaveform(pcm16kBlock);
    var finalUpdates = kws.InputFinished();
}
```

`Detect` / `DetectAsync` 接受 8–96 kHz normalized mono PCM，最多十分钟，内部重采样。
`DetectWaveFile` 接受包内 WAV 读取器支持的 PCM16/24/32、float32 mono/stereo 文件。
`AcceptWaveform` 使用有界音频和 recurrent 缓存，匹配可跨任意输入块。
`InputFinished` 补一秒静音以冲刷尾部，不重复返回结果；之后调用 `Reset` 开始新录音。
取消或原生错误打断已开始的流处理后，必须 `Reset`；实例串行化推理和销毁。
`DetectAsync` 复制输入以隔离调用者后续修改。Unity 麦克风 API 在主线程调用；
Studio 的 live 推理在后台运行，关闭窗口后会等待正在执行的原生调用返回再释放模型。
麦克风断开、设备格式不符或累计两秒处理积压会停止监听并提示重新开始。

## 模型与验证

发音数据来自 MIT 许可的 [pypinyin 0.55.0](https://github.com/mozillazg/python-pinyin/tree/v0.55.0)，
许可见 [发音数据许可](KeywordPronunciationLicense.md)。重建工具为
`Tools~/Models/generate_kws_pronunciations.py --tokens <已安装模型的 tokens.txt>`；
只在维护时需要 pypinyin 0.55.0，运行时全部由 C# 执行。

英文模型准备（本工程已安装到 `Assets/StreamingAssets/MNN/Models/English-KWS-GigaSpeech`）：

```sh
python3 Packages/com.eitan.mnn/Tools~/Models/prepare_english_kws.py --converter /path/to/MNNConvert
# 只验证现有模型，不下载、不转换：
python3 Packages/com.eitan.mnn/Tools~/Models/prepare_english_kws.py --verify-only
```

使用已有官方 MNN 3.6.1 转换器；工具固定 sherpa-onnx 官方
`kws-models/sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01` 发布包及 SHA-256，
校验源 ONNX、转换后 MNN、词表和 SentencePiece 模型后生成 `mnn_kws.json`。
转换日志在 `TestArtifacts~/EnglishKwsPreparation`。新工程需先完成此准备，运行时自动选模型
不会自动联网下载。模型及嵌入词表许可见 [英文模型许可说明](EnglishKeywordModelNotice.md)。
`--vocabulary-output <path>` 是维护时的词表重建选项，需要 Python sentencepiece；产品运行不需要。

中文模型下载固定版本 `df5d5e80a73b139d19217c46a9c72b6508c98b37`，只安装所需的
encoder、decoder、joiner、197-token 词表、说明和一个演示录音；不下载 ONNX 或其他重复权重。
下载时逐文件检查 SHA-256，准备根目录 `mnn_kws.json`；加载时重新验证三个图及词表。
支持仓库原始嵌套目录和本工程的扁平目录，修改模型文件名或任意替换图不受支持。
参考来源：[MNN 的 KWS 示例](https://github.com/alibaba/MNN/blob/master/apps/frameworks/sherpa-mnn/python-api-examples/keyword-spotter.py)、
[模型发布仓库](https://huggingface.co/Jeryuk/kws-zipformer-wenetspeech-3.3M-MNN)、
[sherpa 预训练 KWS 文档](https://k2-fsa.github.io/sherpa/onnx/kws/pretrained_models/index.html)。

测试覆盖关键词搜索、失败转移、重复命中、阈值与无效配置，真实录音、静音、无关关键词、
不规则 PCM 分块、状态复位、下载哈希和 Studio 路由。录音样例测试不能代表远场、
噪声或任意自定义短语的误唤醒率；真实麦克风环境需单独评测。
运行方法和本次证据见 [测试说明](Testing.md)。
