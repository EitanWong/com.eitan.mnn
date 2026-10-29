# OmniVoice CFG and discrete trajectory distillation

This is a bounded research pipeline for Chinese zero-shot voice cloning. It
trains actual parameters of the local original checkpoint, then tests on texts
that were excluded from training and checkpoint selection. It does not install
a student in Unity or change production inference defaults.

The first experiment uses one existing reference recording and teacher-generated
speech, with two training, two validation and two test texts. This checks the
training mechanism and exposes quality regressions; it cannot establish unseen
speaker or language generalization. A good loss curve is not a speech-quality
acceptance result.

## Objectives and data shape

OmniVoice predicts eight discrete codebooks with 1,025 classes, including a mask
class. It is bidirectional; inference must pass an explicit full-attention
mapping to Qwen3 and disable KV caching. A causal LM forward or a continuous
flow-velocity MSE objective would train a different task.

- CFG stage: at the same masked state, minimize codebook-weighted KL between
  single-branch student logits and teacher logits `3*conditional - 2*unconditional`.
  Only masked target positions are supervised. All 1,025 classes participate in
  normalization. Training uses weights `[8,8,6,6,4,4,2,2]`.
- Trajectory stage: pair two original teacher updates, starting from the first
  state and supervising the codes newly committed after the second update.
  Add `0.2 * CFG KL` to regularize predictions at that starting state. This is
  discrete endpoint imitation, not an implemented consistency/flow algorithm.
- Inference keeps the original confidence and Gumbel position selector. Argmax
  excludes class 1,024, confidence includes it. This experiment does **not** train
  a separate position policy; teacher-forced endpoint loss can therefore improve
  while free-running generation worsens.
- Group the teacher's integer unmask budgets in pairs for 32→16 inference. Simply
  recomputing a 16-step schedule changes ceil rounding and is a different ablation.
  This schedule is implemented only in the research evaluator, not the Unity API.

The trainable subset is the last two transformer layers, final norm and audio
head (39,859,712 parameters of the local 612,577,280-parameter checkpoint).
Embeddings and the remaining backbone are frozen. No new dependency, LoRA
implementation, native bridge or modified MNN is required for this subset.

## Reproduce locally

Use the original local checkpoint and upstream OmniVoice checkout at
`08be0b4ccbac3e13e374e86fbfead4b4cac343e2`. Tested dependencies already existed in
`TestArtifacts~/OmniVoiceResearch/.venv`: Python 3.11, PyTorch 2.8.0,
Transformers 5.5.4, NumPy, SoundFile, safetensors and official MNN 3.6.1.
Use a compatible isolated environment; all model loading is local-only.
Artifacts and dependencies belong under `TestArtifacts~/`, model assets under
`Assets/StreamingAssets/MNN/Models/`.

From the repository root, use these task-specific shell variables:

```sh
ov_tools='Packages/com.eitan.mnn/Tools~/Models/OmniVoice/Distillation'
ov_run='TestArtifacts~/OmniVoiceResearch/Distillation'
ov_python='TestArtifacts~/OmniVoiceResearch/.venv/bin/python'
ov_model='Assets/StreamingAssets/MNN/Models/OmniVoice-MNN'
ov_source='TestArtifacts~/OmniVoiceResearch/upstream'
export HF_HUB_OFFLINE=1 TRANSFORMERS_OFFLINE=1 PYTHONDONTWRITEBYTECODE=1

"$ov_python" "$ov_tools/test_objectives.py"
OMNIVOICE_UPSTREAM="$ov_source" "$ov_python" "$ov_tools/test_modeling.py"

"$ov_python" "$ov_tools/codec.py" encode --model "$ov_model" \
  --input "$ov_model/zero_shot_prompt.wav" --output "$ov_run/reference.pt"

"$ov_python" "$ov_tools/collect.py" --upstream "$ov_source" --model "$ov_model" \
  --reference "$ov_run/reference.pt" --reference-text '希望你以后过的比我还好哟！' \
  --cases "$ov_run/cases.json" --output "$ov_run/teacher.pt" --device mps

"$ov_python" "$ov_tools/train.py" --upstream "$ov_source" --model "$ov_model" \
  --cache "$ov_run/teacher.pt" --output "$ov_run/cfg" --device mps --steps 128

"$ov_python" "$ov_tools/train.py" --upstream "$ov_source" --model "$ov_model" \
  --cache "$ov_run/teacher.pt" --output "$ov_run/trajectory" --device mps \
  --initial-patch "$ov_run/cfg/best.pt" --objective trajectory \
  --steps 128 --learning-rate 1e-5

"$ov_python" "$ov_tools/evaluate.py" --upstream "$ov_source" --model "$ov_model" \
  --cache "$ov_run/teacher.pt" --cfg-patch "$ov_run/cfg/best.pt" \
  --trajectory-patch "$ov_run/trajectory/best.pt" \
  --output "$ov_run/evaluation.pt" --device mps

"$ov_python" "$ov_tools/codec.py" decode --model "$ov_model" \
  --input "$ov_run/evaluation.pt" --output "$ov_run/wav"
```

`cases.json` is an array of `{ "id": "train-a", "split": "train", "text":
"你好。", "seconds": 2.4, "seed": 47 }`. Supply `train`, `validation` and `test`
rows with distinct texts. The ignored local six-case manifest is documented in
the research report; callers can use their own texts. Collection and training
refuse existing experiment outputs, so a failed/repeated trial cannot silently
replace a trace or checkpoint. Use a new run directory. Encoding the same
reference is deterministic. Conversion weights and the original checkpoint
are never written by these scripts.

The cache retains float32 teacher logits and requires substantial memory:
about 380 MB for the six short examples. This initial in-memory loader is not a
large-corpus trainer. Formal runs need streamed shards and multiple references
before scaling. `best.pt` contains only trained tensors plus the original
checkpoint SHA-256. Validation, including the initial model, selects the best
checkpoint; test data is used only afterward. Future changes to datasets/losses
require a new untouched test set.

The evaluator compares teacher32, unguided/untrained single32, undistilled
teacher16, CFG32, CFG16 and trajectory16. It reports free-running token agreement
and **LM-only PyTorch** time against unpadded codec duration. Decode and model
loading are excluded. These numbers are not MNN speed, Unity RTF or mobile
measurements, and evaluation order is not reversed for a stable benchmark.

## Independent audio metrics in Unity

`QualityProbe.cs` is an Editor-only standalone harness, ignored inside `Tools~`.
Copy it into an isolated Unity project's `Assets/DistillationQuality/` folder
alongside an asmdef with name `OmniVoice.DistillationQuality`, references
`["MNN.Unity"]` and includePlatforms `["Editor"]`. Use the package as a local
file dependency. Do not copy test models or this probe into a shipping Player.

With the existing local Qwen3-ASR and ERes2NetV2 model folders installed, set
`MNN_MODELS_DIRECTORY` to the absolute model root,
`OMNIVOICE_DISTILLATION_QUALITY_DIRECTORY` to the decoded WAV directory, and
`MNN_INFERENCE_CACHE_DIRECTORY` to a directory under `TestArtifacts~/`. Execute
Unity in batch mode with `-executeMethod QualityProbe.Run`. The harness writes
`quality.json` beside the WAVs, using public `RecognizeWaveFile` and
`EncodeWaveFile` APIs. No model is automatically downloaded. This measures ASR
and speaker proxies; it does not automate human listening or mobile validation.

## Production path and acceptance

For a real zero-shot student, use licensed multi-speaker Chinese data and split
by speaker as well as utterance. Held-out speakers still provide reference audio
at inference; their recordings must not have been used to train the student.
Vary reference length, target length, seeds, numbers, punctuation, voice and
speaking style. Cover student-generated states through teacher relabeling after
an initial offline pass to reduce distribution shift.

Evaluate transcripts/CER with an independent ASR, reference speaker similarity,
prosody/artifacts with blind listening, failure rates and free-running long-form
behavior against the exact original teacher. Compare CFG32 first, then 16 and 8
steps, with the untrained/undistilled controls. Reject a candidate that loses
quality even if it is faster. A single reference, teacher-state KL or audio-token
agreement alone does not meet this gate.

After quality passes, merge the patch into an isolated checkpoint, export through
the existing ONNX full-attention path and official MNN converter, then validate
numerical and complete waveform behavior again. A single-branch student must
have its own sampler/manifest contract: do not feed it into `LoadCompact`, which
still applies CFG to two branches. No distilled checkpoint is deployed by this
research tool. Measure final Unity Player RTF, first-audio latency, peak memory,
GPU placement, power and sustained thermal behavior on actual phones before
claiming real-time operation.

## First experiment result

The local six-text PoC ran both 128-update stages. CFG validation KL fell from
0.923 to 0.540; untouched-test teacher-state KL fell from 0.968 to 0.555. The
trajectory stage selected update 64 by validation. All 12 held-out WAVs were
transcribed correctly by the existing independent Qwen3-ASR model, including
untrained controls. Thus these texts do not demonstrate a transcription gain
from distillation.

| Held-out path | Sentence A LM | Sentence B LM | Speaker cosine A | Speaker cosine B |
| --- | ---: | ---: | ---: | ---: |
| Original CFG / 32 | 4.507 s | 4.239 s | 0.679 | 0.769 |
| Untrained single / 32 | 2.961 s | 2.904 s | 0.592 | 0.658 |
| CFG student / 32 | 2.972 s | 2.897 s | 0.733 | 0.619 |
| Original CFG / 16 | 2.240 s | 2.105 s | 0.755 | 0.683 |
| CFG student / 16 | 1.480 s | 1.451 s | 0.778 | 0.681 |
| Endpoint student / 16 | 1.547 s | 1.507 s | 0.764 | 0.615 |

These are individual M5 PyTorch MPS LM runs with two sequential branches for the
teacher. They exclude codec, reference encoding and loading; do not compare
them directly with the packed MNN graph. Speaker cosine uses the local MNN
ERes2NetV2 encoder against the original reference and is a proxy, not a listening
score. Sentence A/B use different text/seed but the same reference speaker.

The second stage did not outperform the simpler CFG16 control in speaker cosine
on either test utterance. The student failed to establish quality noninferiority;
it remains experimental. The next useful change is multi-speaker data and
student-state teacher relabeling, followed by position-selection supervision if
needed. More updates on these same two training sentences would not resolve the
zero-shot coverage gap. Do not install these patches as the production model.
Full logs, checkpoints, WAVs and the comparison report are in the development
project's `TestArtifacts~/OmniVoiceResearch/Distillation/` directory.
