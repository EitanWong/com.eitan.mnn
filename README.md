# MNN for Unity

Unity bindings and editor tools for [MNN](https://github.com/alibaba/MNN) 3.6.1.
The package includes tensor inference, language and multimodal APIs, model management,
and Chat Studio. It requires Unity 2021.3 or later.

[简体中文](docs/README.zh-CN.md) | [Documentation](Packages/com.eitan.mnn/Documentation~/README.md) | [Changelog](CHANGELOG.md)

## Install

In Unity Package Manager, choose **Add package from git URL** and enter:

```text
https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#dev
```

Use a commit SHA in place of `dev` to pin a version. Import **Basic Inference**
from the package's Samples tab and provide a compatible model to get started.
For editor workflows, open **Window > MNN > Model Manager** or **Chat Studio**.
Model weights are not included in this repository.

## Platform support

The managed bindings call official MNN C++ symbols through platform-specific ABI
contracts. They are maintained by this project and are not an official portable
MNN C# API. Support and validation vary by platform and task; consult the
[native ABI notes](Packages/com.eitan.mnn/Native~/README.md) and
[test evidence](Packages/com.eitan.mnn/Documentation~/Testing.md) before deployment.

## Documentation

The [documentation index](Packages/com.eitan.mnn/Documentation~/README.md) covers
the API, model workflows, acceleration, testing and project structure. Repository-level
documentation and translations are organized under [docs](docs/).

## License

[Apache License 2.0](LICENSE). MNN is maintained by its upstream contributors.
