# MNN for Unity - 依赖和模块说明

## 📦 必需依赖

### 核心依赖（必须）
无硬性依赖，可以独立工作。

### 可选依赖（推荐）

#### 1. Unity.Burst（性能优化）
```
Package Manager → Add package by name → com.unity.burst
```
**作用**: 
- 自动启用LLVM编译优化
- 7-10x性能提升
- 无需修改代码

**不安装的影响**:
- 使用优化的托管实现
- 性能降低但仍可用（2-3x vs naive）

#### 2. Unity.Collections（高性能数据）
```
Package Manager → Add package by name → com.unity.collections
```
**作用**:
- 零拷贝数据访问
- NativeArray支持
- 与Burst配合使用

**不安装的影响**:
- 使用托管数组
- 有GC压力

#### 3. UnityEngine.UnityWebRequestModule（模型下载）
```
Window → Package Manager → Built-in → Unity Web Request → Enable
```
**作用**:
- 支持远程下载模型
- 支持热更新
- 从StreamingAssets复制（Android）

**不安装的影响**:
- 无法下载模型
- 只能使用本地模型
- StreamingAssets需手动复制

---

## 🔧 如何启用UnityWebRequest

### 方法1: Package Manager（推荐）
1. 打开`Window → Package Manager`
2. 左上角选择`Unity Registry`或`Built-in`
3. 找到`Unity Web Request`
4. 点击`Enable`或`Install`

### 方法2: manifest.json
编辑`Packages/manifest.json`，添加：
```json
{
  "dependencies": {
    "com.unity.modules.unitywebrequest": "1.0.0"
  }
}
```

### 方法3: asmdef引用
在`MNN.Unity.asmdef`中添加：
```json
{
  "references": [
    "UnityEngine.UnityWebRequestModule"
  ]
}
```

---

## ✅ 推荐配置

### 最小配置（基础功能）
```
MNN Unity Package
└── 无额外依赖
```
**功能**:
- ✅ 本地模型加载
- ✅ 基础推理
- ⚠️ 性能一般（托管实现）
- ❌ 无法下载模型

### 推荐配置（最佳体验）
```
MNN Unity Package
├── Unity.Burst
├── Unity.Collections
└── UnityEngine.UnityWebRequestModule
```
**功能**:
- ✅ 本地模型加载
- ✅ 模型下载和热更新
- ✅ 高性能推理（Burst优化）
- ✅ 零拷贝数据访问
- ✅ 所有功能可用

---

## 🎯 不同场景的依赖选择

### 场景1: 快速原型（离线模型）
```
需要: 无
可选: Unity.Burst（推荐）
```

### 场景2: 生产应用（固定模型）
```
需要: Unity.Burst + Unity.Collections
可选: UnityWebRequest（如需热更新）
```

### 场景3: 动态更新（云端模型）
```
需要: 全部（Burst + Collections + WebRequest）
```

### 场景4: WebGL部署
```
需要: UnityWebRequest（从服务器加载）
不支持: Burst（WebGL不支持）
```

---

## 🔍 故障排查

### 错误: UnityWebRequest does not exist

**原因**: UnityWebRequestModule未启用

**解决方案**:
1. 启用UnityWebRequestModule（见上方）
2. 或者禁用热更新功能：
   ```csharp
   modelConfig.enableHotUpdate = false;
   ```

### 错误: Burst编译失败

**原因**: Unity版本不支持或未安装Burst

**解决方案**:
1. 安装Unity.Burst package
2. 或者忽略（自动使用托管实现）

### 警告: Collections not found

**原因**: Unity.Collections未安装

**解决方案**:
1. 安装Unity.Collections
2. 或者忽略（使用托管数组）

---

## 📊 依赖对比

| 依赖 | 大小 | 安装难度 | 性能提升 | 必需性 |
|------|------|----------|----------|--------|
| Unity.Burst | ~5MB | 简单 | 7-10x | 可选 |
| Unity.Collections | ~1MB | 简单 | 2-3x | 可选 |
| UnityWebRequest | 内置 | 简单 | N/A | 可选 |

---

## 🎁 设计理念

> **"零依赖可用，有依赖更好"**

- 不安装任何package也能基本工作
- 安装推荐package获得最佳体验
- 开发者可以根据需求选择

---

## 📝 版本兼容性

| Unity版本 | Burst | Collections | WebRequest |
|-----------|-------|-------------|------------|
| 2021.3+   | ✅    | ✅          | ✅         |
| 2020.3+   | ✅    | ✅          | ✅         |
| 2019.4+   | ✅    | ✅          | ✅         |
| 2018.4+   | ⚠️    | ⚠️          | ✅         |
| 5.x       | ❌    | ❌          | ⚠️         |

✅ 完全支持  
⚠️ 部分支持  
❌ 不支持  

---

**建议**: 使用Unity 2021.3 LTS或更高版本以获得最佳体验。
