# SudoRMRF U16 / WSJ0-2mix / 8 kHz

`prepare.py` reproduces the selected FP32 MNN graph and runtime profile. It verifies
the author's source and checkpoint digests before loading the serialized Module,
creates the original class, and loads every state-dict key strictly.

Pinned inputs:

- `etzinis/sudo_rm_rf` revision `cd00f2e21f7ad6281360cdf24ade36f84b0fbad6`;
  source `sudo_rm_rf/dnn/models/improved_sudormrf.py`.
- Zenodo record `6299852`, `Improved_Sudormrf_U16_Bases512_WSJ02mix.pt`;
  SHA-256 `33639f5c5dd846d053c99170078f60f95cab55a0606316b9efb9f5a321e975f2`.
- An official MNN converter plus matching Python MNN runtime, PyTorch,
  NumPy, ONNX and ONNX Runtime.

Run with local inputs and place all generated files under `TestArtifacts~/`:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 prepare.py \
  --source /absolute/path/to/pinned/source \
  --checkpoint /absolute/path/to/Improved_Sudormrf_U16_Bases512_WSJ02mix.pt \
  --converter /absolute/path/to/MNNConvert \
  --output /absolute/path/to/TestArtifacts~/SudoRmRfReproduction
```

The independent oracle calls the unmodified original model at eight raw lengths
from 2 through 32000 samples. Boundary normalization uses the original mean and
unbiased standard deviation. Zero padding to a multiple of 320 happens after
normalization. The graph lowers the global normalization into two sequential
reductions and preserves both original speaker slots. Original PyTorch, lowered
PyTorch, ONNX and MNN must agree below 0.001 relative waveform error per speaker.
The public `Net.mnn_uuid` field is made deterministic; official MNN source and
native libraries are unchanged.

Deploy only `sudormrf_fp32.mnn` and `mnn_sudormrf.json`, alongside the model README,
licenses, attribution, optimization record and manifest. Source weights, ONNX,
dependencies and reports stay outside the deployment directory. The graph input
is normalized padded PCM `[1,N]` with `N` divisible by 320, output `[1,N,2]`;
the managed API crops the raw length and restores each speaker's scale and mean.
FP16 storage and Q8 did not meet the original 0.001 waveform gate and are not
deployment profiles. A separate Unity test is required for every supported backend;
this reproduction command validates CPU only.
