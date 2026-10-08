# MNN for Unity

[![Version](https://img.shields.io/badge/version-3.6.1-blue.svg)](https://github.com/EitanWong/com.eitan.mnn/releases)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-green.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/license-Apache--2.0-orange.svg)](LICENSE.md)

轻量级深度学习端侧推理引擎：专为资源受限的设备设计，能在 Unity 构建出来的程序上高效运行神经网络模型。

> 基于阿里巴巴开源的 [MNN](https://github.com/alibaba/MNN) v3.6.1

## ✨ 特性

- 🚀 **高性能**: 深度优化的 C++ 推理引擎
- 📱 **跨平台**: 支持 iOS, Android, macOS, Windows, Linux, WebGL
- 🎯 **轻量级**: 总大小仅 ~60 MB，体积优化 79%
- 🔧 **易集成**: 开箱即用的 Unity Package
- ⚡ **硬件加速**: Metal (iOS/macOS), OpenCL (Android), CUDA (Windows/Linux)
- 🧠 **模型支持**: CNN / Transformer / LLM / Diffusion

## 📦 平台支持

| 平台 | 架构 | 大小 | 状态 |
|------|------|------|------|
| **iOS** | arm64 (device + simulator) | 15.7 MB | ✅ XCFramework |
| **Android** | armeabi-v7a + arm64-v8a | 9.5 MB | ✅ |
| **macOS** | x86_64 + arm64 | 8.3 MB | ✅ Universal |
| **WebGL** | WebAssembly | 6.0 MB | ✅ |
| **Windows** | x86_64 | 5.1 MB | ✅ |
| **Linux** | x86_64 | 4.0 MB | ✅ |

**总计**: 48.6 MB (所有平台原生库)

## 🚀 安装

### 方式 1: 通过 Git URL (推荐)

在 Unity Editor 中:
1. 打开 **Window → Package Manager**
2. 点击 **+ → Add package from git URL...**
3. 输入: `https://github.com/EitanWong/com.eitan.mnn.git`

### 方式 2: 手动安装

1. 下载最新 release
2. 解压到项目的 `Packages/` 目录

### 方式 3: 修改 manifest.json

编辑 `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.eitan.mnn": "https://github.com/EitanWong/com.eitan.mnn.git"
  }
}
```

## 📖 快速开始

```csharp
using System.Runtime.InteropServices;

public class MNNExample : MonoBehaviour
{
    // 导入 MNN 原生接口
    #if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
    #elif UNITY_ANDROID && !UNITY_EDITOR
        [DllImport("MNN")]
    #else
        [DllImport("MNN")]
    #endif
    private static extern System.IntPtr MNN_GetVersion();
    
    void Start()
    {
        // 获取 MNN 版本
        var versionPtr = MNN_GetVersion();
        string version = Marshal.PtrToStringAnsi(versionPtr);
        Debug.Log($"MNN Version: {version}");
    }
}
```

## 🔧 构建配置

### iOS
- 自动包含 XCFramework
- 支持真机 + 模拟器
- Metal 硬件加速

### Android
- 自动包含 armeabi-v7a 和 arm64-v8a
- OpenCL GPU 加速
- 最低 API Level: 21

### WebGL
- 静态库链接
- WebAssembly 优化
- 单线程模式

### Windows/Linux/macOS
- 动态库加载
- 支持 AVX/AVX2/AVX512 (x86_64)
- Metal 加速 (macOS)

## 📊 性能对比

与官方构建对比:

| 项目 | 我们的构建 | 官方构建 | 节省 |
|------|-----------|---------|------|
| 平台覆盖 | **7/7** (100%) | 2/7 (29%) | +250% |
| Linux | **4 MB** | 55 MB | -93% |
| Android | **9.5 MB** | 195 MB | -95% |
| Windows | **5.1 MB** | 12.4 MB | -59% |
| iOS/WebGL | **唯一可用** | 不可用 | - |

## 🎯 优化说明

- ✅ **Strip 优化**: 移除调试符号 (节省 50+ MB)
- ✅ **仅推理**: 禁用训练/benchmark (节省 35+ MB)
- ✅ **轻量构建**: 禁用非必需功能
- ✅ **全平台**: iOS/Android/WebGL 官方无法提供

## 📚 文档

- [MNN 官方文档](https://www.mnn.zone/)
- [模型转换指南](https://www.mnn.zone/m/0.2/)
- [API 参考](https://github.com/alibaba/MNN/tree/master/doc)

## 🛠️ 支持的模型

- **分类**: ResNet, MobileNet, EfficientNet, SqueezeNet
- **检测**: YOLO, SSD, RetinaNet
- **分割**: U-Net, DeepLab
- **LLM**: Qwen, LLAMA, Baichuan, DeepSeek
- **Diffusion**: Stable Diffusion

## ⚠️ 限制

- 仅支持模型**推理** (不支持训练)
- 不包含 Python 绑定
- 不包含 Benchmark 工具

## 🤝 贡献

欢迎提交 Issue 和 Pull Request!

## 📄 许可证

- **MNN 引擎**: Apache-2.0 License (阿里巴巴)
- **Unity Package**: Apache-2.0 License

## 🙏 致谢

- [MNN](https://github.com/alibaba/MNN) - 阿里巴巴开源的深度学习推理引擎
- 所有 MNN 贡献者

## 📧 联系

- GitHub: [@EitanWong](https://github.com/EitanWong)
- Issues: [GitHub Issues](https://github.com/EitanWong/com.eitan.mnn/issues)

---

**从 243 MB 优化到 60 MB | 完整 7 平台支持 | 生产就绪** ✨
