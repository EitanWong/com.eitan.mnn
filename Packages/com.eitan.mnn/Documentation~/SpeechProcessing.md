# 语音处理模型与验证范围

模型目录使用 `Assets/StreamingAssets/MNN/Models/<名称>-MNN`，每个目录独立保存 MNN 图、必要配置、README、许可证和 SHA-256 清单。
原始 ONNX/PyTorch、参考输出、候选图和日志保存在工程 `TestArtifacts~/SpeechModels`，不放入部署目录。
Player 打包在 `Project Settings > MNN` 选择需要的目录；默认 `None` 不打包任何模型。

## 托管接口

| 任务 | 接口 | Studio |
|---|---|---|
| TIGER16k双源分离 | `MNNTigerSeparation.Separate` / `SeparateWaveFile` | 单WAV，321–48000样本，保留两轨顺序；Studio预览第一轨 |
| Mayavoz DCCRN16k增强 | `MNNDccrnEnhancement.Enhance` / `EnhanceWaveFile` | 单WAV，checkpoint-owned首次eval、补齐hop后裁回原长 |
| Resemble44.1k完整CFM增强 | `MNNResembleEnhancer.Enhance` / `EnhanceWaveFile` | 单WAV，400–132300样本；固定seed0、32NFE midpoint、lambda/tau=.5 |
| Resemble44.1k降噪 | `MNNResembleDenoiser.Enhance` / `EnhanceWaveFile` | 单WAV，400–132300样本；不包含CFM增强器 |
| SEG音频参考目标提取 | `MNNSegExtraction.Extract` / `ExtractWaveFile` | 目标参考WAV与混合WAV，具体状态与长度见模型README |
| SpEx+音频参考目标说话人提取 | `MNNSpExPlusExtraction.EncodeReference` / `Extract` / `ExtractWaveFile` | 单独目标WAV与混合WAV，参考35ms–8秒、混合2.5ms–4秒 |
| DPTNet Libri1Mix16k 增强 | `MNNDptnetEnhancement.Enhance` / `MNNDccrnetEnhancement.Enhance` / `MNNDcunetEnhancement.Enhance` / `EnhanceWaveFile` | Audio processing，1ms–3秒单WAV |
| FullSubNet / FullSubNet+ 16 kHz 增强 | `MNNFullSubNet.Enhance` / `PredictMask` | Audio processing，最长十秒单 WAV |
| MossFormer2 SE 48 kHz 增强 | `MNNMossFormer2Se.Enhance` | Audio processing，40 ms–十秒 WAV，固定每次调用的 dither 种子 |
| MossFormerGAN 16 kHz 增强 | `MNNMossFormerGan.Enhance` | Audio processing，最长十秒单 WAV |
| GTCRN 16 kHz 增强 | `MNNGtcrn.Enhance` / `ProcessSpectrum` | Audio processing，单 WAV |
| DTLN 降噪 | `MNNDtln.Enhance` / `ProcessHop` | Audio processing，单 WAV |
| DeepFilterNet3 48 kHz 增强 | `MNNDeepFilterNet3.Enhance` | Audio processing，单 WAV |
| FRCRN 16 kHz 增强 | `MNNFrcrnSe.Enhance` | Audio processing，单 WAV |
| 双说话人波形分离 | `MNNMossFormer2Separation.Separate` / `MNNSepFormerSeparation.Separate` / `MNNConvTasNetSeparation.Separate` / `MNNDprnnSeparation.Separate` / `MNNSudoRmRfSeparation.Separate` | Audio processing，预览第一个音轨 |
| 音视频目标说话人提取 | `MNNAvMossFormer2Tse.Extract` | 混合 WAV + 有序 PNG/JPEG 人脸帧文件夹 |
| 四说话人活动时间线 | `MNNSortformerDiarization.Analyze` / `AnalyzeWaveFile` | Audio processing，10ms–60秒完整WAV；内部原同步缓存策略 |
| 声纹向量 | `MNNSpeakerEmbedding` / `MNNWeSpeakerEmbedding` | Speaker recognition |
| VAD | `MNNSileroVad` / `MNNFsmnVad` / `MNNMarbleNetVad` | Audio processing |
| 音频轮次结束 | `MNNSmartTurn.Predict` | Audio processing |
| 文本轮次结束 | `MNNLiveKitTurnDetector.Predict` / `MNNNamoTurnDetector.Predict` | Audio processing，ASR 转写文本 |
| 音频／文本多任务轮次判断 | `MNNVoxTurn.Predict` / `PredictBatch` / `EndOfTurn` | VoxTurn v1，六类判断；纯文本、音频或两者组合；保留非商业许可 |
| 带播放参考的回声消除 | `MNNDtlnAec.Process` / `ProcessHop` | Echo cancellation，双 WAV |

声纹向量并不是完整 diarization 时间线；聚类、重叠说话处理和 DER/JER 仍需要独立实现及验证。
各接口的长度、采样率和状态约定以对应模型 README 为准。

## 已有较强的转换数值证据

2026-11-01，在当前 Unity 2021.3.45f2 macOS 主机：

| 模型 | 独立参考 | CPU 输出误差 | Metal 输出误差 | 当前选择 |
|---|---|---:|---:|---|
| TIGER Speech16k | 原始25条件Spectrum与source-ordered pooling逐位一致；完整PCM、重复/变长/Studio | 波形NRMSE最大0.0007298644 | 0.0003042292 | FP32/High、source-float FFT640/hop160；AutoMetalNone/CPU4；完整PCM独占三轮测量、真实Player通过；FP16/Q8/Low/CoreML拒绝 |
| Mayavoz DCCRN16k | 原始checkpoint独立buffer首次eval；22物理条件只读/四阶段改写逐位一致；Unity7/7 | 波形NRMSE最大8.27554e-6 | 混合6.97340e-6 | 一份共享FP32权重；AutoCPU4，显式MetalNone分析/循环在CPU、编码/解码在Metal；完整PCM独占测量、真实Player通过 |
| Resemble Enhance44.1k CFM | 原始checkpoint/DSP；24原条件及形状返回；公共API/Studio9/9与独立真实Player51次完整PCM | Player波形NRMSE最大0.0005803067 | 0.0003343768 | 七图共享1,481,606,628字节；速度场卷积权重无损FP16存储/High；Auto全MetalNone/CPU4；独占48次完整PCM测量通过 |
| Resemble Denoiser44.1k | 原始checkpoint/DSP、source-float FFT1680；25条件与真实Player | 波形NRMSE最大6.27169e-5 | 候选未过门槛 | FP32/High、CPU8；CPU/Auto实际Player通过；不包含CFM增强器 |
| SudoRMRF U16 WSJ02Mix8k | 原作者checkpoint/mean+unbiased std/pad320/恢复幅度；15条件/重复/变长，13/13 Editor | 双路波形NRMSE最大1.64e-5 | 1.08e-5 | FP32/High，AutoMetalNone/CPU4，变形状重建GPU Session；FP16/Q8拒绝；CoreML三种等价图未生成输出 |
| DCCRNet Libri1Mix16k | 原始复数Asteroid/14条件/重复/变长/Studio，13/13 Editor及真实Player | 波形NRMSE最大6.70e-5 | 混合2.81e-5 | FP32/High；AutoMetalNone编码/解码+CPU循环，变形状重建Session；FP16/Q8/整图Metal拒绝；CoreML整图/拆分无有效输出 |
| DCUNet Libri1Mix16k | 原始复数Asteroid/18条件/重复/变长/Studio，13/13 Editor及真实Player | 波形NRMSE最大7.75e-6 | 7.76e-6 | FP32/High；外置去重约39.1MB；AutoMetalNone/CPU4，变形状重建Session；FP16/Q8拒绝；CoreML原图/固定卷积均无输出 |
| NAMO Multilingual v1 | 作者公开FP32 ONNX；39条件含8192tokens；915分词参考；11项独立Editor职责 | 概率最大绝对误差0.000311 | 128/512固定档位、正确mask；29文本及长CPU回退通过 | tiled FP16权重/High；AutoCPU4，Metal≤128/≤512档位，更长CPU；全owner在档位切换时释放；原INT8/Q8拒绝 |
| DPTNet Libri1Mix16k | 原始Asteroid0.7.0/原checkpoint；14条件含1ms/3秒及重复/变长，13/13 Editor | 波形NRMSE最大1.73e-4 | 官方CLI四项实际输入均失败 | FP32/High；AutoCPU4；等价动态注意力/分块/归一化；FP16/Q8拒绝；CPU/Auto实际Player通过 |
| Sortformer4spk v2.1 | 原始NeMo2.6.0数学类/前端/同步cache；13条件含60秒与压缩，11/11 Editor | 概率最大绝对误差1.33e-5 | 1.32e-6 | FP32/High；AutoCPU4；显式MetalNone两图形状变化重建Session；FP16/Q8/复用拒绝 |
| DPRNN WHAM8k | 原始Asteroid0.7.0/原checkpoint；14条件含2样本最小值、4秒上限、重复/变长及Studio；13/13 Editor | 波形NRMSE最大1.71e-4 | 首个双向LSTM开始失败；拒绝 | FP32/High；AutoCPU4；原生LSTM；CPU/Auto实际Player通过 |
| ConvTasNet Libri2Mix8k | 原始Asteroid0.7.0/原checkpoint；14条件含2ms最小值、十秒上限、重复/变长及Studio | 波形NRMSE最大2.14e-5 | 1.16e-5 | FP32/High；AutoMetalNone/CPU4；形状变化重建GPU Session以避免大形状后续调度开销；等价分阶段归一化 |
| SepFormer WSJ02Mix | 原始SpeechBrain1.0.3 encoder/masknet/decoder；14条件含实际十秒、重复/变长及Studio | 波形NRMSE最大1.14e-4 | 4.64e-4 | FP32/High；AutoMetalNone/CPU4；安全GPU Session复用；保持原幅度和speaker槽位 |
| MossFormer2 SE 48 kHz | 原始checkpoint/未修改decode与DSP；11条件含十秒上限、重复/变长及Studio | 波形NRMSE最大1.13e-4 | 3.21e-6 | AutoMetalNone/CPU4；FP32/High；固定每次seed20261030、保留dither；安全复用GPU Session |
| MossFormerGAN | 原始 checkpoint/完整 DSP，9 个非零条件、重复/变长 | 波形 NRMSE 最大1.36e-5 | 3.15e-6 | Auto Metal None / CPU4；FP32/High；FP16/Q8/Fast/Wide拒绝 |
| TitaNet | 原始NeMo2.3.0/原checkpoint；12条件含30秒、重复/变长及Studio | 单位向量L2最大9.33e-5 | 9.31e-5 | FP32/High；AutoCPU4；显式MetalNone在形状变化后重建Session；保留原始CMVN浮点残差 |
| ECAPA-TDNN | 原始SpeechBrain1.0.3/原checkpoint；11条件重复/变长及Studio | 单位向量L2最大8.91e-6 | 4.87e-6 | FP32/High；AutoMetalNone/CPU4；形状变化重建GPU Session；原始零填充/专用Mel前端 |
| DTLN | 官方 ONNX 双阶段及原始演示前缀 | 波形 NRMSE 1.07e-5 | 5.36e-4 | 默认 CPU/FP32/High；显式 GPU FP16 权重/High |
| DeepFilterNet3 | libDF + ONNX Runtime | 波形 NRMSE 4.02e-6 | 约 5.5e-4–8.0e-4 | 默认 CPU/FP32/High；Metal FP16 权重/Normal |
| AV-MossFormer2-TSE | 原始 PyTorch + ONNX | 波形 NRMSE 3.46e-5 | 0.002426 | CPU 非融合 FP32；Metal FP16 权重；均 High |
| WeSpeaker ResNet34-LM | Kaldi-native-fbank + ONNX | 单位向量 L2 误差 0.000173 | 0.00000546 | 所有已验证后端固定 FP32/High |
| FSMN VAD | FunASR 官方浮点 PyTorch、Kaldi/Hamming/CMVN/LFR | posterior max 3.46e-6 | 2.80e-6 | 固定32帧，CPU/Metal FP32/High |
| FRCRN | 官方 sqrt-Hann DSP + spectrum ONNX | 波形 NRMSE 5.83e-6 | 8.46e-7 | FP32/High；拒绝低音量误差高达0.2576的FP16 |
| MossFormer2 SS | 官方浮点 PyTorch，负Pad等价改写，禁用所有transformer融合 | 长输入双路0.000308/0.000462 | 0.0000515/0.0000570 | FP32/High；Auto允许Metal |
| LiveKit legacy v1.2.0 | 原始浮点 ONNX，21组实际文本/tokenizer案例 | 概率最大绝对差4.40e-6 | 约4.41e-5 | 默认CPU；GPU固定128 token图，High |
| MarbleNet VAD | NeMo原始checkpoint前端buffer+官方类及ONNX | posterior max1.79e-7 | 1.79e-7 | L1 FP32/High，CPU默认，拒绝旧FP16 |
| Silero v5.1 | 官方浮点ONNX、逐帧context/state | posterior max2.18e-5 | 1.21e-6 | CPU默认、显式Metal通过、逐帧零分配；非原PyTorch checkpoint证明 |
| SmartTurn v3.2 | 官方浮点ONNX + transformers5.17前端 | posterior max3.87e-6 | 1.64e-6 | CPU4默认、FP32/High；旧Q8/FP16/attention融合拒绝 |
| FullSubNet+ | 原始PyTorch checkpoint/完整DSP；9条件及重复/交替长度 | 波形NRMSE最大0.0007092 | 0.0002383 | AutoCPU1；显式MetalNone，动态前端在变长度后重建Session，固定32帧子带/共享外置权重 |
| FullSubNet | 官方v0.2原始PyTorch checkpoint/完整DSP | 波形NRMSE3.05e-5 | 6.44e-7 | CPU原生LSTM；Metal固定32帧基础算子，FP32/High、外置权重去重 |
| CAM++ | 原始PyTorch checkpoint+官方fbank；重复和交替长度 | 单位向量L2最大2.53e-4 | 2.86e-5 | FP32/High，AutoCPU4；显式MetalNone在变长度后重建原生session，保留Interpreter/权重，避免动态复用错误 |
| ERes2NetV2 | 原始PyTorch checkpoint+官方normalized-PCM fbank | 单位向量L2最大1.27e-4 | 5.62e-6 | FP32/High、Metal Fast；Auto优先Metal，CPU4回退 |
| DTLN-aec128/256/512 | 原始TFLite、官方run_aec.py、LiteRT builtin无delegate | 最大波形NRMSE1.22e-4 | 4.21e-6 | CPU默认；Metal使用CPU归一化+GPU循环核，FP32/High；29项Editor测试通过 |
| GTCRN | 官方DNS3 streaming PyTorch及 supplied ONNX | 波形NRMSE1.26e-6 | 7.10e-7 | CPU/Metal FP32/High；默认CPU一线程 |

上述数字来自有限参考样本，证明转换和前后处理一致性；不代替数据集质量评测。
GTCRN 九个物理条件最大波形 NRMSE 约5.34e-5/3.86e-5，FP16低音量误差0.05145、Q8最差0.17939均已拒绝。其他已发现的模型仍按独立数值审计推进，有限输出 smoke 或资源发现测试不代表转换正确。

Android SM8550 上，WeSpeaker 和 DTLN 已通过官方 CLI 的 CPU/OpenCL/Vulkan 输出比较。
NNAPI 尝试在 SquaredDifference 或 Raster 图构建处失败；目前没有完整管线 NPU 成功或算子硬件放置证据。
Android CLI 结果与 Unity Android Player 结果分别记录，不能互相替代。

## AV 输入协议

选择目标说话人的 25 FPS 同步人脸帧。支持直接 112×112，或对齐好的 224×224 人脸中心裁剪到 112×112；按官方参数灰度归一化。
文件名按序排序，缺失尾帧保持最后一帧，不能把短视频拉伸到音频长度。Studio 最多三十秒、750 帧。
当前不包含自动人脸检测、跟踪或任意视频文件解码；调用方负责正确的目标脸和时间同步。

## 验证入口

Editor fixture：`SpeechModelPackagingTests`、`FftPlanTests`、`DeepFilterNet3Tests`、
`AvMossFormer2StudioTests`、`WeSpeakerParityTests`、`DtlnTests`、`FsmnVadParityTests`、
`FrcrnParityTests`、`MossFormer2ParityTests`、`LiveKitTurnParityTests`、`GtcrnTests`、`MarbleNetParityTests`、`FullSubNetTests`、`SileroSourceParityTests`、`SmartTurnSourceParityTests`、`DtlnAecSourceParityTests`、`ERes2NetV2SourceParityTests`、`CampPlusSourceParityTests`、`FullSubNetPlusSourceParityTests`、`MossFormerGanSourceParityTests`、`EcapaTdnnSourceParityTests`、`TitaNetSourceParityTests`、`MossFormer2SeSourceParityTests`、`SepFormerSourceParityTests`、`ConvTasNetSourceParityTests`、`DprnnSourceParityTests`、`SortformerSourceParityTests`、`DptnetSourceParityTests`、`NamoSourceParityTests`、`NamoTokenizerParityTests`、`SudoRmRfSourceParityTests`、`DccrnetSourceParityTests`、`DcunetSourceParityTests`、`TigerSourceParityTests`、`DccrnSourceParityTests`、`ResembleEnhancerSourceParityTests`、`ResembleEnhancerStudioTests`；完整调用性能另由 `ResembleEnhancerPerformanceTests` 显式运行。
显式真实权重测试不会自动下载；分别设置 `MNN_TEST_DEEPFILTERNET_REFERENCE`、
`MNN_TEST_AV_REFERENCE`、`MNN_TEST_WESPEAKER_REFERENCE` 和 `MNN_TEST_DTLN_REFERENCE` 到已有参考目录。新增模型对应
`MNN_TEST_FSMN_REFERENCE`、`MNN_TEST_FRCRN_REFERENCE`、`MNN_TEST_MOSS_REFERENCE`、
`MNN_TEST_LIVEKIT_REFERENCE`、`MNN_TEST_GTCRN_REFERENCE`、`MNN_TEST_MARBLENET_REFERENCE`、`MNN_TEST_FULLSUBNET_REFERENCE`、`MNN_TEST_FULLSUBNETPLUS_REFERENCE`。

`SpeechProcessingPlayerTests` 使用真实构建的macOS arm64 Mono2x Player读取打包模型。原40模型联合构建通过193/193项测试，无失败或跳过，runner exit0；80项源比较为40 CPU、36 Metal和4 Auto。构建id `d7c63d6e03ec4997b2a4ad3a61c7b598`，XML SHA256 `3fbb308f8e56a90deaa639526665ba322a95ccc9ce5e43bbdc1141184bab9b76`；142项运行文件SHA与冻结快照、构建副本和部署匹配，其中86项为图与外置权重。证据在 `TestArtifacts~/SpeechModels/player-forty-verified.json`；TIGER与Mayavoz DCCRN已包含在本轮真实Player验收中。

2026-11-01，VoxTurn v1通过独立Player构建的116/116项测试，其中3项源输出比较分别覆盖CPU、Metal与Auto，各31条原始条件和首条返回；15个运行文件SHA与正式目录一致。构建id `9e83f116923443b9b0a39673c7ced986`，XML SHA256 `a1f9bc5d3c4e97ea8c71b8204d0e04e3ed9d191c5d46847bcbb72829b130e2fb`，证据在 `TestArtifacts~/SpeechModels/VoxTurn/player-verified-v44.json`。当时独立验收模型共41个；两次Player构建分别保留证据。Auto纯文本走CPU，带音频走可用Metal语言层，音频编码保持CPU；固定512位置、最多两组Metal图对，四线程High。原始LoRA与Qwen3-ASR骨干的加载顺序、许可和完整输出门槛见模型README。

2026-11-01，Resemble完整CFM增强器通过第三次独立Player构建的116/116项测试，无失败或跳过；其中3项模型测试覆盖CPU25、全Metal25及Auto1，共51次完整PCM调用。构建id `1cb2ed489ab0471c869a01dea6340d46`，XML SHA256 `1e2da4f3c8f2357194c8556349b3bcbeec214cf820866f820a7325a9708780cb`；图、共享权重和运行常量与冻结快照及构建副本SHA一致。证据在 `TestArtifacts~/SpeechModels/ResembleEnhance/cfm-lossless-player-v152-verified.json`。独立验收模型现共42个，保留原40、VoxTurn与Resemble三组实际构建证据，不能合称一次42模型联合构建。

通过Editor fixture不等于通过Player，创建GPU会话也不等于所有算子在GPU执行。结构检查见[测试说明](Testing.md)，原生调用仅使用包现有、限定平台/编译器的官方C++符号ABI契约，未新增C++、桥接导出或修改官方MNN源码。

发布工具 `Tools~/Models/publish_speech_models.py --audit-only` 无需凭据，检查目录清洁度、manifest全部文件/bytes/SHA、首选图以及已记录Player图/运行配置/tokenizer/必要运行常量覆盖。预检不会重新推理。当前42模型发布预检和23项发布工具行为测试已通过，记录在 `TestArtifacts~/SpeechModels/publish-preflight-forty-two-v159.log`；清单在 `TestArtifacts~/SpeechModels/deliverable-forty-two-inventory.json`。按用户要求，ModelScope在全部工作结束时统一上传，目前尚未上传。

## 新模型的数值与存储约定

TIGER保留原始PCM幅度与speaker槽位，无跨片段身份保证。FFT640/hop160使用原作者周期Hann、居中reflect；管理层恢复调用者采样率与样本数。Graph和外置权重分别约5.95/3.18MB；精确共享序列化常量降低体积，不使用未过精度门槛的量化。独立可复现工具位于 `Tools~/Models/Tiger`。

Mayavoz DCCRN的原构造器将多个归一化buffer指向同一张量，默认copy加载会覆盖checkpoint中的不同统计量并产生非有限输出。参考使用PyTorch官方严格 `load_state_dict(assign=True)` 保留独立张量，每条clip从新加载状态运行首次eval；导出将该次eval的归一化统计量折叠为只读运算，保留原E-mask算式、分支和输出clamp。运行契约为右补零至100样本倍数、原forward、裁回输入长度；作者2秒 `.enhance()` 分块/OLA属于另一条管线。5张图共享16,940,824字节FP32外置权重，各图通过官方SetExternalFile指定共享文件。整图Metal、CoreML、FP16/Q8和Metal Normal未通过门槛；显式Metal只用于已验证的编码/解码。可复现工具位于 `Tools~/Models/Dccrn`。

Resemble完整增强器在本机两种后端顺序、real→maximum→real共48次完整调用中，源PCM最大NRMSE4.116614065911359e-5；六组CPU/Metal热调用中位耗时比均为2.4058–2.5875，因此Auto使用可用Metal，否则CPU4。real/maximum/返回real的CPU中位数为32646.594/101946.634/32575.509ms，Metal为13376.011/39414.312/13343.706ms。七张图与共享权重由2,035,366,849降至1,481,606,628字节，保留相同逻辑权重与语义指纹。CPU最大输入的一次vmmap约20.1GiB物理占用与当时peak、性能进程1Hz最大RSS12,020,285,440字节是不同指标，均不是部署内存上界；实际Player的单次Metal采样另报告physical footprint36.9G及当时peak37.5G，保留系统原始单位，不能与RSS混为一谈。最终七组CoreML阶段尝试CPU对照通过，但CoreML全部无有效输出，未验证ANE放置；详见模型README。

## 尚未通过的候选

DAVE公开的 `real_avse_tiger_m.ckpt` 是双源音频TIGER-M主干，不包含完整视觉目标归属组件。25条原始Spectrum与导出改写在Torch2.8/CPU1逐位一致，但MNN CPU、Metal以及ONNX Runtime均在479–481样本边界未通过固定NRMSE<0.001门槛。原始Torch本身把CPU1改为CPU4时，480样本频谱NRMSE变为约0.12479；短输入对浮点归约和12次循环误差敏感。候选及诊断保留在 `TestArtifacts~/SpeechModels/DAVE-TIGER-M-16K`，尚未部署、接入或计入完成；没有放宽门槛或删去失败输入。

AV-TFGridNet的静音均值条件仍未过源波形门槛。ISAM已明确双说话人及六层门控的连续Python Random(777)参考流，26条source条件的完整PCM逐位一致；转换后的MNN静音PCM NRMSE约0.0025357，未过0.001门槛，尚未部署。该参考流是显式选择的推理契约，不能视为恢复训练或历史eval的RNG状态。上述候选均未计入已验收模型。VoxTurn已单独完成验收，保留CC-BY-NC4.0非商业限制。RNNoise/WebRTC等DSP/native实现不能冒充已转换MNN图。已验收模型列表不等于调研清单全部条目。

## GPU/NPU验证边界

所有默认策略只依据已记录主机、输入和整次PCM测量选择，不能代表任意设备的全局最优。CoreML独立官方CLI的有效输出与源数值门槛分开检查；exit0但没有输出属于失败。现有专项的ConvTasNet、Sortformer、DPTNet、NAMO等图仍因算子/spec/输入约束失败；TIGER与DCCRN也没有有效CoreML输出，未启用生产NPU或声称ANE算子放置成功。

历史Android SM8550 CLI上WeSpeaker与DTLN通过CPU/OpenCL/Vulkan数值比较，NNAPI在SquaredDifference或Raster构图失败。该CLI记录不能代替Unity Android Player验收。2026-11-01只读检查发现一台已授权的PJA110／SM8550／API36／arm64 Android设备，Unity Android模块、SDK、NDK和IL2CPP已安装；当前连接与构建开关仍不能作为推理证据。已配对iPhone的连接/tunnel不可用，未完成本轮Unity移动真机或NPU验收。

TIGER与Mayavoz DCCRN的独占完整PCM测量在Apple M5/Mac17,2、macOS27.2、10核/24GiB主机完成，三种交错顺序，每配置/输入15次稳定样本，并另测加载、首次和变长调用。TIGER MetalNone/High的1秒/3秒稳定中位数995.978/3296.022ms，CPU4/High为2447.827/6968.987ms；DCCRN CPU4/High为49.229/187.595ms，MetalNone/High为45.479/167.657ms，但变长1秒调用为70.788/106.852ms，故保留CPU默认与显式Metal选择。DCCRN分段CPU1/2/4/8均完成源波形资格与独占测量，各有取舍。加载/首次/变长的原始记录与校验值位于 `TestArtifacts~/SpeechModels/*exclusive*summary.json`，模型各自mnn_optimization.json保留对应摘要；这些结论不代表跨设备全局最优。
