# MNN for Unity

[![Repository checks](https://github.com/EitanWong/com.eitan.mnn/actions/workflows/package-structure.yml/badge.svg?branch=dev)](https://github.com/EitanWong/com.eitan.mnn/actions/workflows/package-structure.yml)

Unity bindings and editor tools for [MNN](https://github.com/alibaba/MNN) 3.6.2.
The package includes tensor inference, language and multimodal APIs, retrieval,
speech and image generation, model management, and Chat Studio. It requires Unity
2021.3 or later.

[简体中文](Packages/com.eitan.mnn/Documentation~/translations/README.zh-CN.md) · [Documentation](Packages/com.eitan.mnn/Documentation~/README.md) · [Changelog](CHANGELOG.md)

## Requirements

- Unity 2021.3 or later; validation uses Unity 2021.3.45f2.
- Desktop Mono: macOS 11+ ARM64/x64, Windows 10+ x64 with UCRT, or Linux x64
  with GLIBC 2.38+ and GLIBCXX_3.4.32. See [desktop Mono](Packages/com.eitan.mnn/Documentation~/DesktopMono.md).
- Windows x64 also supports [IL2CPP Players](Packages/com.eitan.mnn/Documentation~/WindowsPlayer.md).
- Android/iOS ARM64 IL2CPP have separate [mobile contracts](Packages/com.eitan.mnn/Documentation~/MobileInterop.md).
- A compatible model, obtained separately under its own license.

The bindings call official MNN C++ symbols under the bundled compiler/ABI contract.
They are maintained by this project; MNN does not provide a portable C# SDK.
Keep each bundled library and its managed declarations together. macOS/Linux IL2CPP,
Windows/Linux ARM64 and WebGL are unsupported. Check the
[ABI contract](Packages/com.eitan.mnn/Native~/README.md) and
[validation scope](Packages/com.eitan.mnn/Documentation~/Testing.md) before deployment.

## Development status

Core inference, Express, LLM text/chat/streaming, native embedding/reranking and
Omni media input have desktop Mono bindings. Windows Wine Mono and Linux host
Mono each pass 79 task assertions and 22 core assertions against the bundled
native libraries. Windows 11 x64 Unity Players additionally pass 157/157 selected
LLM/core/Express/platform tests on each of Mono and IL2CPP with no skips. Linux
Unity Player validation remains pending. Windows enables CPU/OpenCL and Linux uses
CPU. Waveform callbacks are unavailable.

Unity 2021.3.45f2 macOS ARM64 and x64 under Rosetta each pass 120/120 selected
core/platform/Express Player tests. Model-specific and mobile evidence retains its
own scope; see [validation results](Packages/com.eitan.mnn/Documentation~/Testing.md).

## Install

In Unity Package Manager, choose **Add package from git URL**:

```text
https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#dev
```

`dev` is the development snapshot. Use `main` for merged code or a commit SHA for a
reproducible installation. The package subdirectory in the URL is required.

Import **Basic Inference** from the package's Samples tab and supply a compatible
model. For editor workflows, open **Window > MNN > Model Manager** or **Chat Studio**.
Model weights are excluded from this repository.

## Contribute

Start with the [contribution guide](Packages/com.eitan.mnn/Documentation~/development/Contributing.md)
for setup, checks and pull requests. Use the [issue tracker](https://github.com/EitanWong/com.eitan.mnn/issues)
for reproducible bugs and feature requests; follow the
[security policy](Packages/com.eitan.mnn/Documentation~/community/Security.md) for vulnerabilities.

## License

[Apache License 2.0](LICENSE). Model weights and optional third-party tools retain
their own licenses.
