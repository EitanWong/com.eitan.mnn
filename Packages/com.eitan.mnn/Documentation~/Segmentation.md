# DeepLabV3 语义分割

Studio 的 **Segmentation** 独立页面选择照片、执行分割，显示分类颜色、原图像素
占比及可保存的 PNG 掩码。支持 Pascal VOC 的 20 个物体类别和 background；
它不区分同类物体的不同实例。模型输入为 257 × 257，小物体和细边界可能丢失。

## 准备

```bash
python3 Packages/com.eitan.mnn/Tools~/Models/prepare_image_profile.py \
  --profile deeplab --converter /absolute/path/to/MNNConvert --download
```

需要已有、未修改的官方 MNN 3.6.1 转换器。`--download` 显式下载 TensorFlow
官方 `deeplabv3_257_mv_gpu.tflite`，约 2.8 MB；已有文件时省略。
结果位于 `Assets/StreamingAssets/MNN/Models/TensorFlow--DeepLabV3-MobileNetV2/`，
在 Studio 选择这个本地文件夹。共用工具只规范化公开 schema 的随机 `Net.mnn_uuid`，
不修改算子或权重。诊断及依赖均留在 `TestArtifacts~/`。

源文件为官方 MNN `resource/model/Portrait` 示例指定的
`https://storage.googleapis.com/download.tensorflow.org/models/tflite/gpu/deeplabv3_257_mv_gpu.tflite`。
SHA-256 `68a539782c2c6a72f8aac3724600124a85ed977162b44e84cbae5db717c933c6`。
转换图为 2,769,072 字节，SHA-256
`b510c32e45d689ebc7b8a0bf5070a89dafb77411c350a564f1e5b13f0038b640`。
两次独立转换已确认规范化后的图逐字节一致；Runtime 拒绝其他图。

## Runtime 和坐标

```csharp
using (var model = MNNImageTask.Load(directory))
{
    MNNImageTaskResult result = model.Process(rgbImage);
    MNNImageSegmentationResult mask = result.Segmentation;
    int classId = mask.ClassAt(x, y);
    string label = mask.Labels[classId];
    float confidence = mask.ConfidenceAt(x, y);
    MNNGeneratedImage coloredMask = result.Image;
}
```

跨平台共用 `MNNImageTask.Segmentation.cs` partial。RGB 使用抗锯齿双线性缩放至
257 × 257，再以 `(pixel - 127.5) / 127.5` 归一化。
`sub_7 [1,257,257,3]` 和 `ResizeBilinear_3 [1,257,257,21]` 均为 NHWC float32。
为使参考比较覆盖同一精度，profile 使用共用 Interpreter 的 High 精度。

每个网格点取最大 logit 的类别；相等时取最低类别编号。softmax 使用减去最大值的
稳定计算，输出是模型概率，未经应用场景校准。类别和置信度以最近邻像素中心映射
还原到原图尺寸，坐标原点位于左上。掩码和 `result.Image` 都与原图同尺寸；
最多支持 16,777,216 个源像素。不是高分辨率模型的原生预测。

`ClassIds`、`Confidence`、`Labels`、`ClassPixelCounts` 返回独立数组。
`ClassAt` / `ConfidenceAt` 可避免读取单个像素时复制整个数组，越界会抛出异常。
PNG 使用 Pascal VOC 标准调色板；页面图例复用同一调色板计算。

类别顺序：background、aeroplane、bicycle、bird、boat、bottle、bus、car、cat、
chair、cow、diningtable、dog、horse、motorbike、person、pottedplant、sheep、sofa、
train、tvmonitor。此 profile 不接受其他类别数或排列。

## 当前证据

2026-10-25：macOS ARM64 Mono 桌面回归 85 项全部通过、无跳过，包含 DeepLab
CPU/Auto 实际权重、Candy、解码单元测试、两个独立页面及共享生成管线。
DeepLab CPU 和 Auto（实际 Metal）的结果均与独立 LiteRT 2.3.0 比较。
一张 810 × 1080 公交车／行人照片的预处理逐元素一致；网格及原图投影类别一致率
均为 100%，最大 logit 误差约 0.000036。额外覆盖输出布局、非有限值、极端 logits、
softmax、类别平局、坐标投影、取消、释放、重复执行和 GUI 请求。

OPPO PELM00 Android ARM64 IL2CPP 的合并回归 117 项全部通过、无跳过，包含
DeepLab 和 Candy 各两项 CPU/Auto 实际权重测试，以及平台和基础数值推理。
DeepLab 最大 logit 误差约 0.000040、平均约 0.0000032、类别一致率 100%。
CPU 加载约 62 ms、首轮 145 ms、重复 138 ms；进程峰值 RSS 约 328 MB。
进程内存包含 Unity/native/mapped weights；时间未控制设备温度、电源和后台活动。

这是单张真实输入的独立参考检查，不能作为 Pascal VOC 数据集精度评价。
macOS x86_64 Mono Player（Apple Silicon / Rosetta）合并回归117项通过，无跳过，包含 DeepLab/Candy CPU/Auto（实际 Metal）；物理 Intel Mac 未执行。其他分割模型和后端不自动继承本 profile 的验证结果。iPhone 实际权重推理仍待验证。

独立参考用共用 `Tools~/Testing/generate_image_reference.py --profile deeplab` 生成，
需 Pillow、numpy、ai-edge-litert 以及已经存在的固定源图和 TFLite，不下载文件。
Player 测试要求图、`mnn_image_task.json`、`source.rgb`、`input.f32`、`output.f32` 和
`classes.u8` 已在应用 `persistentDataPath/segmentation/`。桌面也可提供
`run_platform_tests.py --segmentation-root <existing-root>`。
固定参考哈希及两项实际用例必须通过，runner 才写入 `deeplab_segmentation_verified=true`。

测量在独立原始张量检查之后执行，文件缓存和部分内核可能已预热；“首轮”指新建
托管任务实例的首次 Process，不代表操作系统冷缓存启动。

最新合并 iOS ARM64 工程已通过原生编译、链接、开发签名及 strict deep 签名校验；配置包含已知 iPhone17，但设备当前 unavailable，实际推理仍待执行。

最新合并验证记录：`TestArtifacts~/TaskIntegration/ImageTasks/validation-report.md` 和 `validation-summary.json`（开发仓库本地产物）。
