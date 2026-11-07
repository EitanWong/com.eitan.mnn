# Repository maintenance

The repository root is a Unity development project. The distributable package lives
in `Packages/com.eitan.mnn`; see [project structure](ProjectStructure.md) for ownership
and assembly boundaries.

## Tracked source assets

Commit Runtime, Editor, tests, deterministic fixtures, package `Samples~`, documentation,
maintenance tools and package metadata. Preserve Unity `.meta` files, existing GUIDs
and complete framework/xcframework plugins. Native `.dll`, `.so`, `.dylib` and `.a`
files under `Runtime/Plugins` are distributable assets; do not ignore these globally.
Their presence does not establish managed inference support for a platform.

The development host's manifest and ProjectSettings are tracked. Keep local EasyMic
dependencies out of the committed manifest. The committed model fixtures are
`Tests/Fixtures/affine.mnn` and `Tests/Fixtures/affine-inputs.mnn`; additional fixtures require a small, deterministic,
redistributable justification.

## Development host snapshot: 2026-11-07

The tracked Unity 2021.3.45f2 host settings now identify the development app as
`Eitan / SiriAI`, with Android application ID `com.eitan.siriai` and minimum SDK 24.
They preserve the microphone usage description, frame-timing setting, current
anti-aliasing/vSync choices and scene-template dependency settings. The host manifest
includes uGUI and Unity's screen-capture module. These are development-project
settings; they do not change the distributable package's platform or API contract.

The enabled build scene is `Assets/Samples/SiriAI/Scenes/SiriAI.unity`. That imported
sample remains ignored, so a clean clone must supply the matching sample or choose
another build scene before building the host. Existing checked sample migrations,
their exact baselines and recorded validation limits are documented in the
[SiriAI migration guide](../Tools~/Migration/README.md); those patches do not supply
the complete original sample or its model weights.

`EASYMIC_APM_EXPERIMENTAL_FEATURES` is retained for Android, Standalone and iPhone.
EasyMic packages and manifest entries remain local-only. Keep any local manifest
overlay when saving a development snapshot, and validate the actual staged or
committed manifest separately. Do not add imported samples, temporary ordering
files or orphan metadata to make a local project appear reproducible.

Static package/repository checks cover structure, file policy and document links.
They do not establish Unity compilation, scene availability, microphone permissions,
native inference correctness or device rendering/performance for these host settings.

## Local files

Ignore local `com.eitan.easymic` and `com.eitan.easymic.apm` packages, imported
`Assets/Samples`, downloaded models, Unity/IDE caches, build outputs, Python caches,
interrupted downloads, credentials and Mono crash JSON/blob files. Local sample connection settings under root `MNN/`
also remain untracked. A tracked
manifest cannot ignore individual JSON entries: review the staged dependency diff
and let repository validation reject accidental EasyMic dependencies.

Use `Assets/StreamingAssets/MNN/Models` for local model assets and
`Application.persistentDataPath/MNN/Models` for runtime downloads. Prepare the model
files separately for application builds; they are absent from a Git clone.

Keep backups, diagnostic output, test XML/logs, generated media, isolated Unity
projects and builds under `TestArtifacts~/`. Do not put generated files in the package.
Local evidence paths in test guides are not downloadable repository assets.

`.gitignore` does not remove tracked files or erase Git history. To stop tracking
an identified local file, remove only its index entry with `git rm --cached` and
retain its local copy. Treat Unity asset/meta pairs together. Rewriting published
history requires a separate coordinated operation.

## Review and validation

Follow the [contribution checks](development/Contributing.md), inspect the staged
file list and diff, and commit only the current task's work. The repository checker
uses Git's index for file membership and working-tree contents for validation.
Review staged contents separately; local checks must not stand in for CI on the
actual pushed commit.

Use `dev → main` PRs for promotion. See [branches](development/Branches.md),
[CI](development/ContinuousIntegration.md) and [releases](development/Releases.md).
