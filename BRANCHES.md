# 分支管理指南

## 分支结构

本项目维护三个主要分支，各有不同的用途：

### main（主分支）
- **用途**: 稳定的工程版本
- **内容**: 完整的Unity项目，包含Package、示例、测试等
- **保护**: 需要通过PR从dev分支合并
- **适用对象**: 需要查看完整项目结构或参与开发的贡献者

### dev（开发分支）
- **用途**: 日常开发工作
- **内容**: 与main相同，但包含最新的开发中功能
- **工作流程**: 
  1. 在dev分支进行日常开发
  2. 开发完成后推送到GitHub
  3. 功能稳定后创建PR合并到main分支
- **适用对象**: 项目维护者和核心贡献者

### upm（Unity Package分支）
- **用途**: 最终发布的Package版本
- **内容**: 仅包含Package内容（无工程文件）
- **安装方式**: 
  ```
  https://github.com/EitanWong/com.eitan.mnn.git#upm
  ```
- **适用对象**: 终端用户，通过Unity Package Manager直接导入

## 开发工作流

### 1. 日常开发（dev分支）

```bash
# 切换到dev分支
git checkout dev

# 进行开发工作
# ... 修改代码 ...

# 提交更改
git add .
git commit -m "feat: 添加新功能"

# 推送到远程
git push origin dev
```

### 2. 合并到主分支（dev → main）

当dev分支的功能稳定后：

```bash
# 切换到main分支
git checkout main

# 合并dev分支（建议使用PR）
# 或者直接合并（小改动）
git merge dev

# 推送到远程
git push origin main
```

### 3. 更新upm发布分支（main → upm）

当main分支准备发布新版本时：

```bash
# 切换到upm分支
git checkout upm

# 从main分支复制Package内容
git checkout main -- Packages/com.eitan.mnn

# 移动内容到根目录
mv Packages/com.eitan.mnn/* .
rm -rf Packages

# 提交更改
git add .
git commit -m "release: 更新到版本 x.x.x"

# 推送到远程
git push origin upm
```

## 注意事项

1. **不要直接在main分支开发** - 所有开发工作应该在dev分支进行
2. **upm分支是独立的** - 它使用orphan分支，不共享main/dev的提交历史
3. **版本发布流程** - dev → main（稳定后）→ upm（发布前）
4. **远程master分支已废弃** - 请使用main作为主分支

## 分支同步

### 首次推送所有分支

```bash
# 推送main分支
git push -u origin main

# 推送dev分支
git push -u origin dev

# 推送upm分支
git push -u origin upm

# 删除远程废弃的master分支
git push origin --delete master
```

### 保持分支更新

```bash
# 获取远程更新
git fetch origin

# 更新本地分支
git checkout main
git pull origin main

git checkout dev
git pull origin dev

git checkout upm
git pull origin upm
```

## 版本发布检查清单

发布新版本前，确保：

- [ ] dev分支所有功能已测试通过
- [ ] 已合并到main分支
- [ ] 更新了`package.json`中的版本号
- [ ] 更新了`CHANGELOG.md`
- [ ] upm分支已更新Package内容
- [ ] 创建了GitHub Release和Tag
- [ ] 在Release中说明主要变更

## 常见问题

### Q: 为什么需要三个分支？
A: 
- **main**: 保持稳定的工程版本供参考和贡献
- **dev**: 隔离开发工作，避免不稳定代码影响main
- **upm**: 提供纯净的Package内容，方便用户通过Package Manager安装

### Q: 用户应该使用哪个分支？
A: 普通用户应该使用`upm`分支，它只包含Package内容，最轻量。

### Q: 如何贡献代码？
A: 
1. Fork本仓库
2. 从dev分支创建feature分支
3. 完成开发并测试
4. 提交PR到dev分支
