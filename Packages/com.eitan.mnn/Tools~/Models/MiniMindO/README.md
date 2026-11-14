# MiniMind-o preparation

Run with a Python environment providing the versions in `source-lock.json` plus
NumPy, safetensors, transformers' tokenizer dependencies, soundfile/librosa and soxr.
No Python dependency is bundled in the UPM runtime. The preparation script uses only
Python and official MNN conversion tools; it writes no C++ and builds no native bridge.

From the Unity project root:

```sh
python Packages/com.eitan.mnn/Tools~/Models/MiniMindO/prepare.py \
  --reference Assets/StreamingAssets/MNN/Models/MiniMindO-113M-Reference \
  --work TestArtifacts~/MiniMindOPreparation \
  --output Assets/StreamingAssets/MNN/Models/MiniMindO-113M-MNN \
  --download
```

Existing inputs are reused after SHA256 checks. Omit `--download` for an offline run;
this also requires the pinned `sensevoice_model.py` in the work directory. It is
retrieved from the FunASR commit in `source-lock.json`. The exporter loads only its
encoder classes and strictly loads the released encoder state; it does not import
FunASR's unrelated training/inference dependencies or edit upstream source.

`export_core.py` checks ONNX against the released core. `convert_core.py` generates
an official compression guide, requires exactly 56 selected Thinker projections,
keeps every other decoder projection in FP16, and retains FP16 prefill.
`export_components.py` checks audio/image projections, multimodal injection and Mimi
against PyTorch, including multiple sequence lengths. `convert_components.py` uses
isolated working directories because the converter creates shared temporary files.
`check_components.py` compares converted output shapes, finite values, cosine and
signal-to-noise ratio against ONNX. These thresholds check numerical conversion,
not application quality. `executions.jsonl` records conversion commands and literal
stdout/stderr/status. Final assets get `files.sha256.json`.

Verify an existing installation without inference, downloads or conversion:

```sh
python Packages/com.eitan.mnn/Tools~/Models/MiniMindO/prepare.py \
  --reference Assets/StreamingAssets/MNN/Models/MiniMindO-113M-Reference \
  --work TestArtifacts~/MiniMindOPreparation \
  --output Assets/StreamingAssets/MNN/Models/MiniMindO-113M-MNN \
  --verify-only
```

Preparation replaces generated graphs in the chosen output directory. Close active
inference on that installation before rebuilding. Interrupted runs can be restarted;
source hashes and conversion checks are repeated. Inputs and outputs are kept separate.
Large ONNX and converter intermediates are rebuildable; retain logs/check reports and
source/model manifests when reclaiming their disk space. Model redistribution remains
subject to each upstream model's license and terms.

## Publish an accepted deployment

Both Model Manager and runtime discovery must resolve the dedicated MiniMind-o
adapter before publishing. Run `MiniMindOCatalogTests` and
`MiniMindOIntegrationTests` against the installed assets (these fixtures are explicit).
Then, with Python 3.11+ and a logged-in official ModelScope SDK:

```sh
python publish.py --model PROJECT/Assets/StreamingAssets/MNN/Models/MiniMindO-113M-MNN \
  --results PROJECT/TestArtifacts~/minimind-results.xml
```

The publisher verifies all 14 payload hashes, required discovery/native-runtime
cases, and zero failing/skipped tests. It uploads deployment files and model card,
excluding Unity metadata. After publishing, pin the actual commit in
`MNNMiniMindO.Revision` and run `MiniMindOModelScopeTests` explicitly: it downloads
through `MNNModelInstallation`/`MNNModelDownloader`, compares all 14 payloads with
the validated export, and runs the discovered runtime provider on downloaded bytes.
