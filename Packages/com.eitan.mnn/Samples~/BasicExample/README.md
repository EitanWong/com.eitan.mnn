# Basic Inference

无需额外 UI、Burst 或 Collections 依赖的托管 API 示例。

1. 在 Unity Package Manager 中选择 MNN for Unity，导入 **Basic Inference** sample。
2. 新建场景和空 GameObject，添加 `MNNBasicExample` 组件，运行后查看 Console 与屏幕状态。
3. 需要实际推理时，添加 `MNNInferenceExample`，在 Inspector 的 `Model Path` 中设置已有兼容
   `.mnn` 文件的绝对路径；按空格写入随机 float 输入并读取推理输出。
4. 需要纹理输入/输出时，使用 `MNNUnifiedExample`，设置可读纹理与匹配的模型路径和张量形状。

脚本位于 `Scripts`，统一使用 `MNN.Examples` 命名空间，程序集为
`MNN.Unity.Samples.BasicExample`，仅引用 `MNN.Unity`。示例不附带场景或模型，不自动下载权重。
纹理示例不适用于任意形状/类型的模型；具体转换要求见 [API 参考](../../Documentation~/API.md)。

当前推理范围为随包原生库匹配的 macOS 64 位 Mono，默认 Auto 策略优先可用 Metal 并保留 CPU 备用；
CPU 与 Metal 的实际覆盖见 [加速说明](../../Documentation~/Acceleration.md)。其他平台和 IL2CPP 的推理入口
尚未实现。示例导入后能够编译，不代表目标平台具备推理能力。
平台与 ABI 限制见 [包说明](../../README.md) 和 [测试说明](../../Documentation~/Testing.md)。
