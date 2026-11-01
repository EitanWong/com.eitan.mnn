# Candy 风格迁移

Studio 的 **Style transfer** 页面接受照片，执行固定 Candy 风格，预览并保存 PNG。
输出固定为 224 × 224；不需要文本提示。Runtime 共用 `MNNImageTask` 及
`MNNImageTask.Style.cs` partial，没有移动端平行实现。

## 准备本地模型

使用已安装的、未修改的官方 MNN 3.6.1 `MNNConvert`：

```bash
python3 Packages/com.eitan.mnn/Tools~/Models/prepare_image_profile.py \
  --converter /absolute/path/to/MNNConvert --download
```

`--download` 显式下载约 6.7 MB 的固定 ONNX；已有该文件时省略。模型位于
`Assets/StreamingAssets/MNN/Models/DefTruth--FastStyleTransfer-Candy/`。
在 Studio 选择此本地文件夹。当前没有发行者托管的已转换 MNN 下载入口。
转换工具检查源文件和转换结果的 SHA-256；只规范化官方公开 schema 的随机
`Net.mnn_uuid`，保持算子、权重、名称及维度不变。诊断留在 `TestArtifacts~/`。

源 ONNX 镜像为 `DefTruth/lite.ai.toolkit` 的
`f3fa44f82a867df0a3d08a2dd2c7eed88f2b0d92/examples/hub/onnx/cv/style-candy-8.onnx`，
SHA-256 `e3c6f442e12014c5fec12b4307c14ca303df185cda19f67522afeef40c4d22b8`。
转换图 6,734,176 字节，SHA-256
`70c1069827b8df36212313e915abf727a046baf8b4fb95efb8eac612ee66047a`。

## Runtime

```csharp
using (var model = MNNImageTask.Load(directory))
{
    MNNImageTaskResult result = model.Process(rgbImage);
    MNNGeneratedImage styled = result.Image;
}
```

输入采用抗锯齿 RGB 双线性缩放，`input1 [1,3,224,224]` 为原始 0–255 float32。
`output1` 同形状，逐通道裁剪到 0–255 后舍入至字节。输出坐标原点位于左上。
支持取消和重复执行；正在执行的原生调用结束后才观察取消。

该图的 instance normalization 对 FP16 误差敏感，因此共用 Interpreter 管线为此
profile 使用 `High` 精度。macOS Metal Normal 相对独立 ONNX 的最大误差 2.153、
平均误差 0.098，未通过正确性门槛；High 最大误差约 0.0019、平均约 0.00011。

## 验证范围

2026-10-25：macOS ARM64 Mono 的桌面回归共 76 项通过、无失败或跳过，包含实际
Candy 权重 CPU/Auto、共享生成管线、Studio 页面及文档版面回归。
OPPO PELM00 Android ARM64 IL2CPP 的 115 项通过、无失败或跳过，包含 Candy
CPU/Auto 两项和基础平台/数值推理检查。Android Auto 实际为 CPU。

独立参考来自 ONNX Runtime 1.31.0 和 Pillow，覆盖一张 1080 × 1080 的真实照片。
Android 预处理与参考逐元素一致；原始输出最大误差约 0.155、平均约 0.0041；
最终通道最多相差一个灰度值，超过 99.6% 完全一致。测试同时限制原始最大误差
小于 0.25、平均小于 0.01，以及字节最大误差不超过 1、平均小于 0.01。
这是一张图片的跨运行时正确性证据，不是风格质量数据集评价。

Android CPU 加载约 286 ms、首轮 603 ms、重复 587 ms；进程峰值 RSS 约 413 MB。
macOS Auto/Metal 首轮约 41 ms、重复 40 ms。时间受设备温度、电源和系统活动影响；
RSS 含 Unity、MNN 和映射文件，不是模型独占内存。完整测量保存在验证报告中。

iOS ARM64 / Xcode 27.1 原生编译、链接及开发签名校验通过；iPhone 当前不在线，
**该模型的 iPhone 实际权重推理尚未验证**。macOS x86_64 Mono Player 在 Apple Silicon / Rosetta 上的合并回归也已通过117项，无跳过；包含两类模型 CPU/Auto（实际 Metal），尚未在物理 Intel Mac 上执行。
其他模型、分辨率、风格和 GPU 后端不在此 profile 的验证范围内。

可复现入口：

- `Tools~/Testing/generate_image_reference.py`：已有固定 JPEG/ONNX，生成独立参考；需 Pillow、numpy、onnxruntime，不下载。
- `StyleTransferIntegrationTests`：Editor 原始张量、图片及 Studio 实际 backend。
- `StyleTransferTests`：布局、非有限输出、裁剪、取消、配置和独立页面交互。
- `StyleTransferPlayerTests`：实际 Player 图、图片、取消、释放、时间和内存。
- `run_platform_tests.py --style-root <existing-root>`：必须通过两项 Player 测试才写入 `candy_style_verified=true`。

移动测试前，将图、`mnn_image_task.json`、`source.rgb`、`reference-input.f32` 和
`reference-output.f32` 放入应用 `persistentDataPath/style-transfer/`。测试校验独立
参考的固定 SHA-256，拒绝缺失或变化的数据。未提供模型时明确跳过，不能据此宣称通过。

测量在独立原始张量检查之后执行，文件缓存和部分内核可能已预热；“首轮”指新建
托管任务实例的首次 Process，不代表操作系统冷缓存启动。

最新合并验证记录：`TestArtifacts~/TaskIntegration/ImageTasks/validation-report.md` 和 `validation-summary.json`（开发仓库本地产物）。
