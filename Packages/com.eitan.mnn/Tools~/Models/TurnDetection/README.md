# Reproduce turn-model exports and comparisons

Use an official MNN Python installation with its converter, transformers, torch, ONNX,
ONNX Runtime, scipy, soundfile and the model-specific pinned source frontend. Install
additional dependencies inside the project's `TestArtifacts~/` directory. The tools do
not add native libraries, patch official MNN source, or establish a C# C++ ABI contract.

```sh
export MNN_PROJECT_ROOT=/absolute/path/to/com.eitan.mnn
export MNN_TURN_WORK_ROOT="$MNN_PROJECT_ROOT/TestArtifacts~/TurnDetection-20261116"
export MNN_OFFICIAL_EXPORT_ROOT=/absolute/path/to/MNN/transformers/llm/export
export PYTHONDONTWRITEBYTECODE=1
export PYTHONPATH="$MNN_TURN_WORK_ROOT/dependencies"
```

Models and pinned original sources live under `Assets/StreamingAssets/MNN/Models/`.
Work directories contain the saved independent source references and public audio clips.
The scripts intentionally fail if required inputs are absent; do not substitute random
weights or skip source/label checks to publish an export.

- SmartTurn: `benchmark_smart.py` compares FP32/FP16/W8/W4 at 1/2/4 threads.
- Bairong: `benchmark_bairong.py` uses the official frontend and floating ONNX oracle.
- LiveKit multilingual: `export_livekit.py`, then `benchmark_livekit.py`.
- LiveKit English: `benchmark_english.py` reuses existing verified FP32/FP16 exports and
  compares new W8/W4 against the pinned original floating ONNX probabilities.
- Easy-Turn: `export_easy_audio.py`, `export_easy_decoder.py`, `easy_reference.py`,
  `convert_easy_audio.py`, `convert_easy_decoder.py`, `benchmark_easy.py`,
  `benchmark_easy_extra.py`. The official source package is required under
  `dependencies/Easy-Turn`; its weights are in `Easy-Turn-Source/checkpoint.pt`.
- TEN: create the official LLM exporter graph template first, then
  `fill_ten_weights.py 4 TEMPLATE_DIRECTORY` or `8 TEMPLATE_DIRECTORY` with the official
  exporter root above. `ten_source_reference.py` produces the memory-bounded source
  oracle. Template creation alone is not a valid deployment: actual weights,
  untied embeddings, tokenizer and one-token source label parity are mandatory.
  `benchmark_ten.py TEMPLATE_DIRECTORY` compares 1/2/4 CPU threads; repeat with the
  INT8 directory to compare both real weighted exports against that source oracle.
- DualTurn: `prepare_dual_graph.py` lowers single-tick LSTMs, `normalize_dual_slices.py`
  makes startup clamping explicit, `benchmark_dual.py` compares precisions and
  `verify_dual_full.py` checks the selected FP32 graph on 17 streams. Both scripts assert
  the lowered ONNX output matches the original oracle before evaluating MNN.

`native_graph.py` runs the official Interpreter or Express Module Python API. Recurrent
subgraphs require Module; the published DualTurn graph lowers those subgraphs and is
executed by the existing Interpreter facility. A successful conversion is not sufficient:
check dynamic state shapes, startup and long history, posterior error and decision flips.

Publication uses `publish.py --model DEPLOYMENT --results PASSED_XML --tests METHOD ...
--repository OWNER/NAME --license LICENSE`. It rechecks every deployment hash and the
required passing Unity test methods, then uses the existing ModelScope SDK login.
Never place credentials in scripts or model assets. Preserve the original license; both
LiveKit exports require `--license other` and the original LiveKit Agents usage restriction.

After publication, verify remote hashes, pin its actual immutable commit in the shared
`MNNModelRegistry` profile, then run the real package installer download/load regression.
A local export or an empty remote repository must not be advertised as an installed,
published release. Reports are sample source-parity and CPU timing evidence, not full
accuracy, mobile/GPU qualification, or a globally optimal quantization claim.
