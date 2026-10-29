# Changelog

Notable repository changes are recorded here. Package API and engine changes are
also recorded in the [package changelog](Packages/com.eitan.mnn/CHANGELOG.md).
Unreleased entries describe development work, not a published version.

## [Unreleased]

### Added

- Added tracked-file validation for local-only assets, EasyMic manifest dependencies,
  ignored files and local Markdown links. Untracked files on a maintainer's machine
  cannot satisfy documentation links. Python regression tests exercise these checks
  against temporary Git repositories.
- Added Markdown and GitHub Actions linting alongside package-structure validation
  on pull requests and pushes to `dev` and `main`.
- Added isolated Unity CI project preparation and a manual macOS ARM64 Mono job for
  platform-contract or deterministic affine inference checks. The workflow records
  logs, NUnit XML and scope summaries without downloading model weights.
- Added contribution, CI, branch, release, support and security guides; structured
  bug/feature issue forms; a PR template; and community guidelines.
- Added weekly Dependabot updates for pinned GitHub Actions dependencies targeting `dev`.
- Added OmniVoice synthesis and optional compact sampling, with profiling and
  experimental classifier-free guidance and trajectory-distillation notes.

### Changed

- Made the root README a concise English entry point with requirements, installation,
  platform limitations and contribution links. Moved the Chinese translation and
  branch guide into `Packages/com.eitan.mnn/Documentation~`; removed root `docs/`.
- Organized the documentation index by getting started, API/models, development and
  community. All project guides and translations now share the package documentation root.
- Replaced the template's broad Unity/OS build matrix and weekly scheduled jobs with
  static PR checks and a manual native-validation workflow tied to the supported environment.
- Restricted workflow permissions, pinned third-party action revisions, added timeouts
  and concurrency controls, and updated Actions runtimes.
- Replaced automatic main-push publication with a manual version/tag-checked release
  workflow. It archives only the package directory, generates a SHA-256 checksum,
  creates or refreshes a draft, and refuses to modify published releases.
- Corrected CODEOWNERS to the repository maintainer and removed template-author funding links.
- Excluded local EasyMic packages, imported `Assets/Samples`, model files, Mono crash
  JSON/blob files and other generated artifacts. Previously tracked sample assets
  were removed from the current Git tree while their local copies were retained.

### Fixed

- Restored missing `.meta` files for two audio-test directories, preserving their
  existing local GUIDs, and repaired OmniVoice links to pages absent from the committed snapshot.

### Validation scope

- Package and repository checks validate source layout and repository policy. They
  do not establish C# compilation, native ABI correctness, model quality or device support.
- Native and model changes need the applicable Unity checks and explicitly recorded
  environment/results. Existing [test evidence](Packages/com.eitan.mnn/Documentation~/Testing.md)
  retains its original scope.
- The new isolated Unity run exposed unresolved OmniVoice dependencies in the existing
  committed development snapshot. Tests cannot start until the missing runtime types
  and graph APIs are included; the promotion PR remains draft.
