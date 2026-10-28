# OmniVoice on MNN

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
`Assets/StreamingAssets/MNN/Models/OmniVoice/`. The three MNN external weights
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
string root = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "OmniVoice");
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
