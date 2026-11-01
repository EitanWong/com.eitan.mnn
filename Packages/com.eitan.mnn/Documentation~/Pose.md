# MoveNet SinglePose 姿态估计

Studio 的 **Pose estimation** 独立页面选择照片，预测一个主要人物的 17 个 COCO 关节，
显示可调阈值的骨架、源图坐标和置信度表，并可保存 PNG。多人照片请先裁剪；
此模型不是多人检测器。空场景也会产生关节预测，低分、遮挡部位和交叉手臂可能不准确。

## 准备与来源

```bash
python3 Packages/com.eitan.mnn/Tools~/Models/prepare_image_profile.py \
  --profile movenet --converter /absolute/path/to/MNNConvert --download
```

共用工具使用已有、未修改的官方 MNN 3.6.1 转换器。`--download` 显式取得
Xenova/movenet-singlepose-lightning 的 ONNX float32 导出，固定 revision
`ed0f314bb7356fd1dbf1e4f52c2d40791bf6534f`。已有源文件时省略此选项。
输出目录为 `Assets/StreamingAssets/MNN/Models/TensorFlow--MoveNet-SinglePose-Lightning/`，
在 Studio 选择该本地目录。目录名保留模型家族，实际支持的是上述固定 ONNX 导出。
没有直接可下载的已验证 MNN 图，不在 Model Manager 声称任意 MoveNet 仓库可运行。

ONNX 源 SHA-256：`1ad4f8d6c2f776a9967db3993c9ca740bc350104f9d37c151dc183fc29a464ad`。
转换图为 9,349,132 字节，SHA-256：
`79b22749faaf89303f3714ba6fdf0c66a647b20425076cbd7ea64d8d9ec39d69`。
两次转换仅规范化公开 `Net.mnn_uuid` 后逐字节一致；不改变算子、权重或官方源码。
Runtime 加载时核对哈希，拒绝不同导出。

官方 Kaggle Google MoveNet singlepose-lightning-tflite-float16 v1 的 `4.tflite`
已另行下载作对照，SHA-256 `0fac2226112d0371903ca86e3853cec24ef603a0b2f96f589b180f0ebdd135ab`。
官方 3.6.1 转换器因 `ARG_MAX` 不支持而不能直接转换该 TFLite，因此使用 ONNX 路径。
该 float16 TFLite 和 ONNX float32 是不同导出，置信度不能宣称等价。

## 共享 Runtime

```csharp
using (var model = MNNImageTask.Load(directory))
{
    var result = model.Process(rgbImage);
    MNNPoseResult pose = result.Pose;
    foreach (MNNPoseKeypoint point in pose.Keypoints)
        if (point.IsInBounds && point.Confidence >= 0.2f)
            UnityEngine.Debug.Log($"{point.Joint}: {point.X}, {point.Y}");
}
```

实现位于共享 `MNNImageTask.Pose.cs` partial，跨平台复用现有官方 C++
Interpreter/Session/Tensor 调用。没有 Mobile 专用类型、新 C++ 或桥接导出。
同实例串行执行、异步、取消与释放复用现有 API。取消在前后处理与原生调用之间检查，
不能中断正在执行的单次原生图。使用 High 精度；Auto 按既有设备策略选择后端。

输入 `input [1,192,192,3]` 为 NHWC **int32 RGB 0–255**，不是 float 归一化输入。
输出 `output_0 [1,1,17,3]` 为 float32 `(y,x,score)`。RGB 使用共用抗锯齿双线性采样：
先水平后垂直，每步舍入 uint8，再居中填黑到 192 正方形。
最长边为 192，另一边四舍五入（半数向上），最小为 1；奇数多余填充放在右／下侧。
此协议不声称与 TensorFlow 默认 `resize_with_pad` 一致。

坐标以左上为原点，`X=(x*192-left)*sourceWidth/resizedWidth`，Y 同理。
逆变换使用真实取整后的宽高；不裁剪填充区的点，`IsInBounds` 标出是否在源图内。
源坐标为连续边界坐标，右／下边界恰等于尺寸的点在图外。学习偏移也可能
使点落到 192 输入画布之外；有限坐标仍保留，并检查变换后的表示是否溢出。置信度只是模型关节分数，
不是经过校准的人体存在概率。保留全部 17 点，包括低分点；结果只读。

COCO 顺序：Nose、LeftEye、RightEye、LeftEar、RightEar、LeftShoulder、RightShoulder、
LeftElbow、RightElbow、LeftWrist、RightWrist、LeftHip、RightHip、LeftKnee、RightKnee、
LeftAnkle、RightAnkle。左右以被摄人物身体为准。最多 16,777,216 个源像素。
`result.Image` 是源图副本；骨架和显示阈值在 Editor 页面生成，不改变 Runtime 关节数据。

## 参考与验证边界

共用 `Tools~/Testing/generate_image_reference.py --profile movenet` 使用已有源图、ONNX、
官方 TFLite，通过 ONNX Runtime 和 LiteRT 生成两个真实人物裁剪的参考。
需要 numpy、Pillow、onnxruntime、ai-edge-litert；Runtime 不依赖 Python。
使用两个明确的 Pillow uint8 缩放步骤，避免 Pillow 13 单次缩放保留中间精度造成的像素差异。
两个裁剪来自同一张公交车照片，不能代表人体姿态数据集精度。

MNN 正确性严格对照本 profile 的 ONNX，原始 51 个数最大误差门槛 0.001、平均 0.0001；
Player 还要求输入逐元素一致、源坐标误差小于 0.5 像素、置信度误差小于 0.001，
以及重复执行、取消、释放、关节上下关系和可见关节数检查。
官方 float16 TFLite 仅作补充几何对照：两例最大坐标差小于 0.51 个输入像素，
其中一个肩部的分数差约 0.113，不能视为置信度等价。该差异保留在 reference.json 中。

Player 资产目录 `persistentDataPath/pose/` 包含 `movenet-lightning.mnn`、
`mnn_image_task.json`，以及 `person-a/`、`person-b/` 中的 `source.rgb`、`input.i32`、`output.f32`。
桌面可用 `run_platform_tests.py --pose-root <existing-root>` 指定同样目录。
runner 要求两人各 CPU/Auto 共四项实际通过才报告 `movenet_pose_verified=true`；
缺少权重而跳过不算通过。

测量是新建任务实例的加载／首次 Process／重复 Process，之前已做原始张量对照，
文件缓存与部分内核可能预热，不能称为冷启动基准。RSS／峰值包含整个 Unity 进程及其缓存，
不是模型独占内存。平台实际结果见开发仓库本地
`TestArtifacts~/TaskIntegration/Pose/validation-report.md`；编译、签名不作为真机推理证据。

2026-10-25 最终源码验证：Editor 50 项、Android ARM64 IL2CPP 117 项、macOS
ARM64 Mono Player 117 项、macOS x64 Mono Player（Rosetta）117 项，均无失败／跳过。
每个 Player 专项包含两个真实人物各 CPU/Auto 四项。Android 最大原始误差约
0.0000089，源坐标误差小于 0.00011 像素；首次 Process 约 47–58 ms、复用约 43–45 ms，
进程峰值 RSS 约 307 MB。ARM64 Mac Process 约 5–7 ms。物理 Intel Mac 未验证。
iPhone 17 ARM64 IL2CPP：117 项通过、0 失败/跳过，两个真实人物各 CPU/Auto→Metal 四项通过。
首次/复用 Process 约 6–7 ms，原始最大误差低于 0.000009；进程峰值 RSS 约 386 MB，
physical footprint 约 276 MB，包含 Unity/缓存。证据为同目录本地报告的 iOS-results.xml。
13 个 Runtime 条件编译分支与包结构校验通过；这些检查不等于真机推理。
