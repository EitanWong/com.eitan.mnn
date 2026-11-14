# 非阻塞任务控制

`PersistentTaskRuntime` 是任务状态的唯一写入者。前台退出、断线或停止轮询不会取消已受理任务。
新任务立即返回受理回执；完成通过原有持久结果 outbox 回传，不在提交调用内等待推理完成。

| 前台工具 | 输入 | 行为 |
| --- | --- | --- |
| `spawn_thinking` | 包含上下文和完成条件的完整目标 | 返回真实宿主受理 ID；不等任务完成。 |
| `get_agent_task_status` | `active` / 空 / 精确任务 ID | 全部未结束任务 / 全部记录 / 单项详情。无最近10项截断。 |
| `pause_agent_task` | 精确任务 ID | 仅暂停排队工作，持久状态为 `Paused`。 |
| `resume_agent_task` | 精确任务 ID | 原任务恢复排队，同一调度锁决定何时开始。 |
| `cancel_agent_task` | 精确任务 ID | 立即返回 `Cancelling` 或已确认终态；不等待外部后端结束。 |
| `update_agent_task` | 有稳定 RequestId 的指令 JSON | 调整已存在任务，不以新任务代替更新。 |

`Paused` 追加到枚举末尾（数值7），旧状态和四项旧工具操作的数值保持不变。
暂停与继续都在提交存储成功后生效；暂停任务不占执行槽位，重启后保持暂停。
已结束任务不能继续，暂停任务可直接取消；运行中任务没有可移植的挂起契约，返回明确不支持。
不使用取消后重提来模拟暂停，避免重复外部副作用。

HTTP 新增 `POST /v1/tasks/{id}/pause`、`/resume` 与 `/cancel-request`，沿用 token 身份和 owner 隔离。
原 `/cancel` 继续等待确认，保持已有客户端契约。`RemoteTaskClient.RequestCancelAsync`、
`PersistentTaskRuntime.RequestCancel` 和模型工具使用受理接口；`CancelAsync` 仍可显式等待确认。
取消错误被观察并写入活动/执行记录，不把未确认取消报告成成功，不允许迟到结果覆盖终态。
新前台控制工具需要匹配版本 Host；旧 Host 缺少路由时应显示实际错误。

独立验证（输出路径放在项目 `TestArtifacts~/`）：

```sh
PYTHONDONTWRITEBYTECODE=1 python3 Tools~/HarnessChecks/run_checks.py --output OUTPUT
PYTHONDONTWRITEBYTECODE=1 python3 Tools~/HarnessChecks/check_task_controls.py --output OUTPUT
```

第二项实际构建并启动独立 .NET8 Host，使用外部 ACP 测试进程和生产 C# 工具检查13项查询、
暂停/取消/owner边界、跨重启保留、同一任务继续和真实文字展示回执。
夹具不代表真实厂商推理、天气搜索能力、移动设备后台运行或实体语音播报验证。
