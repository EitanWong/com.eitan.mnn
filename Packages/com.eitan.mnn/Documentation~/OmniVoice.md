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

| Backbone precision / steps | First synthesis | Reused synthesis | Warm RTF | Independent Qwen3-ASR transcript |
| --- | ---: | ---: | ---: | --- |
| High / 32 | 6.104 s | 5.471 s | 2.487 | 你好，欢迎来到Only Voice。 |
| Low / 32 | 4.714 s | 4.322 s | 1.965 | 你好，欢迎来到 Omni Voice。 |
| Low / 16 | 3.262 s | 2.192 s | 0.996 | 您好，欢迎来到Only Voice。 |

High / 32 is the default quality-first configuration. Low / 32 is available as
an explicit faster option. The 16-step sample changed a word, so step reduction
is not enabled by default. One ASR transcript does not establish speaker
similarity, prosody, multilingual quality, or artifact-free audio; listen to
the saved WAV files and evaluate the intended languages and voices before
changing production defaults. The original Python/MNN diagnostic and Unity XML
results are under `TestArtifacts~/OmniVoiceResearch/` in the development project.

The measured macOS result does not establish Android/iOS latency or quality.
Mobile deployment uses the package's pinned compiler/platform ABI contract in
[mobile interop](MobileInterop.md); test the actual Player and backend before
promoting it.
