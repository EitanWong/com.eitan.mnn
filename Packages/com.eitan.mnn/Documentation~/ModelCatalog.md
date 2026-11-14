# 双平台 MNN 模型目录

MNN-Pocket 系列的固定部署版本、源权重格式及 SiriAI 默认项见 [MNN-Pocket 接入](MNNPocket.md)。

管理器内置 46 个已发布的 `EitanWang` 语音处理仓库，无需等待 ModelScope 搜索索引；
三个已有 DTLN-AEC 仓库继续使用各自的固定下载方案。46 个仓库下载完整发布文件，
包括配置、运行常量和许可，使用可变的 `master` 版本；目录收录不构成新的推理或平台验证。
输入约定和许可限制以各模型卡为准。尚未发布的 LiveKit Turn Detector 不作为可下载条目。

模型管理器现在搜索 ModelScope 和 Hugging Face 的公开仓库，不再只读取
ModelScope `MNN` 账号和三个指定的 Hugging Face 仓库。

| 平台 | 合并的发现来源 | 文件证据 |
| --- | --- | --- |
| ModelScope | `owner=MNN`、`filter.library=mnn`、`search=mnn` | 逐仓库读取递归文件清单 |
| Hugging Face | `author=taobao-mnn`、`filter=mnn`、`search=mnn` | 分页 `full=true` 结果中的 `siblings` |

管理器按需逐页读取各查询流，并按平台和仓库 ID 去重；搜索词直接发送到两个平台，搜索结果也按需续页。名称搜索会包含误匹配，社区结果必须
列出 `.mnn`、`.mnn.weight`，或在仓库/文件名称、标签明确标识 MNN 时列出归档包。
`mnneely` 之类包含字母子串的账号不算 MNN 标识。官方辅助资源仍可检索，但没有图文件的
占位仓库不能据此运行推理。归档包仅凭文件名和标签入选，其内容尚未检查。

打开管理器优先使用内存或磁盘缓存；常规目录首页在 24 小时后后台刷新，输入搜索词和滚动到末尾仍会按需联网。缓存保存在工程的
`Library/MNN/ModelCatalog/`，按平台、schema 版本分别保存已获取的页面、续页位置和发现警告，
安装状态和本地路径由当前工程重新检查。过期目录仍立即显示，同时后台更新；缓存损坏或
schema 不兼容时重新发现。兼容的旧版 schema 缓存仍可用于临时展示，但始终要求后台刷新；
它不会因原时间戳仍在 24 小时内而被当成新鲜目录。工具栏 **Refresh** 强制跳过缓存，重新搜索并读取仓库文件清单，
不会仅重新绘制旧列表。刷新期间仍可浏览已有目录。

两个平台同时刷新；单个平台失败时保留其旧目录，另一个平台完成后即可显示。
每个平台依次遍历三个搜索流，每次只读取一页；ModelScope 在该页内最多并发读取四份仓库清单。
运行时可通过 `MNNModelManager.SearchModelScopeCatalogPageAsync` 和
`SearchHuggingFaceCatalogPageAsync` 获取单页，并使用返回的页号或游标继续读取。
多处同时请求同一缓存页面会共用一次刷新，避免重复扫描。
分类阶段复用线程安全的正则规则实例，避免超过系统正则缓存容量后反复解析规则。
在 Unity 自带 Mono 上用固定 1,116 条目录对比优化前后源码，五次分类中位耗时从
315.53 ms 降至 47.23 ms，分类结果逐条一致；该测量仅包含分类计算，不包含网络和 UI。
目录元数据请求收到 HTTP 429 时会遵守 `Retry-After`（秒数或日期），同一站点的搜索、
分页和文件清单共享冷却时间。限流后降低请求频率，连续成功后逐步恢复；同一批失败响应
合并计算限速惩罚，避免并发失败把延迟成倍放大。单请求最多重试三次；服务端要求等待超过
30 秒时保留旧列表并报告冷却时间，不提前反复请求。取消会中断退避等待，另一个站点不受影响。
无效响应和一般网络错误不会覆盖已有目录；Hugging Face 连续重复游标会报错，跨页重复模型按身份合并。
远端排序变化时分页结果可能重叠或变化。ModelScope 文件接口返回 401、403、404、410
的仓库会单独报告并跳过，避免搜索索引中的已删除结果阻止所有模型刷新。
已获取页面的快照以临时文件原子替换旧缓存；刷新失败保留旧文件。关闭窗口后不再更新窗口，
共享刷新继续完成并保存缓存，重新打开时可以复用。目录底层的显式发现 API 仍支持取消，
并始终联网，便于审计和测试。

不同作者、不同平台的同名仓库使用带平台和仓库哈希的独立目录。已有官方 ModelScope
目录保持原位置；旧社区安装只有在安装标记与仓库身份吻合时才会复用，不会把同名仓库
误认为已安装。下载仍经过现有后台任务、完整性校验和安装流程，不会因刷新目录而下载权重。
通用仓库安装保留整个仓库，可能包含多个量化版本和其他格式；点击下载前应检查仓库文件与体积。
Hugging Face 搜索结果通常没有逐文件大小，目录中的零大小表示未知，下载阶段重新获取清单。

## 能力与验证范围

当前已实现双平台模型发现、目录接入、安装身份隔离及嵌套语言模型变体发现，并为三个
社区 BERT/MiniLM 模型增加实际向量推理管线。其固定版本安装、分词与池化说明见
[API](API.md#bert--minilm-向量模型)，真实测试见 [测试说明](Testing.md)。
其他新增仓库仍需要逐个确认任务协议及实际结果；全部模型的推理接入尚未完成。

`MNNModelInfo.mnnFiles` 保存文件证据，`supportSummary` 说明当前接入程度。
`runtimeAvailable` 仅表示文件名符合已有管线的候选结构，不是已完成原生推理验证的标记。
实际加载仍检查配置、算子、模型架构、平台 ABI 及任务资源。归档候选、原始计算图、
新 talker、Sherpa ASR、OCR 和其他检测协议等不能自动套用已有管线。

已安装仓库中独立的 `int4/`、`int8/` 等子目录如果包含 `config.json`、`llm_config.json`、
配置中指定的 tokenizer（默认 `tokenizer.txt`，也支持 `tokenizer_file` 指向 `.mtok`）和 `.mnn` 文件，
可以作为独立语言模型被 Studio 发现。
随包官方 tokenizer 已包含 `.mtok` PipelineTokenizer；目录候选识别同时接受根目录 `tokenizer.txt` / `tokenizer.mtok`。
ModelScope `TorryJi/bge-small-zh-v1.5-MNN` 已用官方 Embedding API 通过中文检索和 Studio 真实验证，
其 512 维输出复用既有 native 管线，不使用三种英语 BERT 图的托管 WordPiece profile。
其他 `.mtok` 仓库仍是候选，不能据此认定新架构、量化方式和算子兼容。
ModelScope `MNN/Qwen2.5-0.5B-Instruct-MNN` 已通过 package 下载器安装并在 Studio Chat 正确回答法国首都；
Apple M5 上 Auto 选择 Metal，10 个对话 token 用时约 0.385 秒。同提示的原始生成热身测量为 Metal 14 tokens/56 ms、
CPU 16 tokens/107 ms；这只是该设备与该模型的一次性能样本。Hugging Face revision
`c880456f5ae3d591fd8fc6c32a742cc6ace500cb` 的七个推理文件 SHA-256 与实测 ModelScope 仓库一致，
因此复用相同的推理验证；README 和仓库任务元数据不参与该等价判断。
此前 package 联网快照有 1,117 个仓库；排除误匹配后，分类快照有 1,116 个
（ModelScope 547，Hugging Face 569）。当前包含五个 Whisper profile 推理、安装、
目录缓存和刷新的合并 Editor 回归为 152/152 通过；此轮复用该分类快照测量缓存，
没有为该性能测量重新扫描公开索引。之后在主工程验证旧缓存恢复与真实后台更新：
ModelScope 成功生成 545 条新快照，Hugging Face 复用 569 条有效缓存，窗口显示 1,114 条。
重新打开窗口时列表立即可用，缓存文件没有重写；一个已删除仓库的 404 单独报告。
这些数量对应不同时间的公开目录，仍可能变化。
当前 schema v12 固定目录的缓存读取中位耗时为内存 0.55 ms、磁盘 13.90 ms；读取过程中没有网络请求。
最新合并 Editor 回归为 191/191 通过，覆盖缓存、强制 Refresh、请求合并、限流、分类及 Studio。
嵌套 Embedding/Reranker 保留父仓库任务；Diffusion/TTS/ASR 的内部语言阶段不会作为独立聊天模型暴露。

已有文本/多模态、Embedding、Qwen3 Reranker、SD 1.5、Sana、Supertonic、Piper、
Bert-VITS2、CosyVoice、IndexTTS、FireRed 的实现与真实验证范围见
[API](API.md)、[Studio](ChatStudio.md) 和 [测试说明](Testing.md)。
发现某个架构的其他量化版本，不代表已验证该版本。新架构仍需要对应的输入输出协议、
分词器、前后处理和真实结果测试；本次没有增加原生导出、桥接库、C++ 或修改官方 MNN 源码。
现有编译器/平台限定仍按 [ABI 约定](../Native~/README.md) 执行。

## 可复现的目录审计

在项目根目录运行，无需安装依赖：

```bash
python3 Packages/com.eitan.mnn/Tools~/Testing/discover_model_catalog.py \
  --output TestArtifacts~/ModelDiscovery/LiveAudit
```

脚本只获取搜索结果和文件清单，不下载权重或归档包、不执行原生推理。
输出包括 `catalog.json`、`repositories.tsv`、`summary.json` 及按请求 URL 哈希保存的原始响应。
`summary.json` 单独列出不可访问仓库和其他错误；查询或清单发生其他错误时进程返回非零，
审计结果仍保留以供检查。公开目录会变化，仓库数量是查询时的快照，不是固定测试断言。

Editor 代码也可直接调用：

```csharp
var catalog = new MNN.Unity.Editor.MNNModelRepository();
await catalog.DiscoverModelScopeModelsAsync(cancellationToken: cancellationToken);
await catalog.DiscoverHuggingFaceModelsAsync(cancellationToken: cancellationToken);
// catalog.models: 两个平台的合并目录；catalog.DiscoveryWarnings: 被跳过的不可访问仓库。
```

`LoadHuggingFaceModelsAsync()` 默认执行上述全范围发现；显式传入仓库列表时保持原有指定仓库行为。
`LoadModelScopeModelsAsync` 保留原有官方账号接口；需要社区覆盖时调用 `DiscoverModelScopeModelsAsync`。
无网络的 HTTP 替身测试是 `ModelDiscoveryTests`。显式联网测试
`ModelDiscoveryTests.LiveDiscovery_AuditsBothPublicProviders` 执行 package 本身的双平台发现路径，
输出 `package-catalog.json` 和警告，可用 `MNN_TEST_CATALOG_OUTPUT` 指定项目内产物目录。

搜索覆盖有明确边界：未标记 MNN 且名称不含 MNN 的社区仓库、私有/受限资源、只在 README
提到可转换为 MNN 的模型以及归档内部文件，没有被穷举检查。当前实现不能宣称涵盖两站所有
潜在可转换模型，也不能把目录审计结果当作“所有 MNN 模型已能推理”。

ModelScope `rap20171201/YOLOv8-N-MNN` 新增了实际目标检测管线、SHA 固定的安装准备和
Studio 图片检测/标框导出。该导出需要 anchor-distance + stride 解码；其他 YOLO 文件
不因此成为已验证模型。API 和结果范围见 [API](API.md#yolov8-目标检测) 与 [测试说明](Testing.md)。

Hugging Face `qefro/pp-OCRv5-mnn` 当前包含十二组页面/单行识别 profile：固定
英文、中文、Latin、Cyrillic、希腊文、阿拉伯文、天城文、Eastern Slavic、韩文、泰米尔文、
泰卢固文和泰文图/字典，接通 Studio 的 OCR 工作区及子模型选择。
另有 Chinese v4、Chinese fp16 两组变体，各自关联配套检测图，共十四组可选 profile。
固定仓库全部三十项图/字典资产经 SHA 校验；目录缓存 schema v8 会更新旧安装计划。
新安装默认支持整页检测、逐行识别和标注图。已有安装需重新下载才能获得新增 profile。
Auto 页面配置与韩文单行采用 CPU；其他单行 Auto 保留既有后端选择。
方向分类尚未接入。v4 空格差异、韩文裁剪留白和显式 Metal 的已知准确率限制见 API 文档。
新增性能基准对 CPU/Metal 的 1/2/4/8 线程逐项检查输出质量；已优化 YOLO 的托管预处理。
其他模型仍需各自的性能测量、配置选择和质量回归，不能把 `Auto` 当作已证明最佳性能。

## 分类规则与人工核查

分类实现独立为 `MNNModelRepository.Classification.cs`，优先使用明确的任务元数据，
再用仓库模型名称和图文件证据。作者名和自由描述不会触发分类；`MNN`、`weights`、
`model` 等通用词也不会触发 NLP。无法确认用途的新仓库仍保留 Other。

新增分类包括图像抠图、图像复原、三维重建、数字人动画、人脸关键点、文档版面与表格、
图像/多模态向量、视频生成、音频生成、说话人识别、文档矫正、翻译、深度估计、机器人、
模型合集、运行库资源和模型诊断样本。已有枚举的序号保持不变。
例如 VGGT 属于三维重建，2D106Det 属于关键点，PGL-Net 属于复原；
Qwen-VL-Embedding 属于多模态向量，不当作聊天模型。混合任务归档或图合集保留合集身份。

`ArcherFD/SAFlow` 的模型卡明确描述扩散风格迁移，不能因名称 Flow 将它判断为光流。
`yunfengwang/music3-mnn` 的固定版本模型卡描述 MiniMax 音乐生成，不能套用 TTS。
`ritzz01/cnn-mnn-model` 的模型卡将 MNN 定义为 Multi-Head Attention Network，
仅列出 `Flickr8k_text.zip`，因此排除其归档搜索误匹配；如果将来出现实际 `.mnn` 文件则重新收录。
诊断图、运行库和官方辅助资源可检索，但其类别不是 Studio 推理支持承诺。

显式离线测试 `ModelClassificationTests.AuditCachedCatalog` 使用 package 本身的规则复查缓存，
通过 `MNN_TEST_CLASSIFICATION_INPUT` 指定目录快照、`MNN_TEST_CLASSIFICATION_OUTPUT`
指定项目 `TestArtifacts~/` 内输出位置。输出完整分类、前后对照、排除项和剩余 Other，
不下载权重、不运行图，也不覆盖原始快照。新增通用分类仍需要实际任务管线和质量回归。

本次离线复查从原始 1,117 仓库快照中排除上述一个误匹配，保留 1,116 个
（ModelScope 547、Hugging Face 569），Other 从 47 降至 0。
结果见 `TestArtifacts~/ModelDiscovery/Classification/{catalog.json,classification.tsv,excluded.tsv,summary.json}`。
这是对当前元数据与已核查模型卡的任务归类，不代表每个架构已完成推理验证，
也不保证未来发现的仓库都能自动归类。Studio 新类别均有对应入口；尚无任务适配器的入口
会明确给出准备要求，翻译工作区仅能运行已有完整 LLM 配置的翻译模型。

## Zipformer v1 ASR profiles

Model Manager installs both official English (2023-02-21) and Chinese/English
(2023-02-20) Zipformer repositories from ModelScope or Hugging Face. Their graph
and token hashes match across hubs. Installation verifies these files and writes
`mnn_zipformer.json`; cache schema v8 updates the support metadata. SenseVoiceSmall and keyword-spotting repositories are classified separately. Studio's
**Speech recognition → Transcribe** runs the standalone managed greedy pipeline,
including recording/file attachments. Missing or altered assets are rejected.
Existing installations without a prepared profile must be installed again through
Model Manager; merely finding encoder/decoder/joiner names does not establish a
compatible runtime.

The frontend matches kaldi-native-fbank 1.21.1 on two upstream WAVs (mean absolute
log-mel error below 3e-6). The English sample matches its complete upstream
`trans.txt` reference. The bilingual sample is executable and deterministic but
has a recognition error (`LIBR` in its English segment); this is not a claim of
perfect Chinese/English transcription. Auto selects CPU: measured Metal runs give
the same transcripts but are slower because each chunk synchronizes many states.
These checks do not establish compatibility with other Sherpa architectures.

## SenseVoiceSmall

`wo105090/SenseVoiceSmall-MNN` is a pinned ModelScope profile with `model.mnn`,
`model.mnn.weight` and `tokens.txt`. Model Manager verifies all three SHA-256 hashes,
writes `mnn_sensevoice.json`, and exposes it under Speech recognition. The Studio
backend uses CPU for this graph because the tested Metal session produced degraded
transcripts. A M5 median whole-recording run is about 146 ms for the English fixture
and 223 ms for the Chinese/English fixture with four threads.

HF `xinliu/sensevoice_mnn` is independently pinned at
`7ca517021b1a780246859989f2dd12c1f1db3520` and verified with its graph/token hashes.
Its data-dependent shape operators require the existing official Express path.
Model Manager also prepares its `mnn_sensevoice.json` for Studio. Keyword spotting,
VAD and turn-detection repositories are classified as AudioProcessing rather than
SpeechRecognition.

## Whisper

The four Hugging Face `developerabu/whisper-{tiny-en,tiny,base,small}-mnn`
repositories and ModelScope `rap20171201/sherpa-mnn-whisper-tiny.en` have pinned
installation profiles. Each plan downloads three matching assets at its fixed
revision and verifies their SHA-256 hashes. Model Manager writes
`mnn_whisper.json`; Studio routes the prepared model to the CPU greedy recognizer.
Cache schema v8 updates all five repositories' support metadata. English is
available for all five; only small exposes the tested Chinese prefix in Studio.
Tiny/base Chinese decoding did not meet the fixture quality/termination gates.
See [API](API.md#whisper-speech-recognition) for recording and language limits,
and [Testing](Testing.md#whisper-asr) for exact evidence. Other Whisper repositories
remain unvalidated.

## Qwen3-ASR

The verified 0.6B INT8 export is available from Hugging Face
`hb-dev/Qwen3-ASR-0.6B-INT8-MNN` and ModelScope
`huangzhengxiang/Qwen3-ASR-0.6B-INT8-MNN`. Model Manager uses immutable per-file
plans, verifies sizes/SHA-256, and prepares `mnn_qwen3_asr.json`. Both plans install
the same eight inference assets plus their source notices. ModelScope's newer
configuration is pinned separately from its weight commit; an old mutable resume
manifest cannot override this plan. Renamed installation folders retain ASR routing.

Catalog schema v9 updates their support metadata, while compatible v3–v8 snapshots
remain visible until refresh succeeds. ASR repositories require a prepared task
profile before Studio can run them; generic LLM filenames do not prove ASR support.
Nested `yunfengwang/Qwen3-ASR-MNN` variants have a different audio protocol and are
not covered by this profile. See [API](API.md#qwen3-asr-speech-recognition).

## Paraformer online 8404

以下三个仓库使用相同的五项推理资产，已接入 Model Manager、中文 ASR 和 Studio：

- Hugging Face `KindBrave/speech_paraformer-large_asr_nat-zh-cn-16k-common-vocab8404-online-mnn`，版本 `b6b9dd4471cffd6b59f283b2998284d38f4d31e0`。
- Hugging Face `guoera/speech_paraformer-large_asr_nat-zh-cn-16k-common-vocab8404-online-mnn`，版本 `a0d72b0dddeb36ec1d6c4bf8fe29e87d8832b39d`。
- ModelScope 的同名 KindBrave 仓库，版本 `c6d2ad13a5133ad7f212aa434c5333a9b477c70c`。

安装器只下载固定版本的 encoder、decoder、两份配置、tokenizer 和来源 README，
约 865 MB；校验后生成 `mnn_paraformer.json`。旧的可变续传清单不会覆盖固定方案。
其他名称相近的 Paraformer 导出使用不同协议，不会自动套用该 profile。
默认 CPU；中文参考文本通过逐字比对，夹英文仍有错误，英文质量未验证；无时间戳。

缓存 schema v10 更新 Paraformer 支持信息，同时把具有完整 MuseTalk 图证据的
`bruice/MuseTalk-MNN` 从错误的默认检测标签修正为 AvatarAnimation；这不代表
MuseTalk 推理已接入。旧缓存仍可展示并在后台刷新。

## Silero VAD

Hugging Face `Jeryuk/silero_vad-MNN` 固定版本
`e2502b184542017860ab1ca83c18f8742b221ecd` 已接入语音活动检测与 Studio
Audio processing。安装仅下载经 SHA-256 校验的 1,486,656-byte MNN 图和
来源 README，不下载用于离线参考的 ONNX。安装后生成 `mnn_silero_vad.json`。
目录 schema v12 刷新支持信息；同名其他发布者/平台不会自动套用该协议。

原始图含条件子图，通过现有官方 Module API 执行；公开 MNN 序列化格式
将 PCM/循环状态打包成单输入、概率/新状态打包成单输出。保留磁盘原始图
和子图，不新增 C++、bridge 或自定义导出。默认 CPU1；Metal 质量不通过。
中文/英文/静音及两段语音参考覆盖中有一个 64 ms 的量化边界偏差，见
[Testing](Testing.md#silero-vad)。

## Smart Turn v3.2

Hugging Face `Jeryuk/smart-turn-v3.2-gpu-MNN` 固定版本
`1cc21f7b6f904eb1a5daadc27c6f4931d1f78794` 已接入 Audio processing。
安装方案仅下载 9,061,752-byte MNN 图及 README，按 SHA-256 校验，生成
`mnn_smart_turn.json`。原始 ONNX 仅用于包外验证，不随模型安装重复下载。
目录 schema v12 更新支持信息，同名其他发布者不会自动套用此协议。

它预测当前说话轮次结束概率，不能归为 ASR 或 VAD。默认 CPU4，最后八秒上下文；
完整/未完整阈值为严格大于 0.5，量化概率可能偏移。Studio 可以显示概率及决策，
详细参考和性能范围见 [Testing](Testing.md#smart-turn-v32)。

通用 MNN LLM 仓库安装时会优先保留 `.mnn` 图及其权重、MNN 图配置、
`llm_config.json`、tokenizer 和生成配置，跳过 safetensors、训练数据等转换输入。
只有文件清单同时包含 MNN 图、LLM 配置和 tokenizer 才启用筛选；其它模型保留
完整仓库文件，既有固定模型配置继续使用各自的最小安装计划。

## 新增 TTS 固定安装与验证边界

缓存 schema v13 增加 `dr3334/Kokoro-82M-v1.0-MNN`、
`dr3334/Supertonic-3-INT8-MNN`、`wangjazz/kokoro-82m-mnn` 和
`developerabu/vits-tts-mnn` 的固定安装方案。每个资源限定 revision、尺寸和 SHA-256；
旧续传清单不能替换这些计划。wangjazz 方案补齐配套词典并固定其来源版本。
已完整下载的模型复核文件后直接复用，无需再次获取目录或权重。

Supertonic 2/3 的十音色、Kokoro 三种布局、Indic VITS 的 speaker/emotion 控制已接通
Studio TTS。真实内容、播放保存以及仍存在的 Kokoro 稳定性问题，见
[Testing](Testing.md#kokorosupertonic-23-与-indic-vits2026-10-18)。
Indic 的全部控制仅通过波形检查，语言内容目前只部分核对 Tamil，不能等同全部语种可用。
Qwen3-TTS 的其他导出和其他未适配的 Sherpa VITS
仍需相应协议、前端与真实合成验证；不能把目录中的 TTS 分类视作已经完成运行接入。

schema v14 增加 KindBrave Kokoro v1.1 两站的固定八文件安装方案与外置权重支持。
103 音色均可执行合成，全部音色的内容质量仍待完成；具体未通过项见 Testing。

schema v15 增加 `developerabu/mms-tts-tam-mnn` 的固定 INT8 三文件方案、Tamil 字符前端和
Studio TTS 路由。安装后生成任务准备文件和原始模型来源/许可说明；不因目录含有同名
`model_int8.mnn` 就自动套用。完整句子质量及同仓库其他精度变体仍待验证。

schema v16 将 MMS Tamil 固定安装扩展为全部三个精度图，共五个下载文件。
Studio 的 Precision 菜单可选择已安装的 INT8、FP16/INT8、FP32，切换后重新加载推理实例。
后二者的严格 Tamil ASR 内容审计未通过，不能因管线可执行就宣称质量已验证。

schema v17 接入 KindBrave Chinese Sherpa VITS 的双平台固定六文件方案与五说话人管线。
HF 和 ModelScope 文件哈希一致，声音参考句均通过独立 ASR，Studio 支持 16 kHz 播放保存。
不下载未接入的 Jieba/FST 资源，数字与多音字限制见 API。不会自动套用所有 VITS 导出。

schema v18 增加两种 MOSS Nano 安装方案：ModelScope 的
`kiven2008/moss-tts-nano-100m-mnn`（固定采样帧，十文件）和
`ZhangZes/MOSS-TTS-Nano-MNN`（逐步局部 KV 缓存，九文件）。
两者均进入 Studio TTS，提供 18 音色和 48 kHz 单声道输出；每个文件固定 revision、大小及 SHA-256。
完整安装复核后续传不发 HTTP 请求。普通打开继续使用内存/磁盘缓存，Refresh 仍强制联网。
中文/英文合成通过；音色及语言的内容质量范围单独记录在 Testing，不能由目录可运行标记推断。
Qwen3-TTS 的高层官方 API 实验尚未通过，因此仍不标为可运行。

schema v19 增加 Hugging Face `Bbkblo/DhVaani-0.5-MNN` 的 v1.1 固定八文件安装与 Studio
参考语音管线。revision 为 `9ecccbbba2d6269dc083ab1975800de8b0dbe1c1`；下载文件逐一固定大小和 SHA-256。
Hindi/English 内容样本、实际 Studio 播放保存及无网络续传均已验证，仍需准确参考原文。
同名的其他发布者及早期静音图不自动复用此协议。

TTS 仓库即使包含 `llm_config.json`、tokenizer 和 MNN 图，也不能直接使用通用 LLM
规则标为可运行；仍需相应的语音任务管线。此项避免未通过验证的 Qwen3-TTS 被误标，
普通 Chat LLM 的完整配置继续使用原有通用管线。

schema v20 增加 HF `yunfengwang/Qwen3-TTS-12Hz-0.6B-Base-MNN` 的固定 47 文件计划，
包含三阶段图、FP16 投影/嵌入表、BPE 与预编码 demo 参考声音。
仅此显式 KV 协议标为可用（中文/英文、24 kHz），不含原始 WAV 克隆编码器。
Studio 检查固定图/嵌入表/分词文件的尺寸与预编码音色数据；截断下载不能进入推理。
额外音色放入 `voices/<name>/`，通过同协议校验后在 Voice 菜单显示。
`huangzhengxiang/Qwen3-TTS-0.6B-Base-{INT8,FP16}-MNN` 仍未通过质量验证；
泛化的 llm_config/tokenizer 证据不能自动启用 TTS。

### Piper Lessac high

`ekremabiii/pipertts_mnn` 分类为语音合成，采用固定 11 文件安装计划，包含原仓库缺失的
英文 eSpeak-NG 数据及原始声源说明。Studio 使用 Lessac 单音色、22.05 kHz 单声道，
其标准图输入与原先三种低采样率 fp16 音色分别识别。外部 eSpeak-NG 工具仍需可用。
目录缓存 schema v24 会后台更新旧缓存中的该模型能力与完整安装体积；Refresh 始终强制重取。
真实测试范围及声源条款入口见 [测试说明](Testing.md#piper-lessac-high-完整安装)。

### Kokoro 单音色空 styles 导出

`Jeryuk/kokoro-v1_0-MNN` 原始 `tts_config.json` 的 `styles` 为空，但 `voices.mnn`
恰好包含一份与已验证 `zf_xiaoxiao` 原始表完全相同的数据。仅匹配这份已知 voice 图的
SHA-256，恢复该音色名称，保持原始配置不变；其他空列表不会自动套用这个音色。
固定 8 文件计划补齐匹配的中美英语词典及原始许可证，输出 24 kHz 单声道。
真实中英文合成、独立转写、下载复用及 Studio 测试见
`TestArtifacts~/ModelDiscovery/Tts/kokoro-single-complete.xml`（5/5）。

本地安装状态每次刷新只扫描一次安装标记，并按平台与完整仓库 ID 建立索引。
已正确标记的模型即使文件夹改名，仍可从 `Assets/StreamingAssets/MNN/Models/` 复用；
同名不同作者或不同平台不会合并。一个仓库存在多个改名安装目录时，不任意选择其中之一。
原来的标准安装位置优先，未标记目录仍可由 Studio 直接发现，不据名称猜测仓库身份。

### Kokoro v1.1 expanded

`starsharp06sharp/kokoro-multi-lang-v1.1-MNN-Multilingual` 的 FlatBuffer 元数据声明 103 个有名称音色，但文件实际附加了 53 个未命名表。安装计划固定 8 个可用推理文件，Studio 只暴露 103 个有名称音色；其余表不会被猜名。图和外部权重独立校验，中文/英文词典匹配，24 kHz。已验证 `zf_001`、`zm_009`、`af_maple` 的实际合成和独立转写。

### Bert-VITS2 镜像与自定义仓库

Hugging Face `taobao-mnn/bert-vits2-MNN`、`KindBrave/bert-vits2-MNN-Custom` 和
ModelScope `KindBrave/bert-vits2-MNN-Custom` 使用固定 23 文件计划，包含中英文 BERT
图、外部权重、生成器、全部已实现前端资源和配置。三处全部推理文件与本地已验证
`MNN/bert-vits2-MNN` 逐项哈希相同，因此复用该管线的中文、英文、中英混读与 Studio 证据。
Hugging Face 使用固定仓库 revision，ModelScope 使用逐文件不可变 commit。
下载/改名目录识别/重复安装零请求测试见 `bert-vits2-aliases-install-recheck.xml`。
该报告也包含安装索引检查（2/2）；首次报告中的请求计数失败来自本地 HTTP fixture
没有提供 Range 响应，已将该 fixture 设为单连接，生产下载逻辑未改变。

当前检查清单包含 32 个 TTS 仓库条目，其中 30 个已接入对应模型管线。剩余两个是未通过音频质量的 Qwen Omni 导出。
完整状态保存在 `TestArtifacts~/ModelDiscovery/Tts/current-status.json`；该清单是已检查的
公开索引快照，不代表未来新增仓库也已验证。已接入模型中的个别音色、语种和长文本
质量限制仍见 [测试说明](Testing.md)，不能据仓库数量宣称所有音色均通过转写。

### Pocket-TTS（MNN 全链路）

HF `developerabu/pocket-tts-mnn` 固定 revision
`4e75a38d20a9c532ed55591186584a244c381095`，11 文件 / 123,879,145 字节。
安装五份 MNN 图、bundle、SentencePiece、BOS、公开参考 WAV 和来源说明；
不安装 ONNX 回退或重复的 text_conditioner_w8。所有文件都有固定尺寸和 SHA-256。

CPU 管线在内存中简化空序列拼接，恢复编码器丢失的两处 ONNX Shape 切片语义，
原始模型文件保持不变。英文、24 kHz、原始参考 WAV、最多 50 token / 30 秒；
Studio 接通生成、播放和保存。预设音色文件需要上游访问权限，未下载或冒充支持。
目录缓存 schema v26 更新能力信息；正常加载复用缓存，Refresh 强制重新获取。

本机 CPU4 重复调用中位 3.931 秒生成 1.76 秒音频（RTF 2.233），未达到实时。
复用已加载图和最近一次参考音色状态，避免每次重新编码。实测范围见 [Testing](Testing.md#pocket-tts-mnn-全链路)。

### IndicF5 MNN

HF `developerabu/IndicF5-MNN` revision `72f3cd93d93c3bbff4091ae2ab1d351e6d68889c` 已接入。
安装 7 个固定文件：INT8 DiT、动态 Vocos FP32、官方匹配的 11283 字节字符词表、配置、许可证、
参考 WAV 与 README。词表来自 `mshdev/IndicF5-reelgen` revision
`125ce96087918bf4f815b5b9de6ae3579ebe991f`，Git blob 与官方 `checkpoints/vocab.txt` 一致。

管线使用公开 F5-TTS 字符前端、参考音频 mel、32 步 Euler CFG 采样和 MNN Vocos 解码；
DiT 的位置常量与输入尺寸只在内存中按公开序列化 FlatBuffer 适配，源文件不改写。
Hindi 和 Tamil 参考条件都已生成 24 kHz 音频；Hindi 通过两个识别器核对，Tamil 的独立 Whisper
转写为“வணக்கம் இது ஒரு குரல் சோதனே”，其中“சோதனை”出现近音拼写，保留为质量限制，
不把 Tamil 严格内容测试写成全通过。当前公开 Studio 语言为 Hindi/Tamil，要求参考 WAV 和原文。

性能以当前 macOS64 Mono / MNN 3.6.1 ABI 为准，CPU4、32 步、短句约 35 秒，未达到实时；
默认使用 INT8 DiT 和动态 Vocos FP32，重复请求复用同一实例的图。

### 2026-10 任务接入更新

已接入并用固定实际权重验证的共享管线包括 ModNet、Real-ESRGAN x4、UVDoc、SCRFD、
MobileFaceNet、2D106 landmarks、MobileNetV2、PGL-Net-T、MobileCLIP 图文 embedding 和
3D-Speaker ERes2NetV2 声纹。Studio 会为这些目录生成并校验任务标记；Model Manager 对图像
profile 使用精确文件计划，避免下载同仓库的其他权重。

PP-DocLayoutV3、MobileCLIP 之外的 Qwen-VL embedding、分割/姿态/深度、VGGT、UniTalker、
Wan 视频、音乐生成和其他多阶段仓库仍显示为目录任务，原因是它们的多输入图、解码器、
调度器或 tokenizer 协议没有在本包中得到可复现的官方调用契约。下载模型本身不代表已经接入。
