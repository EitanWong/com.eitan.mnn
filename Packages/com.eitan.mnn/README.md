# MNN for Unity

Unity APIs and editor tools for MNN 3.6.2 inference, language and multimodal models,
retrieval, speech synthesis and image generation. Requires Unity 2021.3 or later;
the verified Unity version is 2021.3.45f2.

## Support and ABI

C# P/Invoke calls official C++ symbols in the bundled library, without additional
C++ sources, custom C exports or a companion bridge. The binding targets **MNN 3.6.2,
Apple clang / libc++ ABI v1, macOS 11+, 64-bit Mono** for language/Express tasks.
Interpreter/Session/Tensor and LLM text/chat/streaming/media input also have
**Android ARM64 IL2CPP (NDK r25c)** and **iOS 15+ ARM64 IL2CPP (Xcode 27.2)** bindings; see [mobile ABI](Documentation~/MobileInterop.md). STL marshalling, aggregate
returns depend on that exact compiler contract. This is a
project-maintained binding, not an official portable MNN C# SDK. An arbitrary
same-version MNN library is not interchangeable with the bundled build.

CPU inference has been verified in arm64 Editor/Player and an x86_64 Player under
Rosetta on Apple Silicon. Intel hardware and x86_64 GPU inference have not been
verified. Metal model checks cover macOS arm64 Mono and iPhone 17 IL2CPP. Four actual
mobile LLM/VLM/audio models pass CPU checks on Android/iPhone and Metal checks on
iPhone in the 2026-10-24 snapshot. Express ownership and native Embedding/Reranker
now share the desktop partials; their updated mobile paths have passed compilation
and symbol audits, with device inference still pending. Waveform callbacks are
unavailable on all platforms; speech requests fail before native execution. Windows, Linux, WebGL and macOS IL2CPP
reject inference before native calls; retained native plugins do not imply managed
inference support. Current mobile validation limits are recorded in the [test evidence](Documentation~/Testing.md). CoreML/NPU support is not claimed.

## Install

In Unity Package Manager, select **Add package from git URL**:

```text
https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#dev
```

The repository contains a development project, so the package subdirectory is
required. Use `#main` for merged code or an existing commit SHA for a reproducible
installation. The old `upm` branch does not contain this development snapshot.

## Check the native library

```csharp
using MNN.Unity;
using UnityEngine;

public class MNNExample : MonoBehaviour
{
    private void Start()
    {
        if (!MNNPlatformSupport.IsSupported)
        {
            Debug.LogError(MNNPlatformSupport.UnsupportedReason);
            return;
        }

        if (MNNVersion.IsLoaded())
            Debug.Log($"MNN Version: {MNNVersion.GetVersion()}");
        else
            Debug.LogError("MNN failed to load");
    }
}
```

This checks availability, not model correctness. Import **Basic Inference** from
Package Manager for graph/tensor examples, then supply a compatible local model.

## Models and editor tools

Open **Window > MNN > Model Manager** to browse the public ModelScope MNN catalog
and download complete repositories with configuration, tokenizer and external
weights. Downloads support background work, pause/resume and parallel transfers.
Use `Assets/StreamingAssets/MNN/Models` for models included in an application build
and `Application.persistentDataPath/MNN/Models` for runtime downloads.

Upper layers can query and acquire models through the runtime `MNNModelCatalog`
and `MNNModelDownloader` APIs, including progress, cancellation and resumable transfers.
See [model acquisition API](Documentation~/ModelAcquisition.md) for examples.

Open **Window > MNN > Chat Studio** for streaming conversations, image/WAV inputs,
search, folders, embeddings and reranking. Its task entries
include SD 1.5 image generation and Supertonic, Bert-VITS2 Chinese and Piper English
speech synthesis. Piper requires upstream eSpeak-NG. Sana editing runs, but semantic
color editing has not met the quality check. Tasks without a dedicated runtime show
model preparation guidance; full-duplex live audio and Sherpa streaming ASR are not
implemented. The local CosyVoice-300M pipeline is available through
`MNNCosyVoice300M` and Studio; see [CosyVoice-300M](Documentation~/CosyVoice300M.md)
for asset preparation and validation scope. See [Chat Studio](Documentation~/ChatStudio.md)
for task limits.

**DTLN AEC 128 / 256 / 512:** search `DTLN`, `AEC` or `回声消除` in Model Manager,
then open the installed model in Studio's **Echo cancellation (AEC)** task. Supply
synchronized 16 kHz microphone and playback-reference WAVs. The package includes
a C# FFT with an optional Burst adapter; no extra DSP library is required. All three
profiles use CPU/High. See [DTLN AEC](Documentation~/DtlnAec.md) for streaming APIs,
performance measurements and the verified platform scope.

Auto acceleration prefers available Metal with task-specific CPU compatibility.
Embedding, Piper, Sana and the Bert-VITS2 generator use CPU. Backend reports are
not evidence that every operator runs on the GPU.

Model weights, local conversation history, caches, generated media and test logs
are excluded from source control. Tests do not automatically download large models.

## Documentation

- [Documentation index](Documentation~/README.md)
- [API reference](Documentation~/API.md)
- [Reusable AI application architecture](Documentation~/ConversationApplicationArchitecture.md)
- [DTLN AEC and optional Burst FFT](Documentation~/DtlnAec.md)
- [Acceleration and fallback](Documentation~/Acceleration.md)
- [Inference evidence and reproduction](Documentation~/Testing.md)
- [Native build and ABI assumptions](Native~/README.md)
- [Project structure and conventions](Documentation~/ProjectStructure.md)
- [Repository maintenance and ignored files](Documentation~/RepositoryManagement.md)
- [Basic example](Samples~/BasicExample/README.md)
- [Changelog](CHANGELOG.md)

Licensed under [Apache License 2.0](LICENSE.md).
