# SepReformer-B WSJ0-2mix reproduction

Pinned author repository: `dmlguq456/SepReformer`, revision
`6dd3d163cc7c1f3a5534f22daf68382a8ea135e9`. Checkpoint
`models/SepReformer_Base_WSJ0/log/scratch_weights/epoch.0180.pth` has SHA-256
`336d6f40035e00496aacbe42c9ddf263d21b25dcb7941b5b90e245e9145043d2`.
The source hash list includes the original model, modules, configuration, padding
helper and author inference engine. Every original state-dict key loads strictly.

```sh
PYTHONDONTWRITEBYTECODE=1 python3 prepare.py \
  --source /absolute/path/to/SepReformer \
  --checkpoint /absolute/path/to/epoch.0180.pth \
  --converter /absolute/path/to/MNNConvert \
  --output /absolute/path/to/TestArtifacts~/SepReformerReproduction
```

Requires PyTorch, NumPy, PyYAML, loguru, ONNX, ONNX Runtime and matching MNN Python
runtime. The unchanged author forward is the oracle. Ten random lengths cover
16–32000 samples, encoder boundaries, four-sample input padding and separator
boundaries. Lowered PyTorch, ONNX and CPU MNN must each satisfy per-speaker NRMSE
0.001 against original waveforms. The tool writes its report only after all pass.

An independent export clone replaces integer-ratio adaptive average pooling with
reshape/mean, makes separator modulo-16 padding unconditional, prunes unused
training auxiliary outputs, and approximates exact GELU through a high-accuracy
absolute-value/exp/erfc polynomial. That polynomial is checked against exact
PyTorch GELU on 400001 scalar inputs with maximum error below 2e-6. The observed
maximum is 4.76837e-7. This avoids the official Metal GELU_STANDARD kernel's tanh
approximation, which fails the unchanged waveform gate even when the converter's
GELU approximation flag is disabled. No official MNN source or native operator
is changed. FP16 and Q8 candidates failed the broader waveform gate.

Output `sepreformer_fp32.mnn` and `mnn_sepreformer.json` form the runtime profile.
The official converter uses FP32, level1, optimizePrefer2 and disabled transformer
projection fusions. A deterministic public `Net.mnn_uuid` supports byte-for-byte
reproduction. Keep raw weights, ONNX, reports and dependencies under
`TestArtifacts~/`; deploy only the selected graph/config plus README, manifest
and attribution. Unity CPU/Metal/Auto, Studio, timing and built-Player validation
are independent of this reproduction command.

The API preserves original network amplitude and speaker slots. It pads mono
8 kHz input to a multiple of four and trims both waveforms. The author's engine
scales each speaker to peak0.9 only when writing audition files; that file-only
scaling is deliberately excluded from the inference contract. The repository
ships Apache-2.0; retain the complete license and upstream README, including
WSJ0 corpus provenance. The code license does not grant access to WSJ0 recordings.
