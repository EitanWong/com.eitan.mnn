# MNN for Unity

<div align="center">

[![Unity](https://img.shields.io/badge/Unity-2021.3+-black?logo=unity)](https://unity.com)
[![Version](https://img.shields.io/github/v/release/EitanWong/com.eitan.mnn?include_prereleases)](https://github.com/EitanWong/com.eitan.mnn/releases)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE.md)

**轻量级深度学习推理引擎 Unity 封装**

基于阿里巴巴开源的 [MNN](https://github.com/alibaba/MNN) 推理框架

[安装指南](#installation) · [快速开始](#quick-start) · [文档](Packages/com.eitan.mnn)

</div>

---

## 特性

- 🚀 **高性能** - 深度优化的 C++ 推理引擎，支持硬件加速
- 📱 **全平台** - iOS, Android, macOS, Windows, Linux, WebGL
- 🎯 **轻量级** - 总大小 58 MB，比官方构建减小 79%
- ⚡ **零依赖** - 无需外部库，开箱即用
- 🔧 **易集成** - 标准 UPM 格式，一键导入

## 平台支持

| Platform | Architecture | Size | Acceleration |
|----------|-------------|------|--------------|
| iOS | arm64 | 15.7 MB | Metal, CoreML |
| Android | armeabi-v7a, arm64-v8a | 9.5 MB | OpenCL, Vulkan |
| macOS | x86_64, arm64 | 8.3 MB | Metal, CoreML |
| WebGL | WebAssembly | 6.0 MB | SIMD |
| Windows | x86_64 | 5.1 MB | CUDA, OpenCL |
| Linux | x86_64 | 4.0 MB | CUDA, OpenCL |

---

## Installation

### Unity Package Manager (推荐)

1. 打开 Unity Editor
2. 进入 **Window → Package Manager**
3. 点击 **+** → **Add package from git URL**
4. 输入: `https://github.com/EitanWong/com.eitan.mnn.git#upm`

### Via manifest.json

添加到 `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.eitan.mnn": "https://github.com/EitanWong/com.eitan.mnn.git#upm"
  }
}
```

### 系统要求

- Unity 2021.3 或更高版本
- iOS 11+, Android 5.0+ (API 21), macOS 10.13+

---

## Quick Start

```csharp
using MNN.Unity;
using UnityEngine;

public class MNNExample : MonoBehaviour
{
    void Start()
    {
        if (MNNVersion.IsLoaded())
        {
            Debug.Log($"MNN Version: {MNNVersion.GetVersion()}");
            MNNVersion.LogInfo();
        }
        else
        {
            Debug.LogError("MNN failed to load");
        }
    }
}
```

---

## 文档

- [完整文档](Packages/com.eitan.mnn/README.md) - API 参考
- [更新日志](Packages/com.eitan.mnn/CHANGELOG.md) - 版本历史
- [MNN 官方文档](https://www.mnn.zone/) - 引擎文档

---

## 开发分支说明

- `main` - 稳定的工程版本
- `dev` - 日常开发分支
- `upm` - Package 发布分支（仅包含 Package 内容）

---

## License

本项目基于 **Apache License 2.0** 授权。

MNN 引擎版权归阿里巴巴集团所有，同样采用 Apache 2.0 授权。

详见 [LICENSE.md](LICENSE.md)。

---

<div align="center">

Made with ❤️ for the Unity community

</div>
