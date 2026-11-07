# Changelog

Notable repository changes are recorded here. Package API and engine changes are
also recorded in the [package changelog](Packages/com.eitan.mnn/CHANGELOG.md).
Unreleased entries describe development work, not a published version.

## [Unreleased]

### Added

- Added macOS ARM64/x64, Windows x64 and Linux x64 desktop Mono contracts for
  core inference, Express, LLM, native embedding/reranking and media input.
  Windows/Linux use pinned GNU compiler builds of unchanged official MNN 3.6.2,
  without added C++ sources, bridge libraries or custom C exports. Setup and
  system requirements are in the [desktop Mono guide](Packages/com.eitan.mnn/Documentation~/DesktopMono.md).
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

- Saved the SiriAI development host settings: product identity, Android minimum
  SDK 24, microphone usage text, frame timing, quality settings, scene templates,
  and the local sample build-scene selection. Added uGUI and screen-capture module
  dependencies; EasyMic remains a local-only dependency. See the
  [development host snapshot](Packages/com.eitan.mnn/Documentation~/RepositoryManagement.md#development-host-snapshot-2026-11-07)
  for clean-clone requirements and validation limits.
- Expanded `AGENTS.md` with the default ub-mode workflow and the reusable local
  model directory, `Assets/StreamingAssets/MNN/Models/`.
- Saved checked SiriAI visual migration stages for source-driven reflections,
  folded-sheet dispersion and light, soft ribbon endpoints, and glass refraction.
  Their exact baselines, recorded Editor evidence and remaining limits are in the
  [migration guide](Packages/com.eitan.mnn/Tools~/Migration/README.md).
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

- Repository validation now accepts both committed deterministic affine fixtures
  and their metadata, while still rejecting downloaded models and same-named files
  outside the fixture directory.
- Markdown checks exclude the verbatim keyword-pronunciation license notice,
  preserving its upstream text alongside the existing license exclusions.
- Restored missing `.meta` files for two audio-test directories, preserving their
  existing local GUIDs, and repaired OmniVoice links to pages absent from the committed snapshot.

### Validation scope

- Package and repository checks validate source layout and repository policy. They
  do not establish C# compilation, native ABI correctness, model quality or device support.
- Native and model changes need the applicable Unity checks and explicitly recorded
  environment/results. Existing [test evidence](Packages/com.eitan.mnn/Documentation~/Testing.md)
  retains its original scope.
- The earlier isolated-project OmniVoice compilation blocker is resolved; see
  [CI evidence](Packages/com.eitan.mnn/Documentation~/development/ContinuousIntegration.md#isolated-validation-evidence).
- Windows Wine Mono and Linux host Mono each pass 79 advanced task assertions and
  22 core assertions against the bundled native libraries. Windows/Linux Unity
  Players and GPU inference remain unverified; their native builds are CPU-only.
- macOS ARM64 and Rosetta x64 Unity 2021.3.45f2 Mono Players each pass 120/120
  selected core/platform/Express tests. Focused Editor checks pass 14/14, with
  13 conditional compilation cases and nine package assemblies compiled.
- These checks do not certify every model pipeline. Native waveform callbacks
  remain unavailable on all platforms; desktop IL2CPP, Windows/Linux ARM64 and
  WebGL remain unsupported. Historical model and device results retain their
  [recorded scope](Packages/com.eitan.mnn/Documentation~/Testing.md).
