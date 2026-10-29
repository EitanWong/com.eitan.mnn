# Contributing

Discuss substantial API or platform changes in an issue before implementation.
Small fixes and documentation corrections can go straight to a pull request.

## Development setup

Clone the repository and work from `dev`. Open the repository root with Unity
2021.3.45f2 when changing Unity code. The distributable package is
`Packages/com.eitan.mnn`; the root Unity project is a development host.

Do not add EasyMic packages or dependencies, imported `Assets/Samples`, model
weights, crash dumps, caches or test artifacts to a commit. Put local model assets
in `Assets/StreamingAssets/MNN/Models` and logs/builds in `TestArtifacts~/`.
The package's `Samples~` directory, native plugins and Unity `.meta` files are source assets.

## Checks

Stage new source files, then run these commands from the repository root with Python 3.11+:

```bash
python3 -B -m unittest discover -s Packages/com.eitan.mnn/Tools~/Validation -p 'test_*.py'
python3 -B Packages/com.eitan.mnn/Tools~/Validation/validate_package.py
python3 -B Packages/com.eitan.mnn/Tools~/Validation/validate_repository.py
git diff --check
git diff --cached --check
```

The repository check reads Git's index for its file inventory. A document link
must resolve to a tracked file or directory, even if an untracked local copy exists.
It inspects working-tree contents; review the staged diff separately before committing.

For Markdown or workflow changes, also run `markdownlint-cli2` and `actionlint`.
CI installs these tools; local installation is optional. Tool versions and check
scope are described in [CI and validation](ContinuousIntegration.md).

Static checks do not compile C# or exercise native inference. For runtime changes,
record the Unity version, architecture, scripting backend, model, device and result.
See [test reproduction](../Testing.md). Tests must not download large models implicitly.

## Code and API changes

Follow `.editorconfig`, `.gitattributes` and the [project structure](../ProjectStructure.md).
Keep existing public namespaces, assembly names and asset GUIDs stable. Pair moved
Unity assets with their `.meta` files. Add a failing behavior test before a bug fix
when an honest test is practical, then verify the corrected behavior.

Managed interop calls the official MNN C++ APIs under an explicit platform/compiler
ABI contract. Do not add C++ sources, native bridges, custom C exports or modifications
to upstream MNN sources. A new platform needs its own ABI implementation and real
validation; retaining a native binary or adding an enum value is insufficient.

## Pull requests

Target `dev` for contributions. Maintainers promote reviewed work from `dev` to
`main`. Describe the user-visible problem, resulting behavior, validation and known
limits. Update the relevant guide and changelog when behavior changes. Keep unrelated
local work outside the commit.

Contributions are distributed under the project's Apache-2.0 license. Follow the
[community guidelines](../community/CodeOfConduct.md) and use the
[security policy](../community/Security.md) for sensitive reports.
