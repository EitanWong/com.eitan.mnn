# Harness 后端接入

SiriAI 的“任务”设置首先选择“本机”或“远程宿主”；移动端只显示远程接入。
组件默认每 15 秒只读检测本机 CLI 和适配器文件，无需先配置宿主或 .NET。
本机默认扫描已安装代理，优先保留已安装的原选择；CLI 和必需适配器都存在后才参与自动选择。
扫描不会安装、登录、启动代理或发起模型推理。远程通过认证发现后端和工作区，使用下拉框选择，
不要求用户填写内部 Harness ID。地址、凭据或配置改变后，必须重新发现，旧响应不能覆盖新选择。

## 安装与配置

| 后端 | 传输 | 安装来源 |
| --- | --- | --- |
| Codex | ACP 外部适配器 | `@openai/codex` + `@agentclientprotocol/codex-acp` |
| Claude Code | ACP 外部适配器 | `@anthropic-ai/claude-code` + `@agentclientprotocol/claude-agent-acp` |
| DeepSeek Harness | 原生 ACP | `@deepseek-ai/dsh` |
| Pi | ACP 外部适配器 | `@earendil-works/pi-coding-agent` + `pi-acp` |
| Hermes | 原生 ACP | 官方 `hermes-agent.nousresearch.com` 安装脚本 |
| OpenClaw | 内置 ACP 桥接 | `openclaw`；需要已启动的网关 |
| Qwen Code | 原生 ACP | `@qwen-code/qwen-code` |
| OpenCode | 原生 ACP | `opencode-ai` |
| Qoder | 原生 ACP | `@qoder-ai/qodercli` |
| MiniMax Code | 原生 ACP | `@minimax-ai/code` |
| Kimi Code | 原生 ACP | `@moonshot-ai/kimi-code` |
| CodeBuddy | 原生 ACP | `@tencent-ai/codebuddy-code` |
| Muse Code | MSP | `dev.meta.ai/install.sh`；宿主直接实现 MSP，SDK 为可选项 |
| Bare LLM | Chat Completions | 内置，实验性，仅文本；填写兼容 API 地址、模型和密钥 |

点击安装才执行安装器，仅安装缺失组件；不会因缺少登录状态而重装或更新已有 CLI。
包名与当前参考实现保持一致，显式安装使用 `@latest`。npm 后端需要用户已安装 Node.js/npm。
Hermes Windows 使用官方 PowerShell 脚本；Muse 的 Windows 自动脚本尚未提供，界面错误会引导使用官方安装方式。
账户、模型、Bailian 等提供商配置继续由各代理的原生登录/配置工具管理；“配置指南”打开官方文档。
Pi 原生工具没有权限审批机制，选择时明确提示。安装、登录配置、协议检查及真实任务推理是不同状态。
参考 [qwen-audio-agent](https://github.com/QwenAudio/qwen-audio-agent) 的选择和引导方式，不沿用其星级作为本项目的验证结论。

## 发布与组件使用

本地宿主需要 .NET 8（ASP.NET Core 运行时），以及发布好的 `SiriAI.HarnessHost.dll` 和同目录依赖。
将 `dotnet publish Tools~/HarnessHost/SiriAI.HarnessHost.csproj` 的完整输出部署至应用的
`StreamingAssets/HarnessHost/`。发布时通过 `HarnessArtifactsRoot` 把输出定向到项目 `TestArtifacts~/`。
自动检测覆盖 PATH、DOTNET_ROOT、常见 .NET 安装位置和上述宿主位置；未发现时可在高级设置中选择已有路径。
进程安装与服务控制只用于桌面平台。移动端通过 HTTPS 访问已部署的宿主。

```csharp
using MNN.Unity;
using MNN.Samples.SiriAI.Harnesses;

var setup = gameObject.AddComponent<MNNHarnessBackendSetup>();
setup.Configure(); // 也可显式传入 dotnet、宿主程序集和私有服务目录
var installed = await setup.ScanAsync();
string backend = HarnessBackendCatalog.ChooseLocal(installed, preferred: "codex");
// 若未安装，让用户选择后再显式调用 await setup.InstallAsync("codex")。
if (backend != null)
{
    var local = await setup.PrepareAsync(backend, projectDirectory);
    await setup.StartHostAsync(); // 等待经过认证的宿主就绪
    string token = setup.ReadAccessToken(local); // 只传给客户端，勿记录日志
    using (var client = new RemoteTaskClient(new System.Uri(local.Address), token))
    {
        var catalog = await client.GetCatalogAsync(System.Threading.CancellationToken.None);
        // 使用 local.Backend / local.Workspace 调用已有任务客户端。
    }
}
```

消费者 asmdef 引用 `MNN.Unity.Harnesses.Components` 和 `MNN.Samples.SiriAI.Harnesses`。
非 Unity 开发者直接使用 `HarnessBackendCatalog`、`HarnessLocalDiscovery`、`DesktopHostServiceClient` 和 `RemoteTaskClient`。
原有 Harness 程序集/命名空间出于兼容性保留，实际实现归 MNN package 所有。

每个“代理＋项目绝对路径”独立注册服务、任务存储和凭据；端口自动选择，重试保留原端口与随机令牌。
SiriAI 退出不会停止独立宿主。通过 `ScopeId` 和 `ReadSavedAccessToken` 恢复本机访问凭据，
密钥不进入 PlayerPrefs、场景或 CLI 参数；只保存于权限受限的服务环境。需要更新运行中的配置或密钥时，
先使用 `DesktopHostServiceClient.ExecuteAsync("stop")` 显式停止对应注册目录，再准备并启动。
不能将一个共享组件同时用于多项准备/安装操作；它拒绝重叠执行。

## 验证范围

`Tools~/HarnessChecks/check_backend_setup.py` 使用真实 .NET 宿主和独立替身 CLI/npm，
验证缺失适配器安装、幂等、私有配置、令牌/端口复用与无效输入拒绝，不安装第三方代理。
`check_backend_profiles.py` 检查传输参数、提供商环境和权限行为；`check_readiness.py`
验证独立 ACP/MSP/HTTP/A2A 对端与有界检查。Editor 测试覆盖选择、设置清理和包目录行为。
这些检查不证明每个第三方账号、版本、操作系统或真实模型任务已经互通；应在目标机器登录后显式检查并执行真实任务。

## 设置更新与宿主配置

后台任务轮询只有收到新任务或更高revision才通知界面；本机安装检测和技能历史
未变化时不通知。普通后台更新只更新任务列表和详情，不重建连接、配置名称或任务
草稿控件；选择器打开、输入聚焦或正在拖动时延后刷新。真正切换宿主/账户仍清空
上一作用域的任务草稿，避免把旧请求提交到新宿主。

远程选择器只显示用户配置，自动产生的 `local-desktop` / `local-desktop-*` 记录
留作本机连接恢复，不出现在远程配置列表；用户自行命名的loopback配置仍可使用。
可新增配置、修改名称和连接信息并保存。仅重命名不会重连或清空任务；更换后端/
工作区后重新连接，保留同地址凭据。删除需要界面确认，移除配置和对应地址的凭据，
若正在使用则断开该客户端；宿主上已经接收的任务及结果不取消、不删除。
共享 `HarnessProfileCatalog.Remove(id)` 以原子替换持久化删除，重复删除是无操作；
删除当前选择后不自动选择另一台宿主。

远程可直接点击“连接并保存宿主”，无需先点击发现；按已认证宿主提供的目录
自动选择后端和工作区，保留有效的已有选择，失效选项重新匹配；不把仅检测到安装
但未配置的代理作为可连接后端。配置名称可留空，连接/保存时使用主机名，仍可自定义。
“发现远程后端”保留为需要提前选择的入口；离线保存/编辑配置不会主动连接。
共享 `HarnessHostCatalog.ChooseBackend/ChooseWorkspace` 负责此决策；只有配置的文本
补全与代理同时存在时默认优先代理，但保留用户此前明确选择的文本补全后端。
共享 `HarnessDesktopPaths.FindWorkspace` 按候选顺序只选择存在的绝对目录，不创建
目录。本机页面依次使用已保存的本地项目、当前会话目录、应用项目；已选择的目录
不会被后台监测重置。检测是只读操作，安装、启动或远程连接均由用户操作触发。
