# MNN for Unity Documentation

Start with the [package README](../README.md) for installation and the supported
macOS Mono ABI. This is a project-maintained compiler-specific C# binding, not an
official portable MNN C# SDK. Existing plugin files and task entries do not imply
that every platform or model pipeline is implemented.

- [Chat Studio](ChatStudio.md): conversations, model preparation, media and task limits.
- [Automatic inference acceleration](Acceleration.md): backend selection, fallback and cache behavior.
- [API reference](API.md): public C# API and inference workflow.
- [Project structure and conventions](ProjectStructure.md): directory ownership, assemblies and maintenance checks.
- [Repository maintenance](RepositoryManagement.md): tracked assets, ignored models/artifacts, snapshots and dev-to-main PRs.
- [Package tools](../Tools~/README.md): local validation and offline test runners.
- [Package README](../README.md): installation and native library check.
- [Basic sample](../Samples~/BasicExample/README.md): import and run the example.
- [Changelog](../CHANGELOG.md): package releases.

- [Real inference and tests](Testing.md): verified models, results and platform limits.
- [Native build](../Native~/README.md): direct C++ calls and compiler ABI contract.

Paths under `TestArtifacts~/` in the test documentation refer to local evidence;
logs, model weights and generated media are excluded from Git. Historical test
results retain their original scope and do not replace validation of a new build.
