# 模型打包与运行时准备

通过 **Edit > Project Settings > MNN** 管理模型发布。默认不把
`Assets/StreamingAssets/MNN/Models/` 中的模型加入 Player。规则自动作用于
Build、Build And Run 和 `BuildPipeline.BuildPlayer`，不需要更换构建入口。

## 设置和职责

设置保存在 `ProjectSettings/MNNSettings.asset`，建议提交版本控制。首次未保存时也使用
“不携带模型”的默认值。Editor 设置由 `Editor/Settings` 管理；筛选、构建快照和恢复由
`Editor/ModelPackaging` 管理；Player 清单和准备 API 属于 `Runtime/Core/Models`。
后续项目设置按职责扩展这些分组，运行时不依赖 UnityEditor 或直接读取 ProjectSettings。

| 设置 | 行为 |
| --- | --- |
| None / 不携带 | 默认排除模型目录，构建过程中不会下载模型 |
| Selected / 指定 | 勾选目录，或在相对路径列表中指定文件、子目录 |
| All / 全部 | 加入全部有效模型文件 |
| Automatic 存储 | 可读随包文件直接加载；Android APK 等 URL 路径先提取到持久目录 |
| PersistentData 存储 | 随包文件也先复制到持久目录 |
| Allow runtime downloads | 按需下载清单声明的缺失文件；有效缓存仍可离线使用 |
| Remote file manifest | 可选 JSON TextAsset，固定远程依赖、下载地址、字节数和 SHA-256 |

路径相对 `MNN/Models`，例如 `Qwen3/llm.mnn`。目录选择递归携带配置、词表、权重和其他依赖；
单文件选择不会推断依赖。重叠规则取并集，不存在的选中路径会阻止构建。
`.meta`、隐藏缓存、`~` 目录、`.mnn-parts`、`.part` 和 `.tmp` 文件不打包；不遍历符号链接。
预览显示未经压缩的模型字节数，不含 Unity、原生插件和其他资源。

仅过滤 `StreamingAssets/MNN/Models`。其他 StreamingAssets 子目录、Resources、Addressables 和
场景资源遵循各自的构建规则。旧 `MNNModelConfig.includeInBuild` 字段不作为项目打包规则。

## 不携带模型与混合发布

创建 JSON 文件并在设置页指定为 Remote file manifest：

```json
{
  "schemaVersion": 1,
  "files": [
    {
      "path": "MyModel/model.mnn",
      "url": "https://your-model-host.example/models/v1/model.mnn",
      "bytes": 123456,
      "sha256": "REPLACE_WITH_THE_ACTUAL_64_HEX_SHA256"
    }
  ]
}
```

替换示例地址和校验值，为配置、词表、外置权重等每个必要文件分别添加记录。
可用 `shasum -a 256 文件路径` 获取校验值。构建只读取清单，不请求远程服务；
未在本地下载的模型也可以列入。若同一文件同时随包携带，本地大小和校验值必须与远程记录一致。
运行时使用构建固定的快照；更新远程清单需要重新构建 Player。

None 模式仅携带交付清单。Selected 模式合并选中文件与远程记录：运行时先复用校验通过的
持久缓存，再读取或复制随包文件，其余文件使用时下载，不会在启动时下载所有模型。

## 运行时 API

在 Unity 主线程调用，完成后再调用现有推理 API。多文件模型优先准备目录：

```csharp
var delivery = await MNNModelDelivery.LoadAsync(cancellationToken);
string directory = await delivery.PrepareDirectoryAsync("MyModel", token: cancellationToken);
// 将 directory 传给对应模型的 Load API。
```

`PrepareDirectoryAsync` 将该目录在清单中声明的全部文件准备到
`Application.persistentDataPath/MNN/Models/MyModel`，使本地与远程依赖处于同一可写目录。
它只处理声明的文件，不推断依赖。`PrepareFileAsync("MyModel/model.mnn")` 按存储策略返回
可供原生文件 API 使用的路径。进度回调可能来自工作线程，更新 Unity UI 时应切回主线程。

`MNNModelConfig.PrepareAsync` 支持 `MNN/Models` 下的 StreamingAssets/PersistentData 单文件配置。
`MNNModelManager.LoadModelAsync` 先准备文件再创建解释器；同步 `LoadModel` 仍只加载已存在文件。
`MNNModelLoader` 在 Player 默认启用 `useProjectDeliverySettings`；Editor 保持本地工作流。
已有的 `downloadUrl` 不会自动转换为完整可校验的远程清单，需要配置交付清单。
关闭加载器的 `useProjectDeliverySettings` 可保留旧下载流程，该流程不受项目交付设置控制。
自定义加载器（包括有独立安装流程的示例）需要显式接入上述 API。

Android StreamingAssets 通常是 `jar:file://...!/assets`，不能交给 `File.Exists` 或原生模型
加载器。清单提供文件列表，无需枚举 APK 目录；提取使用 UnityWebRequest 与 DownloadHandlerFile，
避免整份权重读入托管内存。iOS 可读随包文件可直接使用；需要可写配置或缓存的模型应准备目录，
或选择 PersistentData 策略。

复制和下载验证字节数与 SHA-256，先写临时文件，成功后发布到正式路径；不匹配的缓存重新准备。
下载复用现有的分段、断点续传和校验实现。取消或失败不会将部分文件发布为有效模型。
当前不自动删除设备上的旧模型，避免误删业务仍在使用的缓存。

## 构建管线与兼容性

最低版本与包一致：**Unity 2021.3**。使用公开的 `BuildPlayerProcessor`、
`BuildPlayerContext.AddAdditionalPathToStreamingAssets` 和构建回调；不依赖 Build Profiles、
私有字段或 APK/Xcode 输出目录修改。后续 Unity 版本使用相同公开接口，实际 Player 构建仍需
在所用版本复测。

Unity 没有通用的现有 StreamingAssets 排除入口，因此构建前将 Models 原目录及其根 `.meta`
迁移到同一项目卷的 `TestArtifacts~/MNNBuildStaging`，通过公开 API 从暂存目录加入选中文件。
不复制整份模型缓存，不删除 AssetDatabase 资源，不修改原生代码；结束后恢复原目录与 GUID。
构建期间不要下载模型、刷新模型资源或更改 Models 目录。其他自定义构建脚本若读取源目录，
需在 MNN 暂存前完成读取。

成功构建通过 postprocess 恢复；取消或其他回调失败通过 Editor update 恢复；异常退出后，
下次打开项目根据磁盘事务恢复。失败后立即在同一同步调用中重试构建会被拒绝，需先等待恢复。
若源目录被重新创建，恢复保留两份内容并报错，避免覆盖。设置页提供“恢复模型目录”；
出现恢复错误时不要删除暂存目录。

Player 清单位于 `StreamingAssets/MNN/model-delivery.json`。该路径由包保留，项目内存在同名文件
会阻止构建。清单只含相对路径与运行时配置，不含 Editor 绝对路径。

模型发布与原生推理支持是独立条件。当前直接 C++ ABI 绑定仅实现 macOS 64 位 Mono；
Android/iOS/IL2CPP 推理绑定尚未实现。打包、下载或复制成功不能作为设备推理通过的证据。
