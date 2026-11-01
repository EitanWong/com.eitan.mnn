# CosyVoice-300M-MNN 初代

`MNNCosyVoice300M` 接入 `iic/CosyVoice-300M` 的本地 MNN 转换，输出 22050 Hz 单声道。
它有独立的 Whisper 分词、1024 维 AR/KV 状态、Flow 和 HiFT 管线。
已有 `MNNCosyVoice` 对应 CosyVoice3 / 24000 Hz，两者不能混用。

```csharp
using MNN.Unity;

// modelDirectory 必须是可直接读取的目录；移动端先通过包内模型准备 API 提取资产。
using (var voice = MNNCosyVoice300M.Load(modelDirectory, threads: 4))
{
    MNNGeneratedAudio audio = voice.Synthesize("你好，欢迎使用语音合成。", seed: 42,
        cancellationToken: cancellationToken);
    // audio.Waveform: mono float PCM; audio.SampleRate: 22050
    UnityEngine.Debug.Log($"RTF {voice.LastRealTimeFactor:F3}");
}
```

默认保留完整内置参考文字、语音 token、299 帧 mel 和说话人 embedding，使用 FP16 存储权重、
High 计算精度、10 步 Flow 与 CFG 0.7。AR 在 CPU；声学阶段 `acousticBackend: Auto`
选择可用加速后端，并可在检测到运行错误、非有限结果或最终波形异常饱和时重试 CPU。
`FlowBackend`、`VocoderBackend` 表示各阶段实际会话后端，MNN 可能对部分算子使用 CPU。
传 `acousticBackend: MNNBackendType.CPU` 可运行全 CPU。

最终精简目录包含 `fp16/llm_decode_compact.mnn` 及同名 `.mnn.weight`，
默认用这个 FP16 单 token 专用图执行 AR 解码，原图继续执行 prefill。
它融合 QKV，并去掉 Q=1 不需要的位置窗口和 relative shift；没有减少层数或量化权重。
`UsesCompactDecoder` 可确认是否选中。最终导出标记明确要求这对文件，缺失时目录不完整。
旧版未声明 `decoder_graph` 的目录仍支持原图，以及原 `experimental-decode/` compact 路径。
这会额外常驻一份解码模型，部署前应评估内存。完整参考、High 精度与 10 步 Flow 保持原值。

随包 Metal 运行时在连续改变声学输入长度后可能产生有限但错误的输出；本轮冻结输入对照在
Flow estimator、regulator、F0 复现，完整管线也证明仅重建这三者不足。
当前所有 Metal 声学图绕过共享磁盘调优缓存。Flow estimator、regulator、F0 在长度变化时重建；
encoder 和 HiFT core 按精确输入长度保留独立会话，每个会话只运行一种形状。
`Load(..., metalShapeCacheCapacity: 4)` 默认最多保留四种 encoder/core 长度；允许设为1–4，
容量1降低常驻资源，但不同长度会重新加载权重并编译，首遇或被淘汰长度的耗时仍计入 RTF。
不缓存最终音频，不改变模型权重、参考音频、采样或 Flow 步数。资源在模型释放或 CPU 重试时一起释放。
这会增加模型内存；本轮一个 Unity Editor 进程 RSS 快照约8.2 GiB，包含整个 Editor，
不是 Player/model-only 峰值，也不能据此推断移动端内存可接受。移动端需单独测试并考虑容量1。
若最终波形超过 0.1% 的采样达到约 ±0.99 限幅，运行器保留 AR token、恢复声学 RNG 状态，
将五个声学阶段切到 CPU High 重试一次；CPU 仍饱和则报错。
`LastAcousticCpuRetry` 和实际 `FlowBackend` / `VocoderBackend` 可查看回退。
后续请求继续使用 CPU；重新加载模型才恢复请求的加速后端。
重建、失败尝试和 CPU 重试均计入合成 RTF；饱和检测只覆盖这一类已复现失真，不能证明普遍音质无损。

`experimentalQuantizedDecoder: true` 显式启用混合 INT4/INT8 Q=1 解码图；它会改变数值和采样结果，
历史两条文本的 4 份输出均未严格匹配独立 ASR（“合成”变“合唱”，nearest 变 newest），
未通过内容验收。最终精简目录已删除这些权重，在本目录启用该选项会报缺失文件。推测解码、缩短参考声音和减少 Flow 步数均不作为默认值。
`flowSteps` 可设置 1–20，低于默认值属于需要另外评估质量的速度选项。
托管采样使用 System.Random 与 Box–Muller，保留原模型的 RAS 分布规则，
相同 seed 不保证与 NumPy/Python 生成相同 token 或 PCM。

输入最多 200 个 UTF-16 字符，必须包含文字或数字；特殊模型标记、控制字符等在执行前拒绝。
当前一次生成完整句子，未接入流式 PCM、长文本自动分段、用户参考音频或声音注册。
自然 EOS、2048 上下文和生成 token 上限分别检查，达到上限会报错，避免返回被截断的语音。
同一实例的合成与释放串行；取消在原生步骤之间生效，不能打断正在执行的原生调用。
使用工作线程进行加载及合成，避免阻塞 Unity 主线程。
`LastTokenSeconds`、`LastFlowSeconds`、`LastVocoderSeconds`、`LastSynthesisSeconds`
在 PCM 读取完成后记录；RTF 包含文本编码、AR、Flow、HiFT 与托管 DSP，不包含 `Load` 或写 WAV。

## 模型与 Studio

开发工程资产位于 `Assets/StreamingAssets/MNN/Models/CosyVoice-300M-MNN/`，不随 UPM 包分发。
最小部署包括：

- `mnn_cosyvoice300m.json`：初代模型识别标记。
- `fp16/` 中 `text_encoder`、`llm_cached_projected`、`flow_encoder`、`flow_regulator`、
  `flow_estimator`、`hift_f0`、`hift_core`，加 `llm_decode_compact` 共8组 `.mnn` 与同名 `.mnn.weight`。
- `common/` 中 `llm_embeddings.npz`、`projected_positions.npy`、`flow_speaker.npz`、
  `hift_source.npz`、`prompt.npz`、`whisper.tiktoken`、`whisper-special-tokens.json`。

最终发布共30个文件：上述23个运行文件、识别标记、manifest、README、ModelScope configuration 及三份许可/归属文件。
本地 Unity 目录另保留原 `.meta`，公开下载不包含 `.meta`。
原始 PyTorch/ONNX、草稿、备用参考和前端预处理资产不在部署目录中；两个 `experimental-*` 目录及其 `.meta` 已移到开发工程的 `TestArtifacts~/CosyVoice300M/Publish/ArchivedExperiments/`，不随模型发布。
默认参考已缓存，因此 `campplus` 和 `speech_tokenizer` 不参与当前内置音色管线。
预填充图仍是最终优化管线的必要部分，不能删掉。
清理后逻辑大小约1.38 GiB；位置表本身加载约235 MB，需按目标设备评估内存。

Studio 的 Speech Synthesis 页面可以发现 StreamingAssets 中的该目录，也可选择目录。
显示 `Default` 内置音色和 seed；加载后直接由 C# 调用 MNN，合成结果可播放、保存。
公开转换仓库为 [EitanWang/CosyVoice-300M-MNN](https://www.modelscope.cn/models/EitanWang/CosyVoice-300M-MNN)。
在 Model Manager 搜索 `CosyVoice-300M-MNN`，选择 ModelScope 来源并下载；该条目无需等待 Hub 索引即可显示。
安装器使用 Package 内固定的40位提交及30个文件的大小/SHA-256清单，保留全部外置权重和参考资产，拒绝不匹配的文件，完成后由 Studio 识别。
完整的安装会占用约1.38 GiB；首次下载需要网络，已有文件只有通过哈希验证后才复用。
原 `iic/CosyVoice-300M` 是 PyTorch 仓库，不能直接当作 MNN 安装。
本地已有转换可用 [prepare_unity.py](../Tools~/Models/CosyVoice300M/prepare_unity.py)
补齐分词与识别文件；它不下载模型，不修改图或权重。

## 验证范围

原生调用复用包内官方 Interpreter/Session/Tensor API，未新增 C++、桥接库、自定义 C 导出或修改官方源码。
它受随包 MNN 3.6.1 的编译器和 STL ABI 约定限制；这不是 MNN 官方提供的通用 C# 接口。
macOS Mono 与 Android/iOS ARM64 IL2CPP 的基础约定见 [移动端 ABI](MobileInterop.md)。
基础 ABI 可用不代表该模型已经完成所有设备的推理和质量验收，Windows 当前没有相应直接 C++ 绑定。

本轮 Unity 实测记录在开发工程 `TestArtifacts~/CosyVoice300M/PackageIntegration/REPORT.md`；
计时、WAV、独立识别与 Unity 测试结果保留在同目录。历史 Python 的 RTF 不能替代 Unity 结果。
ASR、幅度及重复性检查只覆盖有限样本，不等同于 MOS、所有语句质量无损或所有设备实时。

本轮 Apple M5 / Unity 2021.3.45f2 Mono，完整参考、10 步 Flow、seed42、4 线程，
中英文各两次完整生成：默认 Auto 实际使用 Metal Flow/HiFT，第二次的 RTF 为 **1.561–1.565**；
全 CPU 为 **1.820–1.919**。RTF 不包含加载和写文件，第一遍也不能视为清除磁盘缓存后的冷启动。
82 项 Unity 检查通过，默认配置的两条文本共 9 份 CPU/Metal/Studio WAV 独立 ASR 匹配；
量化模式的原生执行检查通过、内容验收失败，两类结果分别记录。
**本次 C# 接入没有达到 RTF 0.3，也未验证 Windows、Android 或 iOS 的这条管线。**

后续 Unity 优化的配对计时、数值对照及 WAV 筛查见开发工程
`TestArtifacts~/CosyVoice300M/UnityOptimization/REPORT.md`。
复现入口为 `Tools~/Models/CosyVoice300M/run_unity_optimization.sh`；只链接本地已有模型，
交替测量最终目录内原 prefill 图和 compact 图的四条中英文输入，执行完整 PCM 读取。
Flow 单输入 Module、10 步原生 VARP 状态和线程筛选未建立明显整体收益，未作为默认接入。

最终本轮代码通过 85 项 Unity 检查；四条配对文本的 compact 第二遍完整 RTF 为 **1.453–1.479**。
前三组均为 Metal 声学、没有回退，完整等待约减少 5.2%–7.7%。第四组原图触发 CPU 回退，
不能把该组全部差异归因于解码图。25份非量化 WAV 的独立 ASR 严格匹配21份，
另4份均是“五点”与“5点”的写法差异；四份量化仍内容失败。
这不是音质普遍无损或 RTF≤.3 的证明。配对音色代理、PCM差异、失败尝试和回退代价均保留在优化报告中。


## 保守优化与小样本验证（2026-10-28）

新增 `MNNCosyVoice300MDecoderMode`：默认 `Float16`，显式候选 `ExperimentalInt8` / `ExperimentalFfnInt8`。
传入 `Load(..., decoderMode: ...)` 选择；仅逐 token 解码量化，原 FP16/High prefill 不变，
候选使用 High 计算与 Low memory。不能与旧 `experimentalQuantizedDecoder` 布尔开关同时选择。
54次完整 CPU 合成中，全 INT8 的 AR 耗时减少29%–47%、总耗时减少11%–26%，RTF为1.369–1.587；
输出 token 数和时长发生变化，不能把这些 RTF 当成完全同工作量的比较。
全 INT8 的 close→closed 和仅 FFN INT8 的借阅→建阅出现独立 ASR 内容偏差，均不设为 Studio 默认值。
平均音色代理接近基线不能替代内容与人工听感验收。

小样本实验以16句、两个seed采集完整参考的十步教师和两步输入，按句子固定划分8/4/4句 train/dev/test。
训练10640参数残差 mel 修正网络，原 Flow estimator 保持冻结。这是两步后的端点修正，
不是完整 Flow estimator 或 AR 蒸馏。测试集相对两步输入的教师端点 MSE 减少约72%，
Unity/MNN与Torch相对RMSE小于0.00018，修正网络本身约0.15–0.61毫秒。
8个测试样本的完整CPU RTF为1.084–1.618（中位1.114），十步教师中位1.696；
只计mel误差不足以确认音质，未作为生产 `Synthesize` 的自动替换路径。

最终连续GPU合成、独立ASR/音色代理、失效尝试及限制见开发工程
`TestArtifacts~/CosyVoice300M/ConservativeOptimization/REPORT.md`。
本轮仍没有达到质量验收前提下的 Unity RTF<0.3；Studio继续默认FP16/完整参考/十步Flow。


本轮最终质量结论：保留集教师/两步/修正后的独立 ASR 严格匹配为3/8、3/8、4/8；教师本身也有真实内容偏差，
修正网络的平均音色代理由教师0.532降至0.492，因此端点MSE改善不构成保音质验收，修正网络不接入默认合成。
FP16和INT8共36次Metal连续合成无CPU重试，热RTF1.482–1.578；容量1/4的缓存淘汰测试通过，
86项功能/参数检查及包结构校验通过。仅验证本机有限样本，未达到保音质的RTF<0.3。
