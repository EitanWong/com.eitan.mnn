# 分支与安装

日常开发提交推送到 `dev`，通过 `dev → main` Pull Request 审核合并。
创建 PR 不等于合并或发布；不要直接推送 `main`，也不要强制推送覆盖远端工作。
远端分支有新增提交时，先审查并整合，再推送。

仓库根目录是 Unity 开发工程，UPM 包位于 `Packages/com.eitan.mnn`。
安装当前开发版本必须指定包的子目录：

```text
https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#dev
```

已合并版本将结尾改为 `#main`；可复现依赖使用已存在的提交 SHA。
历史 `upm` 分支不保证包含当前开发实现。`main` 的发布行为由现有 GitHub Actions
工作流及其凭据决定，不应仅凭包版本号假定存在对应发布标签。

源码保存、忽略规则、提交前检查与目录职责见
[仓库维护与提交](Packages/com.eitan.mnn/Documentation~/RepositoryManagement.md)。
