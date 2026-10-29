# MNN for Unity

[![Repository checks](https://github.com/EitanWong/com.eitan.mnn/actions/workflows/package-structure.yml/badge.svg?branch=dev)](https://github.com/EitanWong/com.eitan.mnn/actions/workflows/package-structure.yml)

Unity bindings and editor tools for [MNN](https://github.com/alibaba/MNN) 3.6.1:
local tensor inference, language and multimodal models, retrieval, speech synthesis,
and image generation. Model Manager handles model downloads; Chat Studio provides
an editor workspace for inference and conversations.

[简体中文](Packages/com.eitan.mnn/Documentation~/translations/README.zh-CN.md) · [Documentation](Packages/com.eitan.mnn/Documentation~/README.md) · [Changelog](CHANGELOG.md)

## Requirements

- Unity 2021.3 or later; validation uses Unity 2021.3.45f2.
- macOS 11+ with 64-bit Mono for the committed managed inference bindings.
- A compatible model, obtained separately under its own license.

The bindings call official MNN C++ symbols under the bundled compiler/ABI contract.
They are maintained by this project; MNN does not provide a portable C# SDK.
Other-platform plugin files do not establish managed inference support. Check the
[ABI contract](Packages/com.eitan.mnn/Native~/README.md) and
[validation scope](Packages/com.eitan.mnn/Documentation~/Testing.md) before deployment.

## Development status

The current development snapshot has a [known clean-build blocker](Packages/com.eitan.mnn/Documentation~/development/ContinuousIntegration.md#known-clean-build-blocker)
in OmniVoice. Static checks do not establish that the package compiles; this snapshot
is not release-ready.

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
