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
尚未声称在用户真实全局目录安装成功。启动时lockfile同步见下文。

## 后端启动前同步

管理员可在已有Skills配置内添加：

```json
"Startup": { "OwnerId": "alice" }
```

这是独立的明确启用项；OwnerId必须属于AllowedOwners。只同步本Host配置的、具有安装器映射的本地ACP后端，
不会因为发现本机其他CLI或刷新目录而安装。手机仍只连接远端Host。
默认检查运行Host账户的主目录 `.agents/.skill-lock.json`，以其中skills的source/sourceUrl为来源，
检查目标后端目录的`名称/SKILL.md`。Codex、OpenCode、Kimi共享`.agents/skills`；其他目录对齐固定Qwen实现。
按来源分组，只安装缺失名称，超过64个名称分批，保持现有固定版本、字面argv、全局copy和明确后端目标。
没有lock、已经齐全或没有安装器映射时不执行进程。损坏/超过1MiB的lock会记录错误，继续启动Host。
无来源、无效选项或包含路径分隔符的名称跳过并记录；不会把lock里的名称解释成路径或命令。

顺序为：取得主任务存储租约 → 恢复并处理完技能管理队列 → 读取lock并同步 → 创建主任务运行时恢复队列 → 开始监听。
因此恢复中的remove先改变lock，启动同步不会把它重新安装；恢复的普通后台任务也会在首次扫描前看到已补齐文件。
同步复用`skills-library`单写入者队列，不开另一个安装通道。每个来源保存独立持久操作，失败不妨碍后续来源或Host启动，
日志含来源、名称、任务ID及清理过期lock的提示。退出0但没有预期SKILL.md的安装器仍记失败。
每批受理时绑定owner、检查根目录、lock哈希和已有安装器指纹；执行前重新核对，已存在文件不再调用安装器。
上一个安装器更新lock元数据后，下一来源重新读取哈希；不会因旧哈希把余下来源误判成成功。

可选`Startup.HomeDirectory`仅指定上述检查根目录，默认是Host账户主目录。它不改变任何系统/子进程HOME环境，
也不会为官方skills或后端重定向安装位置；管理员必须确保检查根目录与安装器、后端实际读取的用户目录一致。
若使用自定义安装器/受控配置目录，可显式配置该目录；实际文件检查会暴露配置不一致，不能仅凭进程退出0声称同步成功。

启动同步期间Ctrl+C或POSIX SIGTERM会停止本地安装器并等待清理，退出130，主任务不启动；这不等于撤销已发生的文件修改。
硬崩溃仍遵守原有不确定性规则：旧Running操作不按原任务重放，独立子进程可能继续。
明确启用启动同步意味着以后启动仍按当时lock/缺失文件创建新的补齐操作；跨不同DataDirectory的Host、外部CLI及硬崩溃遗留进程
不受本Host队列的单写入者保证，部署时需避免并发管理同一账户目录，并核对不确定操作。
实际全球安装、Windows服务和所有厂商推理均需各自验收；本同步行为的独立子进程测试不代表这些验收通过。
