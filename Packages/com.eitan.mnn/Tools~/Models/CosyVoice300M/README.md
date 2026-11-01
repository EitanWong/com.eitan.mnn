# CosyVoice-300M-MNN 初代 MNN 转换工具

当前 `Assets/StreamingAssets/MNN/Models/CosyVoice-300M-MNN/` 已精简为最终 Unity 优化版：
8组图/权重位于 `fp16/`（含 `llm_decode_compact`），7个条件/分词文件位于 `common/`。
原始权重、量化/训练/GPU实验与备用参考已删除。下文为历史转换实验记录，所列实验文件不再随最终目录保留。
最终部署清单见包内 `Documentation~/CosyVoice300M.md`，清理记录见 `TestArtifacts~/CosyVoice300M/Cleanup/cleanup.json`。

Unity 续测（2026-10-28）：原生 KV、FP16 固定 Q=1 形状、低精度声学和 GPU Module
均未建立可采用的端到端收益，实验源码及证据归档在 `TestArtifacts~/CosyVoice300M/UnityFastPath/`。
默认仅纳入与全排序 RAS 逐项及随机抽取序列一致的精确 top-25 采样优化；
Studio 显示 Text/AR、Flow、HiFT/DSP 耗时及当次声学 CPU 重试。
没有实现保音质 RTF < 0.3；历史 .20–.24 的实验配置仍不能作为当前 Unity 的性能或音质承诺。

**量化标注更正（2026-10-28）：** 历史混合量化转换日志明确禁用了HQQ；此前对应“HQQ”标注应读作
非对称min/max权重量化。历史模型字节、RTF、数值和ASR记录不变。本地校准脚本中的HQQ代理计算是独立步骤。
当前官方转换器的bool解析需要`--weightQuantAsymmetric=1`，裸参数或`=true`均不能启用HQQ；
新工具将实际HQQ设为显式`--quantizer hqq`并使用独立文件名、检查启用日志，避免混用历史权重。
详细取证见`TestArtifacts~/CosyVoice300M/hqq-provenance-audit.json`及RTF报告末节。

实际启用的HQQ-block64已完成23组69次合成：热RTF .24493–.26089、中位数.25221，EOS23/23、严格ASR20/23。
相同输入解码未提速（minmax 2.94743、HQQ 2.95627ms/token），转写偏差发生变化，未替换主要候选。
FFN INT3在当前low-memory路径产生NaN，五种注意力代数改写慢10.8%–26.9%，均未采用。
后续真正HQQ-block128同输入解码快4.03%/4.36%，23组热RTF .23872–.25633、中位数.24636、ASR21/23。
它新增“请把→起码”等错误；四组配对RTF降1.32%–4.02%，其中三组实际等待增加且音频更长。
仅保留实验，不替换主要推荐。证据见RTF报告“真正HQQ分组128”和`hqq128-delivery-validation.json`。

后续已在OPPO PELM00补齐设备内完整Python TTS，包含分词、真实采样/动态KV、Flow、HiFT和DSP。
CPU2/low短中文热RTF **4.2223–4.4061**，三次EOS/PCM一致，独立ASR匹配；未达到0.3。
HiFT Vulkan热RTF **6.0416–6.1885**，没有加速。Flow OpenCL热RTF虽为**3.2835–3.3654**，
但热音频ASR仅为“.”、近裁剪采样约8.65%，拒绝采用；首次图RTF40.16含约113秒Flow调优。
早期GPU未注册的fallback日志保留，后续通过whole-archive链接未经修改的官方PyMNN重新测试。
这些是Android诊断工具结果，不能作为Unity官方C++ ABI或通用音质验收。
原始证据及后续缓存布局配对见RTF报告“Android完整设备内推理”。
手机Key转置三组完整配对RTF变化为快.92%、慢3.07%、慢1.20%，PCM一致但无一致收益；
HQQ128短中文配对RTF降约1.09%，token由153变143、音频变短，两者ASR匹配，仍只保留实验。

追加CPU线程/大核与GPU诊断见 `TestArtifacts~/CosyVoice300M/RTF_LOWERING_REPORT.md`。
手机CPU4/Power_High短句两轮热RTF约3.93；CPU2/Power_High已确认核心6/7亲和性，仍约4.11–4.19。
当前349帧OpenCL Flow的热输出已确认全零；placeholder/clear_cache未修复，补到352帧回放非零但更慢。
M5主要候选未变；更大RTF收益优先针对训练过的草稿/推测解码、AR蒸馏和Flow蒸馏，尚无新训练或性能承诺。

对应 `iic/CosyVoice-300M` / `FunAudioLLM/CosyVoice-300M`，50 Hz speech token、22050 Hz 单声道输出。
**不是 CosyVoice2/3，不能交给项目现有 CosyVoice3 runner。** 转换工具不修改原生插件或官方 MNN 源码，也没有新增 C++ 或桥接导出。
包内现已提供独立 `MNNCosyVoice300M` C# / Studio 管线；默认完整参考、FP16/High、10步Flow。
API 与验证范围见 [使用说明](../../../Documentation~/CosyVoice300M.md)。
`prepare_unity.py` 只使用已有本地依赖补齐识别标记和 Whisper 词表；
`run_unity_tests.sh` 在指定的隔离工程中编译并执行实际权重和 Studio 测试，证据保存在 `TestArtifacts~/`。
下文历史 RTF 为 Python 工具结果，不代表该 C# 管线的性能。

当前M5性能候选为`splitbody4out8b64nopostfixedshape`：23组热RTF **.24760–.26968**、
中位数**.25386**、EOS23/23、ASR20/23。固定Q=1形状的4组完整配对浮点PCM一致，完整RTF降低**3.54%–5.57%**。
另有可选Key转置布局：23组热RTF .24202–.25887、中位数.25381；完整配对仅0.10%–0.93%变化，未替换主要推荐。
仍有原3个英文偏差，未经过人工音质验收；默认baseline保留。下文保留各轮历史数据，最新复现见“固定Q=1投影形状”。

## 产物与状态

- 模型：仓库根目录 `Assets/StreamingAssets/MNN/Models/CosyVoice-300M-MNN/`。
- `fp16/`：9 个图，FP16 权重存储；计算精度由设备 runtime 配置决定。每个 `.mnn` 必须携带同名 `.mnn.weight`。
- `common/`：embedding、speaker 投影、预计算相对位置、参考声音缓存、HiFT source 权重。
- `int8/`：文本编码器与 LLM 的实验量化图。`mixed8` 只取其中 LLM，文本编码器及其余图仍为 FP16。
- `int4/`：已拒绝，朗读内容损坏；保留用于复现失败实验。
- `experimental-fused/`：未推荐、未接入 runtime 的转换器融合实验。
- `source/`：可复现转换的官方原始权重，不需要随应用发布。
- `manifest.json`：图输入输出、文件 SHA256、来源、平台状态。
- 报告、WAV、原始计时和数值对照：`TestArtifacts~/CosyVoice300M/`，入口 `REPORT.md`。

最新 RTF0.3 实验见 `TestArtifacts~/CosyVoice300M/RTF_TARGET_STUDY.md`：逐层独立 KV、FFN INT4/其余 INT8、
CPU 解码 + Metal Flow/HiFT、1秒参考+完整 speaker embedding。在修正后的三句×三seed回归中，
2步/CFG0候选热RTF .2875–.3151，9/9 EOS与ASR匹配；新增语句仍有内容偏差，不能保证通用0.3或音质无损。
默认仍为 baseline。`--cache-dir` 使用官方GPU调优缓存，在单句/新进程对照中将首次图RTF约.55降至.325，热RTF不变。
更激进的 `splitbody4b32` / 2步 / CFG.7 在11组中热RTF .2740–.2927、EOS11/11、ASR10/11；
一个英文尾部转写偏差、音色和约.2–.5%接近裁剪阈值的采样仍需验收。见报告末节。
后续 `splitbody4b64` 将同输入解码从3.42降至3.23ms/token，11组热RTF **.2624–.2780**，
但ASR仅9/11匹配；`splitmixed4b64` 为.2841–.2978、ASR10/11。均不替换默认配置或视为质量通过。
校准版 `splitawqsafebody4b32` 没有提速，ASR10/11；含FF2缩放的 `splitawqbody4b32` 已拒绝。
进一步选择性保留attention输出投影INT8的 `splitbody4out8b64`，原11组为RTF .2691–.2835、ASR11/11；
新增6条文本×seed47/48为RTF .2663–.2839、ASR9/12，仍有英文偏差。
官方cross-lingual条件模式在这6组新英文上为.2491–.2731、ASR5/6，但音色代理下降、近裁剪采样增加。
这些新数据仍是M5离线Python结果；详细对照和限制见报告“选择性attention精度”章节。

最新runtime使用保持并列token顺序的精确部分top-k，并显式持有输入Var至输出读取。
同一加载图内4组交替配对，浮点PCM完全相同，完整RTF降低1.79%–2.62%。
重新执行全部23组：热RTF **.2624–.2822**、中位数**.2716**，EOS23/23、ASR20/23，原3个英文偏差保留。
首次图RTF .5316–.5834（不含加载、未使用磁盘调优缓存）；GPU跨进程可能因自动选择kernel而有细微数值变化。
`benchmark.py`与manifest记录runtime revision，完整证据见报告“精确top-k、GPU数值复现与输入持有”章节。
`splitbody4out8b128`虽有3.36%的相同输入解码收益，但23组ASR降至16/23，拒绝作为block64替换。
图内位置版`internalbody4out8b64`数值相同但没有提速，也不推荐用于性能。
进一步按FFN/QKV选择性扩大分组的3个图，原11组ASR仅8/11、9/11、8/11，均不替换block64。
官方SME2调度默认保留；41/47实际归一化为相同比例，早期同进程hint配对不能作为独立控制（见下文）。另编译了独立官方CoreML后端，但HiFT编译失败、
Flow出现SIGSEGV，尚无可用CoreML/Neural Engine RTF；原CPU/Metal运行库已恢复并核对哈希。
当前候选复用官方GPU调优缓存的单条短中文对照：新进程首次图RTF .5432→.2944，加载另需约.75秒；
加载+首次合成的RTF约.5748。缓存改善首次调优开销，这些数字不代表所有shape或设备。

最新候选 `splitbody4out8b64nopost` 使用官方Expression API删除Q=1注意力冗余后置mask，权重不变。
12组随机fixture的logits/28个KV完全一致；4组同进程交替配对的浮点PCM完全一致，完整RTF降低**0.27%–2.13%**。
23组热RTF **.25834–.28102**、中位数**.26714**，EOS23/23、ASR20/23，仍是原3个英文偏差。
首次图RTF .52694–.55269，不含加载/未使用磁盘调优缓存。该轮runtime revision为`cosyvoice300m-20261028-mask`；
23组测量时仍标记topk，最终仅更新revision元数据，防护版重新导出图的SHA256完全一致。
另一个`splitbody4out8b64nosentinel`改变了数值计算，原11组ASR降至9/11，拒绝替换。
以上依然只是M5、单参考声音的实验结果；完整证据见报告“Q=1冗余掩码特化”章节。

此前在nopost图上验证的runtime `cosyvoice300m-20261028-cdf`复用重复fallback概率，并使用与本机NumPy2.5.4 choice一致的逆CDF采样。
3870次边界抽样、3132次完整RAS对照均保持token/RNG状态一致；4组同加载图配对的浮点PCM一致，
完整RTF收益为**-0.40%–1.77%**，有一组变慢，不声称各句都加速。23组新回归热RTF**.25578–.29229**、
中位数**.26538**，EOS23/23、ASR20/23；首次图RTF .52797–.56651，不含加载/未使用磁盘调优缓存。
共享13个重复mask Cast的图数值一致但没有提速，未推荐。详见报告“精确逆CDF采样与重复概率复用”。

动态激活block量化`--decode-quant-option 2`已单独进程验证：60个随机fixture全部有限，
q0/q8与默认全部输出一致，q2/q10改变数值且彼此一致。四对独立进程的强制解码均值为默认**3.14341**、
q2 **3.26677ms/token**，q2约慢**3.92%**；23组完整合成为热RTF**.26278–.28550**、
中位数**.27029**、EOS23/23、ASR20/23，出现新的英文偏差，未作为现有候选的替换。
未采用新量化设置；随后固定Q=1形状的最新候选范围见前文。

官方`RuntimeManager.set_hint`会更新当前Executor内的runtime；同backend的manager可能共享该runtime。
Module加载会重设hint，forward的`applyMetaToRuntime`只更新`pMeta`，不能保证不同模型实例各自恢复hint。
因此不同hint的A/B须放在独立进程。CLI中的decode/flow前缀表示设置hint的入口，不承诺隔离到该阶段。
早期混合q0/q2/q8/q10同进程对照出现NaN，已排除；早期同进程SME41/47结果不再用于数值/速度结论。
另核对`ConvInt8TiledExecutor.cpp::_getProportions`：41→(5,1)，47的(5,7)会被归一化为(5,1)，
两者实际策略相同。12个独立进程测量的均值差异不作为调度优化收益，继续使用默认41。
证据为`TestArtifacts~/CosyVoice300M/runtime-hints-delivery-validation.json`与报告“独立进程runtime hint核查”。

Mac Apple M5及Android OPPO PELM00已跑通完整离线Python合成；Android另有官方CLI固定输入的误差记录。
Windows/iOS未实机验证。模型图可以复用，应用仍需实现这里的tokenizer、采样、KV cache、Flow求解和音频DSP。
官方 MNN Python bindings 用于验证；这些结果不代表 Unity 已通过直接 C++ ABI 调用完成集成。

## 已准备的本地环境

以下命令在仓库根目录执行。依赖与构建均位于 `TestArtifacts~/`，不需要再次下载。

```sh
CV_WORK="$PWD/TestArtifacts~/CosyVoice300M"
CV_MODELS="$PWD/Assets/StreamingAssets/MNN/Models/CosyVoice-300M"
CV_TOOLS="$PWD/Packages/com.eitan.mnn/Tools~/Models/CosyVoice300M"
CV_PY="$PWD/TestArtifacts~/ModelDiscovery/ASR/Zipformer/reference-venv/bin/python"
export PYTHONPATH="$PWD/TestArtifacts~/TaskIntegration/Dependencies/Torch:$CV_WORK/Dependencies/python"
export DYLD_LIBRARY_PATH="$CV_WORK/Dependencies/MNNBuild"
export PYTHONPYCACHEPREFIX="$CV_WORK/Pycache"
```

`requirements.txt` 记录本次实际版本。MNN wheel 需要配套的官方 `libMNN`；本机已经在
`Dependencies/MNNBuild` 编译了未经修改的 MNN 3.6.1（shared、train、audio、opencv、imgcodecs、LLM、low-memory、Metal、ARM82）。
不要直接替换项目的 Unity 原生插件。另一个平台需要对应平台的官方 MNN runtime；此目录没有跨平台 Python wheel/应用二进制发布包。

## 下载、导出、转换

`fetch.py` 使用固定 revision 和 SHA256；已有文件校验成功后复用，发现已有异内容文件会拒绝覆盖。

```sh
"$CV_PY" "$CV_TOOLS/fetch.py" --models "$CV_MODELS"
```

导出依赖官方 CosyVoice 历史源码 commit `d2dea3d92846b17e89e25513e855caed64bfc2f5`，已位于
`$CV_WORK/Dependencies/CosyVoice-d2dea3d92846b17e89e25513e855caed64bfc2f5`。
Whisper tokenizer 使用官方 `openai/whisper` 的 `v20240930` 中 `whisper/tokenizer.py` 和 `assets/multilingual.tiktoken`，位于 `Dependencies/whisper/`。

```sh
for CV_STAGE in llm flow hift; do
  "$CV_PY" "$CV_TOOLS/export.py" \
    --source-code "$CV_WORK/Dependencies/CosyVoice-d2dea3d92846b17e89e25513e855caed64bfc2f5" \
    --source-model "$CV_MODELS/source" --output "$CV_MODELS/common" \
    --onnx "$CV_WORK/ONNX" --stage "$CV_STAGE" --project-positions
done

"$CV_PY" "$CV_TOOLS/convert.py" \
  --converter "$PWD/TestArtifacts~/TaskIntegration/Dependencies/OfficialConverterBuild/MNNConvert" \
  --models "$CV_MODELS" --work "$CV_WORK" --precision fp16 --project-positions

"$CV_PY" "$CV_TOOLS/prepare_prompt.py" --models "$CV_MODELS" --work "$CV_WORK"
"$CV_PY" "$CV_TOOLS/validate.py" --models "$CV_MODELS" --work "$CV_WORK"
"$CV_PY" "$CV_TOOLS/manifest.py" --models "$CV_MODELS"
```

变更 exporter/权重时，给 **export.py 与 convert.py 都加 `--force`**，并重新数值验证。
默认会复用已存在 ONNX 和有成功日志的 MNN，不能把缓存命中当成重新转换。
`convert.py --precision int8` 生成实验量化版本；参数为非对称 block64、8bit 权重量化，其他图复用 FP16。
原始全图INT4未通过内容检查；后续独立解码混合INT4/8是不同实验，见下文。MNN normal memory 使用整数 `0`；早期对文本编码器启用低内存 INT8 动态 resize 出现 NaN。
后续实验将 low memory（整数2）严格限定在独立 Q=1 解码图，文本编码器与 prefill 继续保持 normal memory。

## 合成与计时

较保守的 Mac 质量基线：FP16、10 步，CPU 计算，HiFT 请求 Metal：

```sh
"$CV_PY" "$CV_TOOLS/benchmark.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --output "$CV_WORK/Benchmarks/my-baseline" --precision fp16 \
  --backend 0 --hift-backend 1 --compute low --threads 4 --steps 10 --repeats 3 \
  --text '你好，欢迎使用语音合成。'
```

速度候选：同上改 `--precision mixed8 --steps 5`。更激进的 `--steps 3` 仅作实验，降低求解步数可能改变音质。
其他平台先使用 CPU `--backend 0`、不传 `--hift-backend`、`--compute high` 建立数值基线，再单独验证低精度/GPU。
Metal/OpenCL/Vulkan 的请求不保证每个算子都在 GPU 执行，可能回退 CPU。
`--host-positions` 可与默认的 MNN 内部位置窗口切片做 A/B；默认保留 native KV cache，避免逐 token 下载完整 cache。

追加 GPU 对比可用 `--flow-backend 1 --llm-backend 0 --hift-backend 1`，分别选择 Flow、LLM、HiFT 的后端。
在 M5 上，短句测试中将 Flow 放到 Metal 有收益；将 LLM 也放到 Metal 更慢，并出现独立 ASR 内容偏差。
因此后端选择需按阶段验证，不能把请求 GPU 当成性能或精度保证。
`--gpu-record-batch` 测试官方 Metal 的 deferred command encoding 开关；本次未取得明显收益，默认关闭。
针对 RTF0.3 的实测与瓶颈分析见 `TestArtifacts~/CosyVoice300M/GPU_STUDY.md`。

RTF = 文本分词、文本编码、AR 生成、Flow、HiFT 神经网络及 DSP 的总耗时 / 输出音频时长。
模型加载单独记录；参考声音预处理已经缓存，不计入合成耗时；第 0 次为首次图执行，其后为热运行。
这不是首包延迟或流式播放指标。固定 seed42 仍会因计算/量化变化产生不同语音 token 和时长，应同时看文字正确性与音质。
早期基线只覆盖两条中文和一条英文；后续已增加6条语句、多seed和CampPlus相似度代理。
最新范围及失败项见 `RTF_TARGET_STUDY.md`，仍只有单个参考声音，没有MOS/人工音质验收。
非有限值、静音和未生成 EOS 会失败退出；最大上下文 2048（包括参考文本、参考 speech tokens、sentinel 与目标 token），长文应切句。

## Android 官方工具

本机设备 `PBINBYLJPVQST4OR`，OPPO PELM00。官方源码编译的 ARM64
`ModuleBasic.out`/`testModel_expr.out` 在 `Dependencies/AndroidBuild/`；源代码未修改，日志配置关闭 logcat 后输出 stdout。

```sh
"$CV_PY" "$CV_TOOLS/android_validate.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --serial PBINBYLJPVQST4OR --compute high
```

脚本通过 adb 在设备专用目录 `/data/local/tmp/mnn-cosyvoice300m/` 运行，保存原始日志、实际输出与误差。
`--skip-push-models` 仅在对应文件已部署后使用。`--backends 7 --graphs flow_estimator hift_core --output ...`
可限定 GPU 探测；本机 Vulkan Flow estimator 出现 NaN，不能推荐。
官方 CLI 的 1% 最大绝对误差阈值失败会如实记录；tokenizer 另外统计 token 精确一致率。
图级 fixture 形状与完整语句不同，**禁止把组件时间相加后声称得到端到端 RTF**。

## 后续解码实验

`optimize_decode.py` 在原权重上合并 Q/K/V 投影，并专门导出 Q=1 图。
该情况下相对位置的 `rel_shift` 可以精确简化为正位置部分，减少位置数据和算子。
FP32 PyTorch 与原公式对照通过；MNN 的 FP16 累加顺序改变仍可能改变采样结果，因此不声称语音逐样本无损。
`compact` 保留动态 KV 长度；`static` 用左侧 mask 和固定容量 KV，默认容量512，超限报错。
本次固定容量版本没有优于 compact，不是推荐配置。

```sh
"$CV_PY" "$CV_TOOLS/optimize_decode.py" \
  --source-code "$CV_WORK/Dependencies/CosyVoice-d2dea3d92846b17e89e25513e855caed64bfc2f5" \
  --models "$CV_MODELS" --work "$CV_WORK" \
  --converter "$PWD/TestArtifacts~/TaskIntegration/Dependencies/OfficialConverterBuild/MNNConvert"

"$CV_PY" "$CV_TOOLS/optimize_decode.py" \
  --source-code "$CV_WORK/Dependencies/CosyVoice-d2dea3d92846b17e89e25513e855caed64bfc2f5" \
  --models "$CV_MODELS" --work "$CV_WORK" \
  --converter "$PWD/TestArtifacts~/TaskIntegration/Dependencies/OfficialConverterBuild/MNNConvert" \
  --quantize-only
```

`experimental-decode/config.json` 记录图路径。重新执行第一条命令会更新该配置；需要继续执行第二条以注册 INT8 变体。
本机已生成，无需再次转换。图在单独目录，不会替换基础模型。
实验速度配置（仅在 M5 CPU 完整验证，其他平台需重测）：

```sh
"$CV_PY" "$CV_TOOLS/benchmark.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --output "$CV_WORK/Benchmarks/my-compact8" --precision fp16 \
  --decode-profile compact8 --decode-memory low --backend 0 --llm-backend 0 \
  --flow-backend 1 --hift-backend 1 --compute low --threads 4 --steps 5 --repeats 3 \
  --text '你好，欢迎使用语音合成。'

"$CV_PY" "$CV_TOOLS/benchmark_decode.py" --models "$CV_MODELS" \
  --output "$CV_WORK/decode-controlled.json"
```

此处 `--precision fp16` 保留原 FP16 prefill 和其他共享图；`compact8` 只在逐 token 阶段使用 INT8，`decode-memory low` 只作用于该解码图。
MNN runtime 需要编译启用 `MNN_LOW_MEMORY`。FP16候选可选 `--decode-profile compact --decode-memory normal`。
基线仍是 `--decode-profile baseline --decode-memory normal`，默认未改变。
解码实验另外加载一个模块，占用额外权重存储和运行内存；只部署选中变体及其 `.mnn.weight`，不要打包全部实验文件。
`benchmark_decode.py` 强制使用相同 embedding 序列与初始 KV，轮换测试顺序，只比较解码延迟，不输出完整 TTS RTF。

### Q=1冗余掩码特化

在已生成的`splitbody4out8b64`上，使用官方Expression `Var.replace/save`保留原二进制算子与权重，
移除14个softmax之后的冗余Select。只支持Q=1且每行至少有一个有效key；不能用于全mask attention。
诊断JSON只用于定位拓扑，不能将其回转MNN当成无损转换；本机no-op JSON对照已产生数值偏差。

```sh
"$CV_PY" "$CV_TOOLS/specialize_decode.py" \
  --models "$CV_MODELS" --work "$CV_WORK" \
  --converter "$PWD/TestArtifacts~/TaskIntegration/Dependencies/OfficialConverterBuild/MNNConvert" \
  --profile splitbody4out8b64 --mode drop-post-mask
"$CV_PY" "$CV_TOOLS/manifest.py" --models "$CV_MODELS"

"$CV_PY" "$CV_TOOLS/benchmark.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --output "$CV_WORK/Benchmarks/my-nopost" --precision fp16 \
  --decode-profile splitbody4out8b64nopost --decode-memory low \
  --backend 0 --llm-backend 0 --flow-backend 1 --hift-backend 1 \
  --compute low --threads 4 --steps 2 --flow-guidance .7 --repeats 3 \
  --prompt "$CV_MODELS/common/prompt-1.0-fullspeaker.npz" \
  --sampling-policy official-ras --max-token-text-ratio 40 --seed 42 \
  --text '你好，欢迎使用语音合成。'
```

重新导出会将该profile的本地验证状态重置为待验证；不要把历史结论自动赋给新权重。
`.mnn.weight`在本地通过hardlink复用原权重；分发时仍须携带该图同名的weight文件。
Expression保存的图没有显式`Net.outputName`列表，加载Module时须使用`config.json`中有序的29个输出名，
其中KV既用于内部attention又作为输出，不能只按图的末端节点推断输出。
`--mode drop-sentinel`是未推荐实验，另需移除prefill初始KV和对应位置，不能用nopost的一致性证据支持它。
完整结果与限制见 `TestArtifacts~/CosyVoice300M/DECODE_OPTIMIZATION.md`。

### 固定Q=1投影形状

`specialize_shapes.py`在原nopost二进制上用官方Expression `Var.set_inputs/save`固定58个投影输出形状，
并将最后一个token的Gather设为index0/axis1。只支持batch1/Q=1；KV长度、sentinel、mask和位置仍动态，
原量化Convolution/LayerNorm/attention算子及权重不变。JSON仅用于检查拓扑，不用于回转模型。

```sh
"$CV_PY" "$CV_TOOLS/specialize_shapes.py" \
  --models "$CV_MODELS" --work "$CV_WORK" \
  --converter "$PWD/TestArtifacts~/TaskIntegration/Dependencies/OfficialConverterBuild/MNNConvert"
"$CV_PY" "$CV_TOOLS/manifest.py" --models "$CV_MODELS"
```

本机已经生成；重建时显式加`--force`，会重置该profile的验证状态，须重新核对模型哈希及数值/完整合成。
沿用上方nopost完整benchmark命令，只将`--decode-profile`改为`splitbody4out8b64nopostfixedshape`。
动态prefill继续使用原图，不能把此候选用于多token/prefill验证。
分发时必须同时携带该图同名`.mnn.weight`；本机hardlink只节省实验存储。

最终图1378→846ops，Shape/Rank/SliceTf均为0；12组随机fixture的29个输出完全一致。
9轮交替强制解码、去掉首轮：原nopost **3.16337**、fixedshape **2.95582ms/token**，约低6.56%。
4组同加载GPU图、交替完整合成的36次浮点PCM比较全部一致；完整RTF降低3.54%–5.57%。
全部23组各3次新进程回归热RTF **.24760–.26968**、中位数**.25386**，EOS23/23、ASR20/23，
全部语音token数量和原始ASR字符串与此前CDF候选一致。首次图RTF .51157–.53807，不含加载/未使用磁盘调优缓存。
这不增加原候选的质量验收或跨设备保证；仍限M5、单参考声音，原3个英文偏差保留。
证据见`TestArtifacts~/CosyVoice300M/q1-fixedshape-delivery-validation.json`及`RTF_TARGET_STUDY.md`的固定形状章节。

后续`splitbody4out8b64nopostfixedshapeviews`将singleton轴变换改为视图，846→790ops。
12组随机fixture/36次完整浮点PCM比较一致，但四组完整RTF变化约-0.73%至+0.64%，
没有稳定收益，不替换fixedshape候选。更低RTF应优先减少AR串行工作：当前AR约占62.5%，
假设AR耗时减半，整体预算约.175；仅Flow减半则约.227。两者均为预算，尚未实现。
推测解码需要小draft、多token验证及正确的RAS/EOS/KV回滚；Q=1特化图不能直接用于验证。
更小AR/低token率、一步Flow需训练与音质验收，不能直接跳token、删层或缩步后声称等价。

### 可选转置Key缓存

`transpose_keys.py`从已固定Q=1形状的原二进制生成`splitbody4out8b64nopostfixedshapekeyt`。
只将Key改为`[1,16,64,P]`，保留Value/positions布局和量化权重；prefill后转换一次Key，后续native状态不再反复转置历史Key。

```sh
"$CV_PY" "$CV_TOOLS/transpose_keys.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --converter "$PWD/TestArtifacts~/TaskIntegration/Dependencies/OfficialConverterBuild/MNNConvert"
"$CV_PY" "$CV_TOOLS/manifest.py" --models "$CV_MODELS"
```

本机已生成，工具拒绝覆盖。沿用完整benchmark命令，改`--decode-profile splitbody4out8b64nopostfixedshapekeyt`。
图和同名`.mnn.weight`须同时分发。新版runtime revision为`cosyvoice300m-20261028-keylayout`。
正式图与实测原型SHA一致；24个随机fixture及14对96步序列的logits/规范化28个KV精确一致。
最终4组同GPU完整配对的36次浮点PCM比较一致，但完整RTF改善只有0.10%–0.93%，且一组decode稍慢。
23组69次新回归热RTF **.24202–.25887**、中位数**.25381**，EOS23/23、ASR20/23，原3个英文偏差保留。
中位数与前轮fixedshape的.25386很接近，不能据此声称显著提速；此profile可选，主要推荐/defaultbaseline保留。
没有该布局的Android/Windows/iOS/Unity完整RTF；Android fixture工具会在adb前拒绝该profile。
官方原生逐算子和Expression执行模式的负面筛选、原始计时与限制见RTF报告末节及`keyt-delivery-validation.json`。

## Android逐token解码组件验证

`android_decode_validate.py`使用本地已有的**未修改官方ModuleBasic.out**、官方MNN Python参考输出和adb。
无需安装设备端Python或添加C++；模型/二进制/外部权重按SHA256核对后复用。
设备不允许hardlink时在设备内复制相同权重，不重复上传。需要已构建对应Android工具，
本机路径为`$CV_WORK/Dependencies/AndroidBuild/ModuleBasic.out`。

```sh
"$CV_PY" "$CV_TOOLS/android_decode_validate.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --serial PBINBYLJPVQST4OR --compute high --past 31 199 --rounds 2 \
  --output "$CV_WORK/Android-decode-high"
```

默认对比nopost/fixedshape图，CPU4、low memory、20次相同输入forward；每轮交替图顺序。
`--profiles splitbody4out8b64nopostfixedshape --compute low --backends 3 7`测试OpenCL/Vulkan；
GPU下`--threads`是官方numThread模式位，不能解释为GPU线程数。
`--threads 2 --cpu-ids 6 7 --backends 0`请求官方CPU_CORE_IDS hint；**6/7仅适用于本次已核对的PELM00双大核**，
换设备先检查核心分组。不同hint在独立进程测试，脚本不修改系统频率配置。

所有结果是**固定输入、固定past的Q=1组件测试**，不包含动态KV增长、采样、Flow、HiFT或Unity，
不得当作Android完整RTF。`results.json`区分进程退出码、计时、输出有限性及官方误差检查；
ModuleBasic遇到TESTERROR仍可能返回0，所以脚本不会把返回0解释成精度通过。
还保存原始log、29个实际输出及逐输出哈希/误差；同设备首输出对照只在相同backend内比较。

PELM00实测nopost/fixedshape两图在CPU high/low下的29输出一致，
但相对M5参考均未通过官方1%误差检查，未完成Android音频/内容验收。
low/past31时CPU4约17ms，OpenCL约111ms、Vulkan约543ms；当前图/模式下GPU更慢，不能推广到其他GPU或Flow/HiFT。
另两轮筛选中CPU2不指定核心约14.82/14.99ms，CPU4为16.95/29.28ms，慢轮保留；
past199两者约23.7–24.0ms。core6/7 hint未比普通CPU2取得跨长度的一致优势。
这24次组件测试及交付文件核对见`android-views-delivery-validation.json`。
完整原始结果及本机线程实验见`TestArtifacts~/CosyVoice300M/RTF_TARGET_STUDY.md`后续章节。

## 多token验证图与Metal批量吞吐实验

`export_verify.py`从当前接受的固定Q=1二进制生成独立Q=1/2/4/8实验图，
保留原混合INT4/INT8外部权重，只修改固定query形状、相对位置窗口及输出head。
输出每个位置的logits及28个完整KV；**没有draft模型或推测采样器，未接入正常TTS runtime**。

```sh
"$CV_PY" "$CV_TOOLS/export_verify.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --converter "$PWD/TestArtifacts~/TaskIntegration/Dependencies/OfficialConverterBuild/MNNConvert"
```

本机已生成在`experimental-verify/`；重建须显式`--force`，会重置所选图的验证状态。
工具核对原图SHA256与31输入/29输出契约，用官方转换器JSON定位拓扑，
通过原二进制Expression API保存，不将JSON回转模型。完整工具重新生成的4个图哈希与实测图一致。
同名`.mnn.weight`发布时必须携带；本机hardlink仅用于节省存储。

固定batch1/Q，缓存长度P动态；positions为`[14,16,P+2*Q-1,64]`，
按相对lag`P+Q-1 ... -(Q-1)`排列。mask为`[1,Q,P+Q]`，保留masked sentinel并遮蔽每行未来key；
logits为`[1,Q,4097]`，每行预测对应输入位置之后的token。`P+Q<=2048`，每行至少有一个有效key。
Q=1控制图与原图输出仅rank不同，6组CPU/6组Metal的全部29个输出值精确一致。

M5共96组**随机输入数值fixture**，全部输出有限；修改后续输入时首位置logits精确不变。
高精度/normal-memory下Q=2/4/8与顺序Q=1的logits最大误差不超过.000605，top1一致；
CPU low-memory下却有显著差异。官方实现对单token/多token选择不同的动态激活量化路径，
改quant-option2也没有消除差异，不能当成保持当前分布的推测验证器。
Metal low的Q=4/8在各6组fixture里top1一致，但logits/cache并非逐字节一致，未完成音频质量验收。

同96个强制embedding/相同初始KV、past100的组件筛选：CPU Q1/Q8为3.02083/1.48634ms/token；
Metal Q1/Q4/Q8为5.14723/1.46662/.90599ms/token（Metal只有2轮热计时，属初筛）。
后者显示批量GPU吞吐潜力，**不包含draft、接受/拒绝、回滚、采样或完整TTS RTF**。
一次Metal Q8调用约7.25ms，只计验证成本便需平均净推进超过2.4个token才有望快于CPU逐步执行。
原始脚本/日志及汇总在`TestArtifacts~/CosyVoice300M/multiquery-probe-*`，
最终核对见`multiquery-verify-delivery-validation.json`；其他平台、真实draft接受率与完整TTS尚未验证。

### 无训练早退草稿筛选

`experimental-draft/`保留原模型前2/4/6/8/10/12层，接原final norm/head，
共享原量化外部权重。**全部为未训练实验，不推荐部署，未接入正常runtime。**
manifest单列契约、来源、文件哈希和筛选状态；这些图不能替代完整decoder。

M5上4条既有中英文校准文本、2个seed，共8条完整teacher轨迹、1796个真实前缀。
在相同teacher KV上比较目标和草稿的有效RAS分布，得到10776次单步比较：

| 草稿层数 | 平均单步分布重合度 | 独立CPU4成本 ms/token |
|---|---:|---:|
| 2 | .1807 | .7621 |
| 4 | .2068 | 1.1199 |
| 6 | .2631 | 1.4735 |
| 8 | .3423 | 1.8388 |
| 10 | .4415 | 2.2152 |
| 12 | .6396 | 2.5714 |

同组完整14层为2.9316ms/token，各图保留6轮热计时。
重合度`sum(min(p,q))`仅是该前缀下拒绝校正采样的单步条件接受概率，
不是实际自回归草稿分支接受率。RAS分布包括最近10 token重复时回到完整softmax的第二次抽样，
不能只比较top1或top-p候选。32个保存前缀经独立事件枚举复核，192次重合度差异小于5e-14。

用此前Metal Q8约7.248ms/次作示意成本：7次独立2层草稿+1次Q8校验，
即使不计回滚/同步，常量条件接受率也需约.8103才回本；12层即使全接受也不回本。
这不是已测的推测解码性能，且Metal批量图与CPU Q1仍存在数值差异。
本轮因此不接入这些草稿；下一步应优先评估经过蒸馏的轻量draft，而非直接截层。
筛选、独立审计、成本原始日志和脚本在`TestArtifacts~/CosyVoice300M/early-exit-draft-*`及
`{export,screen,benchmark,audit}-early-exit-drafts.py`；完整解释见`RTF_TARGET_STUDY.md`。
当前完整TTS仍为原M5候选.24760–.26968，没有新增完整RTF或四平台验收结论。

### Android真实Flow/HiFT输入与GPU模式

`android_render_validate.py`使用未修改的官方`ModuleBasic.out`，在已连接Android设备上
检查捕获的真实渲染阶段输入。没有新C++/桥接，也不执行完整Android TTS。
本机fixture在`TestArtifacts~/CosyVoice300M/Android-render-real/`：同一条中文语音、
136个speech token、约2.717秒；包含两步Flow的输入和HiFT输入。
reference来自相同FP16图的M5 CPU-high，并另存原FP32 ONNX对照。
它们是组件相同输入比较，不含Android前序阶段误差累积、AR、采样、DSP及完整RTF。

```sh
"$CV_PY" "$CV_TOOLS/android_render_validate.py" \
  --models "$CV_MODELS" --work "$CV_WORK" --serial PBINBYLJPVQST4OR \
  --fixtures "$CV_WORK/Android-render-real" --output "$CV_WORK/Android-render-new-run" \
  --cases hift_core-0 --backends 7 --compute high --gpu-mode 513 --loops 3
```

输出目录须在work内且没有旧`results.json`，保留既有证据。每次实际输出在运行前删除旧文件，
模型/外部权重/官方工具推送后核对SHA256；每次运行独立设备进程，拉回实际输出并核对形状、有限性及误差。
超时如已保存首个实际输出仍会拉回，计时保持null；未保存的输出不推定为有限或非有限。
进程退出0不足以通过；工具同时检查官方文本误差结果，失败以非零退出。
默认`--cache-policy shared`按工具/模型/权重哈希、backend、mode和precision复用官方调优缓存，
同图两步Flow可共享兼容缓存；每行保存运行前cache哈希。`--cache-policy run`保留逐case/round缓存。
缓存仅减少启动/调优成本，ModuleBasic的Avg仍是完成前向并等待设备输出的热组件计时，
不包含加载、校验和文本输出，不能据此报告冷启动或完整TTS收益。

`--threads`仅控制CPU；`--gpu-mode`是官方bitmask，与CPU线程数无关。
1=no tuning、4=wide、16=fast（OpenCL）；64/128分别强制Buffer/Image；512=record batch。
因此Vulkan513表示禁用调优+批量提交。每次GPU配置独立，不能把4解释成GPU有4条线程。
Mali-G57默认AUTO选择Buffer，mode1与65同一内存路径；强制Image应使用129。
工具拒绝多个调优level、冲突内存模式和Vulkan不支持的bit。

PELM00高精度的初筛中，Flow CPU4约2.425秒、OpenCL/wide约1.841秒；
HiFT CPU4约3.603秒、Vulkan/wide约2.876秒，均通过本组官方1%检查。
Vulkan Flow出现非有限值，低精度CPU/OpenCL均未通过误差检查，不能视为质量合格的加速。
HiFT高精度四次交替模式1/513/513/1的均值2.88269/2.81729秒，改善约2.27%，
连同wide在内5次Vulkan实际输出逐字节相同。它仅证明本条输入的组件收益。
OpenCL HiFT的多种配置超时，部分首输出可恢复；不能用超时推断其稳定forward时间。
完整原始结果、ONNX对照、恢复输出和HiFT单独音频诊断见`android-render-summary.json`及RTF报告。
当前iPhone可识别但DDI挂载失败，未执行本轮iOS推理；Windows仍未实测。

## 混合量化与短参考候选

`quantize_state.py`默认`--quantizer minmax`以保持历史权重；真正HQQ需显式选择，生成独立候选。
以下命令已生成过，重复执行会拒绝覆盖；`--force`会重做并重置该profile的验证状态。

```sh
"$CV_PY" "$CV_TOOLS/quantize_state.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --converter "$PWD/TestArtifacts~/TaskIntegration/Dependencies/OfficialConverterBuild/MNNConvert" \
  --base split --contract-profile split8 --policy body4-out8 --block 64 --quantizer hqq
```

内部使用`--weightQuantAsymmetric=1 --hqq`并检查实际启用横幅。
当前转换器的裸bool或`=true`不会启用HQQ，不能仅根据命令包含`--hqq`就标注权重。
HQQ通过固定输入有限值和23组回归仍不等于音质验收或提速；主要minmax候选与默认baseline保留。

`optimize_state.py` 导出每层独立 K/V。`split8` 与 compact8 在三个 past 长度的 logits/cache 完全一致。
`quantize_state.py --policy ffn4 --block 32` 使用官方非对称min/max逐层权重量化，生成 `splitmixed4b32`；
`--policy body4` 进一步将attention量化到4bit，生成 `splitbody4b32`。两者保留input/head INT8。
显式加`--quantizer hqq`才启用HQQ，新profile带`hqqenabled`后缀；工具验证`Use HQQ`日志并拒绝禁用警告。
原文件默认拒绝覆盖，`--force`会重建并重置验证状态。
固定容量缓存、图内位置切片和官方Attention融合也已测试；拒绝项见模型配置，不能仅凭存在文件就部署。

本机已有产物，无需重新导出。速度候选命令：

```sh
"$CV_PY" "$CV_TOOLS/benchmark.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --output "$CV_WORK/Benchmarks/my-rtf-candidate" --precision fp16 \
  --decode-profile splitmixed4b32 --decode-memory low --backend 0 --llm-backend 0 \
  --flow-backend 1 --hift-backend 1 --compute low --threads 4 --steps 2 --flow-guidance 0 --repeats 3 \
  --prompt "$CV_MODELS/common/prompt-1.0-fullspeaker.npz" \
  --sampling-policy official-ras --max-token-text-ratio 40 \
  --cache-dir "$CV_WORK/RuntimeCache/MNN-3.6.1-M5-v1" \
  --text '你好，欢迎使用语音合成。'
```

此配置改变参考条件和Flow质量保障。`--max-token-text-ratio 40`提高生成安全上限，保留自然EOS，
不会强行补齐或截断；默认20与上游一致，英文短句可能在正常结束前触及上限。
默认采样现遵循官方RAS重复fallback的完整分布；旧报告用 `--sampling-policy legacy` 复现。
NumPy与PyTorch随机数序列不同，不声称同seed逐token一致。

`--cache-dir`必须位于 `--work`，按设备/运行库区分；变更图或驱动后使用新目录。调优缓存更新另记，
排除在synthesis RTF之外。缓存只改善该样例首次调优成本，不保证新形状无编译或所有平台有效。

`common/prompt-1.0-fullspeaker.npz` 使用原真人前1秒的speech/mel/文本以及原3.48秒speaker embedding。
可通过 `prepare_prompt.py --reference-audio ... --reference-text ... --output ... --speaker-cache ...` 重建。
原始 `prompt.npz` 保留。不要将短参考候选作为任意说话人质量保证。

## 分组64与通道校准实验

`quantize_state.py --policy body4-out8 --block 64 --base split --contract-profile split8`
生成 `splitbody4out8b64`；`--policy body4-qkv8` 生成较慢的 `splitbody4qkv8b64`（原11组ASR10/11）。
均通过官方逐op策略实现，input/head保持INT8。前者在相同输入配对中比body4/block32快约4%。
在前述完整benchmark命令中改 `--decode-profile splitbody4out8b64` 即可复现。
加 `--reference-mode cross-lingual` 是官方条件模式实验，移除AR参考文字/语音、保留speaker和Flow参考；
本机新英文样例更快，但不能视为相同条件下的无损工程提速或自动语言路由方案。

在速度候选命令中使用 `--decode-profile splitbody4b64` 或 `splitmixed4b64`，并保持
`--steps 2 --flow-guidance 0.7 --max-token-text-ratio 40` 和1秒+完整speaker参考。
本机文件已生成；以下仅用于重新转换，重转后原有验证状态会失效：

```sh
"$CV_PY" "$CV_TOOLS/quantize_state.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --converter "$PWD/TestArtifacts~/TaskIntegration/Dependencies/OfficialConverterBuild/MNNConvert" \
  --base split --contract-profile split8 --policy body4 --block 64
```

改 `--policy ffn4` 生成 `splitmixed4b64`。分组64在长上下文的误差较大，仍需更多语料和设备验证。

`calibrate_decode.py` 收集实际解码输入上的浮点层激活，并搜索AWQ式输入通道缩放。
它没有训练权重；局部HQQ/INT8激活模拟只是代理，与官方转换结果并非逐位一致。
校准语料和随机种子与评估分开，但只有一个参考声音，192个采样位置。

```sh
"$CV_PY" "$CV_TOOLS/calibrate_decode.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --source-code "$CV_WORK/Dependencies/CosyVoice-d2dea3d92846b17e89e25513e855caed64bfc2f5" --stage collect
"$CV_PY" "$CV_TOOLS/calibrate_decode.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --source-code "$CV_WORK/Dependencies/CosyVoice-d2dea3d92846b17e89e25513e855caed64bfc2f5" \
  --stage export --variant splitawqsafe
"$CV_PY" "$CV_TOOLS/quantize_state.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --converter "$PWD/TestArtifacts~/TaskIntegration/Dependencies/OfficialConverterBuild/MNNConvert" \
  --base splitawqsafe --contract-profile split8 --policy body4 --block 32
```

默认只缩放QKV/FF1输入并折叠进LayerNorm，浮点等价检查必须先通过。
用 `--block 64 --variant splitawq64` 导出匹配分组64的通道校准，然后以
`--base splitawq64 --policy body4 --block 64 --contract-profile split8` 转换；
该实验原11组ASR8/11，未优于未校准版本，不推荐替换。
`--scale-ff2 --variant splitawq` 可复现已拒绝的FF2缩放实验；该方案浮点/ONNX等价，量化后误差却严重放大。
不能把代理损失下降或EOS出现当成完整语音质量通过。数值、ASR、音色代理和原始计时见
`TestArtifacts~/CosyVoice300M/RTF_TARGET_STUDY.md` 的通道校准与分组64章节。

`probe_flow_quality.py` 固定speech tokens、Flow条件、初始噪声和HiFT随机状态，比较不同Flow步数：

```sh
"$CV_PY" "$CV_TOOLS/probe_flow_quality.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --output "$CV_WORK/FlowQuality/my-probe" --seed 47 \
  --text 'Turn left at the next street and walk past the library.'
```

先要求2步重放与完整合成波形完全一致，再输出2/3/5/10步WAV和trace。此工具只诊断质量，不报告完整RTF；
不要把其缓存AR重放时间当作TTS性能。正常benchmark不启用trace，也不会产生相应张量复制。

上游模型与 CosyVoice 源码标注 Apache-2.0；Whisper tokenizer 为 MIT，分发时保留相应许可。


选择性分组复现实验（仍属未通过内容检查的候选）：

```sh
"$CV_PY" "$CV_TOOLS/quantize_state.py" --models "$CV_MODELS" --work "$CV_WORK" \
  --converter "$PWD/TestArtifacts~/TaskIntegration/Dependencies/OfficialConverterBuild/MNNConvert" \
  --base split --contract-profile split8 --policy body4-out8 --block 64 --ffn-block 128
```

仅改变QKV时把最后一个flag改为`--qkv-block 128`，两者可组合。
原有无flag的文件名和分组规则保留；新图须重新做数值、转写和听感验收。


## 保守 INT8 与小样本声学蒸馏验证

`quantize_conservative.py` 从现有 `llm_decode_compact.onnx` 生成全投影 INT8 和仅 FFN INT8。
只调用未修改的官方 MNNConvert；不下载模型，不新增 C++。解码保持 High 计算、Low memory，prefill 保留原 FP16/High。
官方转换先完成嵌入式 INT8，再用 MNN→MNN 将其余浮点常量压成 FP16 并导出外置权重。
不能在第一遍加 `--fp16`，也不能把已经外置的 INT8 中间图再次转换，否则本机转换器会跳过量化或丢失权重。

```bash
python3 Packages/com.eitan.mnn/Tools~/Models/CosyVoice300M/quantize_conservative.py \
  --models Assets/StreamingAssets/MNN/Models/CosyVoice-300M-MNN \
  --work TestArtifacts~/CosyVoice300M/ConservativeOptimization/Conversion \
  --onnx TestArtifacts~/CosyVoice300M/ONNX/llm_decode_compact.onnx \
  --converter TestArtifacts~/TaskIntegration/Dependencies/OfficialConverterBuild/MNNConvert
```

脚本拒绝覆盖现有候选。54次完整 CPU 合成确认速度收益，但全 INT8 有 close→closed、FFN INT8 有借阅→建阅的独立 ASR 偏差。
未通过内容验收，不能设为 Studio 默认值。显式 C# 实验入口为 `Load(root, decoderMode: MNNCosyVoice300MDecoderMode.ExperimentalInt8)`
或 `ExperimentalFfnInt8`。默认 `Float16` 不变，旧 INT4 布尔开关不能与新候选同时启用。

`CosyVoice300MDistillationTests.CollectFullPromptFlowTeacherPairs` 需显式设置 `MNN_COSYVOICE_DISTILL_COLLECT=1`。
它以固定的16句、两个 seed、完整参考和 CPU High 采集十步教师/两步 mel，先按句子划分8/4/4句的 train/dev/test。
`distill_mel_refiner.py --data <pairs> --work <training> --models <model root>` 用现有 Torch 环境训练10640参数的残差 mel 修正网络；
训练只用16组 train，dev选择 checkpoint和强度，test只在选择完成后评估。
这是冻结原 Flow 的端点修正实验，不是完整 Flow estimator 或 AR 蒸馏。
最终 ONNX 用官方 `MNNConvert -f ONNX --fp16 --saveExternalData` 转为 `experimental-flow-distill/mel_refiner.mnn`。

`HeldOutRefinerMatchesExportAndRendersSpeech` 通过 `MNN_COSYVOICE_DISTILL_DATA` 和 `MNN_COSYVOICE_DISTILL_TRAINING` 指定样本/训练目录，
重新生成保留集输入，检查 MNN/Torch 数值及教师端点误差，导出实际 WAV 和完整阶段计时。
不根据训练误差自动启用两步 Flow；独立 ASR/音色代理与听感验收仍是单独的门槛。
本轮证据集中在 `TestArtifacts~/CosyVoice300M/ConservativeOptimization/`。


Metal 连续长度验证使用 `CosyVoice300MMetalStabilityTests`。
三阶段重建不足以稳定完整管线；默认运行器重建 Flow estimator/regulator/F0，并对 encoder/HiFT core 按精确长度保留独立会话。
`metalShapeCacheCapacity: 1..4` 控制每类最多保留的长度数（默认4）；缓存淘汰后重新编译，不会复用旧形状的会话。
容量1降低常驻资源，但首遇/淘汰长度成本高；移动端需单独验收内存，不能套用桌面热结果。
36次重复长度测试无CPU重试，FP16热RTF中位1.524、INT8中位1.226；这不代表任意新文本首次合成都能达到相同速度。


本轮最终质量结论：保留集教师/两步/修正后的独立 ASR 严格匹配为3/8、3/8、4/8；教师本身也有真实内容偏差，
修正网络的平均音色代理由教师0.532降至0.492，因此端点MSE改善不构成保音质验收，修正网络不接入默认合成。
FP16和INT8共36次Metal连续合成无CPU重试，热RTF1.482–1.578；容量1/4的缓存淘汰测试通过，
86项功能/参数检查及包结构校验通过。仅验证本机有限样本，未达到保音质的RTF<0.3。
