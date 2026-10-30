# ClearerVoice AV-DPRNN reproduction

Official checkpoint `alibabasglab/log_VoxCeleb2_lip_dprnn_2spk`, revision
`6a356000cef95fd605ca1dc6619d314bca5acb32`, SHA-256
`98e529110aada7576f4ac360ee1d4c338dc63fdd805d52716129754f26b4f1b4`.
Source `modelscope/ClearerVoice-Studio` is pinned to
`6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61`. Every state-dict key loads strictly.

```sh
PYTHONDONTWRITEBYTECODE=1 python3 prepare.py \
  --source /absolute/path/to/ClearerVoice-Studio/train/target_speaker_extraction \
  --checkpoint /absolute/path/to/last_best_checkpoint.pt \
  --converter /absolute/path/to/MNNConvert \
  --output /absolute/path/to/TestArtifacts~/AvDprnnReproduction
```

Requires PyTorch, NumPy, PyYAML, ONNX, ONNX Runtime and matching MNN Python.
The source loader reuses the visual initialization subset of the pinned complete
checkpoint for the original constructor, then strictly loads the complete state.
It never fetches an unrelated lip checkpoint. That temporary initialization file
is removed. The author overlap-add oracle has one unconditional CUDA transfer
of an integer index; on a CPU host only that transfer is a no-op, preserving all
index_add arithmetic and the unchanged source forward.

An independent export clone expresses the depth5/one-channel Conv3d as five
neighboring frames in one spatial Conv2d, retains temporal zero boundaries,
normalization and the complete visual adaptor, and expresses linear-basis
synthesis plus overlap-add as ConvTranspose1d. Visual features and whole outputs
are checked against original inference, including 40 ms to6 s and encoder/chunk
boundaries. Modulo padding is unconditional. Eight seeded random lengths must
pass lowered PyTorch and ONNX, and repeated dynamic MNN execution plus a return
to the first shape. Every visual/whole-waveform NRMSE must remain below0.001.
The report is written only when all checks pass.

Official `--useOriginRNNImpl` is necessary: the default While graph cannot run
these Interpreter sessions. Reusing the original Conv3d visual Session across
frame counts also crashes on this host; independent fresh visual Sessions pass.
The equivalent Conv2d export removes that unsupported resize path. No official
MNN source, native operator or object layout is changed. Public Net UUIDs are
deterministic. Deploy the selected two graphs and `mnn_av_dprnn.json`; keep raw
weights, ONNX, candidates, dependencies and reports under `TestArtifacts~/`.

The broader19-condition source gate rejects FP16/Q8 both jointly and when only
one branch is compressed. Official Metal module probes validate visual inference
but fail audio LSTM waveforms. The Unity candidate therefore uses Metal visual
processing with CPU audio recurrence. Whole-call performance, tuning, Studio and
built-Player checks are independent of this preparation command; do not treat
successful conversion as their completion.

Input is16 kHz mono plus synchronized25 FPS target-face crops,112x112 grayscale,
normalized `(gray/255-0.4161)/0.1688`. Audio amplitude is unchanged. This is one
target waveform, trained on VoxCeleb2 mixtures, with no persistent speaker ID.
Retain the Apache2 model card and full upstream license plus the visual frontend's
MIT/Smeet Shah attribution and AV-DPRNN's Apache2/Kai Li attribution. Corpus
rights are separate; no VoxCeleb recordings are deployed.
