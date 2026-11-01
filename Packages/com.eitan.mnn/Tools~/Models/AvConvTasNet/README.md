# ClearerVoice AV-ConvTasNet LRS2 reproduction

Official checkpoint `alibabasglab/log_LRS2_lip_convtasnet_2spk`, revision
`0b05921c9bf896a0cf772ac75521b7a35a671c4f`, SHA-256
`0025f15957716bf3f46297cf55b3c40930c00a1843821d8f0c642a18e7874d12`.
Source `modelscope/ClearerVoice-Studio` is pinned to
`6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61`. All checkpoint keys load strictly.

```sh
PYTHONDONTWRITEBYTECODE=1 python3 prepare.py \
  --source /absolute/path/to/ClearerVoice-Studio/train/target_speaker_extraction \
  --checkpoint /absolute/path/to/last_best_checkpoint.pt \
  --converter /absolute/path/to/MNNConvert \
  --output /absolute/path/to/TestArtifacts~/AvConvTasNetReproduction
```

Requires PyTorch, NumPy, PyYAML, ONNX, ONNX Runtime and matching MNN Python.
The constructor's visual initialization uses a subset of the same complete
checkpoint, then loads every original key strictly; it fetches no extra weights
and removes its temporary initialization file. The CPU oracle only makes the
unconditional CUDA transfer of overlap-add integer indices a no-op, retaining
all original `index_add_` arithmetic.

The independent clone uses the shared `../clearervoice_av_export.py` lowering:
depth-five/single-channel visual Conv3d becomes five neighboring frames in one
spatial Conv2d, and linear-basis synthesis plus overlap-add becomes
ConvTranspose1d. The source model is unchanged. The broader physical gate checks
21 conditions, including original real audio/target face crops, silence, quiet,
gain, visual changes, encoder/frame/receptive-field boundaries and six seconds.
Original visual features and complete waveforms are compared separately.

This command checks eleven seeded random lengths, lowered PyTorch, ONNX, and
repeated dynamic MNN execution plus a return to the first shape. Both visual and
complete-waveform NRMSE must remain below 0.001. It records deterministic public
Net UUIDs and writes a report only after every check passes. Deploy only the
selected MNN graphs and `mnn_av_convtasnet.json`; raw weights, ONNX, precision
candidates, dependencies and reports remain under `TestArtifacts~/`.

Input is 16 kHz mono plus synchronized 25 FPS target-face crops, 112×112 grayscale,
normalized `(gray/255-0.4161)/0.1688`. Audio amplitude is unchanged. Output is one
target waveform from a model trained on LRS2 two-speaker mixtures. It provides
neither persistent speaker IDs nor streaming or long-recording overlap-add.
The model card's Apache2 declaration, full repository Apache2 and MIT notices
for Kaituo Xu and Smeet Shah must be retained. Corpus rights are separate.

The physical precision gate rejects FP16 and Q8, including single-branch
compression. Conversion success does not validate GPU/NPU placement, whole-call
performance, Unity, Studio or built Player execution; those gates run separately.
No official MNN source, native operator or ABI is changed.
