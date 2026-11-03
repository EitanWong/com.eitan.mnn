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
`runtimeAvailable`；实际加载仍校验固定 profile 和图哈希。MarbleNet、FSMN 等
发布条目保留各自的运行支持状态，不因同属 VAD 任务而自动变为可运行。

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
