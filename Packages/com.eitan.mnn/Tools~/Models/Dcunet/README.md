# DCUNet Libri1Mix 16 kHz reproduction

Pinned model: `JorisCos/DCUNet_Libri1Mix_enhsingle_16k`, revision
`3fa701427576e01e835ae415c8ed7516874b08dd`. Checkpoint SHA-256:
`082a3f30c3f3a32f3c64ab7e6954b26fc5dd4ffa986bdd657049a9f3a93da7b6`.
The original Asteroid 0.7.0 and filterbanks 0.4.0 sources are checked by hash,
and every state-dict key loads strictly. The checkpoint records Asteroid 0.4.1rc0
as its training version. Original complex forward is the independent oracle.

```sh
PYTHONDONTWRITEBYTECODE=1 python3 prepare.py \
  --source /absolute/path/to/asteroid-0.7.0/asteroid \
  --filterbanks /absolute/path/to/asteroid-filterbanks-0.4.0/asteroid_filterbanks \
  --checkpoint /absolute/path/to/pytorch_model.bin \
  --converter /absolute/path/to/MNNConvert \
  --output /absolute/path/to/TestArtifacts~/DcunetReproduction
```

Requires matching MNN Python runtime, PyTorch, NumPy, SciPy, ONNX and ONNX Runtime,
and dependencies imported by the original Asteroid source. Eleven random lengths
cover 1024–48000 samples, STFT hop transitions and pad-to-16 time boundaries.
Real/imaginary convolution retains independent parameters, biases and batch norms.
It preserves all 513 complex frequency bins, kernel1024/stride256 analysis and
synthesis, `fix_length_mode=pad`, mask crop, bounded complex magnitude/phase, and
the original final waveform zero padding. No waveform normalization is added.

The command checks lowered PyTorch, ONNX and CPU MNN against the unchanged original
under NRMSE 0.001. It exports FP32 using official `MNNConvert --saveExternalData`,
then shares only byte-identical external Convolution/Blob/Scale payloads through
the public MNN schema. Complete source byte coverage is required. The deterministic
public `Net.mnn_uuid` allows byte-for-byte graph reproduction. MNN source, native
operators and native object layouts are unchanged.

Deploy `dcunet_fp32.mnn`, `dcunet_fp32.mnn.weight` and `mnn_dcunet.json` together.
Raw weights, ONNX, intermediate external graphs, dependencies and reports belong
under `TestArtifacts~/`. The broader 18-condition source gate and actual Unity
CPU/Metal/Studio/Player validation remain separate checks. FP16/Q8 failed that
source gate, so their smaller files are not deployment candidates.

The original model-card body specifies CC BY-SA 3.0, while metadata lists 4.0.
Retain both declarations and the author's LibriSpeech/WHAM corpus notices,
including WHAM CC BY-NC 4.0/research-only terms. Asteroid/filterbanks code license
does not override those notices.
