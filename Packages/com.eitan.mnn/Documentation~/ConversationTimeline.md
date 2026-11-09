# 会话与执行时间线

这些 API 属于 `MNN.Unity` 托管层，可用于 Editor 与 Player；不增加原生桥接、C ABI
或官方 MNN C++ 源码。测试本页能力不等于验证原生模型推理。

## 分层与使用

- `MNNConversationCatalog` 管理会话目录、标题、草稿、归档、当前选择及一个活动日志写入器。
  `Create` / `Select` 切换日志；切换模型前，调用方先停止并等待旧响应。
  `MNNConversationViewState` 持久化当前页、可见文字锚点、像素偏移、跟随模式和展开项，
  每个会话独立恢复；不使用整个长会话的滚动百分比替代文字位置。
- `MNNConversationTranscriptStore` 保存显示历史，包括普通消息、计划、公开推理摘要、工具调用、
  工具结果、错误与产物记录。`Id` 标识事件，`ParentId` 关联轮次或调用，`State` 是该事件发生时的状态。
  历史里的 Running 记录表示开始或进行中这一历史事实，应结合后续 Completed / Failed / Cancelled
  记录判断结果；不会从记录推断远程任务此刻仍在运行。
- `MNNConversationMemory.Restore(source)` 先读取有界的最近文字消息，再替换模型记忆；
  计划、工具记录和其它显示数据不进入模型提示词，也不占消息数窗口。保留的数据受消息数和字符预算约束。
  恢复失败时应用保持发送禁用；成功之后才标记就绪。
- `MNNAgentProvider.TryReadTrace` 提供线程安全、有界的公开执行记录。调用者定期在历史的所有者线程
  读取并持久化；事件包括选择的动作、工具输入/输出、调用关系与成功、失败、取消。
  不要求或提取模型隐藏思维链。外部提供者如公开摘要，可使用 `ReasoningSummary` 类型。
- `MNNConversationTimelinePresentation.Bind(source)` 及 `Toggle(index)` 管理显示详情；
  `MNNMarkdownHistoryLayout` 进行切片、缓存、视口查询和测量；`MNNConversationRowLayout`
  提供右对齐用户气泡、全宽助手正文和执行行的共用几何参数。宿主绑定 UI toolkit 的控件、颜色与字体。

```csharp
using (var catalog = new MNNConversationCatalog(conversationDirectory))
{
    catalog.History.Add(MNNChatRole.User, "检查设置");
    catalog.History.Add(new MNNConversationTranscriptEntry(
        MNNChatRole.System, "{\"path\":\"settings.json\"}",
        MNNConversationEntryKind.ToolCall, "call-1", "turn-1", "读取设置",
        MNNConversationEntryState.Running));
    catalog.Flush();
    var presentation = new MNNConversationTimelinePresentation();
    presentation.Bind(catalog.History);
    presentation.Toggle(1);
    // 将 presentation 交给 MNNMarkdownHistoryLayout，并绑定其可见切片。
}
```

## 日志、恢复与性能

原有 v1 文字记录保持可读；新的日志使用 v2。旧日志首次写入结构化记录前会升级文件头，
旧版读取器会明确拒绝该版本，避免把新记录误当损坏后缀并截断。新记录保存在同一校验日志中。

持久化文件旁的 `.idx` 是可删除、可重建的性能缓存，包含块偏移、条数、日志长度和写入时间，
自身使用 SHA-256 校验。匹配时仅解码最多 64 条尾部消息；历史块按需读取并验证记录校验和。
文件大小或时间改变、索引丢失、损坏或尾部不合法时，回退到日志扫描。恢复截断不完整的后缀，
保留此前有效记录；损坏的文件头明确报错。缓存不防御刻意保留文件大小和时间的外部篡改，
日志块仍在访问时校验。不要在写入器运行时修改日志或保留旧文件时间来替换日志。

日志只有一个线程所有者和一个写入器。`Flush()` 刷新到设备并更新索引；普通写入和 UI 绘制
不是持久化承诺。SiriAI 将流式显示快照保留到检查点，避免每次布局重建都追加完整回复。
文件容量、文字长度、常驻读取块和渲染缓存都有上限。

`ImportExecution(sourceId, sequence, entry)` 接收外部单调事件序列，事件 Id 必须是
`sourceId + ":" + sequence`。目录游标在日志刷新之后写入；游标丢失时从日志身份恢复，
重连、重复任务快照和目录恢复不会重复导入已经存在的事件。仅在缺少游标时可能扫描历史。

SiriAI Host 将任务活动、ACP 计划、工具调用和更新持久化到任务记录；工具更新保留公开的
协议内容作为文字详情。Host 历史最多 128 步、16 KiB 标题/详情字符预算，过量会记录明确的
历史上限提示，最终结果仍单独保留；单步详情最多 4096 字符。MNN 原生 Agent 的未读缓冲
最多 128 条，溢出由错误记录报告。执行详情的 UI 预览最多 16000 字符，原始会话记录保留完整内容。

此实现提供同类对话结构、工具展开、Markdown 与视口虚拟化能力，没有对 Codex 界面做逐像素一致性认证。
