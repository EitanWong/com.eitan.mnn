# 前台与后台 Agent 的完整交互

任务执行、准入日志、回报投递、追问和续办在包内实现。应用只负责绑定会话、用户输入和展示端口。
现有公开命名空间 `MNN.Samples.SiriAI.Harnesses` 保持兼容；这些 API 位于 UPM package，独立 Host
与非 Unity 消费者均可使用，不依赖 SiriAI 的场景或 UI。

## 应用绑定

```csharp
var frontend = new HarnessFrontendSession();
frontend.BindConversation(chatId); // 草稿传 null；提交首条输入前应先持久提交会话目录。
frontend.BindInputSnapshot(inputId, exactUserText, recentWholeMessages, inputAttachments);
var submit = new HarnessTaskTool(client,
    (request, token) => journal.SubmitAsync(request, client.SubmitAsync, token), frontend,
    HarnessTaskToolOperation.Submit, harnessId, workspaceAlias, network: true);
```

`inputId` 由应用为一次用户输入生成，重试保留同一值，下一次明确请求必须换值。应用应在创建工具前绑定
已提交会话；工具每次调用读取当前作用域。同一输入的上下文/附件不可更改；同一准入身份更换目标会被日志
或 Host 拒绝，不会创建第二项工作。不要按目标文字全局去重。

`BindInputSnapshot` 保留完整当前输入（最多65536字符）为 `foreground-input.txt` 文本附件；
最近历史按完整消息保留，最多4000字符，省略早期历史时有明确标记。应用应澄清未包含的必要要求。
显式附件与输入快照共用32项/256 KiB预算，超限抛出异常。输入附件只发给已连接且支持 Attachments 的后端；ACP 对端还必须声明 embeddedContext，
缺少该能力时任务明确失败且不发送原生 prompt。
`BindInput` 允许应用改用自己的已确认上下文；`CreateSubmission` 的组合目标上限仍为32768字符。
规划器工具输入预算8192字符，JSON转义有独立上限；本地规划预算1024 token并沿用配置的超时，
超限或未产生合法计划会澄清，不截断路径或伪造受理。

SiriAI 将目录 ActiveId、每条用户输入和最近文字历史绑定到该 API。Tasks 页从草稿手动提交时也会先
提交聊天目录。切换会话只撤销前台生产和投递租约，后台工作继续；A→B→A 产生新作用域，旧请求不能进入新代际。
执行记录导入和回报领取均按 `DeliveryConversationId`，兼容协调器独立执行会话。

## 回报与追问

应用实现 `IHarnessResultPresenter`，可选实现 `IHarnessRequestPresenter`，并启动
`HarnessResultFeedback.RunAsync`。连接委托应返回 `new HarnessResultConnection(client, frontend.Scope)`；
草稿、挂起或未连接时返回 null。停止时取消并等待循环结束，再切换模型上下文。

只有实际文字显示、播放开始、播放完成和中断证据可以写对应回执。生成完成或 PCM 入队不代表用户听到。
单条回报失败会持久记录 `DeliveryAttempts`、`NextDeliveryUtcTicks`、`DeliveryError`，按1、2、4…60秒
退避，8次后停止自动重试。其他结果仍能领取。`RetryDeliveryAsync` / `RetryResultDelivery` 显式重置预算，
不会修改任务执行终态或虚构显示。SiriAI 在任务详情提供重试入口。

追问也等待实际展示证据，并按 TaskId + RequestId 去重；展示失败会让出投递机会。
`respond_agent_task` 输入为 `{"TaskId":"TASK","RequestId":"REQUEST","Text":"用户回答","Allow":true}`。
权限必须显式提供 Allow，false 表示拒绝；普通输入不需要 Allow。先读取并核对当前任务、请求与前台会话，
过期请求或切换代际后的响应被拒绝。机密字段与外部 URL 授权转到任务详情，不在语音中索要凭据。

## 完成后续办

`continue_agent_task` 输入为 `{"TaskId":"TASK","Text":"后续工作目标"}`。
HTTP使用 `POST /v1/tasks/{id}/continue`，提交 `ContinuedFromTaskId` 与新的稳定 SubmissionKey；
`RemoteTaskClient.SubmitAsync` 自动选择该路由，旧 Host 返回404，避免忽略新字段而误建普通任务。
Host 创建关联的新任务，原任务的终态、结果、回执和执行记录不改写。
只接受 Completed 源任务以及相同 owner、harness、workspace、执行/投递会话和隔离选项。

能力标志 `HarnessCapabilities.Continue` 与 `IHarnessContinuationAdapter` 表示后端能验证原生会话。
ACP 与 ACP 协调器保存原生 Session 检查点，受理时和执行时均检查；配置变化、会话失效、
不确定操作或不可恢复的进程退出会拒绝/中断续办，不创建替代原生会话。Host 重启后依赖实际 ACP
load/resume 能力；旧版本协调器记录若没有原生检查点，明确拒绝续办。其他协议未声明该能力时不支持。
运行中调整仍用 update，待回答请求用 respond，Paused 工作恢复用 resume，不能互相代替。

SiriAI 新建脚手架和随附场景注册8个工具并启用本地 ModelPlanner，不因此开启云权限。
重建已有脚手架保留用户的路由和后端选择。Host 与前台应一起升级，以获得新增的失败/重试路由和续办字段。

## 验证范围

核心行为检查、独立 Host/ACP 对端、原生 Session 身份及跨重启检查可确定性复现协议行为。
这些对端是测试进程；不代表真实厂商 Agent 完成任务、部署模型路由质量或设备扬声器验证。
真实发布仍需部署模型与登录后的真实 Agent 生成可校验产物，再验证原会话文字/声音及回执。
本次没有添加 C++、原生桥或官方 MNN 源码修改；这些托管链路测试也不证明官方 MNN C++ 直接调用。
