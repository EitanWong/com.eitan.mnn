# com.eitan.mnn

[![Version](https://img.shields.io/badge/version-3.6.1-blue.svg)](https://github.com/EitanWong/com.eitan.mnn/releases)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-green.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/license-Apache--2.0-orange.svg)](LICENSE.md)

**MNN for Unity** - 轻量级深度学习端侧推理引擎 Unity Package

> 基于阿里巴巴开源的 [MNN](https://github.com/alibaba/MNN) v3.6.1

专为资源受限的设备设计，能在 Unity 构建出来的程序上高效运行神经网络模型。

## 🎯 核心优势

- 🚀 **高性能**: 深度优化的 C++ 推理引擎
- 📱 **全平台**: iOS, Android, macOS, Windows, Linux, WebGL
- 🎯 **轻量级**: 仅 ~60 MB，体积优化 79%
- ⚡ **硬件加速**: Metal/OpenCL/CUDA
- 🔧 **易集成**: 开箱即用

## 📦 平台支持 (7/7)

| 平台 | 大小 | 状态 |
|------|------|------|
| iOS | 15.7 MB | ✅ XCFramework |
| Android | 9.5 MB | ✅ v7a + v8a |
| macOS | 8.3 MB | ✅ Universal |
| WebGL | 6.0 MB | ✅ WASM |
| Windows | 5.1 MB | ✅ x64 |
| Linux | 4.0 MB | ✅ x64 |

## 🚀 快速开始

### 安装

在 Unity Package Manager 中:
```
Add package from git URL: https://github.com/EitanWong/com.eitan.mnn.git
```

### 使用

```csharp
using MNN.Unity;

// 检查 MNN 版本
string version = MNNVersion.GetVersion();
Debug.Log($"MNN Version: {version}");

// 检查是否正确加载
if (MNNVersion.IsLoaded())
{
    Debug.Log("MNN loaded successfully!");
    MNNVersion.LogInfo();
}
```

## 📊 性能对比

与官方构建对比:

- **平台覆盖**: 7/7 vs 2/7 (+250%)
- **Linux**: 4 MB vs 55 MB (-93%)
- **Android**: 9.5 MB vs 195 MB (-95%)
- **总体**: 60 MB vs 243 MB (-75%)

## 📂 仓库结构

```
com.eitan.mnn/
├── Packages/com.eitan.mnn/          # Unity Package
│   ├── Runtime/
│   │   ├── Plugins/                 # 原生库 (48.6 MB)
│   │   │   ├── iOS/                 # 15.7 MB
│   │   │   ├── Android/             # 9.5 MB
│   │   │   ├── macOS/               # 8.3 MB
│   │   │   ├── WebGL/               # 6.0 MB
│   │   │   ├── Windows/             # 5.1 MB
│   │   │   └── Linux/               # 4.0 MB
│   │   └── Scripts/
│   │       └── MNNVersion.cs        # C# API
│   ├── Samples~/
│   │   └── BasicExample/            # 基础示例
│   ├── Documentation~/              # 文档
│   ├── package.json                 # Package 定义
│   ├── README.md                    # Package 说明
│   ├── CHANGELOG.md                 # 更新日志
│   └── LICENSE.md                   # 许可证
├── Assets/                          # Unity 项目资源
├── ProjectSettings/                 # Unity 项目设置
└── README.md                        # 本文件
```

## 🛠️ 支持的模型

- **分类**: ResNet, MobileNet, EfficientNet
- **检测**: YOLO, SSD, RetinaNet
- **分割**: U-Net, DeepLab
- **LLM**: Qwen, LLAMA, Baichuan
- **Diffusion**: Stable Diffusion

## 📚 文档

- [Package README](Packages/com.eitan.mnn/README.md) - 完整使用文档
- [CHANGELOG](Packages/com.eitan.mnn/CHANGELOG.md) - 版本更新日志
- [MNN 官方文档](https://www.mnn.zone/)

## 🤝 贡献

欢迎提交 Issue 和 Pull Request!

## 📄 许可证

Apache-2.0 License - 详见 [LICENSE.md](LICENSE.md)

## 🙏 致谢

- [MNN](https://github.com/alibaba/MNN) - 阿里巴巴开源深度学习推理引擎

---

**从 243 MB 优化到 60 MB | 完整 7 平台支持 | 生产就绪** ✨
