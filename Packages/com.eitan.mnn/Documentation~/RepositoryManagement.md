# 仓库维护与提交

仓库根目录是 Unity 开发工程；分发包位于 `Packages/com.eitan.mnn`。
目录职责和程序集边界见 [项目结构](ProjectStructure.md)。

## 应提交的内容

- Runtime、Editor、测试、确定性的小型 fixture、示例、文档与维护工具。
- Unity 资产对应的 `.meta`，以及包的 `package.json`、asmdef、导入配置。
- 开发工程所需的 Assets、ProjectSettings 与 `Packages/manifest.json`。
- `Runtime/Plugins` 中用于分发的原生库及完整 framework/xcframework 插件。
  插件二进制是必要包资产；其他平台的历史插件不代表当前托管 ABI 支持。
- `.editorconfig`、`.gitattributes`、`.gitignore` 和 CI 配置。

当前唯一提交的模型 fixture 是 `Tests/Fixtures/affine.mnn`，用于确定性数值测试。
大模型与下载仓库不随源码提交，新增 fixture 需保持小型、确定性，并说明用途。

## 本地保留、避免上传

`.gitignore` 排除 Unity 的 Library/Temp/Logs/UserSettings、IDE 工程和缓存、Python 缓存、
CMake 中间产物、模型权重、下载临时文件、密钥及环境文件。
`Assets/StreamingAssets` 的模型和随目录产生的 meta 均保留在本机；应用构建前须自行准备。
不要通过忽略所有 `.dll`、`.so`、`.dylib` 或 `.meta` 来排除产物，否则会遗漏必要的插件与资产。

所有本地备份、测试 XML/日志、生成 PNG/WAV、GPU 缓存、隔离 Unity 工程和构建结果
统一放在仓库根目录的 `TestArtifacts~/`。该目录整体忽略，不能将其复制到分发包。
Studio 的用户会话和媒体存储在本地生成目录，具体位置见 [Chat Studio](ChatStudio.md)。
测试文档引用的 `TestArtifacts~/` 是维护者本地证据位置，Git 克隆不会包含这些文件；
可按 [测试说明](Testing.md) 使用已有模型复现。不要把旧桥接 API 的结果当作直接 C++ 调用验证。

## 保存与审查

修改前可在 `TestArtifacts~/Checkpoints/` 保存源码归档、当前提交 SHA、工作区补丁与文件状态。
被忽略的模型/缓存继续留在原处。源码归档是本地恢复点，Git 提交记录可分发源码。

提交前从仓库根目录运行：

```bash
python3 Packages/com.eitan.mnn/Tools~/Validation/validate_package.py
git diff --check
git status --short
git diff --cached --stat
git diff --cached --name-only
```

结构检查覆盖布局、程序集、meta/GUID 和文档链接，不证明编译或推理成功。
按改动范围执行必要的 C# 编译、Unity 和原生推理验证，明确区分实际运行与历史测试记录。
暂存时检查新增文件、模型大小与凭据；`.gitignore` 不会自动停止跟踪已提交文件。
需要取消跟踪时使用针对明确文件的 `git rm --cached`，保留本地文件并同时处理 Unity meta。

## 分支与安装

`dev` 保存开发提交，通过 `dev → main` PR 审核合并；推送与创建 PR 不等于合并或发布。
不要强制推送覆盖远端提交。远端有新增提交时，先审查并整合。

UPM 安装开发版本使用：

```text
https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#dev
```

已合并版本改为 `#main`，可复现依赖使用已有 SHA。仓库根目录不是 UPM 包，不能省略 `?path=`。
历史 `upm` 分支不保证包含 `dev` 的实现；版本号 `3.6.1` 也不证明同名发布标签存在。
`main` 的现有工作流会触发 Unity 验证及 UPM release，发布行为以工作流配置和所需凭据为准。
