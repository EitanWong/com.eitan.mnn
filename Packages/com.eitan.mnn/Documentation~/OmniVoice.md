# OmniVoice-MNN

`MNNOmniVoice` runs the official OmniVoice backbone and Higgs audio codec through
the package's existing, platform-gated MNN C++ Interpreter/Tensor bindings. The
managed code implements the model's full-attention iterative sampler and Qwen2
text tokenizer. No additional native bridge, custom operator, or modified MNN
library is needed.

## Model files

Put these seven files in one readable directory:

```text
omnivoice_lm.mnn            omnivoice_lm.mnn.weight
higgs_encoder.mnn           higgs_encoder.mnn.weight
higgs_decoder.mnn           higgs_decoder.mnn.weight
tokenizer.json
```

The converted files are installed locally in
`Assets/StreamingAssets/MNN/Models/OmniVoice-MNN/`. The three MNN external weights
total about 3.0 GiB. The original safetensors in the same local model asset
directory are conversion sources and are not needed by `MNNOmniVoice` at runtime.
Model weights and test outputs are ignored by Git. For Android, copy or deliver
the files to a real filesystem directory before calling `Load`; APK
`StreamingAssets` URLs cannot be passed to MNN's file loader. See
[model delivery](ModelDelivery.md).

The upstream [model card](https://www.modelscope.cn/models/k2-fsa/OmniVoice)
states that the pretrained weights use **CC-BY-NC**. Check that license before
shipping the weights in a product.

To reproduce the local conversion, use the pinned
[AFun9 ONNX exporter](https://github.com/AFun9/Omnivoice-onnx) at
`4ec8125833a6a2806ff2e07b31e73f106954aad0`, place the original ModelScope
checkpoint beside it as `OmniVoice/`, and run `export_lm.py --device cpu` and
`export_audio_tokenizer.py --device cpu`. Convert each `output/*/model.onnx`
with official MNN 3.6.1:

```sh
MNNConvert -f ONNX --modelFile output/omnivoice_lm/model.onnx --MNNModel omnivoice_lm.mnn --saveExternalData
MNNConvert -f ONNX --modelFile output/audio_tokenizer_encoder/model.onnx --MNNModel higgs_encoder.mnn --saveExternalData
MNNConvert -f ONNX --modelFile output/audio_tokenizer_decoder/model.onnx --MNNModel higgs_decoder.mnn --saveExternalData
```

Do not pass `--allowCustomOp`. Check graph outputs and a full generated WAV
before installing another checkpoint or converter version; the validation in
this report covers only the pinned local assets.

## Unity API

```csharp
string root = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "OmniVoice-MNN");
using (var model = MNNOmniVoice.Load(root, lmBackend: MNNBackendType.Metal))
{
    var reference = MNNReferenceAudio.FromWaveFile(referenceWavPath);
    var voice = model.CreateVoicePrompt(reference, "参考音频对应的文字。");
    MNNGeneratedAudio audio = model.Synthesize("你好，欢迎来到 OmniVoice。", 2.0f, voice);
    byte[] wav = MNNWaveAudio.EncodePcm16(audio.Waveform, audio.SampleRate);
}
```

`CreateVoicePrompt` encodes a supplied mono recording and transcript once; reuse
the returned prompt for subsequent synthesis. A clean 3-10 second reference is
recommended. Omit `voice` for an automatic voice; `instruction` supplies a voice
design prompt. The output is 24 kHz mono. `SynthesizeAsync` runs the same
pipeline on a worker thread, and both APIs accept cancellation between model
passes. Calls on one model instance are serialized.

High precision packs the conditional prompt and the shorter unconditional CFG
branch into one block-masked sequence by default. This uses the same
`omnivoice_lm.mnn` graph and weights, with no extra model file. Check
`UsesPackedCfg` after loading; pass `enablePackedCfg: false` to `Load` for the
original two-batch layout. Low precision retains that original layout because
packing changed some token predictions in local Low-precision tests.

An optional compact graph moves target projection, CFG, argmax and confidence
calculation into MNN, reducing full-logit readback and managed sampling work:

```csharp
using (var model = MNNOmniVoice.LoadCompact(root, lmBackend: MNNBackendType.Metal))
{
    // Reuse this instance and its encoded voice prompt across requests.
    MNNGeneratedAudio audio = model.Synthesize("你好，欢迎来到 OmniVoice。", 2.0f, voice);
}
```

Build/install `omnivoice_lm_compact.mnn` with the
[compact conversion tool](../Tools~/Models/OmniVoice/README.md). It adds only the
graph, sharing `omnivoice_lm.mnn.weight` through official `setExternalFile`.
`LoadCompact` fixes High precision and packed CFG; `UsesCompactSampling` reports
this opt-in. The normal `Load` path remains available for comparison. Floating
point graph reductions can differ from the managed double-precision sampler,
so verify complete speech on each intended backend and checkpoint.

The current API requires an explicit output duration of 0.4-15 seconds and
defaults to 32 iterative steps, CFG 2 and the upstream position sampler.
Long-form chunking, automatic reference transcription, and the upstream
silence-removal postprocessor are not implemented. Supply a trimmed reference
and split longer text into short requests. Each model instance holds large
weights; dispose it when finished.

## Measured performance and quality

Apple M5, macOS Unity 2021.3.45f2 Editor/Mono, MNN 3.6.1, four threads,
2-second target, one Chinese voice-clone case. The CPU Higgs encoder and decoder
were faster than Metal; the backbone used Metal. RTF uses the resulting 2.2-second
waveform as its denominator. Each row is one run; warm timing repeats synthesis
on the same loaded model and excludes reference encoding and ASR.

The following earlier measurements used the original two-batch CFG layout:

| Backbone precision / steps | First synthesis | Reused synthesis | Warm RTF | Independent Qwen3-ASR transcript |
| --- | ---: | ---: | ---: | --- |
| High / 32 | 6.104 s | 5.471 s | 2.487 | 你好，欢迎来到Only Voice。 |
| Low / 32 | 4.714 s | 4.322 s | 1.965 | 你好，欢迎来到 Omni Voice。 |
| Low / 16 | 3.262 s | 2.192 s | 0.996 | 您好，欢迎来到Only Voice。 |

Two same-process Unity Editor comparisons measured High / 32 at 5.881 and
6.152 s warm with the original layout (RTF 2.673 and 2.797), versus 4.255 and
4.715 s with the default packed layout (RTF **1.934 and 2.143**). These are
23-28% wall-time reductions in paired runs. The generated waveforms were
sample-identical for seed 47. With seed 13 and a second Chinese
sentence, both layouts gave the same independent ASR transcript, although
Python PCM correlation was 0.964 rather than exact. Python MNN/Metal with the
same original graph and packed inputs measured RTF 1.729 on the first sentence.
These are individual measurements on this Mac, not a cross-platform latency
or perceptual-quality guarantee.

A Unity Metal System Trace of a further paired High / 32 run measured legacy
RTF 2.643 and packed RTF 1.948 with identical PCM. During sustained synthesis,
Unity's depth-zero Compute intervals occupied about 92-93% of wall time on this
Mac. The trace does not identify individual MNN shaders. Official converter
experiments with `--optimizePrefer 2` and `--transformerFuse` preserved logits
but did not improve the 200-position warm Metal pass; 8-bit weight compression
reduced size without improving pass time and changed token predictions. The
installed model and High-precision default therefore remain unchanged. See the
research report under `TestArtifacts~/OmniVoiceResearch/` for measurements.

The official MNN 3.6.1 Metal Tensor matrix probe also fails to compile on the
tested Apple M5 / macOS 27.0.1 setup, so MNN disables that path. Unity tests
of all five official Metal tuning modes produced identical logits, but their
timing order reversed when the run order was reversed. No tuning mode has a
verified speed advantage over the current Wide default on this machine.

An isolated macOS 26.2 deployment-target build could not run the dynamic
OmniVoice graph, so it does not establish a usable faster matrix path.

High / 32 with packed CFG is now the default. Low / 32 remains available as
an explicit alternative. The 16-step sample changed a word, so step reduction
is not enabled by default. Two ASR transcripts do not establish speaker
similarity, prosody, multilingual quality, or artifact-free audio; listen to
the saved WAV files and evaluate the intended languages and voices before
changing production defaults. The original Python/MNN diagnostic and Unity XML
results are under `TestArtifacts~/OmniVoiceResearch/` in the development project.

The measured macOS result does not establish Android/iOS latency or quality.
Mobile deployment uses the package's pinned compiler/platform ABI contract in
[mobile interop](MobileInterop.md); test the actual Player and backend before
promoting it.

## CosyVoice3-MNN comparison and mobile scope

Studied [CosyVoice3-MNN](https://github.com/Nian27/CosyVoice3-MNN/tree/b4c2fafde21d1f138bfcd0ccaf7ddd9dcca2511a).
Its principal gains require CFG single-branch and 10-to-2-step distillation.
OmniVoice uses iterative bidirectional masked-token prediction, so ordinary
causal KV caching is invalid and flow trajectory distillation is not a drop-in
conversion. Reducing steps or removing CFG without training changes the model.
Persistent sessions, reusing reference encodings, per-stage backend selection,
and avoiding unnecessary host readback are applicable without retraining.

The compact graph was tested with official MNN 3.6.1 in the same Mac Unity
Editor, 32 steps and a 2-second target (2.2 seconds including padding). Each row
is a paired warm run, excluding model loading and reference encoding:

| Case / seed | Normal packed | Compact | RTF normal → compact | Max PCM difference |
| --- | ---: | ---: | ---: | ---: |
| Chinese clone / 47 | 4.006 s | 3.814 s | 1.821 → 1.734 | 0 |
| Second Chinese clone / 13, reversed order | 3.953 s | 3.734 s | 1.797 → 1.697 | 0 |
| Automatic voice / 29 | 2.301 s | 2.142 s | 1.046 → 0.974 | 0 |

An earlier two-case run also reduced time by 5.2–6.9%, with identical PCM.
Three exact examples do not establish universal speech quality. A random
200-position packed diagnostic reduced readback from 6,560,000 to 3,200 bytes,
with all codes matching and maximum confidence difference below 1.83e-5;
neural-pass time changed by less than 1%. The benefit is mostly reduced
managed work, not a faster transformer.

FP16 weight serialization reduced the combined backbone graph/weights from
2.453 GB to 1.228 GB, but did not speed up either Mac Metal or the tested Android
CPU pass, and changed about 1.5% of diagnostic argmax predictions. It is not
installed as the quality-first default.

On a connected PELM00 / MT6853 Android device, Python MNN Module with
OpenCL requested measured 3.552 → 3.476 seconds per warm backbone pass with
all diagnostic codes matching. That small single-run difference is not a
verified stable speedup. CPU FP16 storage measured 10.309 → 10.352 seconds,
also giving no speed benefit. These are single-pass diagnostics, without a full
mobile synthesis or Unity Player ABI validation; they do not establish real-time
mobile speech. The iPhone was offline.

Mobile measurements and remaining limitations are recorded in the development
project at `TestArtifacts~/OmniVoiceResearch/CosyVoice3Comparison/REPORT.md`.
The compact path is an optional measured optimization, not a claim of smooth
mobile playback or a mobile Unity Player validation. Reliable real-time mobile
speech still needs target-device profiling and likely training-based reduction
of repeated backbone computation, with separate quality evaluation.
