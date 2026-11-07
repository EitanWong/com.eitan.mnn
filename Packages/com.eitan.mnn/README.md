# MNN for Unity

Unity APIs and editor tools for MNN 3.6.2 inference, language and multimodal models,
retrieval, speech synthesis and image generation. Requires Unity 2021.3 or later;
the verified Unity version is 2021.3.45f2.

## Support and ABI

C# P/Invoke calls official C++ symbols in the bundled libraries, without additional
C++ sources, custom C exports or a companion bridge. **Core inference, Express, LLM and native retrieval
support desktop Mono on macOS ARM64/x64, Windows x64 and Linux x64.** Each target
uses its own pinned compiler ABI; see [desktop Mono](Documentation~/DesktopMono.md)
for the binaries, system requirements and evidence.

| Desktop Mono target | Implemented native binding | Current execution evidence |
| --- | --- | --- |
| macOS 11+ ARM64/x64 | Core inference and existing language/Express tasks, Apple clang / libc++ ABI v1 | ARM64 Unity Player; historical x64 Player under Rosetta |
| Windows x64 | Core, Express, LLM, retrieval and media input; MinGW-w64 GCC 16.2 | Production C# task APIs under Wine 9 / Wine Mono 9.4.0 |
| Linux x64 | Core, Express, LLM, retrieval and media input; GCC 13.3 | Production C# task APIs under host Mono in an amd64 Linux container |

Windows/Linux checks include real CPU Express, text/chat/streaming LLM, embedding,
reranking and Omni image input. Windows/Linux Unity Players and every model
pipeline have not been verified. Waveform callbacks remain unavailable. Linux requires GLIBC 2.38+ and a
libstdc++.so.6 providing GLIBCXX_3.4.32. Windows path handling follows the bundled
MNN FileLoader's ANSI code page and available DOS short paths; see the desktop guide.
This is a project-maintained compiler-specific binding, not an official portable
MNN C# SDK. An arbitrary same-version MNN library is not interchangeable with the
bundled build.

Interpreter/Session/Tensor and LLM text/chat/streaming/media input also have
**Android ARM64 IL2CPP (NDK r25c)** and **iOS 15+ ARM64 IL2CPP (Xcode 27.2)** bindings;
see [mobile ABI](Documentation~/MobileInterop.md). Metal model checks cover macOS
ARM64 Mono and iPhone 17 IL2CPP. Four actual mobile LLM/VLM/audio models pass CPU
checks on Android/iPhone and Metal checks on iPhone in the 2026-10-24 snapshot.
Updated mobile Express/retrieval paths have passed compilation and symbol audits,
with device inference still pending. Intel hardware and x64 GPU inference have
not been verified. Waveform callbacks are unavailable on all platforms; speech
requests fail before native execution. WebGL and desktop IL2CPP reject inference
before native calls. Current validation limits are recorded in the
[test evidence](Documentation~/Testing.md). CoreML/NPU support is not claimed.

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
- [Desktop Mono contracts and evidence](Documentation~/DesktopMono.md)
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
