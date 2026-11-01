# Resemble Denoiser export

Exports only the denoiser branch of `ResembleAI/resemble-enhance`, checkpoint revision
`4e3510ce4a8391159f665903544c5150bee7b2cb`, with original source revision
`8e978149bfe8abab3eb77d965d579a111afdb0ff`. Source files and the shared checkpoint
are SHA256 checked. All original active denoiser parameters load strictly. The
training-only mel frontend is unused by `Denoiser.forward` and is omitted; its three
buffers are recorded in `preparation.json`.

Keep source checkout, checkpoint, original waveform/spectral oracle, dependencies
and export output under the project's `TestArtifacts~/`. No automatic download is
performed. The reference directory contains `cases.json`, `window.f32`, and each
case's `*-input.f32`, `*-features.f32`, `*-spectrum.f32`, `*-target.f32`. Preserve the
original 24-condition oracle, including DC, very quiet input, silence, impulse,
ramp, shortest input, UNet padding boundaries and three-second input.

Run the original source environment with PyTorch 2.8.0. Keep a different MNN
dependency environment in the validation child process; putting that environment
on the parent `PYTHONPATH` can silently replace the reference Torch version.

```bash
PYTHONDONTWRITEBYTECODE=1 \
MNN_VALIDATION_PYTHONPATH="/path/to/pymnn:/path/to/mnn-dependencies" \
python prepare.py \
  --source /project/TestArtifacts~/resemble-enhance \
  --checkpoint /project/TestArtifacts~/mp_rank_00_model_states.pt \
  --converter /path/to/official/MNNConvert \
  --oracle /project/TestArtifacts~/denoiser-reference \
  --output /project/TestArtifacts~/resemble-export
```

The parent executes the unmodified original waveform method against every oracle
and checks the bounded GroupNorm lowering independently. It exports a dynamic
ONNX spectrum graph, converts it with official MNN tools, and writes a deterministic
public Net UUID. The MNN-only child performs 100 waveform comparisons using CPU2/4,
two repeats and return to the first input shape. The unchanged waveform NRMSE
threshold is 0.001. Deploy `denoiser_fp32.mnn`, the generated config, MIT license,
attribution, README and validated manifest/optimization metadata.

The complete Unity API performs the original two peak normalizations, 441-sample
right padding, centered reflect STFT, dropped final analysis frame, spectral
prediction, replicated final synthesis frame and inverse STFT. Its real FFT uses
the pinned source float butterfly order: ordinary double FFT or a different float
FFT changes low-energy phase and does not preserve the model on DC/ramp input.
The actual denoiser FFT is 1680 (hop 420); the checkpoint's training mel FFT is a
different, unused frontend. FP16 storage and Q8 are evaluated separately; the
deployed FP32/High profile is selected from measured, source-checked candidates.

No C++, bridge, custom export or official MNN source modification is required.
