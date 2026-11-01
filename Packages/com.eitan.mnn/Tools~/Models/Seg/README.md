# SEG YGD → MNN

Pinned ClearerVoice-Studio `6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61`, author
checkpoint `alibabasglab/log_YGD_gesture_seg_2spk` revision
`9d5b3e567d04abc7190d49325e79101e52a28930`. The source/checkpoint digests are
verified before loading. Source inference retains the original index-add decoder;
its hard-coded CUDA integer-index placement is ported to the same CPU device.

Use a prepared Python environment with PyTorch, NumPy, PyYAML, ONNX and official
MNN bindings. Keep the environment, source checkout, original checkpoint, ONNX,
independent source oracles and generated logs under the project's `TestArtifacts~`.
Set `PYTHONDONTWRITEBYTECODE=1`. The original source module search path must also
contain its dependencies, as in the prepared av-export-venv.

```sh
PYTHONDONTWRITEBYTECODE=1 python prepare.py \
  --source /path/to/ClearerVoice-Studio/train/target_speaker_extraction \
  --checkpoint /path/to/last_best_checkpoint.pt \
  --config /path/to/config.yaml \
  --converter /path/to/official/MNNConvert \
  --oracle /path/to/independent/reference \
  --output /path/to/TestArtifacts~/SEG/reproduction
```

The independent oracle manifest has `cases` entries with `case`, `samples`, and
`frames`; each case has float32 files `<case>-input.f32`, `<case>-gesture.f32`,
and `<case>-target.f32`. Oracle targets are generated using the original strict
checkpoint network, not from ONNX or a converted graph. The validated collection
contains 37 conditions, including segmentation and frame-count boundaries, real
speech, quiet/gain/DC/impulse/ramp inputs and changed or absent gesture cues.

The tool checks original-source repeatability and lowered-source parity, exports
the dynamic FP32 graph, normalizes only its public UUID deterministically, then
runs a separate NumPy/MNN process for CPU2/4, two repeats and first-shape return.
It never modifies official MNN source or adds C++/native bridge code. An exit0
and 152 comparison rows are required. Inspect the graph digest before updating
a frozen Unity model profile.

Indexed overlapping windows replace a rank-4 concat which produced wrong MNN
values at a 100-step encoder boundary. A four-stage, 64-element bounded reduction
replaces the first GroupNorm statistics to avoid long-DC accumulation drift. The
original 256→40 linear overlap-add decoder lowers to ConvTranspose1d. The other
network parameters and source amplitude contract remain intact. The export
tracer's `gap > 0` branch is always true mathematically: gap is in [1,100].

`gesture_to_json.py` converts existing author-compatible NumPy features to Studio
JSON. It does not estimate gestures from video or change coordinate conventions.
