# DCCRNet Libri1Mix 16 kHz reproduction

Pinned model: `JorisCos/DCCRNet_Libri1Mix_enhsingle_16k`, revision
`00914c64c47932a360b5b4e0c07ab12d305604ba`. The checkpoint SHA-256 is
`c1f519207604649cd90586e85adf2e93c4f4b26b216f0a71635013a90e64d939`.
Asteroid 0.7.0 and filterbanks 0.4.0 sources are verified by `source-hashes.json`;
the checkpoint records training with Asteroid 0.4.1rc0. A constructor-only cast
from NumPy integer to Python integer accommodates current PyTorch. Every original
state-dict key loads strictly; original forward math remains unchanged.

```sh
PYTHONDONTWRITEBYTECODE=1 python3 prepare.py \
  --source /absolute/path/to/asteroid-0.7.0/asteroid \
  --filterbanks /absolute/path/to/asteroid-filterbanks-0.4.0/asteroid_filterbanks \
  --checkpoint /absolute/path/to/pytorch_model.bin \
  --converter /absolute/path/to/MNNConvert \
  --output /absolute/path/to/TestArtifacts~/DccrnetReproduction
```

Requires matching MNN Python runtime, PyTorch, NumPy, SciPy, ONNX and ONNX Runtime,
plus dependencies imported by the original Asteroid source. Eight original raw
lengths cover 1000–48000 samples. The original complex model is the oracle.
Real/imaginary lowering retains separate biases, batch norms, complex RNN calls,
bounded complex-mask magnitude epsilon and phase, Nyquist removal/padding,
original STFT filters, synthesis and trailing waveform zero padding. No waveform
normalization or convenience `separate()` amplitude scaling is introduced.

Lowered PyTorch, ONNX and CPU MNN must remain below NRMSE 0.001 relative to the
unchanged original waveform. FP16/Q8 failed the broader 14-condition gate, so the
selected candidate uses FP32/High and the official native RNN conversion. The
public `Net.mnn_uuid` is deterministic; no official MNN source is modified.

Generated ONNX, raw weights, dependencies and reports remain in `TestArtifacts~/`.
The command produces one complete CPU graph and three equivalent split graphs.
The split encoder/decoder can run on Metal with the complex recurrent stage on
CPU: whole-graph Metal recurrence failed the original oracle. The command checks
both complete and split execution on CPU at eight lengths. Runtime profile
fields name all four graphs. The 14-condition Unity CPU/Metal/Auto gate is
separate and must confirm each stage backend explicitly. GPU tuning is selected
from whole-call and shape-change measurements; tuning time is part of the cost. Model-card body specifies CC BY-SA 3.0 while metadata lists
4.0, and retains a WHAM CC BY-NC 4.0/research-only notice. Preserve the original
card and applicable notices when distributing a converted artifact.
