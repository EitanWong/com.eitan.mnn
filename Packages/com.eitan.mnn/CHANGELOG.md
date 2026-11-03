# Changelog

Notable package changes are recorded here using [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
The package version is 3.6.2.

## [3.6.2] - 2026-11-01

- Updated the bundled official MNN native libraries and compiler ABI contract to upstream tag 3.6.2.
- Added owned graph adaptation for legacy Qwen3 Embedding causal Attention exports and Apple Qwen3.5 CPU precision defaults.
- Revalidated macOS arm64/x86_64, Android ARM64 and iOS ARM64 Players; mobile and desktop model evidence is recorded under `TestArtifacts~/` and `Documentation~/MNN362Upgrade.md`.

## [Unreleased]

### Added

- Managed language, multimodal, conversation streaming, Omni speech output, Qwen3 embedding and reranking APIs.
- SD 1.5 image generation; Supertonic, Bert-VITS2 Chinese and Piper English speech synthesis.
  Piper uses upstream eSpeak-NG; Sana editing runs but has not met the semantic color-edit quality check.
- Chat Studio with conversation history, folders, search, media, playback/export and 23 task entries.
  Unimplemented dedicated pipelines provide model preparation guidance.
- Complete ModelScope MNN repository catalog, background downloads, parallel transfers and pause/resume.
- Editor/Player test assemblies, deterministic affine fixture, offline model runners and package layout CI.

### Changed

- Current managed interop calls official MNN C++ symbols under the bundled MNN 3.6.2,
  Apple clang / libc++ ABI v1, macOS 11+, 64-bit Mono contract. No new C++ sources,
  custom C exports or companion bridge are introduced. This is not an official portable MNN C# SDK.
- Auto acceleration prefers available Metal with CPU fallback and task-specific compatibility policies.
  Embedding, Piper, Sana and the Bert-VITS2 generator use CPU; Omni speech uses a High-precision
  Metal main runtime with CPU media processors. CoreML/NPU support is not claimed.
- Runtime/Core is organized by domain; editor features, safe handles, tests, tooling and samples
  have explicit directory and assembly ownership while public namespaces remain stable.
- Installation uses the repository package subdirectory; README, API, Studio, test and maintenance
  documentation describe current behavior and limits.
- Ignore rules exclude local models, caches, build/test artifacts and credentials while retaining
  required native plugins, Unity metadata and the deterministic test fixture.

### Fixed

- Published Silero v5.1 is available to runtime catalog consumers; online discovery requires its deployment profile and FP32 graph.
- Matching realtime and offline Silero consumers now share reference-counted inference resources while retaining independent stream state; resources unload with the last owner.

- Compiler ABI differences between arm64 and x86_64 Players for libc++ strings, callbacks and allocation.
- Piper executable/data-directory discovery and invalid saved dictionary paths.
- GPU quality regressions through precision retry, CPU compatibility policies and Omni speech runtime separation.

### Validation scope

- Recorded CPU evidence covers macOS arm64 Mono Editor/Player and x86_64 Mono Player under Rosetta.
  Intel hardware and x86_64 GPU inference have not been verified.
- Recorded arm64 Metal/CPU compatibility regressions cover 97 distinct Editor tests across two runs
  and 116 Player tests. Counts include API/platform checks, not only model tests.
- Other operating systems and IL2CPP reject inference. Conditional C# compilation and rejection-policy
  tests do not establish inference support. See [test evidence](Documentation~/Testing.md).

## [3.6.1] - 2024-10-08

Initial package scaffolding and native plugins for multiple Unity platforms.
Earlier release notes described broad platform and acceleration support. Those historical packaging
claims do not establish support for the current managed C++ binding; use the current
[package README](README.md), [ABI contract](Native~/README.md) and [tests](Documentation~/Testing.md).

For upstream engine changes, see [MNN releases](https://github.com/alibaba/MNN/releases).
