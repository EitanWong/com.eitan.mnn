# 以有界缓冲共享外置 MNN 权重

`stream_mnn_external_weights.py` 将多张已外置权重的 MNN 3.6.1 图合并到
一份共享权重文件。它复用相邻工具的公开序列化 schema 解析器，只改变外置
记录偏移，不修改算子、拓扑、数值、量化或官方 MNN 源码。

输入为各自带 `<graph>.weight` 的模型。支持公开 schema 的 Convolution2D、
Blob 和 Scale 外置记录；有未覆盖的权重字节、越界记录、子图或不支持的记录
时拒绝处理。图数据保留在内存，外置权重按 4 MiB 块读写，不读取整份权重。
仅在完整记录长度、SHA-256 及逐字节比较一致时共享记录。

从工程根目录运行，例如：

```bash
PYTHONDONTWRITEBYTECODE=1 python3 \
  Packages/com.eitan.mnn/Tools~/Models/stream_mnn_external_weights.py \
  --source TestArtifacts~/example/external/encoder_cpu.mnn \
  --source TestArtifacts~/example/external/encoder_metal.mnn \
  --output TestArtifacts~/example/shared \
  --weight-name encoder_cpu.mnn.weight
```

使用独立输出目录。输入图和权重须在整个操作期间保持只读，由一个写入者处理
输出目录。重复执行产生相同结果；临时文件替换使中断后可重新执行。

消费者必须通过官方 MNN `setExternalFile` 指定共享文件，并在创建 Session
之前设置它。只有与共享文件同名的那张图满足常见的 `<graph>.weight` 自动
发现约定，其他图不能依赖该约定。Unity 内已有图执行器提供
`externalWeightsPath` 参数。

该工具只验证存储记录，不验证推理结果。应用到新模型后，仍须核对公开 schema
语义指纹、原始参考数值、动态形状历史、所选后端、实际 Player 副本与运行清单。
共享权重不是低精度计算，也不是 NPU/GPU 放置证据。

行为检查：

```bash
PYTHONDONTWRITEBYTECODE=1 python3 \
  Packages/com.eitan.mnn/Tools~/Models/Tests/test_stream_mnn_external_weights.py
```

测试确认最终字节与原有精确去重一致、禁止完整权重读取、拒绝未引用字节及
源/输出别名，并确认重复执行保持结果。临时文件位于工程 `TestArtifacts~/`。
