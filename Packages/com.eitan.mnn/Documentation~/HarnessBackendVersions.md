# Harness 后端版本检查

通用版本策略属于包内 `Tools~/HarnessHost`；Unity 和手机只调用远端 Host 的就绪接口。
显式 `ProbeReadinessAsync` 先运行宿主上已解析的 CLI 的 `--version`，兼容后再做原有协议检查。
`GetReadinessAsync`、安装扫描和连接设置只读取目录或缓存，不启动 CLI、不安装或升级工具。

最低版本来自 Qwen Audio Agent 固定提交 `12f66d18be807a59c16e8004f573609f9454dac8`
的后端 catalog；Host 的 `BackendProfiles` 是启动、发现、环境和最低版本的共同来源。

| 后端 | 最低版本 |
| --- | --- |
| OpenCode | 1.18.0 |
| Qwen Code | 0.21.6 |
| MiniMax Code | 0.3.7 |
| Kimi Code | 0.31.0 |
| DeepSeek Harness | 0.1.5 |
| Pi | 0.80.4 |

仅为这六种有上游最低要求的后端执行版本探测；其他后端保留既有协议检查。
Pi 查询原生 `NativeCommand`，不会把 ACP adapter 的版本当成 Pi 版本。
版本从首行提取 major.minor.patch 的非负整数三元组；允许前缀和 prerelease 后缀，
按数字比较，保留规范化三元组，不将任意 CLI 输出发送给前台。

`HarnessInstallation.MinimumVersion` 是静态目录元数据。`HarnessReadiness.BackendVersion`
包含 `Version`、`Minimum` 和 `State`：`compatible`、`too_old` 或 `unconfirmed`。
过旧时就绪返回 `unavailable/backend_version_too_old`；不可确认时返回
`unavailable/backend_version_unconfirmed`，不进行协议握手。兼容仅允许继续协议检查，
仍可能协议失败。旧 Host 不提供版本字段时，C# 消费者继续接受既有就绪结果。
独立读取版本 DTO 也会拒绝非法数值及与数值矛盾的状态。

每次版本子进程限时5秒，stdout和stderr各限4KiB，要求零退出和有效UTF-8。
版本探测与后台进程共用显式环境作用域，不转发Host令牌或无关provider凭据。
失败尝试清理进程树；独立测试覆盖等待子进程和持续输出两种情况。
Windows批处理使用固定 `--version` 参数并拒绝会触发shell展开的路径；Windows实机执行尚未验收。

SiriAI Tasks 设置页显式检查后显示已观察版本和最低版本；过旧提示在Host升级，未确认提示检查Host CLI。
检查记录按认证owner、harness和workspace缓存；切换前台Host/目标后清除旧证据。
此检查不会执行用户任务，也不会自动升级或证明账号认证、模型推理、设备后台生命周期或所有厂商兼容。
