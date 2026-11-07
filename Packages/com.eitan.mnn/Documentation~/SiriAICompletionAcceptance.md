# SiriAI 迁移后的验收状态

更新：2026-11-06。通用实现位于 MNN Unity Package 的托管 C# Core/Components。
本轮增加的应用 owner 为 `MNNConversationApplication`，接入方式见
[应用架构](ConversationApplicationArchitecture.md)，可运行装配见
[独立示例](../Samples~/BasicExample/Scripts/MNNStandaloneConversationExample.cs)。

七项验收尚未全部关闭。没有新增 C++、自定义 bridge/导出或修改官方 MNN。
以下每一组结果只覆盖自己的平台、源码快照和测试范围，不能合并成全平台通过。
完整原始证据和行动日志位于仓库 `TestArtifacts~/SiriAICompletion/`；
验收索引为该目录的 `acceptance.md`，行动日志为 `actions.md`。
后续内存、原生故障与 JobTemp 调查单独记录在 `Followup/report.md`、
`Followup/summary.json` 和 `Followup/actions.md`，不覆盖先前失败证据。

## 2026-11-06 后续修复

- Kokoro 的已释放 owner 仍被 UI 或完成的 Task 引用时，原先保留模型字节和词典。
  真实模型回归从残留 146862080 B 降至 -4096 B；负数表示 GC 采样波动。
  此结果验证托管数据释放，不代表整个进程 RSS 已达标。
- 多输入图固化改为只分配一个结果。16 MiB 三输入用例从分配 50334456 B
  降至 16779952 B，原图保持不变；真实 Unity 图执行、重复输入拒绝和零输入复制
  契约通过。单输入和多输入两项分配回归已加入独立 host Mono CI 步骤。
- 本地对话适配器不再用桌面通用 Low 精度覆盖已验证的模型默认值。
  Qwen3.5 CPU 公开 provider 的 `1+1` 从错误的 `1` 修复为 `2`，使用 Normal；
  四项真实模型回归通过。原场景 Auto/Metal 的 `3+5=7` 仍可复现，未宣称解决。
  仅将当前场景语言后端改为 CPU 的独立对照也失败，不能将该场景缺陷唯一归因于 Metal。
- 最终源码通过 24 次真实 Kokoro 加载／合成／释放、三项真实图行为测试、
  两项 host Mono 分配门禁、62 项 Python 工具测试、包结构和 actionlint 检查。
  所有必需程序集编译通过，可选 Burst 未安装；没有新的全平台／全模型通过结论。

后续调查仍开放：

- Editor 原日志存在 JobTempAlloc 的一组三行警告。四次带分配诊断的场景回放，
  包含当前工作区 Siri 场景和图形 Metal，均未复现该警告；各场景测试保留原有
  算术语义失败。尚无分配栈证明根因，因此不能称 JobTempAlloc 已修复。
- 原 Omni＋外部 Cosy 场景重现过 Kokoro `CPUInterp/CPUBilinearSampleC4` 的
  SIGBUS。随后两次原路径通过，独立预加载对照也通过；偶发崩溃根因仍未证明。
- Piper 探针显示大量 malloc 空闲容量仍驻留。独立 Module 候选虽减少原生分配器
  保留容量，仍在短程原门槛验收中出现 145899520 B（约 139 MiB）RSS 增长，
  超过 128 MiB，托管增长也高；候选没有合入。此前约 533 MiB 的长稳增长仍未关闭。
- 官方波形回调使用的 `std::function` 没有已支持的托管编组契约，未验证原生连续
  PCM 全双工接口。遵守 `AGENTS.md` 的无新增 C++／bridge／自定义导出限制，
  未构造假 std::function、私有对象布局或虚表调用来伪造支持。
- crapper、dryer、mutator 和 Unity coverage 仍不可用；没有相应指标达标结论。

## 已交付和未关闭边界

| 验收项 | 本轮完成 | 未关闭 |
| --- | --- | --- |
| 连续音频与原生全双工 | macOS 真实麦克风采集、DSP 输出、呈现回执与两次启停验收通过；共享路由区分轮次式能力 | 本地原生连续 PCM/全双工后端、Omni 原生 `std::function` 波形编组未实现；独立 TTS 不受此能力声明限制 |
| 原生 ABI | 本轮 macOS ARM64 Mono Player 29/29；iOS IL2CPP 真机 Express/Embedding/Reranker 3/3 | 只证明各自选择的接口和模型；没有可移植、官方保证的通用 MNN C# ABI，其他平台/编译器未逐项验证 |
| 既有失败清零 | 最终共享离线 2191 项：2171 通过、0 失败、20 跳过；14 个集成旧契约各自最后执行通过；Siri 托管生命周期 13/13 | 早期完整原生/Editor 快照的 291 个失败已逐项分类，部分后续通过，未全套清零；原先 681/32 与本轮范围不同，不能直接相减 |
| 模型、云与设备 | Lunflux chat 1/1；SiliconFlow ASR/TTS 2/2；macOS 物理音频 1/1；iOS 真机 3/3 | 音质、AEC、全设备路由、移动端语音全链路未完整验收；原场景模型语义和 Kokoro 原生崩溃仍开放 |
| 性能与长稳 | 3600.454 秒、378 次会话、1888 个真实本地文本/TTS 轮次；托管增长约 4.87 MiB，32 MiB 门槛通过；已记录分位数 | 本轮没有 RSS 通过结论，增长约 532.6 MiB；此前 128 MiB 目标失败。native/GPU 峰值、中断延迟、真实设备全链路和极限实时目标尚未验证 |
| 独立应用复用 | 核心 owner 12 项确定性回归；不依赖 Siri/EasyMic/test-framework 的消费工程编译通过；示例 UI 组件真实硬件整合 1/1，通过两次录音/本地 ASR、文本/TTS、混音输出、呈现、历史和关闭重启 | UI 使用相同公开命令，未做像素/hit-test；未知录音内容不作 ASR 准确率 oracle；其他产品场景仍需自己的验收 |
| 持续质量门禁 | 已纳入完整离线回归、host Mono 分配测量、原始证据上传和缺失工具检查；61 项 Python 工具测试通过 | crapper/dryer/mutator 与 Unity coverage 未配置，没有 CRAP、重复度、覆盖率和变异达标结论；未声称远端 CI 成功执行 |

共享离线的 20 项跳过包括 7 项未选联网测试、4 项未导入示例检查，以及
8 项 Unity 分配计数不可用和 1 项 Omni 波形缺失。最后 9 项使共享门禁保持失败。
host Mono 原始分配用例另行执行，不能替代 Unity/IL2CPP 的同项证据。

## 修复与复用入口

- DSP 启动异常已修复：读取系统 DSP 预算不再错误要求先绑定输出 pipeline，
  主线程约束保留；真实 Unity 红灯复现后，设备/会话 17 项通过。
- 应用启动、命令、输入端口、历史、取消/join 与释放重试统一由
  `MNNConversationApplication` 负责。sample 保留兼容构造壳。
- 独立示例硬件整合使用内置麦克风和 SmolLM2/SenseVoice/Piper，23.088 秒内
  完成两次完整启停。检查 response 绑定的滤波后非零波形和 Unity Listener 非零混音；
  已知正弦波探针表明，静音载体 `AudioSource.GetOutputData` 在此链中不能观测
  DSP 注入的音频。此前默认设备启动失败和 fixture 的单位/观测点错误均保留。
- Omni 配合外部 TTS 时只准备模型音频输入；请求模型自身语音仍检查真实波形能力。
- Supertonic 对缺少独立词表项的 U+3002 使用已有句点 token，保留原生词表项；
  6 项回归覆盖正常回退、词表优先、缺失回退和其他不支持文字。
- Siri session 已释放但保留运行故障时，仍完成辅助模型/声学的安全释放并返回原故障；
  quiescence 失败时保留仍可能被使用的资源，允许重试。

Siri 导入目录被 Git 忽略，其兼容修复通过
[版本指纹迁移](../Tools~/Migration/README.md)的 `--stage acceptance` 交付。
源码或 meta 不匹配时零写入拒绝，重复运行零写入，原 GUID 保留。

## 性能观测的限制

一小时测试使用 CPU SmolLM2-135M、PiperLessac，SenseVoice 只完成准备。
首 token p50/p95/p99 为 68.375/514.3355/1176.7159 ms；
首 PCM 为 468.5968/1941.8952/3856.5902 ms。
render 和 presentation 为注入回执，不能解释为扬声器实际首音频。
期间存在其他原生负载，结果不支持唯一性能归因。

RSS 从 710033408 增至 1268465664 B，观测峰值 1335640064 B；
这些是整个 Unity 进程采样，不是 native-only/GPU 分配高水位。
另一个 Resemble Player 验收观察到约 25.1 GB 物理 footprint 和较高 swap，
其数值通过不能作为低内存或实时资格。

历史 FIR 两项 p95 超过 8 ms 的失败保留。后续按预声明顺序、重负载结束后的
一次受控 host Mono 对照为 8/8，通过且热回调零分配；它不抹除历史失败，
也不证明竞争负载就是原因或 Unity/IL2CPP 达标。

## 需要继续定位的具体缺陷

- 原 Siri Qwen3.5-0.8B 场景输出 `3+5=7`，改名后仍回答旧名并漏掉水果。
  完整 2/4/6/8/10 条消息到达模型；直接公开 API 开/关 cache 均重现，
  不能归因为历史被截断，也尚不能唯一归因为权重。
- 完整 macOS 崩溃栈指向 Kokoro 提示音合成的 Express `Module::forward` /
  `CPUInterp` 路径；早先仅由最后一条加载日志推断 CosyVoice 的归因已撤回。
  该原生稳定性问题尚无已证明根因。
- AV-TFGridNet 完整波形提取公共 API 缺失；预处理 API 不替代完整提取。
- FS-EEND Editor Metal 数值失败与相同模型、相同最小输入的 built Player 通过
  之间差异尚未定位。Speaker 的独立参考来源/前端调用契约尚需完整绑定。
- 缺失/损坏或 profile 不匹配的模型与参考资产，以及 Whisper/语音内容/Studio
  的剩余失败，均保留在逐项分类报告中；未降阈值、改 golden 或以 skip 当作通过。

验收完成条件是这些边界有真实实现和目标环境证据。当前结果允许复用已实现的
核心能力，不构成“全部迁移验收完成”或“极致性能、低内存达标”的声明。
