# MNN for Unity

Unity APIs and editor tools for [MNN](https://github.com/alibaba/MNN) 3.6.2:
tensor inference, language and multimodal models, retrieval, speech and image
generation, Model Manager, and Chat Studio. Requires Unity 2021.3 or later;
the tested Unity version is **2021.3.45f2**.

[简体中文](Packages/com.eitan.mnn/Documentation~/translations/README.zh-CN.md) · [Documentation](Packages/com.eitan.mnn/Documentation~/README.md) · [Changelog](CHANGELOG.md)

## Platform support

The table distinguishes implemented bindings from recorded runtime evidence.
`MNNPlatformSupport.IsSupported` checks the implemented ABI; it does not certify
a model, GPU, device, browser, or deployment configuration.

| Platform / runtime | Backend and task scope | Recorded validation and limits |
| --- | --- | --- |
| macOS 11+ ARM64, Mono Editor/Player | Core, Express, text/chat/streaming LLM, retrieval and media input; CPU/Metal by task | Selected Unity Player core/platform/Express tests: 120/120. Model pipelines have separate evidence. |
| macOS 11+ x64, Mono | Same compiler-specific task bindings | 120/120 selected Player tests under Rosetta; physical Intel hardware unverified. |
| Windows 10+ x64/UCRT, Mono and IL2CPP | Core, Express, LLM, retrieval and media input; bundled CPU/OpenCL library | Windows 11 / i7-10700F / RTX 3060: 157/157 selected tests per Player backend, including CPU and Auto. Other GPUs and pipelines need validation. |
| Linux x64, Mono | Core, Express, LLM, retrieval and media input; CPU | Container host Mono: 79 task and 22 core assertions. Linux Unity Player remains unverified. Requires GLIBC 2.38+ and GLIBCXX_3.4.32. |
| Android ARM64, IL2CPP | Core and LLM/media input; CPU, task-specific optional acceleration | Device/model results are scoped in the mobile guide. Updated Express/retrieval bindings have compilation/symbol checks, with device inference pending. |
| iOS 15+ ARM64, IL2CPP | Core and LLM/media input; CPU/Metal | Recorded iPhone 17 model checks; updated Express/retrieval device inference remains pending. Simulator symbol checks are not device validation. |
| WebGL wasm32, IL2CPP — experimental | CPU, one thread, Wasm SIMD; core tensor inference and four text-model adapters | Unity 2021.3.45f2 / Emscripten 2.0.19 / Chrome 157 only. Pocket 0.3B, Qwen2.5-0.5B, Qwen3-0.6B and MiniMind-O text generation verified. No Worker/thread, WebGPU, media, speech or Express certification. |
| macOS/Linux IL2CPP; Windows/Linux ARM64; Android ARMv7; other ABIs | No implemented inference contract | Rejected before inference. Bundled plugin files alone do not establish support. |

See [desktop Mono](Packages/com.eitan.mnn/Documentation~/DesktopMono.md), [Windows Players](Packages/com.eitan.mnn/Documentation~/WindowsPlayer.md),
[mobile ABI](Packages/com.eitan.mnn/Documentation~/MobileInterop.md), [WebGL contract](Packages/com.eitan.mnn/Documentation~/WebGLInterop.md),
and [test evidence](Packages/com.eitan.mnn/Documentation~/Testing.md) for the exact compiler, model and hardware scope.
Android uses NDK r25c; iOS uses the pinned Apple libc++ contract; Windows uses
MinGW-w64 GCC 16.2/libstdc++ and the bundled DLL, including for IL2CPP.

C# calls official C++ symbols directly under project-maintained compiler/STL ABI
contracts. There is no added C++ bridge or custom C export. This is **not an
official portable MNN C# SDK**: an arbitrary same-version native library is not
interchangeable with the bundled build. Native Omni waveform callbacks remain
unavailable; independent graph-based TTS pipelines retain their own model/platform
validation. CoreML/NPU and every-model GPU support are not claimed.

## Install

In Unity Package Manager, select **Add package from git URL**:

```text
https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#main
```

Use `#dev` for the development branch or a commit SHA for a reproducible install.
The package subdirectory is required; the old `upm` branch is not this snapshot.
Model weights are obtained separately under their own licenses.

Import **Basic Inference** from the package Samples tab. Check
`MNNPlatformSupport.UnsupportedReason` and `MNNVersion.IsLoaded()` before loading a
compatible model; library availability is not a model correctness check.

## Text generation and WebGL

Use the [unified API guide](Packages/com.eitan.mnn/Documentation~/PublicApiLayer.md) to choose a task and the
[generated API reference](Packages/com.eitan.mnn/Documentation~/PublicAPI.md) for signatures. Text applications can
load through `MNNModelLoading.LoadAsync` and await `GenerateAsync` or
`GenerateStreamingAsync`; conversation async APIs share the text path.

WebGL calls must originate on Unity's main-thread synchronization context.
Async generation yields **between synchronous native calls**; it does not create
a worker or interrupt an in-progress native call. Await or cancel a request before
reusing or disposing its model. Loading still blocks after its initial yield.
Stage complete model exports into the browser memory filesystem and apply the
[required WebGL linker flags](Packages/com.eitan.mnn/Documentation~/WebGLInterop.md#unity-build-settings).

For MiniMind-O text-only async generation (`GenerateSpeech=false`), the existing
prefill/decode graphs consume the complete prompt before sampling. The existing
512-token prompt-plus-budget limit remains. The full KV output is still checked
for NaN/infinity; a pinned managed scan removes the measured validation bottleneck.

On the recorded 119-prompt-token input, unchanged models/quantization and 16-token
budget, MiniMind's first text callback improved from 14.24 s to **3.07 s** and
wall from 16.44 s to **3.38 s**; the maximum generation frame gap was **162.5 ms**.
The pre-split reference was 2.20 s first callback / 4.40 s wall / 2175.1 ms frame gap:
first response still trades off against responsiveness. Loading remained about
679 ms. These observations do **not** establish a 50 ms frame bound, stable RSS
reduction, hardware-presented TTFT, other-browser support, or universal acceleration.
Synchronous `Generate` still uses full prefill and can block the main thread.
A fresh integrated-package Chrome check recorded 2.92 s first callback / 3.22 s
wall, with the same text and token count; the detailed guide retains both runs.

## Models and editor tools

- **Window > MNN > Model Manager** browses the shared model catalog and installs
  complete ModelScope exports with configuration, tokenizers and external weights.
  Runtime applications use `MNNModelCatalog` and `MNNModelDownloader`.
- **Window > MNN > Chat Studio** provides conversations, model tasks, image/WAV
  input, embeddings and reranking. Individual tasks have their own validation scope.
- Store build-time models under `Assets/StreamingAssets/MNN/Models`; runtime
  downloads use `Application.persistentDataPath/MNN/Models`. Browser staging and
  memory limits are separate concerns.
- Model weights, conversations, caches, generated media and test logs are excluded
  from Git. Tests reuse local fixtures and do not automatically download models.

See [model acquisition](Packages/com.eitan.mnn/Documentation~/ModelAcquisition.md), [model recommendations](Packages/com.eitan.mnn/Documentation~/ModelRecommendations.md),
[Chat Studio](Packages/com.eitan.mnn/Documentation~/ChatStudio.md), [acceleration](Packages/com.eitan.mnn/Documentation~/Acceleration.md), and
[project structure](Packages/com.eitan.mnn/Documentation~/ProjectStructure.md).

## Development and license

Read the [contribution guide](Packages/com.eitan.mnn/Documentation~/development/Contributing.md) and
[native ABI/build contracts](Packages/com.eitan.mnn/Native~/README.md). Tests and source-only checks have
separate evidence; historical results do not certify every later build.

Report reproducible issues on [GitHub Issues](https://github.com/EitanWong/com.eitan.mnn/issues).
Use the [security policy](Packages/com.eitan.mnn/Documentation~/community/Security.md) for vulnerabilities.
Licensed under [Apache License 2.0](LICENSE); model and optional-tool licenses remain separate.
