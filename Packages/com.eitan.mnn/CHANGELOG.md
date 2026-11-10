# Changelog

Notable package changes are recorded here using [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
The package version is 3.6.2.

## [3.6.2] - 2026-11-01

- Updated the bundled official MNN native libraries and compiler ABI contract to upstream tag 3.6.2.
- Added owned graph adaptation for legacy Qwen3 Embedding causal Attention exports and Apple Qwen3.5 CPU precision defaults.
- Revalidated macOS arm64/x86_64, Android ARM64 and iOS ARM64 Players; mobile and desktop model evidence is recorded under `TestArtifacts~/` and `Documentation~/MNN362Upgrade.md`.

## [Unreleased]

### Added

- Unified managed API contracts for offline model inspection, pinned revisions, curated sources and runtime initialization. Studio now uses public APIs without Runtime friendship; semantic checks enforce component/Core/Interop boundaries and generate the public API reference. See [API layers](Documentation~/PublicApiLayer.md).

- Added the official MNN-Pocket 0.3B family catalog, pinned W4A16 deployment plan,
  `.mtok` tokenizer and shared conversation alias. SiriAI now defaults to Pocket;
  source-only/GGUF/Dec entries retain their format requirements. See
  [Pocket integration](Documentation~/MNNPocket.md).

- Added Windows x86_64 and Linux x86_64 Mono Interpreter/Session/Tensor, Express,
  LLM text/chat/streaming, native embedding/reranking and Omni media-input bindings.
  Added matching plugin import settings and reproducible audited GNU builds from
  unchanged official MNN source. See [desktop Mono](Documentation~/DesktopMono.md)
  for compiler pins, OS/runtime requirements, Windows path handling and test scope.

- Managed language, multimodal input, conversation streaming, Qwen3 embedding and reranking APIs.
  Native Omni waveform callbacks remain unavailable; speech-output requests are rejected before native execution.
- SD 1.5 image generation; Supertonic, Bert-VITS2 Chinese and Piper English speech synthesis.
  Piper uses upstream eSpeak-NG; Sana editing runs but has not met the semantic color-edit quality check.
- Chat Studio with conversation history, folders, search, media, playback/export and 23 task entries.
  Unimplemented dedicated pipelines provide model preparation guidance.
- Complete ModelScope MNN repository catalog, background downloads, parallel transfers and pause/resume.
- Editor/Player test assemblies, deterministic affine fixture, offline model runners and package layout CI.

### Changed

- SenseVoice now retains centered training-frontend boundary windows; Paraformer keeps its causal streaming windows. The verified ModelScope profile reduces greedy IDs inside an in-memory graph using the public MNN ArgMax operator, avoiding full vocabulary output copies to managed memory.
- Audio catalogs and Model Manager now separate VAD, turn detection, enhancement, source separation, target speaker extraction, diarization and sound classification, with shared search aliases and offline migration of older cached categories.
- Current managed interop calls official MNN 3.6.2 C++ symbols under pinned desktop
  Mono compiler contracts: Apple clang/libc++ on macOS ARM64/x64, MinGW-w64 GCC 16.2
  on Windows x64 and GCC 13.3 on Linux x64. Android/iOS ARM64 IL2CPP use separate
  [mobile contracts](Documentation~/MobileInterop.md). No new C++ sources, custom
  C exports, companion bridge or official source modifications are introduced.
  This is not an official portable MNN C# SDK.
- Auto acceleration prefers available Metal with CPU fallback and task-specific compatibility policies.
  Windows/Linux native builds use CPU only. Embedding, Piper, Sana and the Bert-VITS2
  generator retain CPU compatibility policies; native Omni waveform output is unsupported.
  CoreML/NPU support is not claimed.
- Runtime/Core is organized by domain; editor features, safe handles, tests, tooling and samples
  have explicit directory and assembly ownership while public namespaces remain stable.
- Installation uses the repository package subdirectory; README, API, Studio, test and maintenance
  documentation describe current behavior and limits.
- Ignore rules exclude local models, caches, build/test artifacts and credentials while retaining
  required native plugins, Unity metadata and the deterministic test fixture.

### Fixed

- Added a checked SiriAI migration that prevents Unity and EasyMic frame updates from reopening capture during recording-tail drainage and marking complete recordings as missing samples.
- Published Silero v5.1 is available to runtime catalog consumers; online discovery requires its deployment profile and FP32 graph.
- Matching realtime and offline Silero consumers now share reference-counted inference resources while retaining independent stream state; resources unload with the last owner.

- Compiler ABI differences between arm64 and x86_64 Players for libc++ strings, callbacks and allocation.
- Piper executable/data-directory discovery and invalid saved dictionary paths.
- GPU quality regressions through precision retry and task-specific CPU compatibility policies.

### Validation scope

- Current Windows Wine 9 / Wine Mono 9.4.0 and Linux container host-Mono checks each
  pass 79 advanced task assertions and 22 core assertions. They use real native
  libraries and model inputs, but do not establish Windows/Linux Unity Player support.
- Current macOS ARM64 and x64 Rosetta Unity 2021.3.45f2 Mono Players each pass
  120/120 selected core/platform/Express tests, including CPU and Auto/Metal affine
  inference. Focused Editor checks pass 14/14; 13 conditional compilation cases and
  nine package assemblies compile. Optional Burst is absent; physical Intel Mac
  hardware and Windows/Linux GPU inference have not been verified.
- Recorded arm64 Metal/CPU compatibility regressions cover 97 distinct Editor tests across two runs
  and 116 Player tests. Counts include API/platform checks, not only model tests.
- Desktop IL2CPP, Windows/Linux ARM64 and WebGL reject inference; mobile ARM64
  IL2CPP retains its separate task/device evidence. Conditional compilation does
  not establish native inference or every model's compatibility. See [test evidence](Documentation~/Testing.md).

## [3.6.1] - 2024-10-08

Initial package scaffolding and native plugins for multiple Unity platforms.
Earlier release notes described broad platform and acceleration support. Those historical packaging
claims do not establish support for the current managed C++ binding; use the current
[package README](README.md), [ABI contract](Native~/README.md) and [tests](Documentation~/Testing.md).

For upstream engine changes, see [MNN releases](https://github.com/alibaba/MNN/releases).
