# OmniVoice compact sampling graph

This optional graph moves target-frame projection, CFG 2, argmax and stable
confidence calculation into official MNN operators. The managed sampler still
chooses positions with its original Gumbel schedule. No training, step reduction,
custom operator or native source changes are involved.

Use the original AFun9/Omnivoice-onnx export at
`4ec8125833a6a2806ff2e07b31e73f106954aad0`, Python 3.11+ with `onnx` and `numpy`,
and official MNN 3.6.1 `MNNConvert` (the pip wheel calls it `mnnconvert`).
Do not feed an already transformed graph. Run from the repository root:

```sh
python3 Packages/com.eitan.mnn/Tools~/Models/OmniVoice/compact_sampling.py \
  --source 'TestArtifacts~/OmniVoiceResearch/onnx-export/output/omnivoice_lm/model.onnx' \
  --work-dir 'TestArtifacts~/OmniVoiceResearch/compact-conversion' \
  --converter /absolute/path/to/MNNConvert \
  --reference-weights Assets/StreamingAssets/MNN/Models/OmniVoice-MNN/omnivoice_lm.mnn.weight \
  --install-dir Assets/StreamingAssets/MNN/Models/OmniVoice-MNN
```

The tool checks the exported ONNX structure and requires byte-identical external
MNN weights before installation. It records SHA-256 provenance and converter
output in the work directory, then atomically installs only
`omnivoice_lm_compact.mnn`. Existing Unity `.meta` files are preserved. The
runtime uses `omnivoice_lm.mnn.weight` directly for both graphs; deliver it once.
APFS clones avoid duplicating the large source ONNX sidecar; other platforms
copy it because the ONNX checker rejects hardlinks and symlinks.

Call `MNNOmniVoice.LoadCompact(root, lmBackend: ...)` to opt in. High precision
and packed CFG are required. Run `OmniVoiceCompactIntegrationTests` with the
local model and `zero_shot_prompt.wav` installed. Those tests compare full
waveforms against `Load`, and store WAVs under the test project's
`TestArtifacts~/OmniVoiceCompact`. Configure `MNN_INFERENCE_CACHE_DIRECTORY`
under `TestArtifacts~/` for local benchmark runs. See
[performance and validation limits](../../../Documentation~/OmniVoice.md).
