# 视觉在场与目标拾音

`com.eitan.mnn` 提供应用无关的在场策略、目标人脸管线、图片上下文和目标拾音路由。
SiriAI 或其他应用通过 API/组件接入；UI、设备权限、账号授权及显示模式仍由应用决定。
本层不新增 C++、桥接库或原生导出，也不改变已有模型的 ABI 支持范围。

## 人脸、身份与在场

- 检测：SCRFD 判断画面中是否存在人脸，输出位置、分数和五点关键点。
- 跟踪：`MNNFaceTargetTracker` 保持当前目标，较大的新来者不会抢占；几何交叉或歧义会中断跟踪。
- 识别：MobileFaceNet 对五点对齐的 112×112 人脸生成 512 维特征；`MNNFaceGallery` 做内存匹配。
- 授权：`MNNPresenceController` 可接收明确的身份允许列表。`null` 表示任何合格目标，空列表表示无人获准。
- 在场：以捕获时间、连续观测和单调时钟决定是否允许开始交互。

人脸检测不是身份认证；几何 track ID 也不是身份。允许列表中的标识来自应用登记，不能从外观推断职业或权限。
特征库没有活体检测、防照片攻击或安全认证能力。识别阈值必须在真实设备和人群上校准。
库仅在内存保存登记向量的副本，可用 `Remove`/`Clear` 删除；应用自行管理登记授权、持久化和保护。

```csharp
// 在模型工作线程运行；加载后的模型由调用方释放。
using var detector = MNNImageTask.Load(scrfdDirectory);
using var recognizer = MNNImageTask.Load(mobileFaceNetDirectory);
var gallery = new MNNFaceGallery();
gallery.Enroll("operator-01", enrolledAlignedFaceEmbedding);
var sensor = new MNNFacePresencePipeline(detector, recognizer, gallery);
var gate = new MNNPresenceController(allowedIdentities: new[] { "operator-01" });

// capturedAt 是拍摄时刻，不是推理结束时刻。image 是方向已校正的 MNNRgbImage。
MNNPresenceObservation observation = sensor.Process(image, capturedAt, token);
MNNPresenceSnapshot state = gate.Observe(observation, now, interactionBusy);
// 摄像头停止返回数据时仍必须 Tick，禁止用旧帧持续保活。
state = gate.Tick(now, interactionBusy);
```

`MNNFacePresencePipeline` 默认选择一个占优目标并保持它，不构成多人活体跟踪器。
初次出现多张面积接近的人脸时不猜测目标；应用也可以用独立跟踪器生成 `MNNPresenceObservation`。
默认只有 Face 可唤醒，`requireFace: false` 可接受外部人员检测结果（Person）。

摄像头组件只依赖 `IMNNPresenceSource`，因此可换成同类人员检测、人脸检测/识别模型的适配器。
`MNNFacePresenceSource.Load` 是已有 SCRFD/MobileFaceNet 的可选便捷装配，其他模型实现相同接口即可。
语音模型实现 `IMNNTargetSpeechExtractor`，通过 `Supports` 声明输入几何、帧率和条件；
`CreateFromProviders` 与委托入口不绑定模型名。`MNNVisualSpeechFrames` 可声明宽高和帧率，AV-MossFormer 适配器只接受它实际支持的 112×112 / 25 FPS。

## 自然的离开和返回

| 情况 | 默认行为 |
| --- | --- |
| 新目标进入 | 连续确认 350 ms，确认前不显示 UI、不接受新对话 |
| 正常在场 | `Present`，允许开始交互 |
| 短暂遮挡或离开 | `Grace`，保留完整 UI，暂停新交互准入 |
| 同目标在 3 s 内返回 | 保留目标连续性，恢复交互，不重复开场 |
| 离开超过 3 s | `Standby`，呈现目标亮度 25% |
| 离开超过 15 s | `Sleeping`，隐藏、微亮待机或黑屏由应用选择 |
| 已有交互进行中 | 可以保持当前轮次，最长保持至最后观测后 30 s；不能以忙碌状态接受新轮次 |
| 目标或已识别身份变化 | 撤销旧目标连续性，重新确认 |
| 摄像头断流 | 750 ms 后禁止新交互，并继续按时间进入待机、休眠 |

`CanStartInteraction` 只控制新的录音/提交；`KeepCurrentInteraction` 决定已开始轮次是否可收尾。
输入被拒绝后不得把环境音发给 ASR/云端。应用应在撤销准入、目标切换和休眠时清理录音预卷、待提交音频与图片缓存。
待机/休眠不会自动终止操作系统、切断显示器或卸载所有模型。

`MNNPresenceComponent` 位于 `Runtime/Components/Realtime`，菜单为 **MNN / Realtime / Presence Interaction**。
将球体、聊天界面等放在 `_content` CanvasGroup 下，可选 `_blackScreen` 为黑色全屏 CanvasGroup。
组件本身留在持续启用的对象上，摄像头低频检测也必须保留，不能随 UI 休眠一起关闭。
组件使用 unscaled time，默认 250 ms 渐入、800 ms 渐出；隐藏后不截获点击，禁用组件会恢复原 Canvas 状态。
`Configure` 设置时序与允许列表，`Report` 在 Unity 主线程投递观测，`SetInteractionBusy` 报告现有轮次。
`Changed`、准入/唤醒/休眠事件由应用连接到 SiriAI 会话、球体或其他呈现层。不要直接把每次检测结果映射为 SetActive。

`MNNFaceCameraComponent`（**MNN / Realtime / Face Camera**）连接已获权限且正在播放的 `WebCamTexture`：
`StartTracking(camera, sensor)`。可在 Inspector 绑定 Presence 组件，或订阅 `Observation` 后投递。
它修正摄像头旋转/竖直镜像，将检测图像边长限制在默认 640，并最多保留一个工作线程推理任务，忙时丢弃中间帧。
默认在场检测 10 FPS、休眠检测 2 FPS；禁用时取消任务，模型和摄像头由调用方拥有。
释放模型/摄像头前必须 `await StopAsync()`，以确保在途推理已结束。错误经 `Failed` 返回，禁止以失败结果伪造在场。
应用自行请求摄像头权限并启动/停止设备；组件不会自动弹权限框。真实相机方向与设备性能仍需实机确认。

## 视听结合与多人拾音

推荐数据顺序为：设备 PCM / 播放参考 → 回声处理 → 目标提取或分离 → 增强 → VAD → ASR / 多模态模型。
应按所选模型的训练条件验证回声处理、分离、增强的组合顺序；不要无条件串联所有增强器。
已有 `MNNAcousticInputProcessor` 负责有界重采样、增强、VAD/ASR 路由；本层位于目标语音交付之前。

```csharp
// avModel、separator 均来自已有导出和加载 API，不会在每个音频块重新加载。
var pickup = MNNMultimodalSpeechModels.Create(
    audioVisual: avModel,                  // MNNAvMossFormer2Tse；可为 null
    separator: separator,                  // MNNSpeechSeparator；可为 null
    selectSpeaker: (sources, trackId) => SelectByEnrolledVoice(sources, trackId));

var visual = new MNNVisualSpeechFrames(normalizedFaceFrames, frameCount, videoStartedAt, targetTrackId);
var block = new MNNMultimodalSpeechInput(monoPcm, sampleRate, audioStartedAt,
    targetTrackId, detectedSpeakerCount, visual);
var selected = pickup.Process(block, presenceSnapshot, now, token);
if (selected.Route != MNNMultimodalSpeechRoute.Rejected)
    SubmitSelectedAudioToAcousticFrontend(selected.Audio, selected.SampleRate);
```

上例中的 `SelectByEnrolledVoice` 和 `SubmitSelectedAudioToAcousticFrontend` 是应用接入点。
源槽位会变化；选择器应比较登记声纹/当前目标证据，不应永久返回 0，也不应以最大音量代表目标身份。
应用可使用已有说话人嵌入 API 或 `MNNAutomaticSpeakerSelector`，并校准其阈值；无法判断时返回 -1。

路由按以下顺序执行：

1. 检查音频时效、目标 track、在场快照时效和准入。确认前采集的音频不予提交。
2. 目标一致且时间对齐的视觉条件可用时，调用视听目标提取，例如 AV-MossFormer2-TSE。
3. 已配置 `referenceTarget` 时调用参考语音目标提取，例如经调用方适配的 SpExPlus。
4. 明确只有一个说话人时，交付单人 PCM；说话人数未知（0）不能当作单人。
5. 明确双人重叠时，调用双路分离并要求目标选择器确认其中一人；歧义拒绝。
6. 未知人数、超过双人且无目标提取器时拒绝；当前双路模型不冒充任意多人分离。

视觉条件为连续同一 track 的 25 FPS、112×112 灰度面部裁剪，按
`MNNClearerVoiceFaceFrames` 约定预处理；音视频起止误差默认均不超过 80 ms。
不能把全景照片或任意单帧复制当作真实同步唇动。捕获层需校正摄像头旋转/镜像，处理丢帧、时钟漂移、设备重启和音频间断。
每次输入最多三秒 PCM、75 张脸帧，持有输入副本；音频结果检查长度、采样率和有限归一化值。
模型或回调异常直接传播，禁止异常后悄悄发送原始多人混合音频。
这些是工作线程块 API，涉及分配和模型推理，不能放在设备回调或 Unity Update 里。

单音频模式可显式使用 `new MNNMultimodalSpeechProcessor(..., requirePresence: false)`，路由仍要求目标或说话人数证据。
本层不虚构声源计数、嘴部活动概率或视听身份对应，必须由已验证的前端提供这些输入。

## SiriAI 的装配与独立开关

`MNNInteractionComponent` 组合包内在场、摄像头、图片缓存与拾音路由；`MNNAcousticProcessing`
拥有通用声学模型生命周期，并通过 `IMNNAcousticModelFactory` 支持已有各类增强/分离/声纹模型。
SiriAI 只保留兼容序列化字段、设备权限、场景呈现和设置持久化，已有声学 owner 转发到包组件，不再复制模型生命周期。

SiriAI 设置提供使用眼睛图标的独立“视觉”分类，集中放置视觉在场、身份匹配、允许列表、图片上下文、检测/识别模型选择器、确认时长、离开宽限、休眠等待及休眠显示方式。
视觉模型复用现有搜索选择器，显示已安装项并支持下载；模型选择不会自动打开摄像头。实际选择时在工作线程校验模型任务与发布者图文件 SHA，取消或过期设置草稿不会被回写。不支持的远程模型仍显示为不可用，不按名称猜测兼容性。
视听目标拾音、参考说话人提取、重叠说话人分离则放在“音频”的高级设置内，各自独立开关。
降噪、VAD/唤醒/ASR 增强、唤醒词仍使用已有独立开关。保存遵循现有草稿及补偿事务，关闭面板不提交。
开启视觉在场后，SiriAI 请求相机权限，装配相机与已安装模型；可通过 `PresenceSourceFactory` 换模型家族。
检测到合格目标后开始连续对话；相机断流或目标离开后按包策略撤销新交互准入。
UI 复用包的渐隐组件，球体通过独立父变换平滑收起，设置面板保持可操作；隐藏后可用 Ctrl/Cmd+, 打开设置恢复。

高级目标拾音需要调用 `ConfigureSpeech` 注入准备好的模型和真实同步采集适配器。
开启而未配置的能力会报错，绝不把未分离的环境音当作目标音发送给识别器。
`TryGetInteractionImage` 提供当前目标的新鲜上下文，支持图像的请求适配器应显式消费它；
本次没有改动所有本地/云端请求协议，也不声称开启开关就让纯文本模型获得视觉能力。

导入的 SiriAI 位于 Git 忽略的 Assets/Samples，因此包内另交付经过哈希校验的迁移：

```bash
python3 Packages/com.eitan.mnn/Tools~/Migration/migrate_siriai_conversation.py --stage multimodal
```

迁移保留原 GUID，重复运行是幂等操作；不同版本或部分改动会在写入前拒绝，需要先合并对应版本。

## 基于实测的默认值

`MNNRecognitionQuality` 按原始错误数/参考字词数计算 CER/WER，比较同一设备、语言、固定语料和指标的结果，
先满足 P95 延迟预算，再选错误率最低者；相同错误率选更低延迟。未验证的结果、跨语料结果不参与排名。
`MNNRecognitionDefaults` 将质量记录和完整设置快照绑定；SiriAI 的声学 owner 可在首次读取设置前注入这个目录及评测范围，
在没有已保存用户设置时采用选中的配置。已有手动开关和保存设置优先。

本轮在 Mac17,2 / Apple M5 / macOS 27.2.1 上完成中文先导实测：FLEURS 前六条人工转写目标录音，分别保留原音、模拟 10 dB 房间噪声、混入另外六条录音形成 6 dB 双人重叠。四个已安装 ASR × 原音/DTLN/GTCRN，共 216 次原生推理，执行错误为零。
SenseVoiceSmall + 原音的总体 CER 为 137/672（20.39%），单人 10/224（4.46%）、模拟房间噪声 13/224（5.80%）、模拟重叠 114/224（50.89%）；同一模型加入 DTLN 后总体为 20.68%，GTCRN 为 23.96%。
这是六条目标录音上的便利样本结果，未覆盖真实远场设备、独立多人采集或同步视觉。因此没有将本批最低值提升为完整视听链路的生产默认值；新增功能仍需按模型及使用环境分别验证。
初始设置保留已有选择，需权限/模型的新增能力默认关闭。界面明确显示未选择可比评测结果。
测试中的合成统计仅验证选择规则，不是语音质量测量。部署方应安装完整评测预设后再宣称“本设备实测最佳”。

## 对话图片上下文

`MNNVisionContext` 只保存一张有界最新图片，默认最多 1920×1080、750 ms 有效期。
`Update(image, capturedAt, trackId)` 后，每轮通过 `TryGet(trackId, now, out image)` 重新检查目标与新鲜度。
获得图片后使用已有多模态对话请求 API 显式提交；缓存不会自动录像、写盘或上传。
离开、目标切换或撤销用户授权时调用 `Clear`。图像上下文与用于语音提取的面部序列用途不同，不能互相替代。

## 质量验收边界

托管回归测试覆盖时序、断流、身份/目标变化、歧义拒绝、数据所有权和路由；Unity 测试覆盖暂停时间下的渐变及组件重启后的权限配置。
这些测试不证明真实摄像头、麦克风、MNN 原生 ABI、实时率、识别准确性或视听模型增益。
本次没有引入新模型，也没有把已有桥接导出的执行结果描述为官方 C++ 直调验证。

设备发布前，使用固定单人/双人/重叠/远场/回声/暗光/转头/遮挡/离开返回录制集，分别比较
原始音频、仅增强、参考目标提取、视听目标提取和分离后 ASR。记录 CER/WER、目标泄漏、误唤醒、错目标率、
漏唤醒、首字截断、端到端 P50/P95 延迟、显存/内存和持续运行实时率，不能只比较输出是否非零。
验收门槛由产品与设备预算指定；在未完成这些测量前，不承诺任意多人环境均可准确拾音。
模型加载、目录和部署校验仍遵循共享 registry 与现有[语音处理](SpeechProcessing.md)、[实时对话](RealtimeConversations.md)约定。
