# 模型目录与联网获取 API

`MNNModelCatalog`、`MNNRemoteModel`、`MNNModelDownloader` 位于 `MNN.Unity`
命名空间和 Runtime 程序集。上层脚本只需引用 `MNN.Unity`，可在 Editor 或
支持 `System.Net.Http` 与文件系统的 Player 中调用，无需打开 Model Manager。

## 直接下载已知模型

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;
using UnityEngine;

public static class ModelAcquisition
{
    public static async Task<string> GetMobileLlmAsync(CancellationToken token)
    {
        string directory = Path.Combine(Application.persistentDataPath,
            "MNN", "Models", "MobileLLM-125M-MNN");
        using (var downloader = new MNNModelDownloader())
        {
            return await downloader.DownloadAsync(MNNModelSource.ModelScope,
                "MNN/MobileLLM-125M-MNN", directory, cancellationToken: token);
        }
    }
}
```

`DownloadAsync` 返回模型目录的绝对路径。已实现专用安装协议的模型会执行与
Model Manager 相同的解压、配置生成和资源校验。通用仓库保留推理文件筛选规则，
下载不代表该仓库已经具备可运行的推理协议；可查看目录项的 `runtimeAvailable`
和 `supportSummary`。

## 查询目录后下载

```csharp
var catalog = new MNNModelCatalog();
await catalog.LoadModelScopeModelsAsync(cancellationToken: token);
var candidates = catalog.SearchModels("MobileLLM");
MNNRemoteModel selected = candidates[0]; // 由上层选择模型。

using (var downloader = new MNNModelDownloader())
{
    string path = await downloader.DownloadAsync(selected, directory,
        onProgress: (receivedBytes, totalBytes, relativeFile) =>
        {
            // 回调可能来自工作线程；更新 UI 时先切回 Unity 主线程。
            // totalBytes 为 0 表示源站未提供大小。
        },
        cancellationToken: token);
}
```

`Models` 为当前目录列表，`GetModelsByCategory(MNNModelCatalogCategory)` 和
`SearchModels` 进行本地筛选。以下方法联网查询元数据，不下载权重：

| 方法 | 用途 |
| --- | --- |
| `LoadModelScopeModelsAsync` | 获取 ModelScope 官方 MNN 组织目录 |
| `DiscoverModelScopeModelsAsync` | 合并组织、标签和名称搜索，并检查仓库文件 |
| `LoadHuggingFaceModelsAsync(repositoryIds, ...)` | 获取指定 Hugging Face 仓库；省略 ID 列表时执行发现 |
| `DiscoverHuggingFaceModelsAsync` | 搜索公共 Hugging Face MNN 仓库 |
| `MNNModelCatalog.CreateDefault()` | 创建包含已发布 DTLN AEC 配置的离线初始目录 |

离线初始目录也包含可运行的 `EitanWang/Silero-v5.1-MNN`。联网发现该仓库时，
只有同时存在 `mnn_silero_vad.json` 和 `silero_v5_16k_fp32.mnn` 才标为
`runtimeAvailable`；实际加载仍校验固定 profile 和图哈希。MarbleNet、FSMN 与 CueLight 通过各自的已实现 profile 加载，
不会把任意同分类的原始图当作相同输入协议。

查询失败不会发布该来源的不完整结果。`DiscoveryWarnings` 提供被跳过的不可用仓库信息。
目录服务不包含编辑器磁盘缓存或窗口状态；上层可自行管理刷新和缓存策略。

## 音频任务分类与搜索

Runtime 目录、Model Manager 和 Studio 使用同一套音频任务分类。`AudioProcessing`
保留给尚无法细分的音频处理资源，界面显示为“其他音频处理”。已明确任务的模型归入：

| 分类枚举 | 用途 | 模型示例 |
| --- | --- | --- |
| `VoiceActivityDetection` | 检测哪些时间段存在语音，简称 VAD | Silero、MarbleNet、FSMN VAD |
| `TurnDetection` | 判断说话轮次是否结束、确定端点 | SmartTurn、TurnSense、NAMO、VoxTurn、CueLight |
| `SpeechEnhancement` | 语音增强、降噪、去混响 | DTLN、GTCRN、FRCRN、DeepFilterNet3 |
| `SpeechSeparation` | 从混合音频输出不同声源的波形 | ConvTasNet、DPRNN、SepFormer、MossFormer2-SS |
| `TargetSpeakerExtraction` | 利用参考语音或视觉条件提取目标波形，简称 TSE | SpExPlus、SEG、AV-DPRNN、AV-MossFormer2-TSE |
| `SpeakerDiarization` | 标记谁在何时说话，输出说话人时间段 | Sortformer、FS-EEND、LS-EEND |
| `AudioClassification` | 声音分类、音频标签与声音事件检测 | 具有对应 hub task 的分类模型 |
| `KeywordSpotting` | 唤醒词与关键词检测，简称 KWS | Zipformer KWS |
| `EchoCancellation` | 利用播放参考消除声学回声，简称 AEC | DTLN AEC |
| `SpeakerRecognition` | 声纹提取、说话人识别或验证 | ECAPA-TDNN、ERes2NetV2、WeSpeaker、VBx |

例如 `catalog.GetModelsByCategory(MNNModelCatalogCategory.TurnDetection)` 只返回轮次检测
模型。Runtime 与 Editor 的 `SearchModels` 均支持分类关键词，包括 `vad`、
`turn detection`、`denoise`、`target speaker`，以及“语音活动检测”“轮次检测”“降噪”
“目标说话人”等中文词。Model Manager 的分类筛选和文本搜索可以同时使用。

分类优先使用明确的任务元数据，再使用有边界的模型家族名称；不会根据描述中提到的其他
模型或仓库作者名称猜测任务。已发布条目同样经过这套规则。CueLight 以轮次检测为主分类，
保留语音活动检测的附加任务标签；目标提取与普通源分离分别展示。

新增枚举追加在末尾，保留原有序列化数字。Model Manager 缓存 schema 29 会在离线读取旧
快照时迁移明确的音频任务分类，保留旧磁盘快照和运行支持状态，并继续按原策略刷新。
Studio 的任务筛选接受新分类，执行仍使用相应的现有音频管线。分类描述模型用途；
`runtimeAvailable` 和各调用方的适配器继续决定模型能否运行。

## 编辑器与运行时统一分页

Model Manager 与运行时目录缓存共同调用 `MNNModelCatalog.FetchCatalogPageAsync`。
无查询词时，该 API 负责在官方组织、MNN 标签和名称三个检索流之间继续分页；
传入查询词时只遍历该查询的分页。调用方使用返回的 `NextStream`、`NextPage`、
`NextCursor` 和 `HasMore` 继续请求，不能在首个匹配模型或空分类页处停止。
`MNNModelCatalog.MatchesSearch` 统一本地名称、仓库、描述、标签、任务及中文音频分类搜索。

SiriAI 保存全部目录元数据，遍历完整分页，并按所选任务显示已安装及未下载模型，
不使用安装状态或示例适配器白名单裁剪目录。在线查询也继续全部分页；无法获取下一页时
保留已取得的条目和继续位置。声学选择经共享 `MNNModelDownloader` 获取模型，并在
确认本地 profile 后才更新设置；失败或取消保留原选择。关闭设置可取消当前下载。
普通 provider 模型仍在会话启动时下载。运行时缓存 schema 3 会重新发现旧版筛掉的目录，
并刷新执行支持和转写需求元数据。

## 共享音频执行入口

以下 Runtime API 按安装目录中的 profile 选择包内已实现的任务运行器。Editor 与 Player
可以调用同一入口；SiriAI 的新增适配使用这些入口，已有流式合成路径继续调用相同的包内运行器。
每个实例拥有独立模型状态，由调用方释放；不要在音频设备回调上执行离线推理。

| 入口 | 支持的已实现协议 |
| --- | --- |
| `MNNVoiceActivityDetector` | Silero、FSMN、MarbleNet、CueLight 的离线 VAD |
| `MNNProfiledVoiceActivity` | 固定 512 样本捕获帧；Silero 保留流式状态，其余使用有界 640 ms 窗口 |
| `MNNTurnDetector` | SmartTurn、NAMO、TurnSense、LiveKit、CueLight、VoxTurn、DualTurn |
| `MNNNoiseSuppressor` | DTLN、GTCRN、FullSubNet/Plus、MossFormerGAN、MossFormer2-SE、FRCRN、DeepFilterNet3、DPTNet、DCCRNet、DCUNet、DCCRN、Resemble Denoiser/Enhancer |
| `MNNSpeechSeparator` | MossFormer2-SS、SepFormer、ConvTasNet、DPRNN、SudoRMRF、TIGER、SepReformer |
| `MNNSpeakerEncoder` | 包内 speaker profile 与 VBx 8/16 kHz，按模型要求重采样 |
| `MNNSpeechRecognizer` | Qwen3-ASR、Paraformer、Whisper、SenseVoice、Zipformer |
| `MNNSpeechSynthesizer` | 包内 16 种已实现的 TTS profile，使用 `MNNSpeechSynthesisOptions` 配置 |

文本轮次检测器通过 `RequiresTranscript` 声明需要转写。SiriAI 即使使用 Omni 且关闭语音
输入校验，也为该检测器准备 ASR；一个候选语句只转写一次，轮次判断、校验和回复复用结果。
CueLight 同时出现在 VAD 和轮次任务中，按所选阶段创建对应 provider。

IndexTTS、FireRed、IndicF5、Pocket、DhVaani 需要用户提供参考 WAV；FireRed、IndicF5、
DhVaani 还需要该录音的准确文字。SiriAI 在选择后显示这些输入，WAV 路径可以是绝对路径，
也可以相对模型目录。缺少参考输入会明确报错。默认音色语言必须按实际运行器和所选音色
声明，例如 IndicVITS 的默认 `TAM_F` 只声明 `ta`，Qwen 显式协议只声明已支持的 `zh/en`。

SiriAI 中 DTLN 在捕获工作线程处理；其他增强模型在语句结束后为识别处理，VAD 与连续唤醒
使用原始音频，界面只显示有效的路由开关。共享增强入口使用独立的三秒块保留输入时长，
块边界可能影响质量。共享分离入口使用三秒窗口及 250 ms 重叠，在重叠上匹配两个声源并
交叉渐变；静音间隔无法保证持久说话人身份。共享 ASR 使用二十秒块，避免固定窗口吞掉
长录音末尾。这些封装不保证实时吞吐或模型在所有设备上的精度。

目录可检索性与可执行协议是不同边界。任意第三方 `.mnn` 文件还需要相应分词、特征、
状态及输出协议，不能仅根据任务标签假定兼容。目标说话人提取和说话人分段也不是对话 VAD
或双声源分离的替代：部分模型需要目标参考或视觉输入；SiriAI 当前没有这些任务的输入阶段。
这类条目保留在共享目录，不能冒充已有的对话适配器。此改动复用现有托管运行器，没有增加
原生桥接或 C++ 代码；现有原生调用测试不构成官方 C++ API 直接互操作验证。

## 下载行为和资源所有权

- `MNNModelSource` 支持 `ModelScope` 和 `HuggingFace`。可直接传仓库 ID，或把
  `MNNRemoteModel` 交给 `DownloadAsync`。通用仓库支持指定 `revision`，默认分别
  为 `master` / `main`；已有专用配置继续遵守其固定版本和哈希约束。
- 每个模型使用独立的可写目录；一个目录同一时刻只发起一次获取操作。Player
  推荐 `Application.persistentDataPath/MNN/Models`。编辑器中需要随包发布的模型放在
  `Assets/StreamingAssets/MNN/Models`，下载后由上层按需刷新 AssetDatabase。
- 默认保留断点文件。取消后再次使用相同目录、来源和版本调用即可续传；已完成文件
  按源站提供的 SHA-256（及已知大小）校验后复用。源站未提供 SHA-256 时会重新传输
  已完成文件，避免将相同大小的旧版本误认为新版本；也不能承诺哈希校验。
- 可通过 `MNNModelTransferOptions` 设置连接数、并行阈值、重试次数与断点保留策略。
  同一个 downloader 共享连接上限。关闭断点保留时，仓库传输失败会清理其下载目录。
- `DownloadRepositoryAsync`、`DownloadHuggingFaceRepositoryAsync` 和
  `DownloadRepositoryFileAsync` 提供仅传输文件的接口，返回字节数，不执行安装准备。
  `GetRepositoryFilesAsync` 返回 ModelScope 文件清单。
- 可以注入 `HttpClient`、ModelScope API 根地址，以及
  `MNNHuggingFaceSourcePool` / `MNNHuggingFaceSource` 配置镜像。注入的客户端由调用方
  释放；默认创建的客户端由 downloader 释放。所有正在执行的操作结束后再释放 downloader。
- 此 API 使用 `System.Net.Http` 和本地文件，不适用于 WebGL。移动端设备上的联网行为
  仍需按项目目标平台验证。

Editor 中已有的 `MNNModelRepository`、`MNNModelDownloadService` 和镜像类型保留原
命名空间与调用签名，内部委托给以上 Runtime API；窗口继续负责安装索引、任务持久化、
最终目录发布与 AssetDatabase 刷新。
