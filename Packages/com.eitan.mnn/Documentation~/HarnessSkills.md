# Host 后台技能管理

后台技能由实际Agent读取和执行。C# Host调用固定的社区 `skills@1.5.22` 安装器；
不将技能加载为语音前台提示词，也不在手机启动CLI。
策略对齐Qwen Audio Agent提交 `12f66d18be807a59c16e8004f573609f9454dac8` 的skill-library。

## 配置

Host配置默认不启用管理。管理员可添加：

```json
"Skills": {
  "Command": "npx",
  "AllowedOwners": ["alice"],
  "WorkingDirectory": "./workspace",
  "TimeoutMilliseconds": 600000
}
```

`AllowedOwners`必须明确列出现有身份，具备修改同一Host用户级技能的权限。
可配置 `Package`、`Arguments` 和显式 `ForwardEnvironment`；请求正文不能覆盖这些配置。
未指定Package时读取 `QWEN_AUDIO_AGENT_SKILLS_CLI_PACKAGE`，再回退固定版本。
Windows不经cmd解释来源URL；常规npm安装可解析node+npx-cli.js，特殊shim/prefix可显式配置
`Command=node.exe`及 `Arguments=[绝对npx-cli.js路径]`。Windows实机尚未验收。

## 远端契约

- `GET /v1/skills/catalog`：被动读取权限、固定包及目标映射。
- `POST /v1/skills/operations`：持久受理显式操作，立即返回202及 `HarnessTask`。
- `GET /v1/skills/operations`、`GET /v1/skills/operations/{id}`：读取本owner的操作及结果。
- `POST /v1/skills/operations/{id}/cancel`：明确取消本地安装器进程，等待观察到终态。

请求包含 `SubmissionKey`、`Operation`，以及按操作适用的 `Source`、`Names`、`Backends`。
操作为 `source-list`、`install`、`list`、`remove`、`update`。
先列出源中的技能，再为install提供至少一个确切名称；不会默认安装整个仓库。
source-list映射 `add SOURCE --list`；install映射
`add SOURCE --skill NAME ... -g --copy -y -a INSTALLER ...`；
其余管理命令保持 `list -g`、`remove NAME -g -y`、`update -g`。
来源为单个argv，支持仓库、URL和宿主本地目录；拒绝选项伪装及控制字符。

| 后端 | 安装器目标 |
| --- | --- |
| Qwen | qwen-code |
| OpenCode | opencode |
| OpenClaw | openclaw |
| Qoder | qoder |
| Kimi | kimi-code-cli |
| Hermes | hermes-agent |
| CodeBuddy | codebuddy |
| Codex | codex |
| Claude | claude-code |
| Pi | pi |

唯一映射来源为包内BackendProfiles。未明确Backends时选择已安装与已配置后端的并集；
没有受支持目标时回退十种目标。受理时保存确切安装器目标，排队期间新增后端不会改变已受理意图。MiniMax与DeepSeek没有上游skills.sh专用映射，不写其私有目录。

## 持久性和前台

管理操作复用包的PersistentTaskRuntime、FileTaskStore、取消及恢复语义，
使用独立持久目录 `DataDirectory/skills-library` 和单并发队列。
前台断开或挂起不取消已受理操作。明确取消只能确认安装器进程已停止，不能回滚已完成的文件变化。
异常输出、非零退出或超时保持失败；每个输出流限16KiB，超时最大10分钟，失败会尝试清理进程树。
Host硬崩溃后不重放不确定的运行中操作；独立子进程可能继续，需要核对实际安装结果。
排队操作执行前重新核对当前授权和受理时的安装器配置，避免在权限或配置变化后执行旧意图。

`RemoteTaskClient`提供类型化接口；`HarnessSkillClient`复用独立SubmissionJournal，
丢失受理响应后以原SubmissionKey重放到管理路由，不重放到用户任务路由。
SiriAI Tasks页提供五种显式操作、历史输出与取消；目录/历史刷新不执行安装器。
切换连接范围时丢弃旧来源草稿及结果；输出不自动注入对话或进行语音播报。
手机使用相同远端接口，无本地CLI依赖。

当前验收包括独立真实子进程参数/持久性/权限测试、C#提交日志测试、实际Unity界面、
以及官方固定版本经C# Host枚举本地技能源。全局安装/移除的文件变化由独立安装器夹具验证；
尚未声称在用户真实全局目录安装成功。Qwen的启动时lockfile补齐同步仍为后续工作。
