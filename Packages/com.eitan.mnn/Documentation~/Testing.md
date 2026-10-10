# 真实推理与测试

本文按日期保留实际测试记录；历史 CPU 回归与最新 Auto/Metal 回归的范围分别说明。
当前绑定依赖本项目维护的特定 MNN/Apple clang/libc++ ABI，不是官方提供的跨平台 C# 接口。
`TestArtifacts~/` 中的 XML、日志、模型、生成媒体与本地 runner 是维护者的本机证据，
已被 Git 忽略，克隆仓库不会包含它们。包内复现工具位于 `Tools~/Testing`，所需已有模型与
素材须自行准备；缺少这些文件时不能把跳过当作推理通过。仓库规则见 [维护说明](RepositoryManagement.md)。

## 自动加速与质量回归（2026-10-10）

加载入口默认使用 Auto 加速策略，真实 Session 探测 Metal → NN → CPU；
明确不可用的 backend、原生加载失败和高层执行失败有 CPU 回退。
新增唯一的原生导入是官方 `Interpreter::getSessionInfo`，当前 macOS ABI 共 60 个导入
（56 个随包库符号、4 个系统符号）；没有新增 C++、桥接库或修改官方 MNN 源码/原生二进制。
具体任务精度、缓存与回退行为见 [自动推理加速](Acceleration.md)。

最终混合语音实现的两组 Editor 回归分别为 50/50 与 47/47，
合计 97 个不同测试全部通过，无跳过。新增 Omni API 检查从 Normal/Low/High 加载，
验证首次语音升级、GPU 媒体输入后语音、重复、释放重载、后台与流式文本后语音；
18 次混合语音全部通过独立 CPU ASR 的 hello/paris 关键词检查，显式 CPU 语音也通过。
arm64 Mono 独立 Player 116/116 通过、无跳过；实际 Auto affine 后端为 Metal，
后台已执行已知值推理，六类代表模型全部通过。Player Omni 在混合输入后两次
流式生成语音，确认主 runtime Metal / 处理器 CPU，并由独立 CPU LFM 转写出 hello。
Player 的 116 项包含平台、API 与 fixture 检查，不等于 116 个模型；原生 affine 10 项、代表模型 6 项。
当次结构验证为 512 个条目、5 个程序集边界；条目数随文档增加而变化。
13 项条件编译检查通过，不代表其他设备推理已验证。

当前机器是 Apple M5，Unity 2021.3.45f2 / macOS arm64 Mono。
SD 1.5 TextEncoder、UNet、VaeDecoder 均通过官方查询确认主后端 Metal；
2 步真实生成、20 步生成、确定性重复调用、取消、释放和重载检查通过。
同一 512×512、prompt、seed、2 步、4 CPU 备用线程的进程内比较：

| 后端 | 加载 | 进程内首次 Generate | 同一实例再次 Generate |
| --- | ---: | ---: | ---: |
| Metal | 105 ms | 11,700 ms | 2,745 ms |
| CPU | 107 ms | 33,048 ms | 17,115 ms |

最新回归的热运行约 6.2 倍（此前同参数测量约 6.5–6.8 倍）；这是单台设备、这个模型与参数的结果，不代表所有模型最快或冷启动性能。
已有 shader cache 被复用；库在启动时的可选 Metal4 Tensor API shader 编译失败后会自行关闭
该路径并使用普通 Metal kernel。通过的 GPU 结果不依赖该可选特性。

质量检查不能仅看 backend 类型或有限输出：初轮真实测试发现中文 BERT/Sana 的非有限值、
Bert-VITS2 控制流生成器在 Metal 下的原生空输出崩溃、Piper 无法正确转写的有限 GPU 波形、
Embedding 的 GPU 相关性排序退化，以及 Omni Normal/FP16 路径的语音失真。
BERT/Sana 和 Embedding 的 High 尝试未解决相应回归。Omni 独立 High Metal runtime
在 4 次短句对照中有 2 次转写失败；共享 High Metal runtime 在初次 4 次检查通过后，
扩大为不同句子、图片理解后语音与流式输出的 12 次检查仍有 4 次失败。
Metal Thinker/Talker + CPU 处理器的混合配置则在 16 次检查全部通过 hello/paris 关键词转写验证，覆盖
Normal/High 主 runtime、重复、重载、后台和流式调用。默认语音改用 High 精度混合配置，
并通过 `Backend` / `MediaBackend` 如实区分主模型与波形处理；显式 CPU 保持 CPU。
这些实验通过已绑定的官方 C++ 调用执行，没有修改原生库。
其余已验证任务继续优先 GPU。低层 Session 自动选择不等于整个模型的质量认证。

原始失败、修正后的 XML、原生日志、PNG/WAV、SD 对比和整理前 Runtime 备份均在
`TestArtifacts~/Acceleration/`。失败记录保留，最终结果与覆盖范围以 `summary.json` 为准。
当前库启用 Metal，未启用 CoreML，未验证或宣称 NPU/Apple Neural Engine 支持。
其他操作系统、IL2CPP 与 x86_64 GPU 的验证范围没有因条件编译检查而扩展。

## 目录整理验证（2026-10-09）

Runtime/Core 按功能域归类，Editor 测试放 `Tests/Editor`，PlayMode/Player 测试保留
`Tests/Runtime`；测试资源位于 `Tests/Fixtures`，独立工具迁至 `Tools~/Testing`。
程序集名称、公开命名空间和保留资源的 GUID 不变。结构与新增文件规则见
[项目规范](ProjectStructure.md)，可通过 `Tools~/Validation/validate_package.py` 检查。

本轮 Unity 2021.3.45f2 / macOS arm64 的 EditMode 回归首次运行 208 项，202 通过、0 失败，
6 项因 runner 素材目录配置少了 `Fixtures` 层级而跳过。修正为已有素材路径后，相关
13 项全部通过，其中包含全部 6 个跳过项；208 个不同测试均有通过记录，不是一次执行 208 项全绿。
没有重新下载模型或素材。五个程序集（含可导入基础示例）均经过 C# 编译检查，
13 组平台/后端条件编译及 native import 检查均通过；这些编译检查不证明各平台推理可用。
Runtime 程序集的 macOS Mono PlayMode 回归按 `PlatformContract` / `NativeInference`
筛选，111 项全部通过、0 失败、0 跳过，覆盖平台保护与内嵌 affine 图的原生数值推理。

日志、XML、资源 GUID/二进制审计及整理前源文件保存在 `TestArtifacts~/PackageStructure/`。
此前的空模板 Demo 和依赖不存在 `MNNTensorOps` API 的旧性能示例移出分发包，
原文件保存在该目录中。当前基础示例不依赖 UI、Burst 或 Collections，已补齐独立程序集和 UPM sample 声明。
本轮未修改原生二进制或官方 MNN 源码，未新增 C++ 或自定义 native exports。

## Piper 字典误作可执行程序修复（2026-10-09）

旧版 `MNN.Studio.EspeakPath` 只校验文件存在，导致已保存的 `espeak-ng-data/af_dict` 被当作程序启动并报 Access denied。现在校验程序/脚本文件头并拒绝数据目录内的文件；Studio 自动删除无效旧设置，自动发现也跳过无效环境变量和工具候选。

`TestArtifacts~/GenerationImplementation/piper-dictionary-regression.xml` 的 **7/7 项通过**，覆盖 af_dict 设置迁移、无效工具候选、文件夹 Browse，以及残留字典设置/环境变量时的真实 Studio 合成、Piper 三音色 ASR、播放与 WAV 保存。真实合成用英语精简模型自带的 en_dict；独立回归覆盖 af_dict。首次测试因精简模型不含 af_dict 的测试前置断言失败，保留于 `piper-dictionary-initial.xml`，修正测试数据后全部通过。汇总在 `piper-dictionary-summary.json`。

## Piper 数据目录与 Browse 修复（2026-10-09）

`espeak-ng-data` 自动从模型下载目录查找；Browse 使用 `OpenFolderPanel` 选择完整数据目录，按模型保存覆盖值，文件和不完整目录均拒绝。文件夹对话框延迟到 IMGUI 绘制结束后打开，空路径与取消不影响当前文本。eSpeak-NG 可执行程序另外自动发现，也能复用项目已有测试依赖；旧版误保存的数据目录不会阻止工具自动查找。

`piper-discovery-regression.xml` 的 42 项全部通过，含 Piper 三音色 ASR、Studio 实际播放/WAV 与窗口回归；最后补充的目录校验和旧设置兼容由 `piper-data-folder-final.xml` 的 4 项全部通过。真实鼠标事件验证按钮触发延迟目录选择流程；批处理测试注入目录选择返回值，未自动打开系统模态窗口。真实后端验证未设置 `MNN_ESPEAK_NG_PATH` 且无已保存工具路径时仍能合成音频。汇总在 `TestArtifacts~/GenerationImplementation/piper-data-folder-summary.json`。下述 `additional-tts-results.xml` 为可重复运行覆盖的输出，106/106 是先前新增 TTS 接入时的历史结果。

## Bert-VITS2 与 Piper 真实验证（2026-10-09）

新增 `MNNBertVits2` 与 `MNNPiper`，接入同一个 Speech synthesis 工作区、音色选择、实际播放/停止和 WAV 保存。
`additional-tts-results.xml` 一次执行 **106/106 通过，0 失败，0 跳过**，包括新增测试及 Supertonic、Interpreter、官方 ABI、平台打包、模型发现和 Studio 回归。
中途的缺失描述和旧原生导入数量断言失败记录保留在 `additional-tts-intermediate.xml` 与 `additional-tts-packaging-intermediate.xml`。

- Bert-VITS2：陈曦中文、44.1 kHz；检查 BERT/音素对齐、数字/标点、变调、有限非静音波形、重复调用、取消、释放和重载。Qwen2.5-Omni 对生成 WAV 转写为“你好，欢迎使用语音合成。”。
- Piper：Amy、Kathleen、Ryan 英文 fp16、16 kHz；检查官方音素 ID、标点/小数处理、音色切换、有限非静音波形、重复调用、取消、释放和重载。三个音色经 LFM2.5 Audio 均转写为 `The capital of France is Paris.`。
- Studio：两种新增架构的真实工作区均通过后台合成、实际播放、停止与 WAV 写出；改名目录、文件缺失提示及中文示例切换也通过。
- 官方调用：已核对新增 Express/Module 导出符号；已知 affine 图验证 `Variable::load/input/compute/readMap` 的数值与重复调用，常量特化测试验证源字节不变且结果正确。

Bert-VITS2 的生成器含条件分支，使用公开 `Module::forward`：将本次声调/语言/BERT 特征写入托管图副本常量，只保留 phone 输入；每次合成重新加载生成器。Piper 使用公开 Express Variable 调用及上游 eSpeak-NG 命令做音素转换。没有新增 C++、自定义 C ABI、桥接库、私有对象/虚表假设或官方源码改动；随包原生库 SHA-256 仍为 `1e63c9c3eef8d38524eda6456e63b8ece8bce783fbbacebd7de9b412a29fe183`。C# STL marshalling 明确限定已有 Apple clang/libc++ ABI，不是可移植的官方 C# 接口。

边界：陈曦当前只接中文，未实现英文 BERT、中英混读和完整 Jieba 词性变调；普通中文样本内容已验证。每次加载生成器适合离线合成，未宣称实时性能。此次新增模型只验证 macOS arm64 Mono CPU，未验证 x64/Metal/IL2CPP/其他平台。

所有下载、SHA-256 清单、上游 eSpeak-NG 测试依赖、原始日志、WAV、ASR 和汇总均在 `TestArtifacts~/GenerationImplementation/`，详见 `additional-tts-summary.json` 与 `additional-tts-abi-audit.json`。运行 `bash TestArtifacts~/GenerationImplementation/run-additional-tts-tests.sh` 可复用本地模型与依赖，在隔离项目中重跑新增测试；无需重新下载到 Assets。该脚本使用已有 Qwen2.5-Omni 和 LFM Audio 进行内容识别，不在单元测试中自动下载模型。

## 图像生成与独立 TTS 真实验证（2026-10-09）

已接入并验证 `MNNSupertonic`（`MNN/supertonic-tts-mnn` fp16）和 `MNNStableDiffusion`（SD 1.5 `general`）。两条管线由 C# 编排，通过现有官方 Interpreter/Tensor 符号执行模型图；没有新增 C++、自定义 C 导出、桥接库或修改官方源码。它们不调用未从随包库导出的高层 Diffusion/Supertonic 类。

TTS 测试检查 44.1 kHz、有限非静音波形、重复调用、F1 音色、取消、释放和重载；ASR 对生成 WAV 的识别结果为 `The capital of France is Paris.`。SD 测试检查 512×512、有效像素、确定性重复调用、取消、释放和重载；人工查看输出，白墙边有红色自行车，车身部分被裁切。

Sana Edit V2 已跑通真实参考图编辑管线。由于 Qwen3 图的 `logits_index` 需要固定输入内容，运行时先按官方 FlatBuffer schema 在内存副本中将该输入特化为常量，再通过现有 Interpreter/Tensor 调用执行 Qwen3、connector、projector、DiT 和 VAE；没有新增 C++ 或虚表调用，也未改动下载的模型文件。测试输出为 512×512 有效像素图，重复调用、取消、释放和重载均通过；同一参考图和 seed 下，不同提示词会产生不同像素。人工查看“红色改蓝色”结果仍主要为红色，语义颜色编辑尚未达标，原因仍需与上游参考实现对照，不能将其直接归因于模型限制。

模型、SHA-256 清单、ABI 导出证据、成功与失败日志、WAV、ASR 文本、PNG 均位于 `TestArtifacts~/GenerationImplementation/`。`GenerationModelTests` 使用占位文件只验证发现和路由；`GenerationPipelineUnitTests` 检查文本处理、噪声、调度与图片方向；`SupertonicIntegrationTests` / `StableDiffusionIntegrationTests` / `SanaIntegrationTests` 执行真实推理，不能用离线单元测试代替。可运行此目录中的 `run-generation-tests.sh` 复用本地模型并在隔离 Unity 项目中重跑。

环境：Unity 2021.3.45f2、macOS Apple Silicon、Mono、CPU 后端、随包 MNN 3.6.1 / Apple clang libc++ ABI v1。本轮未验证 x86_64、Metal、IL2CPP 或其他操作系统的生成管线；现有平台绑定及其局限见 [ABI 约定](../Native~/README.md)。

最终测试：`rerun-results.xml` 的 112 项中 108 项通过，4 项因旧导入数量断言、Omni 模型路径和窗口测试会话状态失败。修正后 `regression-fixes.xml` 对相关 24 项全部通过；按同名测试的最新结果合并，112 项均有通过记录、无未解决失败，并非一次执行 112 项全绿。生成管线、Studio 生成和 Interpreter 的 48 项在主回归中全部通过。原始失败记录保留在汇总中。

## 代表模型验证（2026-10-09）

模型下载到项目的 `Assets/StreamingAssets/MNN/Models`，共约 6.06 GB。
这些模型此前已通过 Package 下载服务下载并校验，本次直接复用本地文件，没有重新下载。
普通测试不会联网下载模型；模型或本地输入素材缺失时会明确跳过。

最终 EditMode 回归 **101 项：99 通过、0 失败、2 项 Explicit 跳过**，跳过项是联网下载测试；
隔离的 macOS arm64 Mono Player **115 项全部通过**，x86_64 Mono Player（Rosetta）**115 项全部通过**，
两种架构各自都真实运行了下列六种代表模型。严格离线模型 runner 单独运行六种模型的 **30 项 EditMode 测试，30 项全部通过**；所需模型或素材缺失时直接失败，不会跳过。macOS arm64 Mono PlayMode **117 项：111 通过、6 项可选模型测试跳过**。
IL2CPP macOS Player **100 项：99 通过、0 失败、1 项原生布局测试按平台限制跳过**；此结果只验证拒绝策略，
不代表 IL2CPP 推理支持。旧自定义 C 接口架构的历史测试不作为证据。

| 能力 | 模型 | 真实输入与结果 |
| --- | --- | --- |
| 文本生成 | Qwen3.5-0.8B-MNN | 中文首都问答“北京”；默认精度下 1/4 线程各三次算术回答“2”；异步、长上下文与重复加载释放 |
| 图像理解 | SmolVLM-256M-Instruct-MNN | 同一问题输入纯红/纯蓝图片，分别回答包含 red / Blue，视觉编码耗时大于零 |
| Omni 图像 | Qwen2.5-Omni-3B-MNN | 红色图片 → Red. |
| Omni 音频 | Qwen2.5-Omni-3B-MNN | 本地语音 → The capital of France is Paris. |
| Omni 图像 + 音频 | Qwen2.5-Omni-3B-MNN | 同时输入蓝色图片与语音，正确回答 blue 与 Paris |
| Omni 语音输出 | Qwen2.5-Omni-3B-MNN | 生成包含 Hello 的回答和单声道 24 kHz 波形；检查有限值、非静音，并交给 LFM 转写验证内容 |
| 语音理解 | LFM2.5-Audio-1.5B-MNN | 同步、后台线程推理均转写为 The capital of France is Paris. |
| Embedding | Qwen3-Embedding-0.6B-MNN | 输出 1024 维向量；相关文档相似度显著高于无关文档；重复编码一致 |
| 重排序 | Qwen3-Reranker-0.6B-MNN | 相关文档评分显著高于无关文档；重复评分一致 |

SmolVLM-256M 能辨别颜色，但红图的回答较啰嗦，达到 24 token 上限。
通过代表模型的测试不代表该系列所有规模、量化版本和任务都已经验证。

既有 Qwen3.5-0.8B 文本测试仍保留：中文首都问答输出“北京”，重复算术问答输出“2”，
异步算术输出“4”，并覆盖较长中文上下文、tokenizer 和反复加载/释放。
最终 Omni 语音回答为 “Hello! How are you today?”，生成 61,440 个 24 kHz 样本
（2.56 秒），LFM 重新转写得到相同文本。

原始结果、日志和生成语音保存在项目内：

- `TestArtifacts~/MNNValidation/PlatformValidation/StandaloneOSX-Mono2x-arm64-inference/summary.json`：arm64 Player，115/115 通过，六模型真实推理。
- `TestArtifacts~/MNNValidation/PlatformValidation/StandaloneOSX-Mono2x-x64-inference/summary.json`：Rosetta x86_64 Player，115/115 通过，六模型真实推理。
- `TestArtifacts~/MNNValidation/PlatformValidation/EditMode/results.xml`：本次完整 EditMode 回归，99 通过、2 个显式联网测试跳过。
- `TestArtifacts~/MNNValidation/PlatformValidation/PlayMode-Mono2x-inference/summary.json`：PlayMode 轻量回归。
- `TestArtifacts~/MNNValidation/PlatformValidation/StandaloneOSX-IL2CPP-arm64-contract/summary.json`：IL2CPP 拒绝策略，不验证推理。
- `TestArtifacts~/MNNValidation/PlatformValidation/CompileMatrix/summary.json`：13 组 Unity 平台/后端 C# 编译条件与原生导入检查。
- `TestArtifacts~/MNNValidation/DirectCpp/Final/results.xml`：最终完整回归结果。
- `TestArtifacts~/MNNValidation/DirectCpp/Final/unity.log`：Unity 日志。
- `TestArtifacts~/MNNValidation/DirectCpp/Final/inference-output.txt`：从最终 XML 提取的真实输出。
- `TestArtifacts~/MNNValidation/DirectCpp/Final/omni-generated.wav`：Omni 生成的语音。
- `TestArtifacts~/MNNValidation/DirectCpp/Final/native-audit.json`：双架构符号、SHA256、依赖和签名检查。

两个架构均具备 43 个 MNN C++ 导入符号，外加 3 个系统 libc++ 符号，均不存在 `MNN_*` 自定义 C 导出。
原生依赖只包含 macOS 系统库和框架，不依赖开发者的源码或构建目录。
最终测试所用原生库与 Package 中的库 SHA256 一致。

Player 实测修复了两项架构 ABI 差异：arm64 与 x86_64 的 libc++ `std::string` 对象布局不同，
x86_64 的 `std::function` 对齐也不同。Player 回归现在覆盖短/长 UTF-8 字符串、中文、Emoji、嵌入 NUL，
以及 Omni 语音回调的 GC 与重复调用。

最初使用 `-nographics` 的回归有两项窗口测试因无图形设备报错；启用图形设备后全量重跑，
没有修改测试断言。该次诊断保存在 `no-graphics-results.xml` 和 `no-graphics-unity.log`。

## 测试覆盖

- 原生版本、官方 C++ API 加载、文件/内存计算图加载、实际仿射数值计算、同步和异步运行。
- 张量类型、长度、空输入、映射/解除映射、动态形状和失效视图。
- libc++ 短/长字符串、中文/Emoji 与嵌入 NUL 的初始化、复制和销毁。
- 语音回调：GC 后重复生成、波形独立所有权、清除回调后再次文本推理。
- 生命周期：重复 Dispose、解释器/会话/张量释放顺序、模型重新加载、释放后访问。
- 文本、图像、音频、混合模态、语音输出、Embedding、重排序的实际结果。
- 不支持的模态、损坏媒体、缺少配置/权重，以及不合法参数的明确错误。
- 路径配置：StreamingAssets、PersistentData、自定义目录与平台覆盖。
- 下载：并发、Range、断点恢复、取消、错误响应、完整性校验、后台任务和主线程通知。
  日常下载单元测试使用内存 HTTP 响应，不额外联网。

两个下载测试为 Explicit，常规套件会跳过：
`DownloadRepositoryFileAsync_ModelScopeLfsRedirect_DownloadsCompleteFile` 和
`MNNModelProvisionTests.DownloadSelectedRepositories`。跳过不计作通过。

## Studio 工作区与多轮接口基线

新增 [MNN Studio](ChatStudio.md) 编辑器工作区；本次完整 EditMode 124 项中 122 通过、0 失败、2 项 Explicit 联网测试跳过，新增 Studio 23 项全部通过。arm64 与 x86_64 Mono Player 各自 115/115 通过，含新增多轮接口。测试报告见
`TestArtifacts~/MNNValidation/Studio/`。加入官方角色对话及逐 token 解码导入后，
Apple Mono 的原生导入数量为 49（46 个 MNN/STL 符号 + 3 个系统 libc++ 符号，包含逐 token 解码）；
上文 43 个 MNN 导入是新增接口之前的历史记录。官方 dylib 未更改。

## Studio 流式、会话管理与语音播放（2026-10-09）

完整 EditMode 共 156 项，154 通过、0 失败，2 项 Explicit 联网下载测试跳过。
其中 Studio/流式相关 55 项全部通过；最终拖拽源清理、搜索定位与取消行为回归 9/9 通过。
覆盖文件夹/会话拖拽、顺序持久化、模型自动命名隔离、标题及正文搜索、消息滚动定位、
语音小球状态布局、真实逐 token 解码、UTF-8 分片、主线程快照交付、取消和关闭期间释放。
真实 Omni 音频测试检查非静音波形、Unity 播放状态和播放位置推进，另验证普通对话自动播放。
自动测试不录制环境麦克风；系统设备权限和实际听感仍需目标机器验证。

13 组平台条件编译检查通过；此类检查不等价于其他平台的原生推理支持。
本次 macOS Mono Player 在六种代表模型测试中加入流式多轮对话、取消后再次推理，
以及 Omni 混合输入与流式文本后生成语音。arm64 与 x86_64 Mono Player 各 115/115 通过；
x86_64 在 Apple Silicon 的 Rosetta 环境执行。完整结果见
`TestArtifacts~/MNNValidation/Studio/live-summary.json` 与平台目录报告。

## C# 任务 API

在 Unity 主线程加载模型。推理和 Dispose 在每个实例内串行执行，Dispose 等待当前推理结束。
`Async` 方法在工作线程执行推理。调用者传入的缓存目录由调用者管理；自动创建的缓存在
Dispose 时清理。推理不会改写下载模型的配置。

`MNNLlm.Load`、`MNNEmbedding.Load`、`MNNReranker.Load` 当前默认使用 Auto 后端和 `Normal`
精度，可通过可选参数 `precisionMode` 显式选择 `Normal`、`High` 或 `Low`。
这些 Module 任务接口不接受 `LowBF16`，因为 MNN 的 LLM JSON 配置没有对应映射。

```csharp
using (var model = MNNLlm.Load(modelDirectory))
{
    var result = await model.GenerateMultimodalAsync(
        "Describe the image and transcribe the audio.",
        imagePath: imageFile, audioPath: waveFile, maxNewTokens: 128);
    Debug.Log(result.Text);
}
using (var embedding = MNNEmbedding.Load(embeddingDirectory))
{
    float[] vector = await embedding.EncodeAsync("Paris is the capital of France.");
}
using (var reranker = MNNReranker.Load(rerankerDirectory))
{
    float relevance = await reranker.ScoreAsync(
        "What is the capital of France?", "Paris is the capital of France.");
}
```

`MNNLlm.Generate` / `GenerateAsync` 保持文本调用方式。
`GenerateMultimodal` 接受一张图片、一个可解码的 WAV 和文本；先检查 `Capabilities`。
`generateSpeech: true` 返回独立的 `Waveform` 和 `SampleRate`，目前支持经过验证的
Qwen2.5-Omni-3B 音频输出配置。新 talker 架构不能直接套用此采样率约定。
返回值还包括 token 数、是否达到 token 上限、视觉/音频处理时间与输入音频秒数。

Embedding 的向量和语音波形都复制到托管数组，不受下一次推理影响。
Qwen3 检索查询应按模型约定添加 `Instruct: ...\nQuery: ...` 前缀；文档直接编码。
`MNNReranker` 当前实现 Qwen3 的查询/文档评分，返回 0 到 1 的相关性概率。
这些入口不提供视频或多图片输入。LLM 后续新增的流式与 token 边界取消接口见
[API 参考](API.md#llm-流式对话)；Embedding/Reranker 不提供生成流式接口。
新增 `GenerateConversation` / `GenerateConversationAsync` 使用官方 `ChatMessages` 角色历史；
可附加当前用户消息的一张图片/一个 WAV，详见 [MNN Studio](ChatStudio.md)。

## 默认精度

本节历史 CPU 回归使用 `Normal` 精度；当前任务默认 Auto 后端，兼容策略见 [加速说明](Acceleration.md)。
历史直接 C++ 调用测试中，Qwen3.5-0.8B 在
1、4 线程下各重复三次均正确回答 1 + 1 = 2。`Low` 等其他精度的历史诊断
来自旧接口架构，没有计入本次默认配置的通过结论。

## 复现

先通过模型管理器下载上述代表模型，或手动运行显式下载测试，指定：

- `MNN_TEST_MODEL_ROOT`：项目内模型目录。
- `MNN_TEST_ARTIFACT_ROOT`：项目内测试产物目录。
- `MNN_TEST_PROVISION_MODELS`：用分号分隔的 MNN 仓库名，例如 `SmolVLM-256M-Instruct-MNN;Qwen2.5-Omni-3B-MNN`。

图片使用 Python 标准库生成；语音使用 macOS 已安装的 Samantha 声音，不下载素材或依赖：

```bash
python3 Packages/com.eitan.mnn/Tools~/Testing/prepare_multimodal_fixtures.py \
  "$PWD/TestArtifacts~/MNNValidation/DirectCpp/Final/Fixtures"
```

在 Unity Test Runner 中运行 EditMode 测试。代表模型测试类别为 `RepresentativeModel`。
可用环境变量 `MNN_TEST_MODEL_DIRECTORY` 指定既有 Qwen3.5 模型，
`MNN_TEST_MODEL_ROOT` 指定五个代表模型的父目录，`MNN_TEST_ARTIFACT_ROOT` 指定素材和产物根目录。

本次在项目内隔离副本执行，避免干扰已经打开的主编辑器：

```bash
rsync -a --delete Packages/com.eitan.mnn/ TestArtifacts~/MNNValidation/UnityProject/Packages/com.eitan.mnn/
MNN_TEST_MODEL_DIRECTORY="$PWD/Assets/StreamingAssets/MNN/Models/Qwen3.5-0.8B-MNN" \
MNN_TEST_MODEL_ROOT="$PWD/Assets/StreamingAssets/MNN/Models" \
MNN_TEST_ARTIFACT_ROOT="$PWD/TestArtifacts~/MNNValidation/DirectCpp/Final" \
  /Applications/Unity/Hub/Editor/2021.3.45f2/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD/TestArtifacts~/MNNValidation/UnityProject" \
  -runTests -testPlatform editmode \
  -testResults "$PWD/TestArtifacts~/MNNValidation/DirectCpp/Final/results.xml" \
  -logFile "$PWD/TestArtifacts~/MNNValidation/DirectCpp/Final/unity.log"
```

隔离项目需已配置 Unity Test Framework。不要在同一项目同时打开两个 Unity 实例。
构建产物、日志和临时诊断都在 `TestArtifacts~/MNNValidation`，不进入构建包。
替换原生库后需要重启 Unity；脚本域重载不会卸载已经载入的原生库。

Player 回归使用包内 runner，不下载模型或素材：

```bash
python3 Packages/com.eitan.mnn/Tools~/Testing/run_platform_tests.py \
  --unity /Applications/Unity/Hub/Editor/2021.3.45f2/Unity.app/Contents/MacOS/Unity \
  --project TestArtifacts~/MNNValidation/UnityProject --platform StandaloneOSX \
  --mac-architecture arm64 --model-root Assets/StreamingAssets/MNN/Models \
  --fixtures TestArtifacts~/MNNValidation/DirectCpp/Final/Fixtures
```

把架构参数换成 `x64` 可在 Apple Silicon 上通过 Rosetta 验证 x86_64 Player；这是 ABI 覆盖，
不是 Intel 硬件测试。省略模型与素材参数会跳过六项代表模型测试。`--platform PlayMode` 运行 Editor PlayMode。
`--backend IL2CPP --contract-only` 只验证不支持平台的保护逻辑。`check_platform_compilation.py` 用本机
Unity C# 编译器检查 13 组平台条件编译与导入数量，不构建目标 Player，也不验证原生链接或推理。

只验证六种代表模型的任务测试时，使用严格离线 runner：

```bash
python3 Packages/com.eitan.mnn/Tools~/Testing/run_model_tests.py \
  --unity /Applications/Unity/Hub/Editor/2021.3.45f2/Unity.app/Contents/MacOS/Unity \
  --project TestArtifacts~/MNNValidation/UnityProject \
  --model-root Assets/StreamingAssets/MNN/Models \
  --fixtures TestArtifacts~/MNNValidation/DirectCpp/Final/Fixtures
```

脚本要求六个模型配置与权重、`red.png`、`blue.png`、`speech.wav` 均已存在；只读复用这些输入，
不会下载、复制或删除模型。每次运行写入独立的 `TestArtifacts~/MNNValidation/ModelTests/<run-id>/`，
并以六个模型类的 30 项测试全部 Passed 作为成功条件。

## 验证边界

已在 macOS arm64 Mono Editor/Player 与 x86_64 Mono Player（Apple Silicon + Rosetta）执行 CPU 推理，
各自通过六种代表模型。没有 Intel 实机、Android、iOS、Windows、Linux 或 WebGL 运行环境；这些平台
仅完成 C# 条件编译检查，其中非 macOS 目标会剔除所有 Apple 原生导入，并在 API 入口明确抛出
`PlatformNotSupportedException`。macOS IL2CPP 仅构建运行了拒绝策略测试。以上不构成这些平台的推理支持。
本节代表模型历史 CPU 回归不包含 Metal 或后续流式实现；最新 arm64 Metal 与流式验证见本文开头。
视频、多图片、全双工实时音频尚未实现；取消只能在托管检查点或 token 边界观察，不能中断原生 kernel。

上述六个本地模型与数值计算图已完整运行当前测试；不能据此保证 ModelScope 上全部模型、
所有代际、量化、尺寸及 talker 架构兼容。新模型必须符合官方 MNN 配置与任务格式，
并加入对应真实输入/结果测试。语音输出只声明已验证的 Qwen2.5-Omni 24 kHz 合约，
重排序只声明 Qwen3 的 yes/no 合约。
