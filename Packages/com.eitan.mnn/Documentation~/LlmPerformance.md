# LLM 性能配置与验证

性能配置应以目标设备、模型和输入长度的真实推理结果决定。当前重点是
Qwen3.5-0.8B，同时验证 SmolLM2-135M、SmolVLM-256M 和 LFM2.5-Audio-1.5B。
本包直接调用限定编译器 ABI 下的官方 MNN C++ API，没有增加 C++ 桥接或修改官方源码。

## 已落实的优化

- 原构建全局使用 `-fno-inline-functions` 保留绑定符号，限制了全部计算内核的优化。
  `Native~/retain-abi.cmake` 将此限制缩小到官方 `Tensor.cpp` 和 `omni.cpp`；其他文件保持 O3 内联。
- 显式启用低内存量化内核、Transformer fusion 和 MNN thread pool。
  Apple 构建启用 ARM82/SME2，由官方运行时检测硬件；Android NDK r25c 构建启用 ARM82，关闭 SME2。
  Android 同时编译 OpenCL、Vulkan 和 NNAPI，Apple 编译 Metal。
- 流式输出只复制、解码新增的 UTF-8 字节，跨 token 保留 Decoder 状态，按请求复用字节和字符缓冲区。
  `GenerateConversationStreamingDeltas` / `GenerateConversationStreamingDeltasAsync` 只交付新增文本，
  避免长回答逐 token 创建累计字符串；最终结果仍包含完整文本。SiriAI provider 已使用 delta 路径。
  原累计回调 API 和每 token 取消行为保留，累计不可变快照仍会分配字符串。
- `CountTokens` 直接统计官方 tokenizer 返回的 vector 长度并按原有 allocator 析构，
  不再复制一份只为计数的托管 token 数组。每实例有界 LRU 复用相同文本的计数，
  超长文本不保留；模型重建与释放时清空。缓存未命中仍由官方 tokenizer 执行。
- `CountConversationTokens` 与 `PrepareConversation` 使用官方完整 chat template 计数。
  Runtime 可按显式模型上限及输出预留，移除最早完整轮次，保留 system 与当前问题；
  不自动摘要或修改文本，媒体 embedding 不用文本计数冒充。裁剪会减少历史信息，需调用者选择。
- 会话结果包含 `ConversationCacheStatus`，区分冷启动、禁用、媒体、不支持的策略、
  历史状态不匹配和中断；稳定的模型缓存能力按句柄缓存，避免每轮重复 dump/解析配置。
  x64 的 SmolLM2 KV 续接出现真实答案质量回归，计数/不计数对照均复现，因此该架构
  暂用完整 prefill 并报告 UnsupportedRuntime；ARM64 的复用仍须通过精确状态匹配。
  量化 attention 配置也使用完整 prefill 并报告 UnsupportedModel：固定 CPU 运行时的
  Key INT8/KV INT8 续接未通过答案质量检查，尚未验证其他后端可安全复用。
- 没有有效会话历史时，直接由官方聊天接口完成模板渲染和分词，不再为缓存比对预先重复执行一次。
  冷请求的 token 总数在官方 prefill 后读取，媒体模型仍保留官方 tokenizer/response 的状态处理。
  有历史但不匹配时继续完整重算；此优化不改变权重、解码内核或输出 token 预算。
- `MNNLlmPerformanceOptions` 提供线程、后端、精度、功耗和官方 weight-online-reorder 配置。
  结果同时暴露 prefill/decode/sample 的原生计时，以便定位托管等待与采样开销。
- SiriAI 首个非空文本增量立即显示，后续增量在累计 32 字符或距离上次发布达到 50 ms 时刷新。
  50 ms 检查由新增量触发。语音文本通过受整条回复字符预算限制的独立缓冲转发，
  LLM 回调不再等待 TTS 或播放队列；音频队列仍保持原容量，取消时等待转发与合成任务退出。
- SiriAI 示例场景改用精简的 `Prompts/ConversationPrompt.txt`，保留完整 `SystemPrompt.txt` 可选。
  PJA110 相同 CPU4 Normal/Normal 的两题、八次真实生成对照中，完整提示从 1,697 降为 345 tokens，
  首次更新中位数约 10.028 降为 1.822 s，所有答案满足算术/城市名和本次精确格式要求。
  这是减少示例默认提示的输入工作量，不是同输入的内核加速；没有自动删减自定义提示或历史。
- Studio 对话使用 Low 精度、High 功耗，保留用户选择的线程数和原有采样策略。
  `Library/MNN/Studio/ShaderCache` 保留编译缓存；缓存按原生库内容、模型文件元数据、配置内容、
  GPU/驱动、线程数和 ABI 区分。并发窗口独占不同槽位，原生对象释放后才解锁。
  可在关闭相关 Studio 会话后删除此目录以清理缓存。

## 使用方式

常规加载默认自动调优，`MNNLlm.Load(modelDirectory)` 和 `LoadForConversation` 使用同一选择逻辑。
优先采用已验证的模型/设备配置：PELM00 上本轮三个模型使用 CPU / 2线程 / Low / High；
PJA110 的 Qwen3.5-0.8B 保留 CPU / 4线程 / Normal / Normal。其他组合以最多4线程、
Low / High 和已有 Auto 后端选择为起点；可用性与兼容回退继续生效。
这些是当前验证范围内的默认策略，不是所有模型、温度和业务负载下的绝对最优保证。
设备档案按标准模型目录名与设备/GPU匹配；重命名目录或未匹配组合使用通用策略。

开发者逐字段覆盖优先。例如只设置 `ThreadCount = 4`，线程数保持4，其余仍自动选择；
即使显式值恰好等于旧默认值也不会被覆盖。省略参数与显式传入 Normal 已可区分。
`new MNNLlmPerformanceOptions()` 的未赋值属性在加载时解析；其未解析 getter 显示通用回退值，
可调用 `Resolve(modelDirectory)` 得到独立快照，便于记录或用于缓存身份。

自动默认路径已通过52项相关Editor测试、macOS的4项真实推理检查，以及PELM00的9个独立进程检查
（共69次生成）。原生dump确认默认选择与逐字段覆盖均生效；两种模型58条非取消请求与相同显式
配置的完整输出IDs/文本一致。证据见本地 `TestArtifacts~/LlmOptimization/AutomaticDefaults/report.md`。

```csharp
// 无需重复指定已验证的设备配置。
using (var model = MNNLlm.Load(modelDirectory))
{
    var result = await model.GenerateAsync("中国的首都是哪里？只回答城市名称。", 32);
}
```

需要固定参数时继续显式设置：

```csharp
var tuning = new MNNLlmPerformanceOptions
{
    BackendType = MNNBackendType.Auto,
    ThreadCount = 4,
    PrecisionMode = MNNPrecisionMode.Low,
    PowerMode = MNNPowerMode.High
};
using (var model = MNNLlm.Load(modelDirectory, tuning, cacheDirectory: persistentModelCache))
{
    var result = model.Generate("中国的首都是哪里？只回答城市名称。", 32);
    Debug.Log($"prefill={result.PrefillMicroseconds} us, decode={result.DecodeMicroseconds} us, sample={result.SampleMicroseconds} us");
}
```

`Load` 使用 greedy 解码。`LoadForConversation(directory, tuning, temperature: ...)`
支持会话采样；温度为零可用于可复现的正确性对照。整数线程重载只覆盖线程，未指定的精度和功耗自动选择。
旧的完整 CLR 方法签名保留；已编译程序集中的旧可选参数常量无法区分是否由源码省略，需重新编译调用方
才会使用新的省略参数行为。显式设置不更改模型权重、模板或采样策略。
High 功耗可能改变持续运行的温度与频率，应同时观察长时间吞吐。

运行时调用者可传入稳定的 `cacheDirectory` 复用编译缓存，并负责在更换模型、原生库、设备或驱动后
失效缓存，以及避免并发写同一目录。未指定时仍使用实例拥有的临时目录，在 Dispose 时删除。
`MNNLlmCache.Acquire` 为 Runtime 提供稳定身份与独占槽位；模型释放后再释放 lease。
身份包含原生构建、设备/驱动、ABI、线程、配置与模型元数据；可用经过校验的完整交付清单身份
代替模型文件枚举。它不逐次 hash 大权重，同大小且保留时间戳的变更必须更新显式模型身份。
SiriAI Player 已使用 build GUID 与主线程捕获的设备信息获取 lease；Editor 保留临时目录。

`EnableWeightReordering` 对应官方 `dynamic_option` 的 bit 3（`WEIGHT_ONLINE_REORDER`），
未赋值时继承模型；显式 true/false 只更改此 bit，保留原有动态量化模式。
`DynamicQuantization`、`FlashAttention`、`PrefillChunkSize` 默认继承，按加载前配置生效。
FlashAttention 保留 KV 位，chunk 不覆盖模型已有的 chunk_limits 形状契约。
权重重排不是反量化权重缓存，也没有对所有模型都更快的保证；启用前测量速度与内存。
本轮未将 KV 量化或推测解码设为默认，也未修改模型权重；实验候选收益单独记录。

## 开源实现与论文的采用边界

[llama.cpp](https://github.com/ggml-org/llama.cpp) 的量化、KV 管理、后端调优和基准方法，
[MLC LLM](https://github.com/mlc-ai/mlc-llm) 的编译部署思路可作为参考。
本包采用可由 C# 状态管理和固定版本官方 MNN 配置实现的部分，不引入第二套原生引擎。
低位权重量化、融合 attention、SIMD/GEMM 与 NPU AOT 仍需相应官方内核和模型导出产物。

[EAGLE-3](https://arxiv.org/abs/2503.01840) 与 [DFlash](https://arxiv.org/abs/2602.06036)
需要匹配目标模型的 draft/特征与官方验证路径。固定 MNN 有策略源码，但当前逐步流式协议、
输出预算和配套模型仍需专项接入；尤其 DFlash 不维护当前读取的同一 generate_str 字段。
不能只写 speculative_type 就视为支持，更不能把论文服务器倍数当作本包手机收益。

固定版本的 lookahead 已做 macOS ARM64 / CPU4 的采用门槛实验：Qwen3-0.6B
图包含所需的 `logits_index`，实际出现一次 `generate(1)` 接受 4 tokens，但请求预算 4
时输出 7 tokens；预算 7 的一例 IDs 与普通 greedy 完全一致，`generate_str` 却漏掉
已接受的草稿文本。因此未接入公开 API，也没有将诊断耗时作为有效加速。
现有 SmolLM2-135M 导出没有 `logits_index`，设置 lookahead 后实际回退普通解码。
完整实验 115 项中 114 通过、Qwen 采用门槛 1 项失败，失败证据保留在本地
`TestArtifacts~/LlmOptimization/Lookahead/report.md`；不代表 Android/iOS 已验证。

[KIVI](https://arxiv.org/abs/2402.02750)、[SnapKV](https://arxiv.org/abs/2404.14469)
需要修改 attention/KV 执行路径；不属于 C# token 数组压缩。官方 TQ3/TQ4 小模型质量风险
较高；当前没有默认开启，模型量化实验必须同时评估中文、长上下文、代码和结构化输出质量。

[MNN-AECS](https://arxiv.org/html/2506.19884v1) 的核心选择研究提示应分别测量 prefill 与 decode。
其阶段切换依赖线程池和 KV 内存池改动，本包只借鉴候选配置的测量方法。
[CORE（MLSys 2026）](https://proceedings.mlsys.org/paper_files/paper/2026/file/136b9a13861308c8948cd308ccd02658-Paper-Conference.pdf)
研究 CPU/GPU/内存频率的联动；普通 Unity C# 配置没有该实验所需的系统调频权限。
本轮没有实现这两套系统，也没有采用论文中的节能数字作为本包收益。

2026-10-28 补充核对了近期论文：
[EnerInfer](https://arxiv.org/html/2606.23001v2) 将首字、持续输出与热状态纳入配置选择；
[MemSpec](https://arxiv.org/html/2608.10362v1) 提醒模型选择须计入驻留和加载成本；
[EdgePace](https://arxiv.org/html/2610.37307v1) 同时评估推理与前台任务的带宽竞争。
本轮采用这些测量思路，在 PELM00 同时记录加载、首字、文本间隔、帧心跳、常驻内存和取消恢复。
EnerInfer 的系统调频、MemSpec 的 CUDA 推测解码和 EdgePace 的 C++ 调度 hook 均未引入；
它们不能直接作为普通 Unity C# 包的能力或手机收益。来源快照与逐项采用边界见本地
`TestArtifacts~/LlmOptimization/AndroidPELM00/Research/`。

### 固定前缀的磁盘 KV 实验

[ContiguousKV（2026）](https://arxiv.org/html/2601.13631v1) 的磁盘前缀恢复研究与
[Marconi（MLSys 2025）](https://proceedings.mlsys.org/paper_files/paper/2025/hash/7c180af017258d239bac6248d1eb26ac-Abstract-Conference.html)
提示应同时检查 I/O 成本与混合模型的递归状态边界。本轮没有移植论文的调度或裁剪算法，
而是用临时 C# 探针调用固定官方 MNN 已有的 `setPrefixCacheFile`，验证精确前缀恢复。

CPU4 / Normal 精度 / Normal 功耗、约 1068–1069-token 固定前缀与 27–33-token 后缀下，
下表为首轮四个短回复任务各三次正式样本的总生成耗时中位数范围，不含模型加载或首次建缓存。

| 设备 / 模型 | 完整计算 | 已有前缀恢复 |
| --- | --- | --- |
| M5 macOS / SmolLM2-135M | 369–390 ms | 71–98 ms |
| M5 macOS / Qwen3.5-0.8B | 1143–1217 ms | 91–171 ms |
| iPhone 17 / SmolLM2-135M | 741–803 ms | 108–137 ms |
| iPhone 17 / Qwen3.5-0.8B | 2290–2389 ms | 176–335 ms |
| PJA110 Android / SmolLM2-135M | 2121–2161 ms | 226–274 ms |
| PJA110 Android / Qwen3.5-0.8B | 5917–6002 ms | 233–389 ms |

各平台首轮均通过 150 项；同配置的完整计算、分段计算与磁盘恢复输出 IDs 一致，读取前后
缓存 SHA256 指纹不变。答案单独评分：Smol 四题通过，Qwen 三题通过，意大利题的完整基准
也错误回答 Napoli/Naples。没有通过相同输出掩盖基准错误。测量已有 OS 页缓存，手机存在热状态影响；
这些是重复长前缀任务的收益，不证明解码内核、冷启动、峰值内存或能耗普遍改善。

每模型的前缀文件约 46–51 MiB。现有 Qwen 导出遗漏层数，且混合递归层与 native 文件验证
条件不一致；探针使用经官方架构及图算子核对的固定模型变通，尚未作为公开用法。
本轮正式 Runtime 未新增磁盘 KV API。内容身份与失效、独占写入、取消/异常后的发布、
容量管理仍需产品级接入和验证。跨进程补测、iOS 主线程 watchdog 的探针修正及完整证据见
本地 `TestArtifacts~/LlmOptimization/PersistentPrefix/report.md`。

2026-10-28 在较慢的 OPPO PELM00（Android 13 / Mali-G57 MC3）上，V7 同一 APK 的
首轮及新进程读取各通过 115 项，共 194 次生成；跨进程输出 IDs 与前缀指纹一致。
下表单独列出每模式每模型 12 次正式样本的首字时间中位数，两进程给出范围，
与上表的总生成时间口径不同。

| PELM00 模型 | 完整计算首字 | 已有前缀恢复首字 |
| --- | --- | --- |
| SmolLM2-135M | 7178–7351 ms | 493–509 ms |
| Qwen3.5-0.8B | 17190–17345 ms | 931–945 ms |

仍为 CPU4 / Normal / Normal、约 1100-token 固定长提示的底层探针，排除加载、首次建缓存及
产品级身份校验，已有 OS 页缓存且温度未控制。Qwen 仍错误回答 Naples；Smol 也不总遵守
“仅输出城市名/暗号”的格式要求。此结果不验证后述异步会话候选的手机文件锁、配额或生命周期。
设备资产补齐、构建与逐次证据见本地 `TestArtifacts~/LlmOptimization/AndroidPELM00/`。

持久化前缀的收益还需扣除读前校验。随后 M5 / CPU4 / Normal 精度与功耗专项复用同一
批缓存，比较完整重算、原始恢复、串行完整 SHA256 校验恢复和四并发逐文件完整校验恢复。
四个独立进程各通过 115 项，共 448 次探针生成。四并发两次测量将校验中位数从 Smol
311–339 ms 降至 104–113 ms、Qwen 284–286 ms 降至 96–98 ms；保留同一逐文件内容检查。
计入校验、恢复与生成后，四题各自中位数相对完整重算，Smol 节省约 197–294 ms、
Qwen 约 952–1334 ms；串行校验下 Smol 有一次反向顺序测量反而更慢。加载另计，首次建
缓存不在本轮范围；页缓存未清空，完整校验也会预读文件，不能据此声称冷磁盘或手机收益。

已测所有路径的输出 IDs 一致，同长度内容损坏、缺失标记和额外文件均被两种校验路径拒绝。
四题答案仍为 Smol 4/4、Qwen 3/4；Qwen 兼容变通仍仅限实验。四并发不是手机默认，
尚须验证线程/内存竞争、取消、跨进程锁、原子发布和损坏回退。本次实现保留在隔离探针，
未新增公开磁盘 KV API。完整结果与新论文采用判断见本地
`TestArtifacts~/LlmOptimization/PrefixValidationCost/report.md`。

后续 SmolLM2 / macOS ARM64 Mono 的生命周期候选已验证完整身份、读前独占锁、
逐文件 SHA256、原生对象释放后 staging→ready 发布，以及损坏/中断后的新句柄完整回退。
最终同一 Player 的两个独立进程各通过 119 项，共 35 次完整生成、8 次有意中断，
输出 IDs 和发布缓存指纹一致；另用同时存活的 Player 验证争用拒绝与释放后重新获取。
这是正确性验证，没有新增性能比例。候选仅保留在隔离实验，尚缺手机、全局配额和正式会话接入。

Unity 2021.3 的 IL2CPP POSIX 源码显示，`FileShare.None` 只检查当前进程；候选额外使用
稳定文件的 `FileStream.Lock`，且不删除锁文件。其底层对部分不支持锁的错误会返回成功，
因此本机通过不能作为任意存储或手机的资格证明。固定 MNN 的 `reset()` 也不清除全部
前缀模式状态，中途失败后不能直接复用同一句柄。实现、源码依据与边界见本地
`TestArtifacts~/LlmOptimization/PrefixLifecycle/report.md`，未新增公开磁盘 KV API。

容量候选进一步增加根目录协调锁、写入前预留、跳过占用条目的 LRU 和显式清理。
120 MiB 策略下，真实 KV 测试验证空闲条目可淘汰、已锁定条目不被删除、全部占用时拒绝
新写入并完整计算；独立进程也验证写入预留与进程终止后的回收。预算约束已发布数据及
预留，不包含活动 KV、文件系统开销，也不能中断一次 native prefill 来强制物理磁盘上限。

SmolLM2 / M5 / CPU4 / Normal 的最终同一 Player 两进程各通过 121 项，实际完成130次生成、
8次有意中断。四并发发布校验保留完整 SHA；计入加载、校验、原生执行及释放后，两轮正式
请求中位数 full/read 约415/266、371/209 ms，首次建立和发布串行约804/685 ms、四并发
约578/498 ms。同轮配对的首次成本减少约192–231 ms；按已测中位数需约1–2次后续命中
覆盖额外建立成本。输入分词/可信交付身份预先准备，页缓存未清空，不代表完整 App 冷启动、
手机或能耗结论；四并发仍为显式候选。完整成本口径和容量边界见本地
`TestArtifacts~/LlmOptimization/PrefixCapacity/report.md`。正式 Runtime 接入仍待设备资格与会话整合。

2026-10-28，隔离候选已接入现有 `MNNLlm` 同步/异步会话及两种流式回调：显式预热，
优先复用完整匹配的内存历史，否则校验磁盘前缀；reader 持有租约至 native 销毁。
中途取消或回调异常丢弃恢复句柄，后续重载；未交付回调的 native 错误使用新句柄完整计算。
容量占用和内容损坏在调用 native setter 前回退。固定 MNN 在加载时绑定目录，
不能只在已加载实例上修改 `prefix_cache_path` 来完成切换。

最终同一 Player 的两个独立进程各通过126项，含13项候选检查；实际记录117次成功返回的
chat 调用（含预热、参考和一次单 token 建缓存），另有2次返回取消、6次恢复阶段取消、
6次写入阶段取消，以及各2次刻意回调异常/native 故障恢复。它们不等于117次完整指令遵循。
已验证磁盘恢复后的内存续接、缓存开关、独立生成、并发 Dispose 等待和租约释放顺序。

SmolLM2 / M5 / CPU4 / Normal、1024-token 共享前缀，交替顺序、每模式1轮预热及4轮正式测量：

| 请求位置 | 首轮 full / disk 首字中位数 | 跨进程反向复测 full / disk |
| --- | --- | --- |
| 已加载实例的首次请求 | 349 / 237 ms | 312 / 240 ms |
| 同一实例再次独立请求 | 334 / 260 ms | 306 / 253 ms |

计时从异步 API 调用至首个回调，包含调度、模板分词、身份、锁、SHA、恢复模型加载/释放；
初始 `MNNLlm.Load` 与显式预热另计。页缓存未清空，替换期间可能短暂持有两个模型，
未验证移动峰值内存、冷启动、能耗或最快 Metal 对照。代码和可复查补丁保留于本地
`TestArtifacts~/LlmOptimization/PrefixConversation/`，未启用正式 Runtime 或新增公共 API。
同目录研究记录核对了2026年9月的 Dynamic Flow, Static Graph、mzCache、TierKV：
其选择性重算、共享 buffer 和分层 attention 依赖图/内核接口，不能作为纯 C# 已实现能力。

## 已观察到的配置差异

| 设备 / Qwen3.5-0.8B | 当前建议 | 证据口径 |
| --- | --- | --- |
| OPPO PELM00 / Mali-G57 / Android 13 | CPU，2 线程，Low，High | Unity 异步中文首字约 731 ms，96-token 解码约 16.9 token/s；对照 CPU4 Normal/Normal 约 829 ms、10.7 token/s；不含加载 |
| OnePlus PJA110 / Adreno 740 / Android 16 | Qwen3.5-0.8B Auto 使用 CPU；SiriAI 自动 4 线程、Normal、Normal | 30 次真实生成全部正确完成；22-token 短提示首次更新约 0.10–0.18 s，计数解码约 55–57 token/s；不包含加载或完整 App 流程 |
| Apple M5 / macOS | 优先测 Metal，4 线程，Low，High | 最终 Unity 真实计数任务 Metal 约 180 token/s；48 次普通/流式生成全部通过；后端优劣不能外推到其他 Mac |
| iPhone 17 / iOS 27.0.1 | 优先测 Metal，4 线程，Low，High | 真实长提示 Metal prefill 约 2869 token/s（原构建约 2184），decode 约 107；66 次生成全部通过 |

### 较慢 Android 的体验基线

PELM00 使用独立 Player 进程比较 CPU4 Normal/Normal、CPU2 Low/High、CPU4 Low/High。
每组包含 31 次生成，短中文、长输入和 96-token 解码各有三次正式样本。
CPU2 Low/High 相对 CPU4 Normal/Normal 的解码中位数约提高 59%，短中文首字约减少 12%；
CPU4 Low/High 只有约 11.6 token/s。这里改变的是组合配置，不能将收益全部归因于精度。
五次连续生成的 CPU2 解码约 17.2 token/s，尚不代表长时间热稳定性。
修复包的新进程复测为首字约735 ms、解码17.15 token/s，长输入首字约11.2 s。
与原CPU2组约731 ms、16.90 token/s、11.6 s接近；两个独立进程观察到的解码增益约59–61%。

约千 token 长输入的首字仍需约 11.6 s；在开始约 100 ms 后取消，仍需等待约 11.5 s
完成原生 prefill。解码阶段取消约 50 ms 后被观察到，随后请求可恢复。异步调用维持主线程
响应，但当前取消令牌不能打断正在执行的原生 prefill；完整 App 仍需明确显示等待和取消状态。
本轮多轮对照的第二次缓存状态为 HistoryMismatch、复用为零，输出 IDs 与完整重算一致，
没有得到 RAM 会话复用的加速证据。不可为追求命中而回退 Qwen 循环状态或删掉历史校验。

目标 60 FPS 的空 Unity 场景中，生成各组单次帧心跳 P95 的最大值约 17 ms。
这不是完整聊天 UI、游戏、相机或语音同时运行的帧率保证。三种 Qwen 配置的八题检查均为
5/8，两个算术题和意大利首都错误；性能与运行测试通过不代表答案可靠。
配置建议只针对这台设备与已测模型；高端手机和不同后端仍需各自验证。

本轮也修复了旧版 Qwen2.5-0.5B 导出的 ChatML 兼容问题：当 tokenizer 没有 Jinja 模板时，
官方多角色接口会直接拼接消息内容。Runtime 现在仅适配两个精确匹配的旧格式，保留 Smol
原默认 system，不覆盖已有 Jinja，也不猜测其他模板。它复用官方 set_config，未增加原生符号。
原先遗漏角色的 Qwen0.5 两组记录只作为缺陷复现，不用于评价模型能力。

修复版 Qwen0.5 在 CPU2 Low/High 下，短中文首字约 374 ms，解码约 30.0 token/s，
长输入首字约 5.9 s；CPU4 Normal/Normal 则约为 591 ms、15.5 token/s、8.6 s。
该解码任务预算为96 tokens，CPU2 实际提前在77 tokens停止，不能按“完成20条建议”评价吞吐。
八题严格检查为6/8：首个算术题错误，另一个内容正确但未遵循纯数字格式。长列表存在重复，
多轮姓名题完整与缓存均错误回答“你好。”。缓存第二轮复用748 tokens，首字从约6.1 s
降至0.32 s，输出 IDs 相同；这验证了加速与一致性，没有验证姓名回答正确。

Smol135M 的修复包回归解码约71.7 token/s，但八题仅1/8，中文建议不完整，持续中文任务
切换到英语。因此本轮没有根据速度自动替换通用中文模型；小模型须按限定任务另行验收。
修复包四组共完成124次生成，并通过9项官方原生模板渲染断言；另有4项Editor策略测试通过。
Qwen0.8和Smol在相同配置下，修复前后58条非取消请求的完整输出IDs和文本均相同。
完整转录、预算截断、构建身份和逐组证据见本地
`TestArtifacts~/LlmOptimization/AndroidPELM00/report.md`。

### CPU 内存策略

官方 `memory` 配置会影响权重加载和 CPU 执行路径，不能根据 `high` 名称推定更快。
M5 / CPU4 / Normal 精度 / Normal 功耗的五个独立进程对照中，现有 Qwen3.5-0.8B
的 42-token 输入 / 128-token 输出在 `low` 下约为 1049–1154 ms，`normal` 为
4382–4515 ms，`high` 约 4392 ms；约 990-token 长输入的短回复也以 `low` 更快。
Qwen 任务后的整个 Unity 进程 RSS 约从 1.2 GiB 增至 3.1–3.8 GiB；Smol 也观察到
normal/high 更慢、驻留更多，因此继续继承官方默认 `low`，未新增公开调优开关。

五次各通过 115 项，探针实际共生成 160 次，含独立的八题质量观察。Smol 为 3/8、Qwen
为 6/8，跨配置长输出 IDs 不同，不能称为无损；基准重复有波动，RSS 包含 Unity 和其他
原生对象，不等同于单独模型内存。没有新增 Android/iOS 或能耗结论。完整结果见本地
`TestArtifacts~/LlmOptimization/FrontierReview/MemoryMode/report.md`。

### 权重 mmap 与重新加载

官方 `use_mmap` 将 CPU 权重缓冲映射到 `tmp_path`；`use_cached_mmap` 可保留并在后续
加载时复用这些缓冲。它与磁盘 KV、shader cache 不同，不能据此预设生成 token/s 提升。
固定版本的 CPU 缓存文件前缀不包含模型身份，且存在 `sync.static` 时可跳过权重加载；
必须隔离模型、原生构建和配置，避免并发写入。已有 `MNNLlmCache` 提供身份及独占槽位，
但不校验 mmap 文件内容，也不提供完整发布、损坏恢复和配额管理。

M5 / CPU4 / Normal 精度与功耗的同一 Player，关闭、临时映射、首次持久化、跨进程读取、
再次关闭五次均通过 115 项，共 160 次探针生成。Smol/Qwen 的关闭 mmap 加载约为
77–83 / 330–351 ms，已存在缓存读取约为 23 / 109 ms；生成耗时没有足够证据支持改默认。
本次 `mmap_size=1024` 让每个模型产生一个逻辑 1 GiB 的稀疏文件，实际分配约为
82 / 518 MiB。探针在读取前逐次进行完整 SHA256 校验，各需约 5.5 s；计入该校验、准备、
加载和首字后约为 5.53 / 5.70 s，抵消加载节省。完整校验是本次验证策略，并非 mmap
固有要求；它还会预读文件，因此上述加载时间不能视为冷磁盘结果。

本轮所有配置的已测输出 IDs 相同，但八题观察仍只有 Smol 3/8、Qwen 6/8，不代表广泛
质量保证。未改公开配置或默认，未测手机内存压力、前后台恢复和能耗；临时 mmap 正常
释放后文件清除，持久化文件前后 hash 相同。原始数据与采用条件见本地
`TestArtifacts~/LlmOptimization/MmapWeights/report.md`。

### CPU 线程数与输入、输出长度

M5 Mac 的 CPU / Normal 精度 / Normal 功耗专项中，3 线程在短输入、128-token 输出任务
上两次复现了较低总耗时；长输入、短回复则明显变慢。下表每个值是单进程三次正式样本的
中位数范围，3 线程有两个进程，4 线程有三个进程，不包含加载时间。

| 模型 / 任务 | CPU 4 线程总耗时 | CPU 3 线程总耗时 | 3 线程相对 4 线程 |
| --- | --- | --- | --- |
| SmolLM2-135M，39-token 输入 / 128-token 输出 | 338–352 ms | 311–313 ms | 耗时降低约 7–12% |
| Qwen3.5-0.8B，42-token 输入 / 128-token 输出 | 1136–1159 ms | 1102–1105 ms | 耗时降低约 3–5% |
| SmolLM2-135M，988-token 输入 / 短回复 | 304–310 ms | 445–460 ms | 耗时增加约 44–51% |
| Qwen3.5-0.8B，990-token 输入 / 短回复 | 951–969 ms | 1468–1470 ms | 耗时增加约 52–55% |

以长输出为主的 CPU 调用者可显式传入以下配置，再用自己的任务验证：

```csharp
var cpuLongOutput = new MNNLlmPerformanceOptions
{
    BackendType = MNNBackendType.CPU,
    ThreadCount = 3,
    PrecisionMode = MNNPrecisionMode.Normal,
    PowerMode = MNNPowerMode.Normal
};
```

这没有替代 Auto 或默认 4 线程，也没有与 Metal 比较。Mac 跨线程数的长输出 token IDs
不同，只能视为相同输入、相同输出数量的任务耗时比较，不能声称逐 token 等价或无损。
八题筛查各配置均为 Smol 3/8、Qwen 6/8，未出现新增失败，但不构成广泛质量保证。
PJA110 Android 的 4→2→3→4→3 独立进程对照未支持降线程，继续保留 4 线程；后续样本
多为 SEVERE 热状态，没有控制频率或测量能耗。

固定 MNN 的 CPU 线程池按 cpuMask 在进程内复用，已有池容量可能限制后续请求的线程数。
对照配置应在新进程中、其他原生推理前加载；不能在同进程中先用 2 线程再用 4 线程，
就认定得到了独立的 4 线程测量。现有配置是加载时整会话设置，不提供 prefill/decode
阶段切换。两平台共 11 次运行各通过 115 项；352 次生成的原始文本、IDs、质量得分、
模型与插件身份，以及论文采用边界见本地 `TestArtifacts~/LlmOptimization/CpuThreads/report.md`。
本专项没有新增 iOS、x64 或其他设备结论。

PJA110 上的 SmolLM2-135M 长输出可显式选择 `BackendType = MNNBackendType.CPU`，
4 线程、Normal 精度、Normal 功耗。相同 52-token 输入 / 128-token 输出的交替对照中，
CPU 总耗时中位数约 689 ms，OpenCL 约 1937 ms；四个至少 20 秒的连续负载块中，
同配置约为 758 / 1945 ms。Low/High 下也观察到 CPU 优势。
但 428-token 输入、8-token 短回复时，OpenCL 首字约 337 ms，CPU 约 463 ms，
因此 Auto 不据此统一改为 CPU。跨后端长输出 IDs 不同，不作为无损质量结论；
连续实验中系统热状态由 LIGHT 升至 SEVERE，不能外推为能耗或热平衡收益。
两个 Android 专项各通过 115 项；完整逐次数据、模型 hash 和限制见本地
`TestArtifacts~/LlmOptimization/AndroidBackend/report.md`。

长上下文专项进一步比较 CPU4 / Normal / Normal 的 attention_mode 0/8/9/10，
Mac ARM64 与 PJA110 Android 各通过 115 项。Smol 的 2588-token 输入、8-token 输出
在默认 FA 模式 8 下，Mac 约 1085 ms、Android 约 7489 ms；关闭 FA 后分别为
1965/11218 ms，支持保留当前默认。Android 的 KV INT8 模式 10 下，Smol 同一长输入
约 6098 ms、677-token 输入/64-token 输出约 1359 ms（默认约 1486 ms），但 Mac
同配置更慢。Qwen 的长输出同轮比较没有一致改善。量化跨配置长输出 IDs 不同，
四道短质量题不足以证明广泛无损；Android 正式样本处于 SEVERE 热状态，未控制温度。
后续 Mac ARM64 的完整公共生成路径检查发现：Smol Key INT8 续接将日本首都回答为 Osaka，
相同模式完整 prefill 回答 Tokyo；KV INT8 续接的法国问题回答 France，完整 prefill 回答 Paris。
Qwen Key INT8 的独立意大利首都题也从非量化的 Rome 变为 Napole。
Android 复现了 Smol Key INT8 的意大利首都续接回归和 KV INT8 的法国续接回归。
流式预算、取消和恢复通过不能抵消这些答案回归。因此暂未新增公开量化选项或修改默认，
现有量化配置保留其精度设置，但禁用托管层的跨轮 KV 续接；这不消除量化本身的精度损失。
官方加载时会将 reuse_kv=true 的 mode 10 转为 9，而加载后 set_config 不重建该 runtime hint，
不能用“运行时已开启复用”推断二者安全兼容。
逐次数据、质量错误和内存口径见本地 `TestArtifacts~/LlmOptimization/LongContext/report.md`。
续接和质量筛查证据见 `TestArtifacts~/LlmOptimization/KvInt8/report.md`。

OPPO 原插件的 CPU 4 线程 Normal 合成解码为 14.44 token/s，调优后约提高 48%；
这是同时改变编译与运行配置的组合结果。仅恢复内联、保持 CPU 4 线程 Normal 时为 16.67 token/s。
同机最新候选 OpenCL 解码约 10.84 token/s，低于 CPU；Vulkan 未产出有效的完整测量。
不能把后端注册、符号审计或合成吞吐当作所有算子已在 GPU/NPU 上执行的证据。

PJA110 上 Qwen3.5-0.8B 的 OpenCL 首次短回复更新耗时 3.317 s，第二次为 0.166 s；
374-token 提示在 OpenCL 低内存 GEMV 调优中原生崩溃。因此该模型与 Adreno 740 的
Auto 组合在进入推理前选择 CPU，显式 OpenCL 请求仍保留。Android SiriAI 保留 Normal/Normal：
16 次正反顺序真实生成中，其长提示首次更新中位数为 1.673 s，Low/High 为 1.724 s；
温度未严格控制，不能外推为所有设备的最优配置。完整 App 的加载、历史 prefill、TTS 和调度
耗时需要分别测量，不能用独立短提示的首次更新代替端到端延迟。

## 最新源码与硬件路径的实际限制

2026-10-26 核对了官方 HEAD `f0b6105c5c7f901b1531dd4c23afa8a00b454db3`（仍为 3.6.1），
并用未修改的源码构建 Android、iOS 与 macOS 双架构候选。它通过了多项 LLM 检查，
但 Apple ARM64 CPU 的 Qwen3 Embedding 语义检索检查失败；Normal/High、1/4 线程均复现，
关闭 SME2 后仍未满足同一质量门槛。较早官方提交 `024a946b0b8fcf87c8a418229fadd4cd7858ffba`
的优化构建通过全部 8 项检索检查。因此保留该提交作为本轮发布源码，最新候选与失败日志留作复查。
不能用版本字符串相同代替源码身份或质量验证，也不能混用不同提交的 C++ benchmark 与库。

M5 上，同一最新库由 SDK 27 构建的官方 `llm_bench` 可以启用 Metal 4 tensor/flash-attention 路径；
Unity 2021.3.45f2 宿主的 SDK 为 13.1，运行时探测报 `mpp`/`dextents` 未声明并回退。
仅重编 MNN 没有让这个旧宿主获得相同路径。该差异需要在采用现代 SDK 的 Unity 宿主中继续验证；
本包不修改 Unity 可执行文件，也不绕过 MNN 的硬件探测。iPhone 没有据此宣称 ANE 已被使用。

## 复现与证据

2026-10-27 的托管增量优化在 macOS Mono ARM64/x64、Android ARM64 IL2CPP、
iOS ARM64 IL2CPP 的专项回归中，各通过 140 项（包含 delta、精确预算、计数、配置、
lease、真实重载和多轮恢复）；两款 LLM 为 SmolLM2-135M 与 Qwen3.5-0.8B。
macOS x64 使用 Rosetta，并对未通过质量验证的 KV 续接采用完整 prefill。
Editor 的 23 项专项检查、八个必需程序集和 13 个平台条件编译通过。
源码/插件身份、移动 build ID、逐次结果与边界保存在本地
`TestArtifacts~/LlmOptimization/ManagedPolicies/report.md`、`validation-summary.json`。
期间并行修改的四份 Realtime 音频组件/缓冲文件不属于该冻结回归覆盖范围。

后续冷会话去重回归在 macOS ARM64、x64/Rosetta 和 Android 真机各通过 144 项。
相同 389/391-token 输入与 32-token 输出预算下，macOS Metal 两模型的首字延迟中位数
约下降 2.5/3.5 ms，CPU 与总回复耗时未得到稳定改善；没有据此宣称 decode token/s 提升。
后续 iOS 真机已在持久前缀专项首轮通过 150 项，其中 148 项为正式 Runtime 回归、
2 项为临时磁盘前缀探针；包含此处的四项冷会话对照。iOS 尚未据此新增冷会话性能收益结论。
原始对照、源码身份、论文核对与限制见本地 `TestArtifacts~/LlmOptimization/ColdPrefill/report.md`。

随后在 macOS ARM64 与 PJA110 Android 真机比较了官方 `generate(1/4/8)` 的调用粒度，
相同 128-token 输出的 IDs 一致，回调数从 128 降至 33/17，但完整耗时没有稳定改善；
批次还会延后观察 C# 取消，因此只保留实验，不新增公开批量选项。
另以 CPU4 / Normal / Normal 比较 dynamic_option 0/8/1/9/2/10：Mac Qwen 的小幅差异
没有在其他组合复现，Android 候选全部较慢，继续继承模型配置。
两项原型分别在两平台各通过 117/115 项；量化探针的四道质量题单独记录，测试通过数
不代表答案全部正确。它们不是新增正式 Runtime 优化或 iOS 验证，数据与限制见本地
`TestArtifacts~/LlmOptimization/StreamingBatch/report.md`。

同一输入的分配探针中，2,048 次更新累计快照分配约 16.85 MB，delta 约 65.8 KB；
重复计数探针在 Android 上 64 次同一文本的原生 tokenizer 约 792–911 ms，缓存约 0.29 ms。
这些只证明指定托管分配/重复分词工作量减少；未得到真实长输出生成 token/s、峰值内存、
能耗或持续热性能的通用提升结论。chunk=32 在本次短任务中较慢，保持可选。

`Tools~/Testing/benchmark_llm.py` 调用已有的官方 `llm_bench`，每个配置单独运行，使用
`-pg 128,64 -p 0 -n 0` 复用同一 KV 状态。预热由官方工具完成，`--repeats` 是正式重复次数。
工具记录 runner/library/config 身份，拒绝后端配置不符、缺项或全局 CPU 回退；不下载权重。
官方 benchmark 使用合成 token，不能验证答案。性能数字未控制设备温度，不构成统计性能保证。

```bash
python3 Packages/com.eitan.mnn/Tools~/Testing/benchmark_llm.py \
  --runner TestArtifacts~/MyBuild/arm64/llm_bench \
  --library-directory TestArtifacts~/MyBuild/arm64 \
  --model Assets/StreamingAssets/MNN/Models/Qwen3.5-0.8B-MNN/config.json \
  --threads 2,4 --backends 0,1 --precision 0,2 --power 0,1 \
  --output TestArtifacts~/LlmSweep
```

后端参数使用本工具的编号 CPU=0、Metal=1、OpenCL=3、Vulkan=7；工具转换为官方要求的名称。
Android 使用 `--adb-serial` 和设备上现有 `config.json` 路径，仅临时复制 runner/library。
输出目录必须位于本工程 `TestArtifacts~/`，且是新目录。

`LlmPerformanceTests.CompareNativeAndManagedLatencyWithCorrectnessGates` 是显式 Editor 测试，
通过 `MNN_LLM_PERFORMANCE_MODEL`、`MNN_LLM_PERFORMANCE_OUTPUT` 指向本地模型和输出目录。
它比较 CPU/Auto、2/4 线程，覆盖短中文、长中文和计数，交替普通/流式生成，检查同配置输出一致、
取消与恢复，并记录原生计时、墙钟耗时与首个流式更新。移动 Player 的 plan 支持同样的显式调优字段。

项目本地 `TestArtifacts~/LlmPerformance/validation-report.md` 和 `validation-summary.json`
汇总最终二进制身份、通过范围、逐次测量和失败实验。构建/符号审计、条件编译与真实模型推理
是不同的验证层次；可选跳过不计作通过，x86_64 的 Rosetta 结果不代表 Intel 真机。

同步原生测试可能长时间占用 Unity 帧。平台 runner 的 Player 心跳超时现为 1800 秒；
此前 180 秒会在长测试期间断开 Editor，使 Unity Test Framework 1.1.33 的结果发送队列忙等。
Android 采样已将这一问题定位到 `SendDataRoutine`，完整最终回归使用延长超时后的新构建，
中断运行不计作完整测试通过。
