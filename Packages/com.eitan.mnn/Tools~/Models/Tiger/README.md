# TIGER / DAVE audio separation export

Uses the pinned original source and checkpoint declared in `contracts.json`.
Both profiles are audio-only two-source separation. DAVE's released TIGER-M
checkpoint does not include its independent audiovisual attribution components.

Use the original PyTorch2.8.0 revision
`a1cb3cc05d46d198467bebbb6e8fba50a325d4e7`, NumPy, safetensors and the original
model's Hugging Face I/O dependencies. `--io-dependency-path` appends an optional
existing dependency directory only after the original Torch is bound and checked.
The MNN-only child uses `--mnn-pythonpath`; it never imports Torch.

```sh
env -u PYTHONPATH PYTHONDONTWRITEBYTECODE=1 /path/to/torch28/python prepare.py \
  --profile tiger \
  --source /path/to/pinned/TIGER \
  --checkpoint /path/to/model.safetensors \
  --oracle /path/to/original/reference \
  --converter /path/to/official/MNNConvert \
  --output /path/to/TestArtifacts/export \
  --mnn-pythonpath /path/to/pymnn:/path/to/mnn/dependencies
```

Use `--profile dave`, the pinned DAVE source and `real_avse_tiger_m.ckpt` for
the second profile. Checkpoint loading is strict for both profiles.

The independent original oracle must contain25 physical conditions, original
PCM inputs/targets, source STFT features, captured source output spectra,
`window.f32` and `cases.json` with the original Torch version/revision.
Cases cover lengths321–48000 including hop/pooling boundaries, real speech,
quiet/amplified speech, short/long silence, DC, ramp and impulse. Reference
waveforms are produced by the unmodified original model; export outputs must
never be used to generate the oracle.

Every original spectral wrapper and source-ordered pooling lowering is checked
bitwise against every original output spectrum. The adaptive pool preserves
the original sequential interval addition and the distinct output-size-one
native mean path. FFT640/hop160 remains in managed code. The official converter
produces a dynamic FP32 graph; the shared public-schema external-weight tool
deduplicates identical records without changing operators or values. Only the
public Net UUID is normalized deterministically.

The independent child checks CPU2/4, all25 conditions twice plus a return to the
first shape:104 inferences and208 separately checked source waveforms.
NRMSE must remain strictly below0.001 for each source without slot permutation.
Any missing output, nonfinite value, shape mismatch, source mismatch or abnormal
process exit fails the export. Logs, references and intermediate files remain
in the scratch output. Deploy only the final graph, external weights, verified
profile JSON, licenses and model documentation/metadata after Unity validation.

The real-valued spectral wrapper follows the original TIGER forward operations;
its MIT source notice is retained in `LICENSE.source.md`. DAVE source and
checkpoint distribution retain their separate original Apache-2.0 notices.

Current DAVE status: the pinned Torch CPU1 spectral and pooling lowerings match
all25 source cases bitwise, but ORT and MNN CPU/Metal fail the original full-PCM
0.001 gate at short479–481 sample boundaries. Even original Torch CPU4 differs
from original CPU1 on480 samples (spectrum NRMSE about0.12479). The DAVE profile
therefore remains an undeployed research candidate; its contract entry is not a
claim of a validated deployable model. No source threshold or minimum length was
changed to hide these failures.
