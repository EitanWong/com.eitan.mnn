# A2A 远端 Harness

独立 Host 实现在 `Tools~/HarnessHost`；Unity 与移动应用通过包的 `RemoteTaskClient`、
`SubmitTaskRequest`、`SubmissionJournal` 和持久任务 API 使用它，无需 A2A SDK 或原生扩展。

### A2A 用户上下文复用

A2A Harness 配置可选 `ReuseContext: true`，默认 `false`。同一配置中的同一认证 `OwnerId`
复用远端返回的 opaque Context ID，每次新工作仍创建新 Task，不发送旧 Task ID。其他用户和其他
Harness 配置不共享映射；前台历史、宿主身份和路径不会作为上下文自动传给远端。

独立工作通过 `SubmitTaskRequest.IsolatedContext = true`（或 `TaskSubmission` 的
`isolatedContext: true`）绕过复用，且不改变该用户原来的 Context。这一标记参与幂等身份、
提交日志、持久任务快照及 HTTP v1 委派；同一 submission key 改标记会拒绝。

同一用户并发首次提交只等待服务端分配 Context，不等待首个 Task 完成。等待分配时取消不会
向远端发送任务；没有 Context 的响应会释放等待者。已建立的 Context 发生变化时中断并保留
原映射。缓存仅驻留 Host 内存，最多保留 100 个用户映射，运行中的用户不淘汰，可暂时超限。
Host 重启会清空该可选映射；已经持久化的 Task/Context 仍按原任务句柄恢复，绝不重提不确定的受理。
历史由远端 Agent 保留，`historyLength` 只限制响应历史。支持 1.0/0.3 的 JSONRPC、HTTP+JSON
以及原生 SSE。该行为按 Qwen 固定提交 `12f66d1` 的 A2A 会话连续性契约实现。

运行独立协议检查（需已构建的 Host DLL）：

```sh
SIRIAI_PROTOCOL_CHECK_DLL=HOST_DLL SIRIAI_PROTOCOL_CHECK_OUTPUT=OUTPUT_DIRECTORY \
python3 Packages/com.eitan.mnn/Tools~/HarnessChecks/check_a2a_contexts.py
```

检查使用独立标准库 HTTP/SSE 服务端，验证协议行为；不能作为真实厂商、模型推理或手机后台的验收。
