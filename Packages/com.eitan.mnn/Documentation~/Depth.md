# Depth Anything V2 Small 深度估计

Studio 的 **Depth estimation** 独立页面支持图片输入、相对逆深度预览和原始 PFM 导出。
亮色表示更近。输出没有米等物理单位，不能把不同图片的数值直接作为距离比较。
这里只验证固定 Small 导出，不能推广为其他 Depth Anything 模型或真实场景的深度精度评测。

## 模型准备

```bash
python3 Packages/com.eitan.mnn/Tools~/Models/prepare_image_profile.py \
  --profile depth --converter /absolute/path/to/MNNConvert --download
```

使用已有、未修改的官方 MNN 3.6.1 转换器；需要已有的 Python numpy、onnx。
脚本不安装依赖。省略 `--download` 时使用已存在的原始 ONNX。权重位于
`Assets/StreamingAssets/MNN/Models/DepthAnything--V2-Small/`，在 Studio 选择此本地目录。
不把未准备的任意同名仓库标记为可推理。

来源为 `onnx-community/depth-anything-v2-small`，revision
`4472b7362082ad9968fee890ca0f1e5aca36b93d`，Apache-2.0。
原始 ONNX SHA-256：`afb6a5c28f3b6bf1618c6e43f02073ef9dfdc70e937502d51603e57b0a1df10c`。
准备后的 MNN 为 99,030,556 字节，SHA-256：
`a91629bc3c318f129ba0461384a8a5f9a58e6c998f8e01a6121ec8bb30b5f9fa`，加载前校验。

MNN 3.6.1 的 CPU 双三次插值在负半像素边界使用截断整数，造成动态长宽比结果偏差。
准备工具在 ONNX 图中把位置编码插值等价展开为标准 Gather／算术：
half_pixel、a=-0.75、floor 和边界索引夹取，仍支持动态长宽，不修改原始权重。
展开后的 ONNX 与原始 ONNX 在真实图片上对照，再用官方转换器转换；
保留精确 GELU，关闭 MatMul-to-Conv。仅规范化最终公开 Net.mnn_uuid 元数据以得到可重复哈希。
没有新增 C++、自定义导出、桥接库或官方源码修改。

## 共享 Runtime

```csharp
using (var model = MNNImageTask.Load(directory))
{
    var result = model.Process(rgbImage);
    MNNImageDepthResult depth = result.Depth;
    float[] grid = depth.RelativeInverseDepth; // GridHeight × GridWidth，行优先
    float center = depth.SampleAt(depth.Width / 2, depth.Height / 2);
    // result.Image 是源图尺寸的灰度预览；不改变原始 grid。
}
```

实现复用 `MNNImageTask.Depth.cs` partial 和已有 Interpreter／Session／Tensor 绑定，
跨平台共用，没有 Mobile 平行 API。原生调用受当前限定编译器／平台 ABI 契约约束，
不是 MNN 官方提供的可移植 C# 接口，详情见 [移动端绑定](MobileInterop.md)。
同实例串行执行，支持 ProcessAsync、取消和释放；取消不能打断正在运行的单次原生图。

**CPU / High** 是当前允许的后端，Auto 选择 CPU；显式 GPU／NN 请求直接拒绝。
Metal 虽可执行，但独立参考误差未通过，不启用。首个 QKV MatMul 的偏差可通过
官方 MatMul-to-Conv 转换消除，但完整模型仍未通过，候选图仅留作诊断。
其他加速后端尚未验证此图。

输入 pixel_values 为 float32 `[1,3,H,W]` RGB。DPT minimal 缩放选择与 1 最接近的
518/宽或518/高，保持比例，将各轴舍入到 14 的倍数（中点取偶数）。
Pillow 风格抗锯齿双三次先水平、再垂直，每步 uint8；随后乘 1/255，按
mean `[.485,.456,.406]`、std `[.229,.224,.225]` 归一化为 CHW。
例如 810×1080→518×686，1920×1080→924×518，没有方形拉伸或填充。
最多 16,777,216 个源像素，缩放后最多 3072 个 14×14 patch，以限制全局 attention 的内存。
极端长宽比或取整后某轴不足 14 时拒绝，调用者应先裁剪。

输出 predicted_depth 为 float32 `[1,H,W]` 非负相对逆深度。原始网格不做归一化，
返回数组为防御副本。SampleAt 接受源图整数像素中心，以 half_pixel 双线性映射到模型网格。
预览按该图原始最小／最大值线性映射为灰度；平坦网格预览为黑。
PFM 导出保留原始网格和数值：灰度 Pf、底行优先、little-endian float32、scale=-1.0。

## 验证

独立参考使用原始 ONNX、ONNX Runtime CPU 和 Pillow：

```bash
python3 Packages/com.eitan.mnn/Tools~/Testing/generate_depth_reference.py \
  --image street=/path/to/bus.jpg --image square=/path/to/style.jpg
```

工具不下载资源，输出限定在项目 TestArtifacts~/。
当前 fixture 为 810×1080 街景与 1080×1080 图片，两种几何；
预处理须与独立输入最大差小于 1e-6，输出最大误差／参考跨度小于 .001、
平均误差／跨度小于 .0001。门槛在比较前确定，保留早期失败的转换／GPU 记录。
这是转换与执行一致性检查，不是深度数据集准确率或 metric depth 校准。

`DepthTests` 覆盖几何、attention 限制、非法值／类型／形状、坐标采样、预览、PFM 行序、
取消和独立页面请求／实际绘制。`DepthIntegrationTests` 对照真实权重，覆盖 Runtime 和
Studio 原生 backend。`DepthPlayerTests` 两输入各 CPU/Auto，检查预处理、原始浮点结果、
重复执行、取消／释放，记录加载／首次／复用耗时和整个 Unity 进程的 RSS／峰值。
设备前已生成预处理输入，文件缓存／内核可能预热，不称为冷启动或受控性能基准。

Player 目录 persistentDataPath/depth/ 包含 depth-anything-v2-small.mnn、mnn_image_task.json，
以及 street/、square/ 内 source.rgb、input.f32、output.f32。桌面可以
`run_platform_tests.py --depth-root <root>` 指定同样布局；四项都通过才报告
`depth_anything_v2_verified=true`，缺权重跳过不计验证。

本轮 Editor 相关回归 74 项通过。iPhone 17 ARM64 IL2CPP、macOS ARM64 Mono、
macOS x64 Mono（Rosetta）Player 各 117 项通过、无失败或跳过，均含四项真实深度对照。
iPhone 加载约 .56–.61 秒，首次／复用约 1.52–2.22 秒，最大误差／跨度 .000308；
进程峰值 RSS 约 880 MB，包含 Unity、原生图和缓存，非模型独占内存。
Android OPPO PELM00 ARM64 IL2CPP 合并深度／Laya 回归 119 项通过、无失败或跳过，
包含四项真实深度对照。深度首次／复用约 5.29–7.69 秒，峰值 RSS 约 1.05 GB；
这不包含后续 Laya 的 3.07 GB 进程峰值。
物理 Intel Mac、Windows、Linux 和此图的加速后端尚未验证。
本地完整证据：TestArtifacts~/TaskIntegration/Depth/validation-report.md。
