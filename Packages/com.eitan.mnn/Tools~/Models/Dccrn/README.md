# Mayavoz DCCRN reproducible conversion

Use the exact original source/checkpoint and PyTorch2.8.0 build specified in
contracts.json. The model card declares Apache-2.0; the source and derived
export-only arithmetic retain MIT terms in LICENSE.source.md.

```sh
env -u PYTHONPATH PYTHONDONTWRITEBYTECODE=1 /path/to/torch28/python prepare.py \
  --source /path/to/pinned/mayavoz \
  --checkpoint /path/to/pytorch_model.ckpt \
  --oracle /path/to/original/reference \
  --converter /path/to/official/MNNConvert \
  --metadata-dependency-path /path/to/existing/omegaconf \
  --mnn-pythonpath /path/to/pymnn:/path/to/mnn/dependencies \
  --output /path/to/TestArtifacts/new-export
```

The independent oracle must contain22 original physical PCM conditions,
cases.json, and input/target .f32 files. The tool reruns the unmodified original
forward from the serialized checkpoint for every clip; original, readonly
normalization and four-stage lowering must all match each oracle bitwise.

The original constructor aliases normalization buffers; official strict
load_state_dict(assign=True) retains the distinct serialized tensors. References
represent the first eval call after loading each clip. The readonly export folds
that call's statistics, retaining the original E-mask arithmetic and clamp.
Inputs are right-zero-padded to100 samples and outputs cropped to original
length. This is distinct from the author's2-second enhance()/OLA wrapper.

The export-only atan2 symbolic avoids the legacy ONNX0/0 division and preserves
numeric axis/quadrant results. Runtimes may canonicalize negative zero; no
signed-zero bitwise claim is made. Native MNN LSTM lowering avoids dynamic While
resize failures. Official converter external data is compacted by exact records
across five graphs, with one shared FP32 weight file and deterministic public
Net UUIDs. Original native source code is never modified.

A separate MNN/NumPy child validates CPU1/4 whole graphs and CPU4 four-stage
chains:138 original full waveforms, including repeats and first-shape returns.
All require finite exact-length outputs and NRMSE<0.001. The child never imports
Torch. Independent CLI/Unity tests must additionally qualify Metal
encoding/decoding with CPU analysis and recurrence before deployment. Whole
Metal, CoreML, FP16 or Q8 must not be selected merely because conversion exits0.

Only final models/, verified runtime profile, licenses and metadata belong in
the deployment. Original checkpoint, ONNX, dependencies and all diagnostics stay
under TestArtifacts~. Unity/Studio/Player and exclusive end-to-end measurements
are separate acceptance gates.
