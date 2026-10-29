# Documentation

MNN for Unity provides managed inference APIs and editor tools for the bundled
MNN 3.6.1 build. Start with the package README and verify the target platform before
choosing a model. A task entry or native plugin file alone does not establish support.

## Start here

- [Package overview and installation](../README.md)
- [Basic Inference sample](../Samples~/BasicExample/README.md)
- [Chat Studio](ChatStudio.md): conversations, media, model preparation and task limits.
- [简体中文 project overview](translations/README.zh-CN.md)

## API and models

- [API reference](API.md): public C# types and inference workflows.
- [Acceleration](Acceleration.md): backend selection, precision, fallback and caching.
- [OmniVoice](OmniVoice.md): conversion, synthesis, model licensing and measured limits.
- [Native build and ABI contract](../Native~/README.md): required compiler and library assumptions.

## Development

- [Contributing](development/Contributing.md): setup, checks and review expectations.
- [Project structure](ProjectStructure.md): directory ownership and assembly boundaries.
- [Repository maintenance](RepositoryManagement.md): source assets, local files and Git hygiene.
- [Branches and installation references](development/Branches.md)
- [CI and validation](development/ContinuousIntegration.md): automatic checks and optional Unity runs.
- [Release procedure](development/Releases.md): versioning, tags, package archives and draft releases.
- [Test evidence and reproduction](Testing.md): actual model results and platform limits.
- [Tool reference](../Tools~/README.md)

## Community and history

- [Support](community/Support.md)
- [Community guidelines](community/CodeOfConduct.md)
- [Security policy](community/Security.md)
- [Package changelog](../CHANGELOG.md)
- [Repository changelog](https://github.com/EitanWong/com.eitan.mnn/blob/dev/CHANGELOG.md)

## Documentation conventions

All project guides and translations live in `Documentation~`. The repository and
package READMEs remain short entry points; release history stays in the standard
CHANGELOG files. Topic guides currently retain their original language. New
navigation and maintainer guides use English; translations belong in `translations/`.
Keep executable examples, supported platforms and validation claims consistent
with the code included in the same commit.

Paths under `TestArtifacts~/` refer to local evidence excluded from Git. Historical
results describe the tested build and environment; they do not certify a later build.
